using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace HLE.Collections;

public ref struct ValueStack<T>
{
    public int Count { get; private set; }

    public readonly int Capacity => _stack.Length;

    private readonly Span<T> _stack = Span<T>.Empty;

    public ValueStack()
    {
    }

    public ValueStack(Span<T> stack, int count = 0)
    {
        _stack = stack;
        Count = count;
    }

    public void Push(T item) => _stack[Count++] = item;

    public T Pop() => _stack[--Count];

    [Pure]
    public readonly T Peek() => _stack[Count - 1];

    public bool TryPush(T item)
    {
        if (Count >= Capacity)
        {
            return false;
        }

        Push(item);
        return true;
    }

    public bool TryPop([MaybeNullWhen(false)] out T item)
    {
        if (Count <= 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    public readonly bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (Count <= 0)
        {
            item = default;
            return false;
        }

        item = Peek();
        return true;
    }

    public void Clear()
    {
        Count = 0;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            _stack.Clear();
        }
    }

    [Pure]
    public readonly T[] ToArray()
    {
        return _stack[..Count].ToArray();
    }

    public static implicit operator ValueStack<T>(Span<T> stack) => new(stack);

    public static implicit operator ValueStack<T>(T[] stack) => new(stack);
}