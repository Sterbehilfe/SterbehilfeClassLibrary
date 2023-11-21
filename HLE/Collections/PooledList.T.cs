using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Memory;

namespace HLE.Collections;

// ReSharper disable once UseNameofExpressionForPartOfTheString
[DebuggerDisplay("Count = {Count}")]
[CollectionBuilder(typeof(PooledList), nameof(PooledList.Create))]
public sealed class PooledList<T>(int capacity)
    : IList<T>, ICopyable<T>, ICountable, IEquatable<PooledList<T>>, IDisposable, IIndexAccessible<T>, IReadOnlyList<T>, ISpanProvider<T>,
        ICollectionProvider<T>
    where T : IEquatable<T>
{
    public ref T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);
            return ref Unsafe.Add(ref _buffer.Reference, index);
        }
    }

    T IIndexAccessible<T>.this[int index] => this[index];

    T IReadOnlyList<T>.this[int index] => this[index];

    T IList<T>.this[int index]
    {
        get => this[index];
        set => this[index] = value;
    }

    public ref T this[Index index] => ref this[index.GetOffset(Count)];

    public Span<T> this[Range range] => AsSpan(range);

    public int Count { get; internal set; }

    public int Capacity => _buffer.Length;

    bool ICollection<T>.IsReadOnly => false;

    internal RentedArray<T> _buffer = capacity == 0 ? [] : ArrayPool<T>.Shared.RentAsRentedArray(capacity);

    public PooledList() : this(0)
    {
    }

    public PooledList(ReadOnlySpan<T> items) : this(items.Length)
    {
        CopyWorker<T>.Copy(items, _buffer.AsSpan());
        Count = items.Length;
    }

    public void Dispose() => _buffer.Dispose();

    [Pure]
    public Span<T> AsSpan() => _buffer.AsSpan(..Count);

    [Pure]
    public Span<T> AsSpan(int start) => new Slicer<T>(ref _buffer.Reference, Count).CreateSpan(start);

    [Pure]
    public Span<T> AsSpan(int start, int length) => new Slicer<T>(ref _buffer.Reference, Count).CreateSpan(start, length);

    [Pure]
    public Span<T> AsSpan(Range range) => new Slicer<T>(ref _buffer.Reference, Count).CreateSpan(range);

    [Pure]
    public T[] ToArray()
    {
        if (Count == 0)
        {
            return [];
        }

        T[] result = GC.AllocateUninitializedArray<T>(Count);
        CopyWorker<T>.Copy(_buffer.AsSpan(..Count), result);
        return result;
    }

    [Pure]
    public List<T> ToList()
    {
        if (Count == 0)
        {
            return [];
        }

        List<T> result = new(Count);
        CopyWorker<T> copyWorker = new(ref _buffer.Reference, Count);
        copyWorker.CopyTo(result);
        return result;
    }

    Span<T> ISpanProvider<T>.GetSpan() => AsSpan();

    private void GrowIfNeeded(int sizeHint)
    {
        int freeSpace = Capacity - Count;
        if (freeSpace >= sizeHint)
        {
            return;
        }

        int neededSize = sizeHint - freeSpace;
        int newSize = BufferHelpers.GrowByPow2(_buffer.Length, neededSize);
        using RentedArray<T> oldBuffer = _buffer;
        _buffer = ArrayPool<T>.Shared.RentAsRentedArray(newSize);
        if (Count != 0)
        {
            CopyWorker<T>.Copy(oldBuffer.AsSpan(..Count), _buffer.AsSpan());
        }
    }

    public void Add(T item)
    {
        GrowIfNeeded(1);
        Unsafe.Add(ref _buffer.Reference, Count++) = item;
    }

    public void AddRange(IEnumerable<T> items)
    {
        if (items.TryGetNonEnumeratedCount(out int itemsCount))
        {
            if (itemsCount == 0)
            {
                return;
            }

            GrowIfNeeded(itemsCount);
            T[] buffer = _buffer.Array;
            if (items.TryNonEnumeratedCopyTo(buffer, Count))
            {
                Count += itemsCount;
                return;
            }

            ref T destination = ref _buffer.Reference;
            foreach (T item in items)
            {
                Unsafe.Add(ref destination, Count++) = item;
            }

            return;
        }

        foreach (T item in items)
        {
            Add(item);
        }
    }

    public void AddRange(List<T> items) => AddRange((ReadOnlySpan<T>)CollectionsMarshal.AsSpan(items));

    public void AddRange(params T[] items) => AddRange((ReadOnlySpan<T>)items);

    public void AddRange(Span<T> items) => AddRange((ReadOnlySpan<T>)items);

    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
        {
            return;
        }

        GrowIfNeeded(items.Length);
        ref T destination = ref Unsafe.Add(ref _buffer.Reference, Count);
        CopyWorker<T> copyWorker = new(items);
        copyWorker.CopyTo(ref destination);
        Count += items.Length;
    }

    public void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _buffer.AsSpan(..Count).Clear();
        }

        Count = 0;
    }

    public void EnsureCapacity(int capacity) => GrowIfNeeded(capacity - Capacity);

    [Pure]
    public bool Contains(T item) => _buffer.AsSpan(..Count).Contains(item);

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        _buffer.AsSpan((index + 1)..).CopyTo(_buffer.AsSpan(index..));
        Count--;
        return true;
    }

    [Pure]
    public int IndexOf(T item) => _buffer.AsSpan(..Count).IndexOf(item);

    public void Insert(int index, T item)
    {
        GrowIfNeeded(1);
        Count++;
        _buffer.AsSpan(index..^1).CopyTo(_buffer.AsSpan((index + 1)..));
        _buffer[index] = item;
    }

    public void RemoveAt(int index)
    {
        _buffer.AsSpan((index + 1)..).CopyTo(_buffer.AsSpan(index..));
        Count--;
    }

    public void CopyTo(List<T> destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(T[] destination, int offset = 0)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(Memory<T> destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(Span<T> destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(ref T destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(ref destination);
    }

    public unsafe void CopyTo(T* destination)
    {
        CopyWorker<T> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    [Pure]
    public ArrayEnumerator<T> GetEnumerator() => new(_buffer.Array, 0, Count);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [Pure]
    public bool Equals(PooledList<T>? other) => ReferenceEquals(this, other);

    [Pure]
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    [Pure]
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(PooledList<T>? left, PooledList<T>? right) => Equals(left, right);

    public static bool operator !=(PooledList<T>? left, PooledList<T>? right) => !(left == right);
}
