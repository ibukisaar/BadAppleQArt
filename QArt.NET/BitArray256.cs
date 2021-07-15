using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitArray256 {
        private ulong v0, v1, v2, v3;

        public BitArray256(ReadOnlySpan<char> bits) {
            this = default;

            for (int i = 0; i < bits.Length; i++) {
                if (bits[i] == '1') {
                    this[i] = true;
                }
            }
        }

        public bool this[int i] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => (Unsafe.Add(ref Unsafe.AsRef(v0), i >> 6) & (1UL << (i & 63))) != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set {
                if (value) {
                    Unsafe.Add(ref v0, i >> 6) |= 1UL << (i & 63);
                } else {
                    Unsafe.Add(ref v0, i >> 6) &= ~(1UL << (i & 63));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Xor(in BitArray256 other) {
            v0 ^= other.v0;
            v1 ^= other.v1;
            v2 ^= other.v2;
            v3 ^= other.v3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int FirstOne() {
            if (v0 != 0) return BitOperations.TrailingZeroCount(v0);
            if (v1 != 0) return 64 + BitOperations.TrailingZeroCount(v1);
            if (v2 != 0) return 128 + BitOperations.TrailingZeroCount(v2);
            if (v3 != 0) return 192 + BitOperations.TrailingZeroCount(v3);
            return -1;
        }

        [SkipLocalsInit]
        unsafe public readonly override string ToString() {
            char* buffer = stackalloc char[256];
            for (int i = 0; i < 256; i++) {
                buffer[i] = this[i] ? '1' : '0';
            }
            return new string(buffer, 0, 256);
        }
    }
}
