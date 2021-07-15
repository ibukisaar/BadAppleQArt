﻿using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QArt.NET {
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{Value==0 ? \"\" : $\"a^{ExpTable[Value]} = \",nq}{Value==0 ? (byte)0 : Value}")]
    public readonly struct GF {
        const int N = 255;


        static ReadOnlySpan<byte> ValueTable => new byte[] {
            0x00,0x01,0x02,0x04,0x08,0x10,0x20,0x40,0x80,0x1d,0x3a,0x74,0xe8,0xcd,0x87,0x13, // 0-15
            0x26,0x4c,0x98,0x2d,0x5a,0xb4,0x75,0xea,0xc9,0x8f,0x03,0x06,0x0c,0x18,0x30,0x60, // 16-31
            0xc0,0x9d,0x27,0x4e,0x9c,0x25,0x4a,0x94,0x35,0x6a,0xd4,0xb5,0x77,0xee,0xc1,0x9f, // 32-47
            0x23,0x46,0x8c,0x05,0x0a,0x14,0x28,0x50,0xa0,0x5d,0xba,0x69,0xd2,0xb9,0x6f,0xde, // 48-63
            0xa1,0x5f,0xbe,0x61,0xc2,0x99,0x2f,0x5e,0xbc,0x65,0xca,0x89,0x0f,0x1e,0x3c,0x78, // 64-79
            0xf0,0xfd,0xe7,0xd3,0xbb,0x6b,0xd6,0xb1,0x7f,0xfe,0xe1,0xdf,0xa3,0x5b,0xb6,0x71, // 80-95
            0xe2,0xd9,0xaf,0x43,0x86,0x11,0x22,0x44,0x88,0x0d,0x1a,0x34,0x68,0xd0,0xbd,0x67, // 96-111
            0xce,0x81,0x1f,0x3e,0x7c,0xf8,0xed,0xc7,0x93,0x3b,0x76,0xec,0xc5,0x97,0x33,0x66, // 112-127
            0xcc,0x85,0x17,0x2e,0x5c,0xb8,0x6d,0xda,0xa9,0x4f,0x9e,0x21,0x42,0x84,0x15,0x2a, // 128-143
            0x54,0xa8,0x4d,0x9a,0x29,0x52,0xa4,0x55,0xaa,0x49,0x92,0x39,0x72,0xe4,0xd5,0xb7, // 144-159
            0x73,0xe6,0xd1,0xbf,0x63,0xc6,0x91,0x3f,0x7e,0xfc,0xe5,0xd7,0xb3,0x7b,0xf6,0xf1, // 160-175
            0xff,0xe3,0xdb,0xab,0x4b,0x96,0x31,0x62,0xc4,0x95,0x37,0x6e,0xdc,0xa5,0x57,0xae, // 176-191
            0x41,0x82,0x19,0x32,0x64,0xc8,0x8d,0x07,0x0e,0x1c,0x38,0x70,0xe0,0xdd,0xa7,0x53, // 192-207
            0xa6,0x51,0xa2,0x59,0xb2,0x79,0xf2,0xf9,0xef,0xc3,0x9b,0x2b,0x56,0xac,0x45,0x8a, // 208-223
            0x09,0x12,0x24,0x48,0x90,0x3d,0x7a,0xf4,0xf5,0xf7,0xf3,0xfb,0xeb,0xcb,0x8b,0x0b, // 224-239
            0x16,0x2c,0x58,0xb0,0x7d,0xfa,0xe9,0xcf,0x83,0x1b,0x36,0x6c,0xd8,0xad,0x47,0x8e, // 240-255
        };

        static ReadOnlySpan<byte> ExpTable => new byte[] {
            255,  0,  1, 25,  2, 50, 26,198,  3,223, 51,238, 27,104,199, 75, // 0-15
              4,100,224, 14, 52,141,239,129, 28,193,105,248,200,  8, 76,113, // 16-31
              5,138,101, 47,225, 36, 15, 33, 53,147,142,218,240, 18,130, 69, // 32-47
             29,181,194,125,106, 39,249,185,201,154,  9,120, 77,228,114,166, // 48-63
              6,191,139, 98,102,221, 48,253,226,152, 37,179, 16,145, 34,136, // 64-79
             54,208,148,206,143,150,219,189,241,210, 19, 92,131, 56, 70, 64, // 80-95
             30, 66,182,163,195, 72,126,110,107, 58, 40, 84,250,133,186, 61, // 96-111
            202, 94,155,159, 10, 21,121, 43, 78,212,229,172,115,243,167, 87, // 112-127
              7,112,192,247,140,128, 99, 13,103, 74,222,237, 49,197,254, 24, // 128-143
            227,165,153,119, 38,184,180,124, 17, 68,146,217, 35, 32,137, 46, // 144-159
             55, 63,209, 91,149,188,207,205,144,135,151,178,220,252,190, 97, // 160-175
            242, 86,211,171, 20, 42, 93,158,132, 60, 57, 83, 71,109, 65,162, // 176-191
             31, 45, 67,216,183,123,164,118,196, 23, 73,236,127, 12,111,246, // 192-207
            108,161, 59, 82, 41,157, 85,170,251, 96,134,177,187,204, 62, 90, // 208-223
            203, 89, 95,176,156,169,160, 81, 11,245, 22,235,122,117, 44,215, // 224-239
             79,174,213,233,230,231,173,232,116,214,244,234,168, 80, 88,175, // 240-255
        };

        static readonly UnmanagedArray<GF> MulTable = new UnmanagedArray<GF>(65536);

        public readonly static GPolynom[] Gs = new GPolynom[31];



        static GF() {
            // 创建乘法表

            ref byte expTable = ref MemoryMarshal.GetReference(ExpTable);
            ref byte valueTable = ref MemoryMarshal.GetReference(ValueTable);

            for (int b = 0; b < 256; b++) {
                MulTable[b] = 0;
            }

            for (int a = 1; a < 256; a++) {
                int offset = a << 8;
                MulTable[offset] = 0;
                for (int b = 1; b < 256; b++) {
                    int e = Unsafe.Add(ref expTable, a) + Unsafe.Add(ref expTable, b);
                    if (e >= N) e -= N;
                    MulTable[offset + b] = Unsafe.Add(ref valueTable, e + 1);
                }
            }


            // 枚举生成多项式

            Gs[1] = new GPolynom(new GF[] { 1 });

            for (int e = 1; e < 30; e++) {
                var lastG = Gs[e];
                var newG = new GPolynom(lastG.Count + 1);
                GF aN = ValueTable[e + 1];
                ref GF nMultiplier = ref aN.Multiplier;

                newG[0] = lastG[0] + aN;
                for (int i = 1; i < lastG.Count; i++) {
                    newG[i] = Unsafe.Add(ref nMultiplier, lastG[i - 1]) + lastG[i];
                }
                newG[^1] = Unsafe.Add(ref nMultiplier, lastG[^1]);

                Gs[e + 1] = newG;
            }
        }



        public readonly byte Value;

        public int Exp {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                ref byte expTable = ref MemoryMarshal.GetReference(ExpTable);
                return Unsafe.Add(ref expTable, Value);
            }
        }

        public ref GF Multiplier {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref MulTable[(nint)Value << 8];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GF(byte value) => Unsafe.As<byte, GF>(ref value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator byte(GF @this) => Unsafe.As<GF, byte>(ref @this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GF operator +(GF a, GF b) {
            return (GF)(Unsafe.As<GF, byte>(ref a) ^ Unsafe.As<GF, byte>(ref b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GF operator *(GF a, GF b) {
            return Unsafe.Add(ref a.Multiplier, b);
        }

        public static int PolynomMod(int a, int b) {
            int aBits = 32 - BitOperations.LeadingZeroCount((uint)a);
            int bBits = 32 - BitOperations.LeadingZeroCount((uint)b);
            for (int i = aBits - bBits; i >= 0; i--) {
                if ((a & (1 << (i + bBits - 1))) != 0) {
                    a ^= b << i;
                }
            }
            return a;
        }

        public string ToString(string? format, IFormatProvider? provider) {
            return Value == 0 ? "0" : $"a^{ExpTable[Value]} = {Value.ToString(format, provider)}";
        }

        public string ToString(string? format) {
            return ToString(format, null);
        }

        public string ToString(IFormatProvider? provider) {
            return ToString(null, provider);
        }

        public override string ToString() {
            return ToString(null, null);
        }

        /// <summary>
        /// 生成多项式
        /// </summary>
        public readonly struct GPolynom {
            readonly UnmanagedArray<GF> coefficients;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public GPolynom(ReadOnlySpan<GF> coefficients) => this.coefficients = new UnmanagedArray<GF>(coefficients);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public GPolynom(int count) => coefficients = new UnmanagedArray<GF>(count);


            public int Count {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => coefficients.Length;
            }

            public ref GF this[int index] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref coefficients[index];
            }

            public override string ToString() {
                if (coefficients is null) return "";

                var sb = new StringBuilder();
                sb.Append("x^").Append(Count);
                for (int i = 0; i < Count; i++) {
                    sb.Append(" + a^").Append(coefficients[i].Exp);

                    int xExp = Count - 1 - i;
                    if (xExp >= 2) {
                        sb.Append("*x^").Append(xExp);
                    } else if (xExp == 1) {
                        sb.Append("*x");
                    }
                }
                return sb.ToString();
            }
        }
    }
}
