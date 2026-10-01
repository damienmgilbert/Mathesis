using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Mathesis.Numbers;

/// <summary>
/// Marker for number types whose arithmetic is exact (no rounding), such as <see cref="BigRational"/>.
/// Generic algorithms use it, through <see cref="NumberTraits{T}.IsExact"/>, to pivot for exactness instead of stability.
/// </summary>
public interface IExactNumber
{
}

/// <summary>Static facts about a number type that generic algorithms need.</summary>
/// <typeparam name="T">The number type.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Specified by docs/design/04-type-system.md as NumberTraits<T>.IsExact.")]
public static class NumberTraits<T>
{
    [UnconditionalSuppressMessage("AOT", "IL2059", Justification = "T is already in use, so its static constructor is preserved.")]
    static NumberTraits()
    {
        IsExact = IsExactByDefault();

        // Generic wrappers such as Complex<TInner> override IsExact from their own static constructor.
        RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
    }

    /// <summary>
    /// <c>true</c> for <see cref="System.Numerics.BigInteger"/>, the built-in integer types, any <see cref="IExactNumber"/>,
    /// and wrappers (<see cref="Complex{T}"/>) of an exact type.
    /// </summary>
    public static bool IsExact { get; private set; }

    internal static void Override(bool isExact) => IsExact = isExact;

    private static bool IsExactByDefault()
    {
        var t = typeof(T);
        return typeof(IExactNumber).IsAssignableFrom(t)
            || t == typeof(System.Numerics.BigInteger)
            || t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(sbyte)
            || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(byte)
            || t == typeof(Int128) || t == typeof(UInt128) || t == typeof(nint) || t == typeof(nuint);
    }
}
