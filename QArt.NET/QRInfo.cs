using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [StructLayout(LayoutKind.Explicit)]
    unsafe public struct QRMapInfo {
        [FieldOffset(00)] public nint MapOffset;
        [FieldOffset(08)] public QRType Type;
        [FieldOffset(12)] public int X;
        [FieldOffset(16)] public int Y;

        /// <summary>
        /// <see cref="Type"/>: FinderPattern | Separator | TimingPatterns | AlignmentPatterns | OtherPatterns
        /// </summary>
        [FieldOffset(20)] public QRValue Value;

        /// <summary>
        /// <see cref="Type"/>: FormatInformation | VersionInformation
        /// </summary>
        [FieldOffset(20)] public int Offset;

        /// <summary>
        /// <see cref="Type"/>: Data | Ecc
        /// </summary>
        [FieldOffset(20)] public int BitIndex;
        [FieldOffset(24)] public QRDataInfo* ByteInfo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() {
            return $"X={X}, Y={Y}, Type={Type}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct QRDataInfo {
        public QRType Type;
        public int ByteOffset;
        public int BlockRow;
        public int BlockColumn;
        public QRPointsInfo MapOffsets;
        // public fixed long MapOffsets[8];

        public override string ToString() {
            return $"R={BlockRow}, C={BlockColumn}, Offset={ByteOffset}, Type={Type}";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QRPointsInfo {
        public nint p0, p1, p2, p3, p4, p5, p6, p7;

        unsafe public ref nint this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref ((nint*)Unsafe.AsPointer(ref p0))[index];
        }

        unsafe public ref nint this[nint index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref ((nint*)Unsafe.AsPointer(ref p0))[index];
        }
    }
}
