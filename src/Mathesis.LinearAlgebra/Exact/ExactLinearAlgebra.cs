using System.Collections.Immutable;
using System.Numerics;
using Mathesis.Polynomials;

namespace Mathesis.LinearAlgebra.Exact;

/// <summary>The three elementary row operations (catalog <c>linalg.sys.row-ops</c>).</summary>
public enum RowOperationKind : byte
{
    /// <summary>Exchange two rows.</summary>
    Swap,

    /// <summary>Multiply one row by a non-zero factor.</summary>
    Scale,

    /// <summary>Add a multiple of one row to another.</summary>
    AddMultiple,
}

/// <summary>One recorded elementary row operation.</summary>
/// <typeparam name="T">The entry type.</typeparam>
/// <param name="Kind">The kind of operation.</param>
/// <param name="Row">
/// <see cref="RowOperationKind.Swap"/>: the first row. <see cref="RowOperationKind.Scale"/>: the row multiplied by
/// <paramref name="Factor"/>. <see cref="RowOperationKind.AddMultiple"/>: the row that changes.
/// </param>
/// <param name="OtherRow">
/// <see cref="RowOperationKind.Swap"/>: the second row. <see cref="RowOperationKind.AddMultiple"/>: the row whose multiple is
/// added. Unused (−1) for <see cref="RowOperationKind.Scale"/>.
/// </param>
/// <param name="Factor">The scale factor or multiple; the multiplicative identity for swaps.</param>
public readonly record struct RowOperation<T>(RowOperationKind Kind, int Row, int OtherRow, T Factor)
{
    /// <summary>A short description such as <c>R2 ← R2 + (−3)·R1</c> (one-based rows), for explanations.</summary>
    public override string ToString() => Kind switch
    {
        RowOperationKind.Swap => $"R{Row + 1} ↔ R{OtherRow + 1}",
        RowOperationKind.Scale => $"R{Row + 1} ← ({Factor})·R{Row + 1}",
        _ => $"R{Row + 1} ← R{Row + 1} + ({Factor})·R{OtherRow + 1}",
    };
}

/// <summary>The reduced row echelon form of a matrix together with the operations that produced it.</summary>
/// <typeparam name="T">The entry type.</typeparam>
/// <param name="Reduced">The reduced row echelon form (catalog <c>linalg.sys.rref</c>), unique by <c>linalg.sys.rref-unique</c>.</param>
/// <param name="PivotColumns">The pivot column of each non-zero row, in increasing order.</param>
/// <param name="Operations">The row operations applied, in order, to turn the original matrix into <paramref name="Reduced"/>.</param>
public sealed record RowReduction<T>(DenseMatrix<T> Reduced, ImmutableArray<int> PivotColumns, ImmutableArray<RowOperation<T>> Operations)
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
        IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
{
    /// <summary>The rank: the number of pivots.</summary>
    public int Rank => PivotColumns.Length;

    /// <summary>
    /// Applies the recorded operations to <paramref name="original"/> and returns the result, which equals
    /// <see cref="Reduced"/> when <paramref name="original"/> is the matrix that was reduced.
    /// </summary>
    public DenseMatrix<T> Replay(DenseMatrix<T> original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var data = original.AsSpan().ToArray();
        foreach (var operation in Operations) ExactLinearAlgebra.Apply(data, original.Columns, operation);
        return DenseMatrix.FromRowMajor<T>(original.Rows, original.Columns, data);
    }
}

