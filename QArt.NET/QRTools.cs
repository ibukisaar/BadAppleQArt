using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace QArt.NET {
    unsafe internal static class QRTools {
        static readonly UnmanagedArray<ulong> ParallelBitDepositTable = new(length: 256);
        static readonly ulong* ParallelBitDepositPointer = ParallelBitDepositTable.Pointer;

        static QRTools() {
            for (int i = 0; i < 256; i++) {
                ParallelBitDepositPointer[i] = ParallelBitDeposit((byte)i);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static ulong ParallelBitDeposit(byte v) {
                ulong x = v;
                ulong r = 0;
                r |= (x & 0x80) >> 7;
                r |= (x & 0x40) << 2;
                r |= (x & 0x20) << 11;
                r |= (x & 0x10) << 20;
                r |= (x & 0x08) << 29;
                r |= (x & 0x04) << 38;
                r |= (x & 0x02) << 47;
                r |= (x & 0x01) << 56;
                return r;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReverseBits(int v) {
            uint x = (uint)v;
            x = ((x >> 1) & 0x55555555) | ((x & 0x55555555) << 1);
            x = ((x >> 2) & 0x33333333) | ((x & 0x33333333) << 2);
            x = ((x >> 4) & 0x0F0F0F0F) | ((x & 0x0F0F0F0F) << 4);
            return BinaryPrimitives.ReverseEndianness((int)x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ParallelBitDeposit(byte v) => ParallelBitDepositPointer[v];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ParallelBitExtract(ulong v) => (byte)((v * 0x8040201008040201) >> 56);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetByteCount(int bitCount) => (bitCount + 7) >> 3;

        public static int ToByteArray(ReadOnlySpan<bool> input, Span<byte> output) {
            int byteCount = GetByteCount(input.Length);
            if (output.Length < byteCount) throw new ArgumentOutOfRangeException(nameof(output), "输出空间不够");

            ToByteArrayNoCheck(input, output);
            return byteCount;
        }

        public static byte[] ToByteArray(ReadOnlySpan<bool> input) {
            int byteCount = GetByteCount(input.Length);
            byte[] output = GC.AllocateUninitializedArray<byte>(byteCount);
            ToByteArrayNoCheck(input, output);
            return output;
        }

        internal static void ToByteArrayNoCheck(ReadOnlySpan<bool> input, Span<byte> output) {
            nint loopCount = input.Length >> 3;
            nint i = 0;
            ref bool @in = ref MemoryMarshal.GetReference(input);
            ref byte @out = ref MemoryMarshal.GetReference(output);
            for (; i < loopCount; i++) {
                Unsafe.Add(ref @out, i) = ParallelBitExtract(Unsafe.Add(ref Unsafe.As<bool, ulong>(ref @in), i));
            }

            @in = ref Unsafe.Add(ref @in, i * 8);
            switch (input.Length - i * 8) {
                case 0:
                    break;
                case 1:
                    Unsafe.Add(ref @out, i) = (byte)(Unsafe.As<bool, byte>(ref @in) << 7);
                    break;
                case 2:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<ushort>(ref @in));
                    break;
                case 3:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<Block3>(ref @in));
                    break;
                case 4:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<uint>(ref @in));
                    break;
                case 5:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<Block5>(ref @in));
                    break;
                case 6:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<Block6>(ref @in));
                    break;
                case 7:
                    Unsafe.Add(ref @out, i) = ParallelBitExtract(ReadUInt64<Block7>(ref @in));
                    break;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static ulong ReadUInt64<T>(ref bool @in) where T : unmanaged {
                ulong t = 0;
                Unsafe.As<ulong, T>(ref t) = Unsafe.As<bool, T>(ref @in);
                return t;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 3)] private readonly struct Block3 { }
        [StructLayout(LayoutKind.Sequential, Size = 5)] private readonly struct Block5 { }
        [StructLayout(LayoutKind.Sequential, Size = 6)] private readonly struct Block6 { }
        [StructLayout(LayoutKind.Sequential, Size = 7)] private readonly struct Block7 { }
    }
}
