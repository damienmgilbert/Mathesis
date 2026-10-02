using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mathesis.Numbers;

namespace Mathesis.Symbolics.Serialization;

/// <summary>
/// Mathesis's own JSON form of an expression (docs/design/05-syntax-trees-and-notation.md, "MathJSON"): an application is
/// <c>{"op": "add", "args": […]}</c> and a number is the string <c>"3/4"</c> so it stays exact. Other nodes:
/// <c>{"sym": "x"}</c> (with <c>"sort"</c> when it is not ℝ), <c>{"float": 2.5}</c>, <c>{"const": "Pi"}</c>,
/// <c>{"bind": "Sum", "vars": […], "data": […], "body": …}</c>, <c>{"matrix": [[…], […]]}</c>, <c>{"set": […]}</c>,
/// <c>{"tuple": […]}</c>, <c>{"interval": {…}}</c>, <c>{"piecewise": […]}</c>, <c>{"wild": "a"}</c>.
/// Number display hints are not stored (they never affect equality).
/// </summary>
public sealed class ExprJsonConverter : JsonConverter<Expr>
{
    /// <inheritdoc />
    public override Expr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FromElement(document.RootElement);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        WriteExpr(writer, value);
    }

    // ----- Writing -----

    private static void WriteExpr(Utf8JsonWriter w, Expr e)
    {
        switch (e)
        {
            case Number n:
                w.WriteStringValue(n.Value.ToString());
                break;
            case Float f:
                w.WriteStartObject();
                if (double.IsFinite(f.Value)) w.WriteNumber("float", f.Value);
                else w.WriteString("float", double.IsNaN(f.Value) ? "NaN" : f.Value > 0 ? "Infinity" : "-Infinity");
                if (f.PrecisionBits != 53) w.WriteNumber("bits", f.PrecisionBits);
                w.WriteEndObject();
                break;
            case Symbol s:
                w.WriteStartObject();
                w.WriteString("sym", s.Name);
                if (s.DeclaredSort != Sort.Real)
                {
                    w.WritePropertyName("sort");
                    WriteSort(w, s.DeclaredSort);
                }
                w.WriteEndObject();
                break;
            case Constant c:
                w.WriteStartObject();
                w.WriteString("const", c.Id.ToString());
                w.WriteEndObject();
                break;
            case Apply a:
                w.WriteStartObject();
                w.WriteString("op", a.Operator.Id);
                WriteList(w, "args", a.Arguments);
                w.WriteEndObject();
                break;
            case Bind b:
                w.WriteStartObject();
                w.WriteString("bind", b.Binder.ToString());
                WriteList(w, "vars", b.Bound.Cast<Expr>());
                WriteList(w, "data", b.Data);
                w.WritePropertyName("body");
                WriteExpr(w, b.Body);
                w.WriteEndObject();
                break;
            case MatrixLiteral m:
                w.WriteStartObject();
                w.WriteStartArray("matrix");
                for (var r = 0; r < m.Rows; r++)
                {
                    w.WriteStartArray();
                    for (var c = 0; c < m.Columns; c++) WriteExpr(w, m[r, c]);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
                break;
            case SetLiteral s:
                w.WriteStartObject();
                WriteList(w, "set", s.Elements);
                w.WriteEndObject();
                break;
            case TupleLiteral t:
                w.WriteStartObject();
                WriteList(w, "tuple", t.Elements);
                w.WriteEndObject();
                break;
            case IntervalLiteral i:
                w.WriteStartObject();
                w.WriteStartObject("interval");
                w.WritePropertyName("lower");
                WriteExpr(w, i.Lower);
                w.WritePropertyName("upper");
                WriteExpr(w, i.Upper);
                w.WriteBoolean("lowerClosed", i.LowerClosed);
                w.WriteBoolean("upperClosed", i.UpperClosed);
                w.WriteEndObject();
                w.WriteEndObject();
                break;
            case Piecewise p:
                w.WriteStartObject();
                w.WriteStartArray("piecewise");
                foreach (var (value, condition) in p.Cases)
                {
                    w.WriteStartObject();
                    w.WritePropertyName("value");
                    WriteExpr(w, value);
                    w.WritePropertyName("when");
                    WriteExpr(w, condition);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
                break;
            case Wild wild:
                w.WriteStartObject();
                w.WriteString("wild", wild.Name);
                if (wild.Constraint is not null)
                {
                    w.WritePropertyName("constraint");
                    WriteExpr(w, wild.Constraint);
                }
                w.WriteEndObject();
                break;
            default:
                throw new JsonException($"Cannot serialize an expression of type {e.GetType().Name}.");
        }
    }

    private static void WriteList(Utf8JsonWriter w, string name, IEnumerable<Expr> items)
    {
        w.WriteStartArray(name);
        foreach (var item in items) WriteExpr(w, item);
        w.WriteEndArray();
    }

    private static void WriteSort(Utf8JsonWriter w, Sort sort)
    {
        switch (sort)
        {
            case SimpleSort s:
                w.WriteStringValue(s.Name);
                break;
            case ResidueSort r:
                w.WriteStartObject();
                w.WriteString("residue", r.Modulus.ToString(CultureInfo.InvariantCulture));
                w.WriteEndObject();
                break;
            case SetSort s:
                w.WriteStartObject();
                w.WritePropertyName("set");
                WriteSort(w, s.Element);
                w.WriteEndObject();
                break;
            case RandomVariableSort r:
                w.WriteStartObject();
                w.WritePropertyName("randomVariable");
                WriteSort(w, r.Element);
                w.WriteEndObject();
                break;
            case TupleSort t:
                w.WriteStartObject();
                w.WriteStartArray("tuple");
                foreach (var c in t.Components) WriteSort(w, c);
                w.WriteEndArray();
                w.WriteEndObject();
                break;
            case VectorSort v:
                w.WriteStartObject();
                w.WriteStartObject("vector");
                w.WritePropertyName("length");
                WriteExpr(w, v.Length);
                w.WritePropertyName("element");
                WriteSort(w, v.Element);
                w.WriteEndObject();
                w.WriteEndObject();
                break;
            case MatrixSort m:
                w.WriteStartObject();
                w.WriteStartObject("matrix");
                w.WritePropertyName("rows");
                WriteExpr(w, m.Rows);
                w.WritePropertyName("columns");
                WriteExpr(w, m.Columns);
                w.WritePropertyName("element");
                WriteSort(w, m.Element);
                w.WriteEndObject();
                w.WriteEndObject();
                break;
            case FunctionSort f:
                w.WriteStartObject();
                w.WriteStartObject("function");
                w.WritePropertyName("domain");
                WriteSort(w, f.Domain);
                w.WritePropertyName("codomain");
                WriteSort(w, f.Codomain);
                w.WriteEndObject();
                w.WriteEndObject();
                break;
            default:
                throw new JsonException($"Cannot serialize a sort of type {sort.GetType().Name}.");
        }
    }

    // ----- Reading -----

    private static JsonException Bad(string message) => new(message);

    internal static Expr FromElement(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String:
                var text = e.GetString()!;
                return BigRational.TryParse(text, CultureInfo.InvariantCulture, out var value) ? new Number(value) : throw Bad($"'{text}' is not an exact number.");
            case JsonValueKind.Number:
                return new Number(BigRational.Parse(e.GetRawText(), CultureInfo.InvariantCulture));
            case JsonValueKind.Object:
                break;
            default:
                throw Bad($"Unexpected JSON {e.ValueKind} where an expression was expected.");
        }

        if (e.TryGetProperty("op", out var op))
        {
            var id = op.GetString() ?? throw Bad("'op' must be a string.");
            if (!Operators.TryGet(id, out var oper)) throw Bad($"Unknown operator '{id}'.");
            var args = List(e, "args");
            try
            {
                return new Apply(oper, args);
            }
            catch (ArgumentException ex)
            {
                throw Bad(ex.Message);
            }
        }
        if (e.TryGetProperty("sym", out var sym))
        {
            var sort = e.TryGetProperty("sort", out var s) ? ReadSort(s) : Sort.Real;
            try
            {
                return new Symbol(sym.GetString() ?? string.Empty, sort);
            }
            catch (ArgumentException ex)
            {
                throw Bad(ex.Message);
            }
        }
        if (e.TryGetProperty("float", out var f))
        {
            var bits = e.TryGetProperty("bits", out var b) ? b.GetInt32() : 53;
            var v = f.ValueKind == JsonValueKind.String ? f.GetString() switch { "NaN" => double.NaN, "Infinity" => double.PositiveInfinity, "-Infinity" => double.NegativeInfinity, _ => throw Bad("Invalid float.") } : f.GetDouble();
            return new Float(v, bits);
        }
        if (e.TryGetProperty("const", out var c))
        {
            return Enum.TryParse<ConstantId>(c.GetString(), out var id) && Enum.IsDefined(id) ? new Constant(id) : throw Bad($"Unknown constant '{c.GetString()}'.");
        }
        if (e.TryGetProperty("bind", out var bind))
        {
            if (!Enum.TryParse<Binder>(bind.GetString(), out var binder) || !Enum.IsDefined(binder)) throw Bad($"Unknown binder '{bind.GetString()}'.");
            var vars = List(e, "vars");
            if (vars.Any(x => x is not Symbol) || !e.TryGetProperty("body", out var body)) throw Bad("A binder needs symbol 'vars' and a 'body'.");
            try
            {
                return new Bind(binder, [.. vars.Cast<Symbol>()], List(e, "data"), FromElement(body));
            }
            catch (ArgumentException ex)
            {
                throw Bad(ex.Message);
            }
        }
        if (e.TryGetProperty("matrix", out var matrix))
        {
            var rows = matrix.EnumerateArray().Select(r => r.EnumerateArray().Select(FromElement).ToList()).ToList();
            if (rows.Count == 0 || rows[0].Count == 0 || rows.Any(r => r.Count != rows[0].Count)) throw Bad("A matrix needs rows of equal, non-zero length.");
            return new MatrixLiteral(rows.Count, rows[0].Count, [.. rows.SelectMany(r => r)]);
        }
        if (e.TryGetProperty("set", out _)) return new SetLiteral(List(e, "set"));
        if (e.TryGetProperty("tuple", out _)) return new TupleLiteral(List(e, "tuple"));
        if (e.TryGetProperty("interval", out var interval))
        {
            return new IntervalLiteral(FromElement(interval.GetProperty("lower")), FromElement(interval.GetProperty("upper")), interval.GetProperty("lowerClosed").GetBoolean(), interval.GetProperty("upperClosed").GetBoolean());
        }
        if (e.TryGetProperty("piecewise", out var piecewise))
        {
            var cases = piecewise.EnumerateArray().Select(c => (FromElement(c.GetProperty("value")), FromElement(c.GetProperty("when")))).ToImmutableArray();
            return cases.IsEmpty ? throw Bad("A piecewise expression needs at least one case.") : new Piecewise(cases);
        }
        if (e.TryGetProperty("wild", out var wild))
        {
            try
            {
                return new Wild(wild.GetString() ?? string.Empty, e.TryGetProperty("constraint", out var constraint) ? FromElement(constraint) : null);
            }
            catch (ArgumentException ex)
            {
                throw Bad(ex.Message);
            }
        }
        throw Bad("Unrecognized expression object.");
    }

    private static ImmutableArray<Expr> List(JsonElement e, string name) =>
        e.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(FromElement)]
            : throw Bad($"Expected an array '{name}'.");

    private static Sort ReadSort(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.String)
        {
            return e.GetString() switch
            {
                "Any" => Sort.Any,
                "Boolean" => Sort.Boolean,
                "Number" => Sort.Number,
                "Complex" => Sort.Complex,
                "Real" => Sort.Real,
                "ExtendedReal" => Sort.ExtendedReal,
                "Algebraic" => Sort.Algebraic,
                "Rational" => Sort.Rational,
                "Integer" => Sort.Integer,
                "Natural" => Sort.Natural,
                var other => throw Bad($"Unknown sort '{other}'."),
            };
        }
        if (e.TryGetProperty("residue", out var r)) return Sort.ResidueOf(BigInteger.Parse(r.GetString() ?? "0", CultureInfo.InvariantCulture));
        if (e.TryGetProperty("set", out var set)) return Sort.SetOf(ReadSort(set));
        if (e.TryGetProperty("randomVariable", out var rv)) return Sort.RandomVariableOf(ReadSort(rv));
        if (e.TryGetProperty("tuple", out var tuple)) return new TupleSort([.. tuple.EnumerateArray().Select(ReadSort)]);
        if (e.TryGetProperty("vector", out var v)) return Sort.VectorOf(FromElement(v.GetProperty("length")), ReadSort(v.GetProperty("element")));
        if (e.TryGetProperty("matrix", out var m)) return Sort.MatrixOf(FromElement(m.GetProperty("rows")), FromElement(m.GetProperty("columns")), ReadSort(m.GetProperty("element")));
        if (e.TryGetProperty("function", out var f)) return Sort.FunctionOf(ReadSort(f.GetProperty("domain")), ReadSort(f.GetProperty("codomain")));
        throw Bad("Unrecognized sort.");
    }
}

/// <summary>The System.Text.Json source-generation context for expressions (no reflection, safe for NativeAOT).</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Expr))]
internal sealed partial class MathesisJsonContext : JsonSerializerContext
{
}

/// <summary>Serialization of expressions to and from Mathesis JSON.</summary>
public static class ExprJson
{
    /// <summary>Writes <paramref name="expr"/> as compact Mathesis JSON.</summary>
    public static string Serialize(Expr expr)
    {
        ArgumentNullException.ThrowIfNull(expr);
        return JsonSerializer.Serialize(expr, MathesisJsonContext.Default.Expr);
    }

    /// <summary>Reads an expression from Mathesis JSON.</summary>
    /// <exception cref="JsonException">The JSON is malformed or does not describe an expression.</exception>
    public static Expr Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize(json, MathesisJsonContext.Default.Expr) ?? throw new JsonException("The JSON is null.");
    }
}
