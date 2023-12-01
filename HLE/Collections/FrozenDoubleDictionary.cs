using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using HLE.Memory;

namespace HLE.Collections;

// ReSharper disable once UseNameofExpressionForPartOfTheString
[DebuggerDisplay("Count = {Count}")]
public sealed class FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue> : ICollection<TValue>, IReadOnlyCollection<TValue>, ICopyable<TValue>,
    ICountable, IEquatable<FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>>, IReadOnlySpanProvider<TValue>, ICollectionProvider<TValue>
    where TPrimaryKey : IEquatable<TPrimaryKey>
    where TSecondaryKey : IEquatable<TSecondaryKey>
{
    public TValue this[TPrimaryKey key] => _values[key];

    public TValue this[TSecondaryKey key] => _values[_secondaryKeyTranslations[key]];

    public int Count => _values.Count;

    bool ICollection<TValue>.IsReadOnly => true;

    public ImmutableArray<TValue> Values => _values.Values;

    internal readonly FrozenDictionary<TPrimaryKey, TValue> _values;
    internal readonly FrozenDictionary<TSecondaryKey, TPrimaryKey> _secondaryKeyTranslations;

    public static FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue> Empty { get; } = new([]);

    private FrozenDoubleDictionary(DoubleDictionary<TPrimaryKey, TSecondaryKey, TValue> dictionary,
        IEqualityComparer<TPrimaryKey>? primaryKeyEqualityComparer = null,
        IEqualityComparer<TSecondaryKey>? secondaryKeyEqualityComparer = null)
    {
        _values = dictionary._values.ToFrozenDictionary(primaryKeyEqualityComparer);
        _secondaryKeyTranslations = dictionary._secondaryKeyTranslations.ToFrozenDictionary(secondaryKeyEqualityComparer);
    }

    public static FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue> Create(DoubleDictionary<TPrimaryKey, TSecondaryKey, TValue> dictionary,
        IEqualityComparer<TPrimaryKey>? primaryKeyEqualityComparer = null,
        IEqualityComparer<TSecondaryKey>? secondaryKeyEqualityComparer = null)
        => dictionary.Count == 0 ? Empty : new(dictionary, primaryKeyEqualityComparer, secondaryKeyEqualityComparer);

    public bool TryGetByPrimaryKey(TPrimaryKey key, [MaybeNullWhen(false)] out TValue value)
        => _values.TryGetValue(key, out value);

    public bool TryGetBySecondaryKey(TSecondaryKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_secondaryKeyTranslations.TryGetValue(key, out TPrimaryKey? primaryKey))
        {
            return TryGetByPrimaryKey(primaryKey, out value);
        }

        value = default;
        return false;
    }

    [Pure]
    public bool ContainsPrimaryKey(TPrimaryKey key) => _values.ContainsKey(key);

    [Pure]
    public bool ContainsSecondaryKey(TSecondaryKey key) => _secondaryKeyTranslations.ContainsKey(key);

    [Pure]
    public bool Contains(TValue item) => _values.Values.Contains(item);

    ReadOnlySpan<TValue> IReadOnlySpanProvider<TValue>.GetReadOnlySpan() => Values.AsSpan();

    public TValue[] ToArray()
    {
        if (Count == 0)
        {
            return [];
        }

        TValue[] result = GC.AllocateUninitializedArray<TValue>(Count);
        CopyWorker<TValue>.Copy(Values.AsSpan(), result);
        return result;
    }

    public List<TValue> ToList()
    {
        if (Count == 0)
        {
            return [];
        }

        List<TValue> result = new(Count);
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(result);
        return result;
    }

    public void CopyTo(List<TValue> destination, int offset = 0)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(TValue[] destination, int offset = 0)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(Memory<TValue> destination)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(Span<TValue> destination)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(ref TValue destination)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(ref destination);
    }

    public unsafe void CopyTo(TValue* destination)
    {
        CopyWorker<TValue> copyWorker = new(Values.AsSpan());
        copyWorker.CopyTo(destination);
    }

    void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException();

    void ICollection<TValue>.Clear() => throw new NotSupportedException();

    public bool Remove(TValue item) => throw new NotSupportedException();

    public ArrayEnumerator<TValue> GetEnumerator()
    {
        TValue[]? array = ImmutableCollectionsMarshal.AsArray(Values);
        Debug.Assert(array is not null);
        return new(array, 0, Count);
    }

    IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    [Pure]
    public bool Equals([NotNullWhen(true)] FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>? other) => ReferenceEquals(this, other);

    [Pure]
    public override bool Equals([NotNullWhen(true)] object? obj) => ReferenceEquals(this, obj);

    [Pure]
    public override int GetHashCode() => HashCode.Combine(_secondaryKeyTranslations, _values);

    public static bool operator ==(FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>? left, FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>? right)
        => Equals(left, right);

    public static bool operator !=(FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>? left, FrozenDoubleDictionary<TPrimaryKey, TSecondaryKey, TValue>? right)
        => !(left == right);
}
