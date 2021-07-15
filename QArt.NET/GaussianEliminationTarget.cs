using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QArt.NET {
    unsafe internal ref struct GaussianEliminationTarget {
        private readonly int maxCount;
        private readonly int[] eliminationRecord;

        public int Count { get; private set; }
        public BitArray256[] Left { get; }
        public BitArray256[] Right { get; }
        public int[] LinearlyIndependent { get; }
        public int MaxCount => maxCount;

        public GaussianEliminationTarget(int maxCount) {
            if (maxCount is <= 0 or >= 256) throw new ArgumentOutOfRangeException(nameof(maxCount));

            this.maxCount = maxCount;
            Count = 0;

            Left = GC.AllocateUninitializedArray<BitArray256>(maxCount);
            Right = GC.AllocateUninitializedArray<BitArray256>(maxCount);
            LinearlyIndependent = GC.AllocateUninitializedArray<int>(maxCount);
            eliminationRecord = GC.AllocateUninitializedArray<int>(maxCount);
        }

        public bool AddVector(BitArray256 vector) {
            if (Count == maxCount) return false;

            int firstOne = vector.FirstOne();
            if (firstOne < 0) return false;

            int firstRow = -1;
            int eliminationRecordCount = 0;

            bool Elimination(ref BitArray256 rightFirst, ref int erFirst, ref BitArray256 vector) {
                vector.Xor(Unsafe.Add(ref rightFirst, firstRow));
                firstOne = vector.FirstOne();
                if (firstOne < 0) return false;
                Unsafe.Add(ref erFirst, eliminationRecordCount++) = firstRow;
                return true;
            }

            ref BitArray256 leftFirst = ref MemoryMarshal.GetArrayDataReference(Left);
            ref BitArray256 rightFirst = ref MemoryMarshal.GetArrayDataReference(Right);
            ref int liFirst = ref MemoryMarshal.GetArrayDataReference(LinearlyIndependent);
            ref int erFirst = ref MemoryMarshal.GetArrayDataReference(eliminationRecord);

            if (Count == 0) goto Success;
            if (firstOne < liFirst) goto Success;

            for (firstRow++; firstRow < Count - 1;) {
                if (firstOne > Unsafe.Add(ref liFirst, firstRow) && firstOne < Unsafe.Add(ref liFirst, firstRow + 1)) {
                    goto Success;
                } else if (firstOne == Unsafe.Add(ref liFirst, firstRow)) {
                    if (!Elimination(ref rightFirst, ref erFirst, ref vector)) return false;
                    continue;
                }
                firstRow++;
            }

            if (firstOne == Unsafe.Add(ref liFirst, firstRow)) {
                if (!Elimination(ref rightFirst, ref erFirst, ref vector)) return false;
            }

        Success:
            firstRow++;
            Insert(ref liFirst, firstRow, firstOne);
            Insert(ref rightFirst, firstRow, vector);

            BitArray256 newLeftVector = default;
            newLeftVector[Count] = true;
            for (int i = 0; i < eliminationRecordCount; i++) {
                int leftRow = Unsafe.Add(ref erFirst, i);
                newLeftVector.Xor(Unsafe.Add(ref leftFirst, leftRow));
            }
            Insert(ref leftFirst, firstRow, newLeftVector);
            Count++;

            ref BitArray256 refVector = ref Unsafe.Add(ref rightFirst, firstRow);

            for (int row = firstRow + 1; row < Count; row++) {
                if (vector[Unsafe.Add(ref liFirst, row)]) {
                    refVector.Xor(Unsafe.Add(ref rightFirst, row));
                    Unsafe.Add(ref leftFirst, firstRow).Xor(Unsafe.Add(ref leftFirst, row));
                }
            }
            for (int row = 0; row < firstRow; row++) {
                if (Unsafe.Add(ref rightFirst, row)[firstOne]) {
                    Unsafe.Add(ref rightFirst, row).Xor(refVector);
                    Unsafe.Add(ref leftFirst, row).Xor(Unsafe.Add(ref leftFirst, firstRow));
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Insert<T>(ref T array, int index, in T value) where T : unmanaged {
            for (int i = Count; i > index; i--) {
                Unsafe.Add(ref array, i) = Unsafe.Add(ref array, i - 1);
            }
            Unsafe.Add(ref array, index) = value;
        }

        public override string ToString() {
            var sb = new StringBuilder();
            for (int row = 0; row < Count; row++) {
                for (int col = 0; col < Count; col++) {
                    sb.Append(Left[row][col] ? '1' : '.');
                }
                sb.Append(" | ");
                for (int col = 0; col < maxCount; col++) {
                    sb.Append(Right[row][col] ? '1' : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
