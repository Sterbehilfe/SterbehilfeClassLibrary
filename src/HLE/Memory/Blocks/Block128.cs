using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace HLE.Memory.Blocks;

[StructLayout(LayoutKind.Explicit, Size = 128)]
[SuppressMessage("Major Code Smell", "S3898:Value types should implement \"IEquatable<T>\"")]
public readonly struct Block128;