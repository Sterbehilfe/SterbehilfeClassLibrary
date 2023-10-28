using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HLE.Marshalling;
using Xunit;

namespace HLE.Tests.Marshalling;

public sealed class StringMarshalTest
{
    [Fact]
    public void FastAllocateStringTest()
    {
        string str = StringMarshal.FastAllocateString(5, out Span<char> chars);
        "hello".CopyTo(chars);
        Assert.Equal("hello", str);
        Assert.Equal("hello".Length, str.Length);
        Assert.False(ReferenceEquals("hello", str));
    }

    [Fact]
    public void AsMutableSpanTest()
    {
        const string str = "hello";
        Span<char> chars = StringMarshal.AsMutableSpan(str);
        Assert.True(chars is "hello");
    }

    [Fact]
    public void AsStringTest()
    {
        ReadOnlySpan<char> span = "hello";
        string str = StringMarshal.AsString(span);
        Assert.True(span.SequenceEqual(str));

        ref char spanRef = ref MemoryMarshal.GetReference(span);
        ref char strRef = ref MemoryMarshal.GetReference(str.AsSpan());
        Assert.True(Unsafe.AreSame(ref spanRef, ref strRef));
    }
}
