using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace QArt.NET {
    /// <summary>
    /// 高性能BitArray，但是耗内存
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    unsafe internal readonly ref struct QRBitArray {
        private readonly bool* array;
        private readonly int length;

        public int Length => length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QRBitArray(bool* array, int length) {
            this.array = array;
            this.length = length;
        }

        public ref bool this[int i] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array[i];
        }

        public ref bool this[nint i] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array[i];
        }

        public void Write(int dstIndex, int src, int srcLength) {
            using var writer = CreateWriter(dstIndex);
            writer.Write(src, srcLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ReadOnlySpan<bool> bits) {
            bits.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<bool>(array), length));
        }

        public override string ToString() {
            char[] chars = new char[length];
            for (int i = 0; i < chars.Length; i++) {
                chars[i] = this[i] ? '1' : '0';
            }
            return new string(chars);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0090:使用 \"new(...)\"", Justification = "<挂起>")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferedWriter CreateWriter(int offset = 0) => new BufferedWriter(this, offset);

        internal ref struct BufferedWriter {
            private readonly QRBitArray bitArray;
            private nint offset;
            private int buffer;
            private int bufferBits;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BufferedWriter(QRBitArray bitArray, int offset) {
                this.bitArray = bitArray;
                this.offset = offset;
                buffer = 0;
                bufferBits = 0;
            }

            public void Write(int value, int bits) {
                bufferBits += bits;
                buffer |= value << (32 - bufferBits);
                for (; bufferBits >= 8; bufferBits -= 8, buffer <<= 8, offset += 8) {
                    Unsafe.As<bool, ulong>(ref bitArray[offset]) = QRTools.ParallelBitDeposit((byte)((uint)buffer >> 24));
                }
            }

            public void Flush() {
                if (bufferBits == 0) return;

                ulong t = QRTools.ParallelBitDeposit((byte)((uint)buffer >> 24));
                bool* dst = bitArray.array + offset;
                switch (bufferBits) {
                    case 7: Unsafe.AsRef<Block7>(dst) = Unsafe.As<ulong, Block7>(ref t); break;
                    case 6: Unsafe.AsRef<Block6>(dst) = Unsafe.As<ulong, Block6>(ref t); break;
                    case 5: Unsafe.AsRef<Block5>(dst) = Unsafe.As<ulong, Block5>(ref t); break;
                    case 4: Unsafe.AsRef<uint>(dst) = (uint)t; break;
                    case 3: Unsafe.AsRef<Block3>(dst) = Unsafe.As<ulong, Block3>(ref t); break;
                    case 2: Unsafe.AsRef<ushort>(dst) = (ushort)t; break;
                    case 1: Unsafe.AsRef<byte>(dst) = (byte)t; break;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                Flush();
            }

            [StructLayout(LayoutKind.Sequential, Size = 3)] private readonly struct Block3 { }
            [StructLayout(LayoutKind.Sequential, Size = 5)] private readonly struct Block5 { }
            [StructLayout(LayoutKind.Sequential, Size = 6)] private readonly struct Block6 { }
            [StructLayout(LayoutKind.Sequential, Size = 7)] private readonly struct Block7 { }

        }
    }
}
