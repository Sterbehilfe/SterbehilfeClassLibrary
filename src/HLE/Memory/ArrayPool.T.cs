using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

namespace HLE.Memory;

/// <summary>
/// A pool of arrays from which you can rent arrays and return arrays to in order to reuse them.<br/>
/// You can also return random arrays that were create anywhere else in the application to the pool in order to reuse them.
/// </summary>
/// <typeparam name="T">The type of items stored in the rented arrays.</typeparam>
public sealed partial class ArrayPool<T> : System.Buffers.ArrayPool<T>, IEquatable<ArrayPool<T>>
{
    public static new ArrayPool<T> Shared { get; } = new();

    internal readonly Bucket[] _buckets;

    [ThreadStatic]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ThreadStatic")]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "ThreadStatic")]
    [SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types")]
    private static ThreadLocalBucket t_threadLocalBucket;

    public ArrayPool()
    {
        int bucketCount = BitOperations.TrailingZeroCount(ArrayPool.MaximumArrayLength) - BitOperations.TrailingZeroCount(ArrayPool.MinimumArrayLength) + 1;
        Debug.Assert(ArrayPool.BucketCapacities.Length == bucketCount);

        Bucket[] buckets = GC.AllocateArray<Bucket>(bucketCount, true);
        int arrayLength = ArrayPool.MinimumArrayLength;
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new(arrayLength, ArrayPool.BucketCapacities[i]);
            arrayLength <<= 1;
        }

        _buckets = buckets;

        Debug.Assert(arrayLength >>> 1 == ArrayPool.MaximumArrayLength);
    }