/// <summary>
/// Exact linear algebra over any field, using only the field operator interfaces and a pluggable zero test, so the same code
/// works for <see cref="Numbers.BigRational"/>, <see cref="Numbers.Complex{T}"/> and (later) symbolic entries with a simplifying
/// zero test. Do not use these with <see cref="double"/> for numerical work: they pivot on the first non-zero entry, not the
/// largest. Use <see cref="MatrixSolvers"/> instead.
/// </summary>
/// <remarks>
/// Every <c>isZero</c> parameter defaults to equality with <c>T.AdditiveIdentity</c>. For <see cref="BigInteger"/> entries
/// <see cref="Determinant{T}"/> and <see cref="CharacteristicPolynomial{T}"/> are exact (Bareiss's divisions are exact and
/// Berkowitz's algorithm is division-free); the others divide and need a field.
/// </remarks>
public static class ExactLinearAlgebra
{
    internal static void Apply<T>(T[] data, int columns, RowOperation<T> op)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IUnaryNegationOperators<T, T>,
            IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        switch (op.Kind)
        {
            case RowOperationKind.Swap:
                for (var j = 0; j < columns; j++) (data[op.Row * columns + j], data[op.OtherRow * columns + j]) = (data[op.OtherRow * columns + j], data[op.Row * columns + j]);
                break;
            case RowOperationKind.Scale:
                for (var j = 0; j < columns; j++) data[op.Row * columns + j] *= op.Factor;
                break;
            default:
                for (var j = 0; j < columns; j++) data[op.Row * columns + j] += op.Factor * data[op.OtherRow * columns + j];
                break;
        }
    }

    /// <summary>
    /// Gauss–Jordan elimination to reduced row echelon form (catalog <c>linalg.sys.gauss-jordan</c>), pivoting on the first
    /// non-zero entry of each column and recording every row operation.
    /// </summary>
    public static RowReduction<T> RowReduce<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        isZero ??= static x => x == T.AdditiveIdentity;
        var rows = a.Rows;
        var columns = a.Columns;
        var data = a.AsSpan().ToArray();
        var operations = ImmutableArray.CreateBuilder<RowOperation<T>>();
        var pivots = ImmutableArray.CreateBuilder<int>();

        void Do(RowOperation<T> op)
        {
            Apply(data, columns, op);
            operations.Add(op);
        }

        var r = 0;
        for (var c = 0; c < columns && r < rows; c++)
        {
            var p = -1;
            for (var i = r; i < rows; i++)
            {
                if (!isZero(data[i * columns + c]))
                {
                    p = i;
                    break;
                }
            }
            if (p < 0) continue;
            if (p != r) Do(new(RowOperationKind.Swap, r, p, T.MultiplicativeIdentity));
            var pivot = data[r * columns + c];
            if (pivot != T.MultiplicativeIdentity) Do(new(RowOperationKind.Scale, r, -1, T.MultiplicativeIdentity / pivot));
            for (var i = 0; i < rows; i++)
            {
                if (i == r) continue;
                var f = data[i * columns + c];
                if (!isZero(f)) Do(new(RowOperationKind.AddMultiple, i, r, -f));
            }
            pivots.Add(c);
            r++;
        }
        return new(DenseMatrix.FromRowMajor<T>(rows, columns, data), pivots.ToImmutable(), operations.ToImmutable());
    }

    /// <summary>The rank: the number of pivots in the reduced row echelon form.</summary>
    public static int Rank<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool> =>
        RowReduce(a, isZero).Rank;

    /// <summary>
    /// A basis of the null space {x : A·x = 0}, one vector per free column (catalog <c>linalg.sys.homogeneous</c>); empty when the
    /// columns are independent. Its size is n − rank.
    /// </summary>
    public static ImmutableArray<DenseVector<T>> NullSpace<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        var reduction = RowReduce(a, isZero);
        var pivotSet = reduction.PivotColumns.ToHashSet();
        var basis = ImmutableArray.CreateBuilder<DenseVector<T>>();
        for (var free = 0; free < a.Columns; free++)
        {
            if (pivotSet.Contains(free)) continue;
            var v = new T[a.Columns];
            Array.Fill(v, T.AdditiveIdentity);
            v[free] = T.MultiplicativeIdentity;
            for (var row = 0; row < reduction.PivotColumns.Length; row++) v[reduction.PivotColumns[row]] = -reduction.Reduced[row, free];
            basis.Add(new(v));
        }
        return basis.ToImmutable();
    }

    /// <summary>A basis of the column space: the columns of A in the pivot positions (catalog <c>linalg.sys.rouche-capelli</c> context).</summary>
    public static ImmutableArray<DenseVector<T>> ColumnSpace<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool> =>
        [.. RowReduce(a, isZero).PivotColumns.Select(a.Column)];

    /// <summary>
    /// The determinant by fraction-free Bareiss elimination (catalog <c>linalg.det.bareiss</c>): every intermediate entry is a
    /// minor of A, so integer entries stay integers. Rows are exchanged when a pivot is zero.
    /// </summary>
    /// <exception cref="ArgumentException">The matrix is not square.</exception>
    public static T Determinant<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.Rows != a.Columns) throw new ArgumentException("The determinant needs a square matrix.", nameof(a));
        isZero ??= static x => x == T.AdditiveIdentity;
        var n = a.Rows;
        if (n == 0) return T.MultiplicativeIdentity;
        var m = a.AsSpan().ToArray();
        var negate = false;
        var previous = T.MultiplicativeIdentity;
        for (var k = 0; k < n - 1; k++)
        {
            if (isZero(m[k * n + k]))
            {
                var swap = -1;
                for (var i = k + 1; i < n; i++)
                {
                    if (!isZero(m[i * n + k]))
                    {
                        swap = i;
                        break;
                    }
                }
                if (swap < 0) return T.AdditiveIdentity;
                for (var j = 0; j < n; j++) (m[k * n + j], m[swap * n + j]) = (m[swap * n + j], m[k * n + j]);
                negate = !negate;
            }
            for (var i = k + 1; i < n; i++)
            {
                for (var j = k + 1; j < n; j++) m[i * n + j] = (m[i * n + j] * m[k * n + k] - m[i * n + k] * m[k * n + j]) / previous;
            }
            previous = m[k * n + k];
        }
        var det = m[n * n - 1];
        return negate ? -det : det;
    }

    /// <summary>
    /// The inverse by Gauss–Jordan elimination on [A | I] (catalog <c>linalg.mat.inverse-gauss-jordan</c>); fails when A is not
    /// square or does not reduce to the identity (A is singular).
    /// </summary>
    public static Outcome<DenseMatrix<T>> Inverse<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.Rows != a.Columns) return Outcome.Fail<DenseMatrix<T>>(MathError.Domain("Only square matrices have an inverse."));
        var n = a.Rows;
        var augmented = DenseMatrix.Create(n, 2 * n, (i, j) => j < n ? a[i, j] : i == j - n ? T.MultiplicativeIdentity : T.AdditiveIdentity);
        var reduction = RowReduce(augmented, isZero);
        if (reduction.PivotColumns.Length < n || (n > 0 && reduction.PivotColumns[n - 1] != n - 1))
        {
            return Outcome.Fail<DenseMatrix<T>>(MathError.Domain("The matrix is singular."));
        }
        return Outcome.Ok(reduction.Reduced.Block(0, n, n, n));
    }

    /// <summary>
    /// The characteristic polynomial det(x·I − A) by the Berkowitz algorithm (Berkowitz 1984): division-free, built from
    /// Toeplitz matrix–vector products over the trailing submatrices, so it is exact for any commutative ring of entries.
    /// The result is monic of degree n.
    /// </summary>
    public static Polynomial<T> CharacteristicPolynomial<T>(DenseMatrix<T> a, Func<T, bool>? isZero = null)
        where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IMultiplyOperators<T, T, T>, IDivisionOperators<T, T, T>,
            IUnaryNegationOperators<T, T>, IAdditiveIdentity<T, T>, IMultiplicativeIdentity<T, T>, IEqualityOperators<T, T, bool>
    {
        ArgumentNullException.ThrowIfNull(a);
        if (a.Rows != a.Columns) throw new ArgumentException("The characteristic polynomial needs a square matrix.", nameof(a));
        var n = a.Rows;

        // Descending coefficients of the characteristic polynomial of the trailing r×r submatrix, for r = 1..n.
        var p = new[] { T.MultiplicativeIdentity };
        for (var r = 1; r <= n; r++)
        {
            var offset = n - r;
            var t = new T[r + 1];
            t[0] = T.MultiplicativeIdentity;
            t[1] = -a[offset, offset];

            // t[k] = −R·M^(k−2)·C for k ≥ 2, with R the rest of the first row, C the rest of the first column, M the trailing block.
            var column = new T[r - 1];
            for (var i = 0; i < r - 1; i++) column[i] = a[offset + 1 + i, offset];
            for (var k = 2; k <= r; k++)
            {
                var dot = T.AdditiveIdentity;
                for (var i = 0; i < r - 1; i++) dot += a[offset, offset + 1 + i] * column[i];
                t[k] = -dot;
                if (k < r)
                {
                    var next = new T[r - 1];
                    for (var i = 0; i < r - 1; i++)
                    {
                        var s = T.AdditiveIdentity;
                        for (var j = 0; j < r - 1; j++) s += a[offset + 1 + i, offset + 1 + j] * column[j];
                        next[i] = s;
                    }
                    column = next;
                }
            }

            var q = new T[r + 1];
            for (var i = 0; i <= r; i++)
            {
                var s = T.AdditiveIdentity;
                for (var j = 0; j <= Math.Min(i, r - 1); j++) s += t[i - j] * p[j];
                q[i] = s;
            }
            p = q;
        }
        return new(p.Reverse(), isZero);
    }
}
