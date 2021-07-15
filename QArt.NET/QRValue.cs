using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct QRValue : IEquatable<QRValue> {
        public static readonly QRValue White = false;
        public static readonly QRValue Black = true;

        private readonly bool value;

        public bool IsWhite { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => !value; }
        public bool IsBlack { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator QRValue(bool value) => Unsafe.As<bool, QRValue>(ref value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(in QRValue value) => value.value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator QRValue(byte value) => Unsafe.As<byte, QRValue>(ref value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(QRValue a, QRValue b) => (bool)a == (bool)b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(QRValue a, QRValue b) => (bool)a != (bool)b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(QRValue other) => this == other;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) => obj is QRValue other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => ((bool)this).GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => value ? "黑" : "白";
    }
}