    [Pure]
    public override T[] Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumLength);

        int length = minimumLength >= 1 << 30 ? Array.MaxLength : (int)BitOperations.RoundUpToPowerOf2((uint)minimumLength);
        switch (length)
        {
            case > ArrayPool.MaximumArrayLength:
                return GC.AllocateUninitializedArray<T>(length);
            case < ArrayPool.MinimumArrayLength:
                length = ArrayPool.MinimumArrayLength;
                break;
        }

        Debug.Assert(BitOperations.PopCount((uint)length) == 1);

        int bucketIndex = BitOperations.TrailingZeroCount(length) - ArrayPool.BucketIndexOffset;
        return TryRentFromThreadLocalBucket(length, bucketIndex, out T[]? array) ? array : RentFromSharedBuckets(bucketIndex);
    }

    [Pure]
    public T[] RentExact(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        switch (length)
        {
            case > ArrayPool.MaximumArrayLength:
                return GC.AllocateUninitializedArray<T>(length);
            case < ArrayPool.MinimumArrayLength:
                return length == 0 ? [] : new T[length];
        }

        T[]? array;
        int roundedLength = (int)BitOperations.RoundUpToPowerOf2((uint)length);
        int bucketIndex = BitOperations.TrailingZeroCount(roundedLength) - ArrayPool.BucketIndexOffset;
        if (roundedLength == length)
        {
            if (TryRentFromThreadLocalBucket(length, bucketIndex, out array))
            {
                return array;
            }
        }
        else
        {
            bucketIndex--;
        }

        Bucket bucket = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buckets), bucketIndex);
        return bucket.TryRentExact(length, out array) ? array : GC.AllocateUninitializedArray<T>(length, true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // don't inline as slow path
    private T[] RentFromSharedBuckets(int bucketIndex)
    {
        const int MaximumTryCount = 3;

        Bucket[] buckets = _buckets;
        ref Bucket bucket = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), bucketIndex);
        int bucketsLength = buckets.Length;
        int tryCount = 0;
        do
        {
            if (bucket.TryRent(out T[]? array))
            {
                return array;
            }

            bucket = ref Unsafe.Add(ref bucket, 1);
            bucketIndex++;
            tryCount++;
        }
        while (tryCount < MaximumTryCount && bucketIndex < bucketsLength);

        return Unsafe.Subtract(ref bucket, MaximumTryCount).Rent();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] // inline as fast path
    private static bool TryRentFromThreadLocalBucket(int arrayLength, int bucketIndex, [MaybeNullWhen(false)] out T[] array)
    {
        Debug.Assert(BitOperations.PopCount((uint)arrayLength) == 1);

        ref ThreadLocalBucket threadLocalBucket = ref t_threadLocalBucket;
        if (!threadLocalBucket.IsInitialized(arrayLength))
        {
            threadLocalBucket.SetInitialized(arrayLength);
            array = GC.AllocateUninitializedArray<T>(arrayLength, true);
            return true;
        }

        ref T[]? arrayReference = ref Unsafe.Add(ref threadLocalBucket.GetPoolReference(), bucketIndex);
        if (arrayReference is not null)
        {
            array = arrayReference;
            arrayReference = null; // remove reference from the pool
            return true;
        }

        array = null;
        return false;
    }

    [Pure]
    [MustDisposeResource]
    public RentedArray<T> RentAsRentedArray(int minimumLength) => new(Rent(minimumLength), this);

    public override void Return(T[]? array, bool clearArray = false)
    {
        if (!TryGetBucketIndex(array, out int bucketIndex, out int pow2Length))
        {
            return;
        }

        if (TryReturnToThreadLocalBucket(array, pow2Length, bucketIndex, clearArray))
        {
            return;
        }

        ReturnToSharedBucket(array, bucketIndex, clearArray);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] // don't inline as slow path
    private void ReturnToSharedBucket(T[] array, int bucketIndex, bool clearArray)
    {
        ref Bucket bucket = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buckets), bucketIndex);
        bucket.Return(array, clearArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] // inline as fast path
    private static bool TryReturnToThreadLocalBucket(T[] array, int pow2Length, int bucketIndex, bool clearArray)
    {
        // TODO: check if array can still be returned even though it is not a pow2 length
        if (BitOperations.PopCount((uint)array.Length) != 1)
        {
            return false;
        }

        ref ThreadLocalBucket threadLocalBucket = ref t_threadLocalBucket;
        if (!threadLocalBucket.IsInitialized(pow2Length))
        {
            ClearArrayIfNeeded(array, clearArray);
            threadLocalBucket.SetInitialized(pow2Length);
            Unsafe.Add(ref threadLocalBucket.GetPoolReference(), bucketIndex) = array;
            return true;
        }

        ref T[]? arrayReference = ref Unsafe.Add(ref threadLocalBucket.GetPoolReference(), bucketIndex);
        if (arrayReference is not null)
        {
            return false;
        }

        ClearArrayIfNeeded(array, clearArray);
        arrayReference = array;
        threadLocalBucket.SetInitialized(pow2Length);

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearArrayIfNeeded(T[] array, bool clearArray)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || clearArray)
        {
            Array.Clear(array);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetBucketIndex([NotNullWhen(true)] T[]? array, out int bucketIndex, out int pow2Length)
    {
        if (array is not { Length: >= ArrayPool.MinimumArrayLength and <= ArrayPool.MaximumArrayLength })
        {
            bucketIndex = -1;
            pow2Length = -1;
            return false;
        }

        pow2Length = (int)BitOperations.RoundUpToPowerOf2((uint)array.Length);
        if (pow2Length != array.Length)
        {
            pow2Length >>= 1;
        }

        bucketIndex = BitOperations.TrailingZeroCount(pow2Length) - ArrayPool.BucketIndexOffset;
        return true;
    }

    public void Clear()
    {
        Bucket[] buckets = _buckets;
        ref Bucket bucketReference = ref MemoryMarshal.GetArrayDataReference(buckets);
        int lengths = buckets.Length;
        for (int i = 0; i < lengths; i++)
        {
            ref Bucket bucket = ref Unsafe.Add(ref bucketReference, i);
            bucket.Clear();
        }
    }

    [Pure]
    public bool Equals([NotNullWhen(true)] ArrayPool<T>? other) => ReferenceEquals(this, other);

    [Pure]
    public override bool Equals([NotNullWhen(true)] object? obj) => ReferenceEquals(this, obj);

    [Pure]
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    public static bool operator ==(ArrayPool<T>? left, ArrayPool<T>? right) => Equals(left, right);

    public static bool operator !=(ArrayPool<T>? left, ArrayPool<T>? right) => !(left == right);
}
