using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    internal readonly struct BoolByte {
        [FieldOffset(0)]
        public readonly bool Bool;
        [FieldOffset(0)]
        public readonly byte Byte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(in BoolByte @this) => @this.Bool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator byte(in BoolByte @this) => @this.Byte;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BoolByte(bool @bool) => Unsafe.As<bool, BoolByte>(ref @bool);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BoolByte(byte @byte) => Unsafe.As<byte, BoolByte>(ref @byte);
    }
}
