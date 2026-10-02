using System.Numerics;
using Mathesis.Numbers;
using Mathesis.Symbolics;

namespace Mathesis.Knowledge.Tests.Verification;

/// <summary>Dense matrices of complex numbers for the verifier: just enough linear algebra to check statements about determinants and systems.</summary>
internal static class MatrixValues
{
    public static Complex<double>[,] Identity(int n)
    {
        var m = new Complex<double>[n, n];
        for (var i = 0; i < n; i++) m[i, i] = Complex<double>.One;
        return m;
    }

    public static Complex<double>[,] Multiply(Complex<double>[,] a, Complex<double>[,] b)
    {
        var rows = a.GetLength(0);
        var inner = a.GetLength(1);
        var columns = b.GetLength(1);
        var result = new Complex<double>[rows, columns];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < columns; j++)
            {
                var sum = Complex<double>.Zero;
                for (var k = 0; k < inner; k++) sum += a[i, k] * b[k, j];
                result[i, j] = sum;
            }
        }
        return result;
    }

    public static Complex<double>[,] Transpose(Complex<double>[,] a)
    {
        var result = new Complex<double>[a.GetLength(1), a.GetLength(0)];
        for (var i = 0; i < a.GetLength(0); i++)
        {
            for (var j = 0; j < a.GetLength(1); j++) result[j, i] = a[i, j];
        }
        return result;
    }

    public static Complex<double>[,] Map(Complex<double>[,] a, Func<Complex<double>, Complex<double>> f)
    {
        var result = new Complex<double>[a.GetLength(0), a.GetLength(1)];
        for (var i = 0; i < a.GetLength(0); i++)
        {
            for (var j = 0; j < a.GetLength(1); j++) result[i, j] = f(a[i, j]);
        }
        return result;
    }

    /// <summary>The determinant by Gaussian elimination with partial pivoting.</summary>
    public static Complex<double> Determinant(Complex<double>[,] a)
    {
        var n = a.GetLength(0);
        var m = (Complex<double>[,])a.Clone();
        var det = Complex<double>.One;
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < n; r++)
            {
                if (m[r, col].Magnitude > m[pivot, col].Magnitude) pivot = r;
            }
            if (m[pivot, col].Magnitude == 0) return Complex<double>.Zero;
            if (pivot != col)
            {
                for (var c = 0; c < n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                det = -det;
            }
            det *= m[col, col];
            for (var r = col + 1; r < n; r++)
            {
                var factor = m[r, col] / m[col, col];
                for (var c = col; c < n; c++) m[r, c] -= factor * m[col, c];
            }
        }
        return det;
    }

    /// <summary>The inverse by Gauss–Jordan elimination; <c>null</c> for a (numerically) singular matrix.</summary>
    public static Complex<double>[,]? Inverse(Complex<double>[,] a)
    {
        var n = a.GetLength(0);
        var m = (Complex<double>[,])a.Clone();
        var inv = Identity(n);
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < n; r++)
            {
                if (m[r, col].Magnitude > m[pivot, col].Magnitude) pivot = r;
            }
            if (m[pivot, col].Magnitude < 1e-12) return null;
            if (pivot != col)
            {
                for (var c = 0; c < n; c++)
                {
                    (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                    (inv[col, c], inv[pivot, c]) = (inv[pivot, c], inv[col, c]);
                }
            }
            var p = m[col, col];
            for (var c = 0; c < n; c++)
            {
                m[col, c] /= p;
                inv[col, c] /= p;
            }
            for (var r = 0; r < n; r++)
            {
                if (r == col) continue;
                var factor = m[r, col];
                for (var c = 0; c < n; c++)
                {
                    m[r, c] -= factor * m[col, c];
                    inv[r, c] -= factor * inv[col, c];
                }
            }
        }
        return inv;
    }

    /// <summary>The adjugate (transposed cofactor matrix).</summary>
    public static Complex<double>[,] Adjugate(Complex<double>[,] a)
    {
        var n = a.GetLength(0);
        var result = new Complex<double>[n, n];
        if (n == 1)
        {
            result[0, 0] = Complex<double>.One;
            return result;
        }
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var minor = new Complex<double>[n - 1, n - 1];
                var mr = 0;
                for (var r = 0; r < n; r++)
                {
                    if (r == i) continue;
                    var mc = 0;
                    for (var c = 0; c < n; c++)
                    {
                        if (c == j) continue;
                        minor[mr, mc++] = a[r, c];
                    }
                    mr++;
                }
                var cofactor = Determinant(minor);
                result[j, i] = (i + j) % 2 == 0 ? cofactor : -cofactor;
            }
        }
        return result;
    }

    public static bool Close(Complex<double>[,] a, Complex<double>[,] b, double tolerance)
    {
        if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
        var scale = 1.0;
        foreach (var x in a) scale = Math.Max(scale, double.IsFinite(x.Magnitude) ? x.Magnitude : 1);
        foreach (var x in b) scale = Math.Max(scale, double.IsFinite(x.Magnitude) ? x.Magnitude : 1);
        for (var i = 0; i < a.GetLength(0); i++)
        {
            for (var j = 0; j < a.GetLength(1); j++)
            {
                if ((a[i, j] - b[i, j]).Magnitude > tolerance * scale) return false;
            }
        }
        return true;
    }

    public static bool IsMatrixSort(Sort sort) => sort is MatrixSort or VectorSort;
}
