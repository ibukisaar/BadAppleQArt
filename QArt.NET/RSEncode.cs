using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    unsafe public static class RSEncode {
        const int MaxEccLength = 30;

        static readonly int[] maxMsgLengths = new int[] { 0, 0, 0, 0, 0, 0, 0, 19, 0, 0, 34, 0, 0, 13, 0, 55, 28, 9, 69, 0, 81, 0, 88, 0, 99, 0, 108, 0, 117, 0, 123 };
        static readonly UnmanagedArray<ulong>?[] eccTablesRef = new UnmanagedArray<ulong>[MaxEccLength + 1];
        static readonly UnmanagedArray<UnmanagedArray<ulong>.RawArray> eccTables = new UnmanagedArray<UnmanagedArray<ulong>.RawArray>(MaxEccLength + 1, default);

        static UnmanagedArray<ulong>? CreateEccTable(int eccLen) {
            int maxMsgLen = maxMsgLengths[eccLen];
            if (maxMsgLen is 0) return null;

            int u64Count = (eccLen + 7) >> 3;
            if ((u64Count & (u64Count - 1)) != 0) {
                u64Count = 2 << BitOperations.Log2((uint)u64Count);
            }
            int u64Bytes = u64Count * sizeof(ulong);
            var u64Buffer = new UnmanagedArray<ulong>(maxMsgLen * u64Count << 8);
            byte* u8Buffer = (byte*)u64Buffer.Pointer;
            GF.GPolynom gp = GF.Gs[eccLen];

            GF* prevEcc = stackalloc GF[u64Bytes];
            prevEcc[0] = 1;

            int offset = 0;
            for (int msgLen = 1; msgLen <= maxMsgLen; msgLen++, offset += u64Count << 8) {
                u64Buffer.AsSpan(offset, u64Count).Clear();

                GF* pEcc1 = (GF*)u8Buffer + offset * 8 + u64Bytes;
                ref GF divMultiplier = ref prevEcc[0].Multiplier;
                int eccLen_1 = eccLen - 1;
                for (int i = 0; i < eccLen_1; i++) {
                    pEcc1[i] = prevEcc[i + 1] + Unsafe.Add(ref divMultiplier, gp[i]);
                }
                pEcc1[eccLen_1] = Unsafe.Add(ref divMultiplier, gp[eccLen_1]);

                GF* pEccN = pEcc1 + u64Bytes;
                for (int c = 2; c < 256; c++, pEccN += u64Bytes) {
                    ref GF multiplier = ref ((GF)c).Multiplier;
                    for (int i = 0; i < eccLen; i++) {
                        pEccN[i] = Unsafe.Add(ref multiplier, pEcc1[i]);
                    }
                }

                for (int i = 0; i < u64Count; i++) {
                    ((ulong*)prevEcc)[i] = ((ulong*)pEcc1)[i];
                }
            }

            return u64Buffer;
        }

        static RSEncode() {
            for (int eccLen = 1; eccLen <= MaxEccLength; eccLen++) {
                if (CreateEccTable(eccLen) is UnmanagedArray<ulong> eccTable) {
                    eccTablesRef[eccLen] = eccTable;
                    eccTables[eccLen] = eccTable.Raw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Encode(ReadOnlySpan<byte> msg, int eccCount) {
            var ecc = GC.AllocateUninitializedArray<byte>(eccCount);
            Encode(msg, ecc);
            return ecc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Encode(ReadOnlySpan<byte> msg, Span<byte> outEcc) {
            switch ((outEcc.Length + 7) >> 3) {
                case 1: Encode1(msg, outEcc); break;
                case 2: Encode2(msg, outEcc); break;
                case 3: Encode3(msg, outEcc); break;
                case 4: Encode4(msg, outEcc); break;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        ref struct EncodeResult1 {
            public ulong v0;
        }

        static void Encode1(ReadOnlySpan<byte> msg, Span<byte> outEcc) {
            EncodeResult1 r = default;
            ulong* eccTable = eccTables[outEcc.Length].NativeArray + ((msg.Length - 1) << 8);

            for (int i = 0; i < msg.Length; i++, eccTable -= 256) {
                ulong* xorEcc = eccTable + msg[i];
                r.v0 ^= xorEcc[0];
            }

            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(&r), outEcc.Length).CopyTo(outEcc);
        }

        [StructLayout(LayoutKind.Sequential)]
        ref struct EncodeResult2 {
            public ulong v0;
            public ulong v1;
        }

        static void Encode2(ReadOnlySpan<byte> msg, Span<byte> outEcc) {
            EncodeResult2 r = default;
            ulong* eccTable = eccTables[outEcc.Length].NativeArray + ((msg.Length - 1) << 9);

            for (int i = 0; i < msg.Length; i++, eccTable -= 256 * 2) {
                ulong* xorEcc = eccTable + msg[i] * 2;
                r.v0 ^= xorEcc[0];
                r.v1 ^= xorEcc[1];
            }

            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(&r), outEcc.Length).CopyTo(outEcc);
        }

        [StructLayout(LayoutKind.Sequential)]
        ref struct EncodeResult3 {
            public ulong v0;
            public ulong v1;
            public ulong v2;
        }

        static void Encode3(ReadOnlySpan<byte> msg, Span<byte> outEcc) {
            EncodeResult3 r = default;
            ulong* eccTable = eccTables[outEcc.Length].NativeArray + ((msg.Length - 1) << 10);

            for (int i = 0; i < msg.Length; i++, eccTable -= 256 * 4) {
                ulong* xorEcc = eccTable + msg[i] * 4;
                r.v0 ^= xorEcc[0];
                r.v1 ^= xorEcc[1];
                r.v2 ^= xorEcc[2];
            }

            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(&r), outEcc.Length).CopyTo(outEcc);
        }

        [StructLayout(LayoutKind.Sequential)]
        ref struct EncodeResult4 {
            public ulong v0;
            public ulong v1;
            public ulong v2;
            public ulong v3;
        }

        static void Encode4(ReadOnlySpan<byte> msg, Span<byte> outEcc) {
            EncodeResult4 r = default;
            ulong* eccTable = eccTables[outEcc.Length].NativeArray + ((msg.Length - 1) << 10);

            for (int i = 0; i < msg.Length; i++, eccTable -= 256 * 4) {
                ulong* xorEcc = eccTable + msg[i] * 4;
                r.v0 ^= xorEcc[0];
                r.v1 ^= xorEcc[1];
                r.v2 ^= xorEcc[2];
                r.v3 ^= xorEcc[3];
            }

            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(&r), outEcc.Length).CopyTo(outEcc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ByteEncode(byte byteMsg, int xExponent, int eccCount) {
            var ecc = GC.AllocateUninitializedArray<byte>(eccCount);
            ByteEncode(byteMsg, xExponent, ecc);
            return ecc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ByteEncode(byte byteMsg, int xExponent, Span<byte> outEcc) {
            ReadOnlySpan<byte> shiftTable = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
            ref byte shiftFirst = ref MemoryMarshal.GetReference(shiftTable);
            int shift = Unsafe.Add(ref shiftFirst, outEcc.Length);

            ulong* r = eccTables[outEcc.Length].NativeArray + (xExponent << shift + 8) + (byteMsg << shift);
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(r), outEcc.Length).CopyTo(outEcc);
        }
    }
}
