using Mathesis.LinearAlgebra;
using Mathesis.Numbers;
using Mathesis.Numerics;
using Mathesis.Numerics.Integration;
using Mathesis.Numerics.Ode;
using Mathesis.Polynomials;
using Mathesis.Testing;

namespace Mathesis.Numerics.Tests;

/// <summary>
/// Integration (ADR-19): the same jet runs through Mathesis's generic <c>Roots</c>, <c>Quadrature</c>, <c>OdeSolver</c>,
/// <c>DenseMatrix</c> LU solve, <c>Polynomial</c> and <c>Complex</c> code, which is the reason a jet implements <c>IFloatingPointIeee754</c>
/// (ADR-14). Each test compares the gradient obtained by differentiating through the algorithm with a closed form.
/// </summary>
[TestClass]
public class JetIntegrationTests
{
    private static readonly StoppingCriteria Tight = new(AbsoluteTolerance: 1e-14, RelativeTolerance: 1e-14, FunctionTolerance: null, MaxIterations: 200);

    [TestMethod]
    public void BrentRootGradientMatchesTheImplicitFunctionTheorem()
    {
        // f(x; p) = x² − p on [0, 3]: root √p, d/dp = 1/(2√p).
        var gen = new Gen(4301);
        for (var s = 0; s < 20; s++)
        {
            var p = gen.Uniform(0.5, 8.0);
            var parameter = JetD.Variable(p, 0, 1);
            var result = Roots.Brent<JetD>(x => x * x - parameter, JetD.Constant(0.0), JetD.Constant(3.0), Tight);
            Assert.IsTrue(result.Converged, gen.Describe($"p = {p:R}"));
            Assert.AreEqual(Math.Sqrt(p), result.Root.Value, 1e-12, gen.Describe($"root, p = {p:R}"));
            Assert.AreEqual(1.0 / (2.0 * Math.Sqrt(p)), result.Root.Gradient[0], 1e-6, gen.Describe($"d root/dp, p = {p:R}"));
        }
    }

    [TestMethod]
    public void NewtonRootGradientMatchesTheImplicitFunctionTheorem()
    {
        // f(x; p) = exp(x) − p: root ln p, d/dp = 1/p. With the analytic derivative as the second function Newton's iteration is differentiable end to end.
        var gen = new Gen(4302);
        for (var s = 0; s < 20; s++)
        {
            var p = gen.Uniform(0.5, 8.0);
            var parameter = JetD.Variable(p, 0, 1);
            var result = Roots.Newton<JetD>(x => JetD.Exp(x) - parameter, x => JetD.Exp(x), JetD.Constant(1.0), Tight);
            Assert.IsTrue(result.Converged, gen.Describe($"p = {p:R}"));
            Assert.AreEqual(Math.Log(p), result.Root.Value, 1e-13, gen.Describe($"root, p = {p:R}"));
            Assert.AreEqual(1.0 / p, result.Root.Gradient[0], 1e-10, gen.Describe($"d root/dp, p = {p:R}"));
        }
    }

    [TestMethod]
    public void QuadratureGradientOfAParametricIntegralEqualsTheIntegralOfTheParameterDerivative()
    {
        // F(p) = ∫₀¹ exp(−p·x²) dx, so F′(p) = ∫₀¹ −x²·exp(−p·x²) dx.
        var stop = new StoppingCriteria(AbsoluteTolerance: 1e-13, RelativeTolerance: 1e-13, FunctionTolerance: null, MaxIterations: 200);
        var gen = new Gen(4303);
        for (var s = 0; s < 10; s++)
        {
            var p = gen.Uniform(0.3, 4.0);
            var parameter = JetD.Variable(p, 0, 1);
            var integral = Quadrature.GaussKronrod<JetD>(x => JetD.Exp(-parameter * x * x), JetD.Constant(0.0), JetD.Constant(1.0), stop);
            var reference = Quadrature.GaussKronrod<double>(x => -x * x * Math.Exp(-p * x * x), 0.0, 1.0, stop);
            var value = Quadrature.GaussKronrod<double>(x => Math.Exp(-p * x * x), 0.0, 1.0, stop);
            Assert.IsTrue(integral.Converged && reference.Converged, gen.Describe($"p = {p:R}"));
            Assert.AreEqual(value.Value, integral.Value.Value, 1e-12, gen.Describe($"F(p), p = {p:R}"));
            Assert.AreEqual(reference.Value, integral.Value.Gradient[0], 1e-8, gen.Describe($"F'(p), p = {p:R}"));
        }
    }

    [TestMethod]
    public void OdeSensitivityMatchesTheClosedFormSolution()
    {
        // y′ = −a·y, y(0) = y0: y(t) = y0·exp(−a·t), ∂y/∂a = −t·y0·exp(−a·t).
        var gen = new Gen(4304);
        var options = new OdeOptions(RelativeTolerance: 1e-11, AbsoluteTolerance: 1e-13, InitialStep: null, MaxStep: null, MaxSteps: 100_000);
        for (var s = 0; s < 10; s++)
        {
            var a = gen.Uniform(0.2, 2.0);
            var y0 = gen.Uniform(0.5, 3.0);
            var t1 = gen.Uniform(0.5, 3.0);
            var parameter = JetD.Variable(a, 0, 1);
            var solution = OdeSolver.DormandPrince<JetD>((_, y) => -parameter * y, JetD.Constant(0.0), JetD.Constant(y0), JetD.Constant(t1), options);
            Assert.IsTrue(solution.Converged, gen.Describe($"a = {a:R}"));
            var end = solution.States[^1][0];
            Assert.AreEqual(y0 * Math.Exp(-a * t1), end.Value, 1e-9, gen.Describe($"y(t1), a = {a:R}"));
            Assert.AreEqual(-t1 * y0 * Math.Exp(-a * t1), end.Gradient[0], 1e-6, gen.Describe($"dy/da, a = {a:R}, t1 = {t1:R}"));
        }
    }

