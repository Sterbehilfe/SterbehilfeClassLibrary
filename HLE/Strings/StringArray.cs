using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Collections;
using HLE.Marshalling;
using HLE.Memory;

namespace HLE.Strings;

/// <summary>
/// An array that is specialized in storing strings by optimizing data locality, which makes search operations faster, but comes at the cost of higher memory usage and initialization time.
/// </summary>
public sealed class StringArray : ICollection<string>, IReadOnlyCollection<string>, ICopyable<string>, ICountable, IIndexAccessible<string>,
    IEquatable<StringArray>, IReadOnlySpanProvider<string>, ICollectionProvider<string>
{
    public string this[int index]
    {
        get => _strings[index];
        set => SetString(index, value);
    }

    public string this[Index index]
    {
        get => _strings[index];
        set => SetString(index.GetOffset(Length), value);
    }

    public ReadOnlySpan<string> this[Range range] => _strings.AsSpan(range);

    public int Length => _strings.Length;

    int ICountable.Count => Length;

    int ICollection<string>.Count => Length;

    int IReadOnlyCollection<string>.Count => Length;

    bool ICollection<string>.IsReadOnly => false;

    internal readonly string[] _strings;
    internal readonly int[] _stringLengths;
    internal readonly int[] _stringStarts;
    internal char[] _stringChars;
    private int _freeCharBufferSpace;

    public StringArray(int length)
    {
        _strings = new string[length];
        _stringLengths = new int[length];
        _stringStarts = new int[length];
        int roundedLength = (int)BitOperations.RoundUpToPowerOf2((uint)length);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(roundedLength, 1 << 30, nameof(length));

        int charBufferLength = roundedLength << 2;
        _stringChars = GC.AllocateUninitializedArray<char>(charBufferLength);
        _freeCharBufferSpace = charBufferLength;
    }

    public StringArray(List<string> strings) : this(strings.Count)
        => FillArray(CollectionsMarshal.AsSpan(strings));

    public StringArray(params string[] strings) : this(strings.Length)
        => FillArray(strings);

    public StringArray(Span<string> strings) : this(strings.Length)
        => FillArray(strings);

    public StringArray(ReadOnlySpan<string> strings) : this(strings.Length)
        => FillArray(strings);

    public StringArray(IEnumerable<string> strings)
    {
        _strings = strings.ToArray();
        _stringLengths = new int[_strings.Length];
        _stringStarts = new int[_strings.Length];
        int charBufferLength = (int)BitOperations.RoundUpToPowerOf2((uint)_strings.Length) << 2;
        _stringChars = GC.AllocateUninitializedArray<char>(charBufferLength);
        _freeCharBufferSpace = charBufferLength;

        FillArray(_strings);
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetStringLength(int index) => _stringLengths[index];

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> GetChars(int index) => _stringChars.AsSpanUnsafe(_stringStarts[index], _stringLengths[index]);

    [Pure]
    public ReadOnlySpan<string> AsSpan() => _strings;

    [Pure]
    public string[] ToArray()
    {
        if (Length == 0)
        {
            return [];
        }

        string[] result = new string[Length];
        CopyWorker<string>.Copy(_strings, result);
        return result;
    }

    [Pure]
    public List<string> ToList()
    {
        if (Length == 0)
        {
            return [];
        }

        List<string> result = new(Length);
        CopyWorker<string> copyWorker = new(_strings);
        copyWorker.CopyTo(result);
        return result;
    }

    ReadOnlySpan<string> IReadOnlySpanProvider<string>.GetReadOnlySpan() => AsSpan();

    [Pure]
    public int IndexOf(string str, int startIndex = 0) => IndexOf(str.AsSpan(), startIndex);

    [Pure]
    public int IndexOf(ReadOnlySpan<char> chars, int startIndex = 0) => IndexOf(chars, StringComparison.Ordinal, startIndex);

    [Pure]
    public int IndexOf(string str, StringComparison comparison, int startIndex = 0) => IndexOf(str.AsSpan(), comparison, startIndex);

    [Pure]
    [SkipLocalsInit]
    public int IndexOf(ReadOnlySpan<char> chars, StringComparison comparison, int startIndex = 0)
    {
        ref string stringsReference = ref MemoryMarshal.GetArrayDataReference(_strings);
        ref int startsReference = ref MemoryMarshal.GetArrayDataReference(_stringStarts);
        ReadOnlySpan<char> charBuffer = _stringChars;
        ReadOnlySpan<int> stringLengths = _stringLengths;

        int charsLength = chars.Length;
        ref char charsReference = ref MemoryMarshal.GetReference(chars);
        ReadOnlySpan<int> lengths = stringLengths[startIndex..];

        Span<int> indices = default;
        int[]? rentedIndices = null;
        if (!MemoryHelper.UseStackAlloc<int>(lengths.Length))
        {
            rentedIndices = ArrayPool<int>.Shared.Rent(lengths.Length);
            indices = rentedIndices.AsSpan(..indices.Length);
        }
        else
        {
            indices = SpanMarshal.ReturnStackAlloced(stackalloc int[lengths.Length]);
        }

        try
        {
            int indicesLength = lengths.IndicesOf(charsLength, indices);
            indices = indices.SliceUnsafe(..indicesLength);

            for (int i = 0; i < indices.Length; i++)
            {
                int actualIndex = indices[i] + startIndex;
                ReadOnlySpan<char> str = Unsafe.Add(ref stringsReference, actualIndex);
                ref char stringReference = ref MemoryMarshal.GetReference(str);
                if (Unsafe.AreSame(ref charsReference, ref stringReference))
                {
                    return actualIndex;
                }

                int strStart = Unsafe.Add(ref startsReference, actualIndex);
                if (chars.Equals(charBuffer.SliceUnsafe(strStart..(strStart + charsLength)), comparison))
                {
                    return actualIndex;
                }
            }

            return -1;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rentedIndices);
        }
    }

    [Pure]
    public bool Contains(string str) => Contains(str.AsSpan());

    [Pure]
    public bool Contains(string str, StringComparison comparison) => IndexOf(str.AsSpan(), comparison) >= 0;

    [Pure]
    public bool Contains(ReadOnlySpan<char> chars) => IndexOf(chars) >= 0;

    [Pure]
    public bool Contains(ReadOnlySpan<char> chars, StringComparison comparison) => IndexOf(chars, comparison) >= 0;

    void ICollection<string>.Add(string str) => throw new NotSupportedException();

    bool ICollection<string>.Remove(string str) => throw new NotSupportedException();

    internal void MoveString(int sourceIndex, int destinationIndex)
    {
        if (sourceIndex == destinationIndex)
        {
            return;
        }

        Span<string> strings = _strings;
        Span<int> stringLengths = _stringLengths;
        Span<int> stringStarts = _stringStarts;
        Span<char> stringChars = _stringChars;

        string sourceString = strings[sourceIndex];
        int sourceLength = stringLengths[sourceIndex];
        int sourceStart = stringStarts[sourceIndex];
        int destinationLength = stringLengths[destinationIndex];
        int destinationStart = stringStarts[destinationIndex];

        int greaterIndex = sourceIndex > destinationIndex ? sourceIndex : destinationIndex;
        bool isSourceRightOfDestination = greaterIndex == sourceIndex;
        int smallerIndex = isSourceRightOfDestination ? destinationIndex : sourceIndex;
        if (isSourceRightOfDestination)
        {
            strings[destinationIndex..sourceIndex].CopyTo(strings[(destinationIndex + 1)..]);
            stringLengths[destinationIndex..sourceIndex].CopyTo(stringLengths[(destinationIndex + 1)..]);
            stringChars[destinationStart..sourceStart].CopyTo(stringChars[(destinationStart + sourceLength)..]);
        }
        else
        {
            strings[(sourceIndex + 1)..(destinationIndex + 1)].CopyTo(strings[sourceIndex..]);
            stringLengths[(sourceIndex + 1)..(destinationIndex + 1)].CopyTo(stringLengths[sourceIndex..]);
            stringChars[(sourceStart + sourceLength)..(destinationStart + destinationLength)].CopyTo(stringChars[sourceStart..]);
        }

        strings[destinationIndex] = sourceString;
        stringLengths[destinationIndex] = sourceLength;
        UpdateStringStarts(stringStarts, stringLengths, smallerIndex, greaterIndex);
        sourceString.CopyTo(stringChars[stringStarts[destinationIndex]..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateStringStarts(Span<int> stringStarts, Span<int> stringLengths, int startIndex, int endIndex)
    {
        int nextStart = stringStarts[startIndex] + stringLengths[startIndex];
        for (int i = startIndex + 1; i <= endIndex; i++)
        {
            stringStarts[i] = nextStart;
            nextStart += stringLengths[i];
        }
    }

    private void FillArray(ReadOnlySpan<string> strings)
    {
        Debug.Assert(strings.Length == Length);
        ref string stringsReference = ref MemoryMarshal.GetReference(strings);
        int stringsLength = strings.Length;
        for (int i = 0; i < stringsLength; i++)
        {
            SetString(i, Unsafe.Add(ref stringsReference, i));
        }
    }

    public void Clear()
    {
        Array.Clear(_strings);
        Array.Clear(_stringLengths);
        Array.Clear(_stringStarts);
        _freeCharBufferSpace = _stringChars.Length;
    }

    public ArrayEnumerator<string> GetEnumerator() => new(_strings, 0, _strings.Length);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(List<string> destination, int offset = 0)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(string[] destination, int offset = 0)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination, offset);
    }

    public void CopyTo(Memory<string> destination)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(Span<string> destination)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    public void CopyTo(ref string destination)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(ref destination);
    }

    public unsafe void CopyTo(string* destination)
    {
        CopyWorker<string> copyWorker = new(AsSpan());
        copyWorker.CopyTo(destination);
    }

    private void SetString(int index, string str)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));

        Span<int> stringLengths = _stringLengths;

        int stringLength = str.Length;
        Span<string> strings = _strings;
        Span<int> stringStarts = _stringStarts;
        Span<char> stringChars = _stringChars;

        if (stringLength == 0)
        {
            stringLengths[index] = 0;
            strings[index] = string.Empty;
            stringStarts[index] = 0;
            return;
        }

        int currentStringLength = stringLengths[index];
        int lengthOfLeftStrings = stringLengths[..index].SumUnchecked();
        strings[index] = str;
        stringLengths[index] = stringLength;
        stringStarts[index] = lengthOfLeftStrings;

        if (currentStringLength > stringLength)
        {
            Span<char> charsToCopy = stringChars[(lengthOfLeftStrings + currentStringLength)..];
            Span<char> charDestination = stringChars[(lengthOfLeftStrings + stringLength)..];
            charsToCopy.CopyTo(charDestination);

            str.CopyTo(stringChars[lengthOfLeftStrings..]);
            _freeCharBufferSpace -= currentStringLength - stringLength;
            return;
        }

        if (currentStringLength < stringLength)
        {
            GrowBufferIfNeeded(currentStringLength, stringLength);

            Span<char> charsToCopy = stringChars[(lengthOfLeftStrings + currentStringLength)..^_freeCharBufferSpace];
            Span<char> charDestination = stringChars[(lengthOfLeftStrings + stringLength)..];
            charsToCopy.CopyTo(charDestination);

            str.CopyTo(stringChars[lengthOfLeftStrings..]);
            _freeCharBufferSpace -= stringLength - currentStringLength;
            return;
        }

        str.CopyTo(stringChars[lengthOfLeftStrings..]);
    }

    private void GrowBufferIfNeeded(int currentStringLength, int newStringLength)
    {
        int neededSpace = Math.Abs(newStringLength - currentStringLength);
        if (_freeCharBufferSpace > neededSpace)
        {
            return;
        }

        int newBufferLength = (int)BitOperations.RoundUpToPowerOf2((uint)(_stringChars.Length + neededSpace));
        if (newBufferLength < _stringChars.Length)
        {
            ThrowMaximumBufferCapacityReached();
        }

        char[] newBuffer = GC.AllocateUninitializedArray<char>(newBufferLength);
        _freeCharBufferSpace += newBufferLength - _stringChars.Length;
        CopyWorker<char>.Copy(_stringChars, newBuffer);

        char[] oldBuffer = _stringChars;
        _stringChars = newBuffer;
        if (ArrayPool<char>.IsCommonlyPooledType)
        {
            ArrayPool<char>.Shared.Return(oldBuffer);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMaximumBufferCapacityReached()
        => throw new InvalidOperationException("The maximum buffer capacity has been reached.");

    public bool Equals(StringArray? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(StringArray? left, StringArray? right) => Equals(left, right);

    public static bool operator !=(StringArray? left, StringArray? right) => !(left == right);
}
