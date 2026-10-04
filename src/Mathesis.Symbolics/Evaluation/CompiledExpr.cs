using System.Collections.Immutable;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Evaluation;

/// <summary>
/// An expression compiled to a flat post-order instruction array and run by a stack interpreter
/// (docs/design/07-engines.md, "Evaluation and compilation"). Create it with <see cref="Evaluator.Compile{T}"/>.
/// </summary>
/// <typeparam name="T">The number type: <see cref="double"/>, <c>Complex&lt;double&gt;</c>, <c>Interval&lt;double&gt;</c> or <c>Dual&lt;double&gt;</c>.</typeparam>
public sealed class CompiledExpr<T>
    where T : struct
{
    internal enum Op : byte
    {
        LoadVar, LoadConst, Add, Mul, Sub, Div, Neg, Pow, PowInt, PowRational, Unary, Binary,
    }

    internal readonly record struct Instruction(Op Op, int A = 0, int B = 0);

    private readonly ImmutableArray<Instruction> _code;
    private readonly ImmutableArray<T> _constants;
    private readonly int _stackSize;
    private readonly NumberOps<T> _ops;

    internal CompiledExpr(ImmutableArray<Symbol> parameters, ImmutableArray<Instruction> code, ImmutableArray<T> constants, int stackSize, NumberOps<T> ops)
    {
        Parameters = parameters;
        _code = code;
        _constants = constants;
        _stackSize = stackSize;
        _ops = ops;
    }

    /// <summary>The parameter symbols, in argument order.</summary>
    public ImmutableArray<Symbol> Parameters { get; }

    /// <summary>The number of instructions.</summary>
    public int InstructionCount => _code.Length;

    /// <summary>Evaluates with the arguments in parameter order.</summary>
    /// <exception cref="ArgumentException">The number of arguments differs from <see cref="Parameters"/>.</exception>
    public T Invoke(ReadOnlySpan<T> arguments)
    {
        if (arguments.Length != Parameters.Length) throw new ArgumentException($"Expected {Parameters.Length} arguments but got {arguments.Length}.", nameof(arguments));
        var stack = new T[_stackSize];
        var top = 0;
        var ops = _ops;
        foreach (var ins in _code)
        {
            switch (ins.Op)
            {
                case Op.LoadVar: stack[top++] = arguments[ins.A]; break;
                case Op.LoadConst: stack[top++] = _constants[ins.A]; break;
                case Op.Add:
                    {
                        var acc = stack[top - ins.A];
                        for (var i = top - ins.A + 1; i < top; i++) acc = ops.Add(acc, stack[i]);
                        top -= ins.A - 1;
                        stack[top - 1] = acc;
                        break;
                    }
                case Op.Mul:
                    {
                        var acc = stack[top - ins.A];
                        for (var i = top - ins.A + 1; i < top; i++) acc = ops.Mul(acc, stack[i]);
                        top -= ins.A - 1;
                        stack[top - 1] = acc;
                        break;
                    }
                case Op.Sub: top--; stack[top - 1] = ops.Sub(stack[top - 1], stack[top]); break;
                case Op.Div: top--; stack[top - 1] = ops.Div(stack[top - 1], stack[top]); break;
                case Op.Neg: stack[top - 1] = ops.Neg(stack[top - 1]); break;
                case Op.Pow: top--; stack[top - 1] = ops.Pow(stack[top - 1], stack[top]); break;
                case Op.PowInt: stack[top - 1] = ops.PowInt(stack[top - 1], ins.A); break;
                case Op.PowRational: stack[top - 1] = ops.PowRational(stack[top - 1], ins.A, ins.B); break;
                case Op.Unary: stack[top - 1] = ops.Unary((UnaryFn)ins.A, stack[top - 1]); break;
                case Op.Binary: top--; stack[top - 1] = ops.Binary((BinaryFn)ins.A, stack[top - 1], stack[top]); break;
                default: throw new InvalidOperationException("Corrupt instruction array.");
            }
        }
        return stack[0];
    }

    /// <summary>Evaluates a one-parameter expression.</summary>
    public T Invoke(T x) => Invoke([x]);

    /// <summary>Evaluates a two-parameter expression.</summary>
    public T Invoke(T x, T y) => Invoke([x, y]);

    /// <summary>Evaluates a three-parameter expression.</summary>
    public T Invoke(T x, T y, T z) => Invoke([x, y, z]);

    /// <summary>Evaluates the expression for each element of <paramref name="x"/> (a one-parameter expression), writing into <paramref name="results"/>.</summary>
    /// <exception cref="ArgumentException">The spans differ in length or the expression does not have exactly one parameter.</exception>
    public void InvokeMany(ReadOnlySpan<T> x, Span<T> results)
    {
        if (Parameters.Length != 1) throw new ArgumentException("InvokeMany needs a one-parameter expression.");
        if (x.Length != results.Length) throw new ArgumentException("The input and output spans must have the same length.", nameof(results));
        for (var i = 0; i < x.Length; i++) results[i] = Invoke(x[i]);
    }
}