    [TestMethod]
    public void LuSolveDerivativeMatchesMinusAInverseAPrimeAInverseB()
    {
        // A(p) = A0 + p·A1 (3 × 3), b constant: d(A⁻¹b)/dp = −A⁻¹·A1·A⁻¹·b, computed with plain doubles from the same factorization code.
        var gen = new Gen(4305);
        for (var s = 0; s < 20; s++)
        {
            const int n = 3;
            var a0 = new double[n, n];
            var a1 = new double[n, n];
            var b = new double[n];
            for (var i = 0; i < n; i++)
            {
                b[i] = gen.Uniform(-1, 1);
                for (var j = 0; j < n; j++)
                {
                    a0[i, j] = gen.Uniform(-1, 1) + (i == j ? 3.0 : 0.0);   // diagonally dominant, well conditioned
                    a1[i, j] = gen.Uniform(-1, 1);
                }
            }
            var p = gen.Uniform(-0.5, 0.5);
            var parameter = JetD.Variable(p, 0, 1);
            var matrix = DenseMatrix.Create(n, n, (i, j) => JetD.Constant(a0[i, j]) + parameter * JetD.Constant(a1[i, j]));
            var rhs = DenseVector.Create(n, i => JetD.Constant(b[i]));
            var x = MatrixSolvers.Lu(matrix).Solve(rhs);

            var plain = DenseMatrix.Create(n, n, (i, j) => a0[i, j] + p * a1[i, j]);
            var lu = MatrixSolvers.Lu(plain);
            var xPlain = lu.Solve(DenseVector.Create(n, i => b[i]));
            // A′x as a vector, then solve A·dx = −A′x.
            var rhsDerivative = DenseVector.Create(n, i =>
            {
                var sum = 0.0;
                for (var j = 0; j < n; j++) sum -= a1[i, j] * xPlain[j];
                return sum;
            });
            var dx = lu.Solve(rhsDerivative);
            for (var i = 0; i < n; i++)
            {
                Assert.AreEqual(xPlain[i], x[i].Value, 1e-13, gen.Describe($"x[{i}]"));
                Assert.AreEqual(dx[i], x[i].Gradient[0], 1e-10, gen.Describe($"dx[{i}]/dp"));
            }
        }
    }

    [TestMethod]
    public void PolynomialEvaluateDifferentiatesWithRespectToCoefficientsAndArgument()
    {
        // p(x) = c0 + c1·x + c2·x² + c3·x³ with the coefficients and x as the 5 variables: ∂p/∂cᵢ = xⁱ, ∂p/∂x = c1 + 2·c2·x + 3·c3·x².
        var gen = new Gen(4306);
        for (var s = 0; s < 50; s++)
        {
            var values = new double[5];
            for (var i = 0; i < values.Length; i++) values[i] = gen.Uniform(-2, 2);
            var v = JetD.Variables(values);
            var polynomial = new Polynomial<JetD>([v[0], v[1], v[2], v[3]], jet => jet.Value == 0.0);
            var result = polynomial.Evaluate(v[4]);
            double c0 = values[0], c1 = values[1], c2 = values[2], c3 = values[3], x = values[4];
            Assert.AreEqual(c0 + c1 * x + c2 * x * x + c3 * x * x * x, result.Value, 1e-13, gen.Describe("value"));
            double[] expected = [1.0, x, x * x, x * x * x, c1 + 2 * c2 * x + 3 * c3 * x * x];
            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], result.Gradient[i], 1e-13 * (1 + Math.Abs(expected[i])), gen.Describe($"gradient {i}"));
        }
    }

    [TestMethod]
    public void ComplexExponentialOfJetsHasTheClosedFormGradient()
    {
        // exp(a + b·i) = eᵃ(cos b + i·sin b): ∂Re/∂a = eᵃ·cos b, ∂Re/∂b = −eᵃ·sin b, ∂Im/∂a = eᵃ·sin b, ∂Im/∂b = eᵃ·cos b.
        var gen = new Gen(4307);
        for (var s = 0; s < 100; s++)
        {
            double a = gen.Uniform(-2, 2), b = gen.Uniform(-3, 3);
            var z = new Complex<JetD>(JetD.Variable(a, 0, 2), JetD.Variable(b, 1, 2));
            var e = ComplexFunctions.Exp(z);
            double ea = Math.Exp(a), cb = Math.Cos(b), sb = Math.Sin(b);
            var context = gen.Describe($"exp({a:R} + {b:R}i)");
            Assert.AreEqual(ea * cb, e.Real.Value, 1e-14 * Math.Max(1.0, ea), context);
            Assert.AreEqual(ea * sb, e.Imaginary.Value, 1e-14 * Math.Max(1.0, ea), context);
            Assert.AreEqual(ea * cb, e.Real.Gradient[0], 1e-14 * Math.Max(1.0, ea), context + " dRe/da");
            Assert.AreEqual(-ea * sb, e.Real.Gradient[1], 1e-14 * Math.Max(1.0, ea), context + " dRe/db");
            Assert.AreEqual(ea * sb, e.Imaginary.Gradient[0], 1e-14 * Math.Max(1.0, ea), context + " dIm/da");
            Assert.AreEqual(ea * cb, e.Imaginary.Gradient[1], 1e-14 * Math.Max(1.0, ea), context + " dIm/db");
        }
    }
}
