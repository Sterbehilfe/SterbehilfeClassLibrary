using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Collections;

namespace HLE.Memory;

[DebuggerDisplay("{ToString()}")]
public unsafe struct NativeMemory<T> : IDisposable, ICollection<T>, ICopyable<T>, IEquatable<NativeMemory<T>>, ICountable, IIndexAccessible<T>,
    IReadOnlyCollection<T>, ISpanProvider<T>, ICollectionProvider<T>
    where T : unmanaged, IEquatable<T>
{
    public readonly ref T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length);
            return ref Unsafe.AsRef<T>(Pointer + index);
        }
    }

    readonly T IIndexAccessible<T>.this[int index] => this[index];

    public readonly ref T this[Index index] => ref this[index.GetOffset(Length)];

    public readonly Span<T> this[Range range] => AsSpan(range);

    public readonly T* Pointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, typeof(NativeMemory<T>));
            return _pointer;
        }
    }

    public readonly int Length
    {
        get => (int)(_lengthAndDisposed & 0x7FFFFFFF);
        private init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _lengthAndDisposed = (_lengthAndDisposed & 0x80000000) | (uint)value;
        }
    }

    private bool IsDisposed
    {
        readonly get => (_lengthAndDisposed & 0x80000000) == 0x80000000;
        set
        {
            uint valueAsUInt = (uint)(value ? 1 : 0);
            _lengthAndDisposed = (_lengthAndDisposed & 0x7FFFFFFF) | (valueAsUInt << 31);
        }
    }

    public readonly ref T Reference => ref Unsafe.AsRef<T>(Pointer);

    readonly int IReadOnlyCollection<T>.Count => Length;

    readonly int ICollection<T>.Count => Length;

    readonly int ICountable.Count => Length;

    readonly bool ICollection<T>.IsReadOnly => false;

    internal readonly T* _pointer;

    // | 0 | 000 0000 0000 0000 0000 0000 0000 |
    // most significant bit is the disposed state
    // the other bits are the length
    private uint _lengthAndDisposed;

    public static NativeMemory<T> Empty => new();

    public NativeMemory()
    {
        _pointer = null;
        _lengthAndDisposed = 0;
    }

    public NativeMemory(int length, bool zeroed = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Length = length;
        IsDisposed = false;

        long byteCount = checked(sizeof(T) * (long)length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)byteCount, nuint.MaxValue);

        _pointer = (T*)NativeMemory.AlignedAlloc((nuint)byteCount, (nuint)sizeof(nuint));
        if (zeroed)
        {
            Unsafe.InitBlock(_pointer, 0, (uint)byteCount);
        }
    }

    [Pure]
    public readonly Span<T> AsSpan() => new(Pointer, Length);

    [Pure]
    public readonly Span<T> AsSpan(int start) => new Slicer<T>(Pointer, Length).CreateSpan(start);

    [Pure]
    public readonly Span<T> AsSpan(int start, int length) => new Slicer<T>(Pointer, Length).CreateSpan(start, length);

    [Pure]
    public readonly Span<T> AsSpan(Range range) => new Slicer<T>(Pointer, Length).CreateSpan(range);

    readonly Span<T> ISpanProvider<T>.GetSpan() => AsSpan();

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Debug.Assert((nuint)_pointer % (nuint)sizeof(nuint) == 0);
        NativeMemory.AlignedFree(_pointer);
        IsDisposed = true;
    }

    public readonly void CopyTo(List<T> destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(T[] destination, int offset)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(Memory<T> destination)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(Span<T> destination)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(ref T destination)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(ref destination);
    }

    public readonly void CopyTo(T* destination)
    {
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(destination);
    }

    [Pure]
    public readonly T[] ToArray()
    {
        if (Length == 0)
        {
            return [];
        }

        T[] result = GC.AllocateUninitializedArray<T>(Length);
        CopyWorker<T>.Copy(AsSpan(), result);
        return result;
    }

    [Pure]
    public readonly List<T> ToList()
    {
        if (Length == 0)
        {
            return [];
        }

        List<T> result = new(Length);
        CopyWorker<T> copyWorker = new(Pointer, Length);
        copyWorker.CopyTo(result);
        return result;
    }

    readonly void ICollection<T>.Add(T item) => throw new NotSupportedException();

    public readonly void Clear() => Unsafe.InitBlock(Pointer, 0, (uint)(sizeof(T) * Length));

    readonly bool ICollection<T>.Contains(T item) => AsSpan().Contains(item);

    readonly bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    public readonly NativeMemoryEnumerator<T> GetEnumerator() => new(_pointer, Length);

    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ReSharper disable once ArrangeModifiersOrder
    [Pure]
    public override readonly string ToString()
    {
        if (typeof(T) == typeof(char))
        {
            return Length == 0 ? string.Empty : new((char*)Pointer, 0, Length);
        }

        Type thisType = typeof(NativeMemory<T>);
        Type typeOfT = typeof(T);
        return $"{thisType.Namespace}.{nameof(NativeMemory<T>)}<{typeOfT.Namespace}.{typeOfT.Name}>[{Length}]";
    }

    [Pure]
    public readonly bool Equals(NativeMemory<T> other) => Length == other.Length && Pointer == other.Pointer;

    [Pure]
    // ReSharper disable once ArrangeModifiersOrder
    public override readonly bool Equals(object? obj) => obj is NativeMemory<T> other && Equals(other);

    [Pure]
    // ReSharper disable once ArrangeModifiersOrder
    public override readonly int GetHashCode() => HashCode.Combine((nuint)Pointer, Length);

    public static bool operator ==(NativeMemory<T> left, NativeMemory<T> right) => left.Equals(right);

    public static bool operator !=(NativeMemory<T> left, NativeMemory<T> right) => !(left == right);
}