/// <summary>Builds <see cref="CompiledExpr{T}"/> from expressions.</summary>
internal sealed class ExprCompiler<T>(NumberOps<T> ops, ImmutableArray<Symbol> parameters)
    where T : struct
{
    private readonly List<CompiledExpr<T>.Instruction> _code = [];
    private readonly List<T> _constants = [];
    private int _depth;
    private int _maxDepth;

    public static Outcome<CompiledExpr<T>> Compile(Expr expr, ImmutableArray<Symbol> parameters)
    {
        var ops = NumberOps<T>.For();
        if (ops is null) return Outcome.Fail<CompiledExpr<T>>(new MathError(MathErrorKind.Unsupported, $"Compiling to {typeof(T).Name} is not supported; use double, Complex<double>, Interval<double> or Dual<double>.", expr));
        var compiler = new ExprCompiler<T>(ops, parameters);
        var error = compiler.Emit(expr);
        if (error is not null) return Outcome.Fail<CompiledExpr<T>>(new MathError(MathErrorKind.Unsupported, error, expr));
        return Outcome.Ok(new CompiledExpr<T>(parameters, [.. compiler._code], [.. compiler._constants], Math.Max(compiler._maxDepth, 1), ops));
    }

    private void Push(CompiledExpr<T>.Op op, int a = 0, int b = 0, int pops = 0)
    {
        _code.Add(new(op, a, b));
        _depth += 1 - pops;
        _maxDepth = Math.Max(_maxDepth, _depth);
    }

    private void Const(T value)
    {
        _constants.Add(value);
        Push(CompiledExpr<T>.Op.LoadConst, _constants.Count - 1);
    }

    // Returns null on success, otherwise why the expression cannot be compiled.
    private string? Emit(Expr expr)
    {
        switch (expr)
        {
            case Number n:
                Const(ops.FromRational(n.Value));
                return null;
            case Float f:
                Const(ops.FromDouble(f.Value));
                return null;
            case Constant c:
                if (!ops.TryConstant(c.Id, out var value)) return $"The constant '{c}' has no {typeof(T).Name} value.";
                Const(value);
                return null;
            case Symbol s:
                var index = parameters.IndexOf(s);
                if (index < 0) return $"The symbol '{s.Name}' is not one of the parameters.";
                Push(CompiledExpr<T>.Op.LoadVar, index);
                return null;
            case Apply a:
                return EmitApply(a);
            default:
                return $"Expressions of kind {expr.Kind} cannot be compiled.";
        }
    }

    private string? Fold(Apply a, CompiledExpr<T>.Op op)
    {
        foreach (var arg in a.Arguments)
        {
            if (Emit(arg) is { } error) return error;
        }
        Push(op, a.Arguments.Length, pops: a.Arguments.Length);
        return null;
    }

    private string? Unary(Expr argument, UnaryFn fn)
    {
        if (!ops.Supports(fn)) return $"'{fn}' is not supported for {typeof(T).Name}.";
        if (Emit(argument) is { } error) return error;
        Push(CompiledExpr<T>.Op.Unary, (int)fn);
        return null;
    }

    private string? Binary(Expr first, Expr second, BinaryFn fn)
    {
        if (!ops.Supports(fn)) return $"'{fn}' is not supported for {typeof(T).Name}.";
        var error = Emit(first) ?? Emit(second);
        if (error is not null) return error;
        Push(CompiledExpr<T>.Op.Binary, (int)fn, pops: 1);
        return null;
    }

    private string? EmitApply(Apply a)
    {
        var args = a.Arguments;
        var id = a.Operator.Id;
        switch (id)
        {
            case "add": return Fold(a, CompiledExpr<T>.Op.Add);
            case "mul": return Fold(a, CompiledExpr<T>.Op.Mul);
            case "sub": return Emit(args[0]) ?? Emit(args[1]) ?? Op2(CompiledExpr<T>.Op.Sub);
            case "div": return Emit(args[0]) ?? Emit(args[1]) ?? Op2(CompiledExpr<T>.Op.Div);
            case "neg":
                if (Emit(args[0]) is { } negError) return negError;
                Push(CompiledExpr<T>.Op.Neg);
                return null;
            case "pow": return EmitPower(args[0], args[1]);
            case "max" or "min":
                var fn = id == "max" ? BinaryFn.Max : BinaryFn.Min;
                if (!ops.Supports(fn)) return $"'{id}' is not supported for {typeof(T).Name}.";
                if (Emit(args[0]) is { } first) return first;
                for (var i = 1; i < args.Length; i++)
                {
                    if (Emit(args[i]) is { } next) return next;
                    Push(CompiledExpr<T>.Op.Binary, (int)fn, pops: 1);
                }
                return null;
        }
        if (OperatorMap.Lower(a) is { } lowered) return Emit(lowered);
        if (OperatorMap.TryUnary(a, out var unary)) return Unary(args[0], unary);
        if (OperatorMap.TryBinary(a, out var binary)) return Binary(args[0], args[1], binary);
        return $"The operator '{id}' cannot be compiled.";
    }

    private string? Op2(CompiledExpr<T>.Op op)
    {
        Push(op, pops: 1);
        return null;
    }

    private string? EmitPower(Expr b, Expr e)
    {
        if (Emit(b) is { } error) return error;
        if (OperatorMap.TryRational(e, out var r))
        {
            if (r.IsInteger && BigRational.Abs(r) <= 1 << 20)
            {
                Push(CompiledExpr<T>.Op.PowInt, (int)r.Numerator);
                return null;
            }
            if (r.Numerator.GetBitLength() < 31 && r.Denominator.GetBitLength() < 31)
            {
                Push(CompiledExpr<T>.Op.PowRational, (int)r.Numerator, (int)r.Denominator);
                return null;
            }
        }
        if (Emit(e) is { } error2) return error2;
        Push(CompiledExpr<T>.Op.Pow, pops: 1);
        return null;
    }
}
