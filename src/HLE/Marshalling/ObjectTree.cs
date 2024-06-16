using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using HLE.IL;
using HLE.Memory;

namespace HLE.Marshalling;

internal static class ObjectTree
{
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> s_fieldInfoCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_getInlineArrayElementsSizeCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_getArrayElementsSizeCache = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_getSizeCache = new();

    [Pure]
    public static unsafe nuint GetSize<T>(ref T obj)
    {
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            return (uint)sizeof(T);
        }

        nuint size;
        if (typeof(T).IsValueType)
        {
            size = (uint)sizeof(T);
            if (IsInlineArray<T>(out int length))
            {
                return size + GetInlineArrayElementsSize(ref obj, length);
            }
        }
        else
        {
            if (obj is null)
            {
                return 0;
            }

#pragma warning disable HAA0601 // Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site.
            size = ObjectMarshal.GetObjectSize(obj);
#pragma warning restore HAA0601
            if (typeof(T) == typeof(string))
            {
                return size;
            }

            if (typeof(T).IsArray)
            {
                if (typeof(T).IsSZArray)
                {
                    Type elementType = typeof(T).GetElementType()!;
                    if (ObjectMarshal.IsReferenceOrContainsReference(elementType))
                    {
#pragma warning disable HAA0601 // Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site.
                        return size + GetArrayElementsSize(Unsafe.As<Array>(obj), elementType);
#pragma warning restore HAA0601
                    }

                    return size;
                }

                ThrowHelper.ThrowNotSupportedException("Multidimensional arrays are not yet supported.");
            }
        }

        return size + GetFieldsSize(ref obj);
    }

    private static nuint GetFieldsSize<T>(ref T obj)
    {
        nuint size = 0;
        ReadOnlySpan<FieldInfo> instanceFields = GetFields(typeof(T));
#pragma warning disable HAA0601 // Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site.
        object o = obj!; // boxing into a local to only box once and not for each loop iteration
#pragma warning restore HAA0601
        foreach (FieldInfo field in instanceFields)
        {
            if (!ObjectMarshal.IsReferenceOrContainsReference(field.FieldType))
            {
                continue;
            }

            object? fieldValue = field.GetValue(o); // TODO: boxes 'obj' and the field's value
            size += fieldValue is not null ? GetSizeNonGeneric(fieldValue, field.FieldType) : 0;
        }

        return size;
    }

    private static unsafe nuint GetSizeNonGeneric(object? obj, Type type)
    {
        Debug.Assert(obj is not null);

        if (!s_getSizeCache.TryGetValue(type, out MethodInfo? method))
        {
            MethodInfo nonGenericMethod = typeof(ObjectTree).GetMethod(nameof(GetSize), BindingFlags.Public | BindingFlags.Static)!;
#pragma warning disable HAA0101
            method = nonGenericMethod.MakeGenericMethod(type);
#pragma warning restore HAA0101
            s_getSizeCache.TryAdd(type, method);
        }

        delegate*<ref byte, nuint> getSize = (delegate*<ref byte, nuint>)method.MethodHandle.GetFunctionPointer();
        return type.IsValueType ? getSize(ref UnsafeIL.Unbox<byte>(obj)) : getSize(ref Unsafe.As<object, byte>(ref obj));
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
    private static ReadOnlySpan<FieldInfo> GetFields(Type type)
    {
        if (s_fieldInfoCache.TryGetValue(type, out FieldInfo[]? fieldInfos))
        {
            return fieldInfos;
        }

        fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        s_fieldInfoCache.TryAdd(type, fieldInfos);
        return fieldInfos;
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
    private static unsafe nuint GetArrayElementsSize(Array array, Type elementType)
    {
        Debug.Assert(array.Rank == 1);
        Debug.Assert(ObjectMarshal.IsReferenceOrContainsReference(array.GetType().GetElementType()!));

        if (!s_getArrayElementsSizeCache.TryGetValue(elementType, out MethodInfo? method))
        {
            MethodInfo nonGenericMethod = typeof(ObjectTree).GetMethod(nameof(GetArrayElementsSizeCore), BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning disable HAA0101
            method = nonGenericMethod.MakeGenericMethod(elementType);
#pragma warning restore HAA0101
            s_getArrayElementsSizeCache.TryAdd(elementType, method);
        }

        delegate*<Array, nuint> getArrayElementsSize = (delegate*<Array, nuint>)method.MethodHandle.GetFunctionPointer();
        return getArrayElementsSize(array);
    }

    private static nuint GetArrayElementsSizeCore<T>(T[] array)
    {
        nuint size = 0;
        if (!typeof(T).IsValueType)
        {
            for (int i = 0; i < array.Length; i++)
            {
                size += GetSize(ref array[i]);
            }
        }
        else
        {
            for (int i = 0; i < array.Length; i++)
            {
                size += GetFieldsSize(ref array[i]);
            }
        }

        return size;
    }

    private static bool IsInlineArray<T>(out int arrayLength)
    {
        InlineArrayAttribute? attribute = typeof(T).GetCustomAttribute<InlineArrayAttribute>();
        if (attribute is null)
        {
            arrayLength = 0;
            return false;
        }

        arrayLength = attribute.Length;
        return true;
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
    private static unsafe nuint GetInlineArrayElementsSize<T>(ref T array, int length)
    {
        Debug.Assert(typeof(T).IsValueType);

        ReadOnlySpan<FieldInfo> fields = GetFields(typeof(T));
        Debug.Assert(fields.Length == 1);

        FieldInfo field = fields[0];
        Type arrayElementType = field.FieldType;

        if (!s_getInlineArrayElementsSizeCache.TryGetValue(typeof(T), out MethodInfo? method))
        {
            MethodInfo nonGenericMethod = typeof(ObjectTree).GetMethod(nameof(GetInlineArrayElementsSizeCore), BindingFlags.Static | BindingFlags.NonPublic)!;
#pragma warning disable HAA0101
            method = nonGenericMethod.MakeGenericMethod(typeof(T), arrayElementType);
#pragma warning restore HAA0101
            s_getInlineArrayElementsSizeCache.TryAdd(typeof(T), method);
        }

        delegate*<ref T, int, nuint> getInlineArrayElementsSize = (delegate*<ref T, int, nuint>)method.MethodHandle.GetFunctionPointer();
        return getInlineArrayElementsSize(ref array, length);
    }

    private static nuint GetInlineArrayElementsSizeCore<TArray, TElement>(ref TArray array, int length) where TArray : struct
    {
        Debug.Assert(RuntimeHelpers.IsReferenceOrContainsReferences<TElement>());

        nuint size = 0;
        Span<TElement> elements = InlineArrayHelpers.AsSpan<TArray, TElement>(ref array, length);
        if (!typeof(TElement).IsValueType)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                size += GetSize(ref elements[i]);
            }
        }
        else
        {
            for (int i = 0; i < elements.Length; i++)
            {
                size += GetFieldsSize(ref elements[i]);
            }
        }

        return size;
    }
}
