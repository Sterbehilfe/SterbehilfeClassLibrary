using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Memory;

namespace HLE.Collections;

public ref struct ValueList<T> where T : IEquatable<T>
{
    public readonly ref T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_buffer.Length);
            return ref Unsafe.Add(ref MemoryMarshal.GetReference(_buffer), index);
        }
    }

    public readonly ref T this[Index index] => ref this[index.GetOffset(_buffer.Length)];

    public readonly Span<T> this[Range range] => _buffer[range];

    public int Count { get; private set; }

    public readonly int Capacity => _buffer.Length;

    private readonly Span<T> _buffer = [];

    public static ValueList<T> Empty => new();

    public ValueList()
    {
    }

    public ValueList(Span<T> buffer) => _buffer = buffer;

    [Pure]
    public readonly Span<T> AsSpan() => _buffer[..Count];

    [Pure]
    public readonly T[] ToArray()
    {
        int count = Count;
        if (count == 0)
        {
            return [];
        }

        T[] result = GC.AllocateUninitializedArray<T>(count);
        CopyWorker<T>.Copy(_buffer[..count], result);
        return result;
    }

    [Pure]
    public readonly List<T> ToList()
    {
        int count = Count;
        if (count == 0)
        {
            return [];
        }

        List<T> result = new(count);
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(result);
        return result;
    }

    public void Add(T item)
    {
        ThrowIfNotEnoughSpace(1);
        _buffer[Count++] = item;
    }

    public void AddRange(IEnumerable<T> items)
    {
        int count = Count;
        if (items.TryGetReadOnlySpan(out ReadOnlySpan<T> span))
        {
            ThrowIfNotEnoughSpace(span.Length);
            Span<T> destination = _buffer[count..];
            CopyWorker<T>.Copy(span, destination);
            Count = count + span.Length;
            return;
        }

        if (items.TryGetNonEnumeratedCount(out int itemCount))
        {
            ThrowIfNotEnoughSpace(itemCount);
            ref T destinationReference = ref MemoryMarshal.GetReference(_buffer);
            foreach (T item in items)
            {
                Unsafe.Add(ref destinationReference, count++) = item;
            }

            Count = count;
            return;
        }

        foreach (T item in items)
        {
            Add(item);
        }
    }

    public void AddRange(List<T> items) => AddRange(CollectionsMarshal.AsSpan(items));

    public void AddRange(params T[] items) => AddRange(items.AsSpan());

    public void AddRange(Span<T> items) => AddRange((ReadOnlySpan<T>)items);

    public void AddRange(ReadOnlySpan<T> items)
    {
        ThrowIfNotEnoughSpace(items.Length);
        Span<T> destination = _buffer[Count..];
        CopyWorker<T>.Copy(items, destination);
        Count += items.Length;
    }

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _buffer[..Count].Clear();
        }

        Count = 0;
    }

    [Pure]
    public readonly bool Contains(T item) => IndexOf(item) >= 0;

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        int count = Count;
        Span<T> buffer = _buffer[..count];
        buffer[(index + 1)..].CopyTo(buffer[index..]);
        Count = count - 1;
        return true;
    }

    [Pure]
    public readonly int IndexOf(T item) => _buffer[..Count].IndexOf(item);

    public void Insert(int index, T item)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);
        ThrowIfNotEnoughSpace(1);

        _buffer[index..^1].CopyTo(_buffer[(index + 1)..]);
        _buffer[index] = item;
        Count++;
    }

    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);

        _buffer[(index + 1)..].CopyTo(_buffer[index..]);
        Count--;
    }

    public readonly void CopyTo(List<T> destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(T[] destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public readonly void CopyTo(Memory<T> destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(Span<T> destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public readonly void CopyTo(ref T destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(ref destination);
    }

    public readonly unsafe void CopyTo(T* destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void ThrowIfNotEnoughSpace(int itemsToAdd)
    {
        if (itemsToAdd > Capacity - Count)
        {
            ThrowNotEnoughSpace();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNotEnoughSpace() => throw new InvalidOperationException("Maximum buffer capacity reached.");

    [Pure]
    public readonly MemoryEnumerator<T> GetEnumerator() => new(ref MemoryMarshal.GetReference(_buffer), Count);

    [Pure]
    public readonly bool Equals(ValueList<T> other)
        => _buffer == other._buffer && Count == other.Count;

    [Pure]
    public override readonly bool Equals([NotNullWhen(true)] object? obj) => false;

    [Pure]
    public override readonly int GetHashCode() => _buffer.Length;

    public static bool operator ==(ValueList<T> left, ValueList<T> right) => left.Equals(right);

    public static bool operator !=(ValueList<T> left, ValueList<T> right) => !(left == right);
}
