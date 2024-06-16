using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace HLE.Text;

public sealed partial class PooledStringBuilder
{
    [InterpolatedStringHandler]
    public readonly ref struct InterpolatedStringHandler
    {
        private readonly PooledStringBuilder _builder;

        private const int AssumedAverageFormattingLength = 16;

        public InterpolatedStringHandler(int literalLength, int formattedCount, PooledStringBuilder builder)
        {
            builder.EnsureCapacity(builder.Length + literalLength + formattedCount * AssumedAverageFormattingLength);
            _builder = builder;
        }

        public void AppendLiteral(string str) => _builder.Append(str);

        public void AppendFormatted(string str) => _builder.Append(str);

        public void AppendFormatted(List<char> chars) => _builder.Append(chars);

        public void AppendFormatted(char[] chars) => _builder.Append(chars);

        public void AppendFormatted(ReadOnlyMemory<char> memory) => _builder.Append(memory.Span);

        public void AppendFormatted(scoped ReadOnlySpan<char> chars) => _builder.Append(chars);

        public void AppendFormatted(char value) => _builder.Append(value);

        public void AppendFormatted<T>(T value) => _builder.Append(value);

        public void AppendFormatted<T>(T value, string? format) => _builder.Append(value, format);

        [Pure]
        public bool Equals(InterpolatedStringHandler other) => _builder.Equals(other._builder);

        [Pure]
        public override bool Equals(object? obj) => false;

        [Pure]
        public override int GetHashCode() => _builder.GetHashCode();

        public static bool operator ==(InterpolatedStringHandler left, InterpolatedStringHandler right) => left.Equals(right);

        public static bool operator !=(InterpolatedStringHandler left, InterpolatedStringHandler right) => !(left == right);
    }
}