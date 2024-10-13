using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using HLE.Collections;
using HLE.IL;
using HLE.Memory;

namespace HLE.Marshalling;

public static unsafe class StructMarshal
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> GetBytes<T>(ref T item) where T : struct
        => MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref item), sizeof(T));

    /// <inheritdoc cref="EqualsBitwise{TLeft,TRight}(ref TLeft,ref TRight)"/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsBitwise<TLeft, TRight>(TLeft left, TRight right)
        where TLeft : struct
        where TRight : struct
        => EqualsBitwise(ref left, ref right);

    /// <inheritdoc cref="EqualsBitwise{TLeft,TRight}(ref TLeft,ref TRight)"/>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsBitwise<TLeft, TRight>(TLeft left, ref TRight right)
        where TLeft : struct
        where TRight : struct
        => EqualsBitwise(ref left, ref right);

    /// <summary>
    /// Compares two structs bitwise.
    /// </summary>
    /// <param name="left">A struct.</param>
    /// <param name="right">Another struct.</param>
    /// <typeparam name="TLeft">The type of the left struct.</typeparam>
    /// <typeparam name="TRight">The type of the right struct.</typeparam>
    /// <returns>True, if the structs have the same size and bitwise memory values, otherwise false.</returns>
    [Pure]
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsBitwise<TLeft, TRight>(ref TLeft left, ref TRight right)
        where TLeft : struct
        where TRight : struct
    {
        if (sizeof(TLeft) != sizeof(TRight))
        {
            return false;
        }

        switch (sizeof(TLeft))
        {
            case sizeof(byte):
                return Unsafe.As<TLeft, byte>(ref left) == Unsafe.As<TRight, byte>(ref right);
            case sizeof(short):
                return Unsafe.As<TLeft, short>(ref left) == Unsafe.As<TRight, short>(ref right);
            case sizeof(int):
                return Unsafe.As<TLeft, int>(ref left) == Unsafe.As<TRight, int>(ref right);
            case sizeof(long):
                return Unsafe.As<TLeft, long>(ref left) == Unsafe.As<TRight, long>(ref right);
        }

        if (Unsafe.AreSame(ref Unsafe.As<TLeft, byte>(ref left), ref Unsafe.As<TRight, byte>(ref right)))
        {
            return true;
        }

        if (Vector512.IsHardwareAccelerated && sizeof(TLeft) == Vector512<byte>.Count)
        {
            return Unsafe.As<TLeft, Vector512<byte>>(ref left) == Unsafe.As<TRight, Vector512<byte>>(ref right);
        }

        if (Vector256.IsHardwareAccelerated && sizeof(TLeft) == Vector256<byte>.Count)
        {
            return Unsafe.As<TLeft, Vector256<byte>>(ref left) == Unsafe.As<TRight, Vector256<byte>>(ref right);
        }

        if (Vector128.IsHardwareAccelerated && sizeof(TLeft) == Vector128<byte>.Count)
        {
            return Unsafe.As<TLeft, Vector128<byte>>(ref left) == Unsafe.As<TRight, Vector128<byte>>(ref right);
        }

        return GetBytes(ref left).SequenceEqual(GetBytes(ref right));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte AsByte(this bool b) => UnsafeIL.As<bool, byte>(b);

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity")]
    public static bool IsBitwiseEquatable<T>() // where T : unmanaged
        => typeof(T) == typeof(bool) ||
           typeof(T) == typeof(byte) ||
           typeof(T) == typeof(sbyte) ||
           typeof(T) == typeof(short) ||
           typeof(T) == typeof(ushort) ||
           typeof(T) == typeof(char) ||
           typeof(T) == typeof(int) ||
           typeof(T) == typeof(uint) ||
           typeof(T) == typeof(long) ||
           typeof(T) == typeof(ulong) ||
           typeof(T) == typeof(nint) ||
           typeof(T) == typeof(nuint) ||
           typeof(T) == typeof(Half) ||
           typeof(T) == typeof(float) ||
           typeof(T) == typeof(double) ||
           typeof(T) == typeof(decimal) ||
           typeof(T) == typeof(Int128) ||
           typeof(T) == typeof(UInt128) ||
           typeof(T) == typeof(DateTime) ||
           typeof(T) == typeof(DateTimeOffset) ||
           typeof(T) == typeof(DateOnly) ||
           typeof(T) == typeof(TimeOnly) ||
           typeof(T) == typeof(TimeSpan) ||
           typeof(T) == typeof(Guid) ||
           typeof(T) == typeof(Range) ||
           typeof(T) == typeof(Rune) ||
           typeof(T) == typeof(CLong) ||
           typeof(T) == typeof(CULong) ||
           typeof(T) == typeof(NFloat) ||
           typeof(T) == typeof(RawArrayData<>) ||
           typeof(T) == typeof(RawStringData) ||
           typeof(T) == typeof(RangeEnumerator) ||
           typeof(T).IsEnum ||
           typeof(T).IsAssignableTo(typeof(IBitwiseEquatable<T>));

    /// <summary>
    /// Unboxes a struct without a type check.
    /// It will not be checked if the type of the boxed struct inside <paramref name="obj"/> matches the type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The box.</param>
    /// <typeparam name="T">The type of the struct that will be read from the box.</typeparam>
    /// <returns>A reference to the struct in the box.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T Unbox<T>(object obj) => ref UnsafeIL.Unbox<T>(obj);
}
