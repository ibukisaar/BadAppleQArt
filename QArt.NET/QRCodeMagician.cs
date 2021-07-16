using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace QArt.NET {
    public static class QRCodeMagician {
        [SkipLocalsInit]
        unsafe public static void Match(ReadOnlySpan<MagicBit> templateData, ReadOnlySpan<MagicBit> templateEcc, Span<bool> outData) {
            if (templateData.Length != outData.Length) throw new ArgumentException($"{nameof(templateData)}和{nameof(outData)}长度不一致");
            if (templateData.Length % 8 != 0) throw new ArgumentException($"{nameof(templateData)}的长度必须是8的倍数");
            if (templateEcc.Length % 8 != 0) throw new ArgumentException($"{nameof(templateEcc)}的长度必须是8的倍数");
            int dataLength = templateData.Length / 8;
            int eccLength = templateEcc.Length / 8;

            ref MagicBit templateDataFirst = ref MemoryMarshal.GetReference(templateData);
            ref MagicBit templateEccFirst = ref MemoryMarshal.GetReference(templateEcc);
            ref bool outDataFirst = ref MemoryMarshal.GetReference(outData);

            StackList<int> unknowns = new(stackalloc int[templateData.Length]); // 未知数列表
            for (int i = 0; i < templateData.Length; i++) {
                if (Unsafe.Add(ref templateDataFirst, i).Type == MagicBitType.Freedom) {
                    unknowns.Add(i);
                } else {
                    Unsafe.Add(ref outDataFirst, i) = Unsafe.Add(ref templateDataFirst, i).Value;
                }
            }

            int unknownCount = unknowns.Count;
            if (unknownCount == 0) return;

            StackList<int> expects = new(stackalloc int[templateEcc.Length]); // 期望值列表
            for (int i = 0; i < templateEcc.Length; i++) {
                if (Unsafe.Add(ref templateEccFirst, i).Type == MagicBitType.Expect) {
                    expects.Add(i);
                }
            }
            if (expects.Count == 0) {
                for (int i = 0; i < unknownCount; i++) {
                    Unsafe.Add(ref outDataFirst, unknowns[i]) = Unsafe.Add(ref templateDataFirst, unknowns[i]).Value;
                }
                return;
            }
            // unknowns和expects将组成方程组

            Span<byte> tempEcc = stackalloc byte[eccLength];
            ref byte tempEccFirst = ref MemoryMarshal.GetReference(tempEcc);
            var gaussianElimination = new GaussianEliminationTarget(Math.Min(unknownCount, expects.Count));
            int overflowCount = 0;

            RandomOrder(unknowns);

            for (int i = 0; i + overflowCount < unknownCount;) {
                RSEncode.ByteEncode((byte)(1 << (7 - (unknowns[i] & 7))), dataLength - 1 - (unknowns[i] >> 3), tempEcc);

                Unsafe.SkipInit<BitArray256>(out var eccVector);
                for (int j = 0; j < expects.Count; j++) {
                    eccVector[j] = (Unsafe.Add(ref tempEccFirst, expects[j] >> 3) & (1 << (7 - (expects[j] & 7)))) != 0;
                }

                if (gaussianElimination.AddVector(eccVector)) {
                    i++;
                } else {
                    int j = unknownCount - ++overflowCount;
                    int t = unknowns[i];
                    unknowns[i] = unknowns[j];
                    unknowns[j] = t;
                }
            }


            int actualUnknownCount = unknownCount - overflowCount;
            Debug.Assert(actualUnknownCount > 0);

            //Console.WriteLine($"自由: {unknownCount}, 实际自由: {actualUnknownCount}, 溢出: {overflowCount}, 期望: {expects.Count}");

            Span<bool> targetEcc = stackalloc bool[expects.Count];
            ref bool targetEccFirst = ref MemoryMarshal.GetReference(targetEcc);

            for (int i = 0; i < targetEcc.Length; i++) {
                Unsafe.Add(ref targetEccFirst, i) = Unsafe.Add(ref templateEccFirst, expects[i]).Value;
            }

            byte* overflowData = stackalloc byte[dataLength];
            for (int i = 0; i < templateData.Length; i++) {
                if (Unsafe.Add(ref templateDataFirst, i) is { Type: MagicBitType.Expect, Value: true }) {
                    overflowData[i >> 3] |= (byte)(1 << (7 - (i & 7)));
                } else {
                    overflowData[i >> 3] &= (byte)~(1 << (7 - (i & 7)));
                }
            }
            foreach (var i in unknowns.AsReadOnlySpan(actualUnknownCount)) {
                bool value = Unsafe.Add(ref templateDataFirst, i).Value;
                Unsafe.Add(ref outDataFirst, i) = value;
                if (value) {
                    overflowData[i >> 3] |= (byte)(1 << (7 - (i & 7)));
                }
            }


            RSEncode.Encode(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(overflowData), dataLength), tempEcc);

            for (int i = 0; i < expects.Count; i++) {
                int bitIndex = expects[i];
                bool value = (Unsafe.Add(ref tempEccFirst, bitIndex >> 3) & (1 << (7 - (bitIndex & 7)))) != 0;
                Unsafe.Add(ref targetEccFirst, i) ^= value;
            }

            BitArray256 resultLeft = default;
            ref BitArray256 leftFirst = ref MemoryMarshal.GetArrayDataReference(gaussianElimination.Left);
            ref int liFirst = ref MemoryMarshal.GetArrayDataReference(gaussianElimination.LinearlyIndependent);
            for (int i = 0; i < gaussianElimination.Count; i++) {
                if (Unsafe.Add(ref targetEccFirst, Unsafe.Add(ref liFirst, i))) {
                    resultLeft.Xor(Unsafe.Add(ref leftFirst, i));
                }
            }

            for (int i = 0; i < actualUnknownCount; i++) {
                Unsafe.Add(ref outDataFirst, unknowns[i]) = resultLeft[i];
            }
        }

        static void RandomOrder<T>(StackList<T> list) where T : unmanaged {
            var random = new Random();
            for (int i = 0; i < list.Count; i++) {
                int j = random.Next(i, list.Count);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        unsafe public static void WriteDataToImage(QRLayout layout, ReadOnlySpan<bool> encodedData, Span<MagicBit> imageBits, QRMaskVersion maskVersion = QRMaskVersion.Version000) {
            if (imageBits.Length != layout.Map2D.Length) throw new ArgumentOutOfRangeException(nameof(imageBits));
            if (encodedData.Length > layout.DataTemplate.Length * 8) throw new ArgumentOutOfRangeException(nameof(encodedData));

            QRDataInfo** info = layout.DataTemplate.Pointer;
            UnmanagedArray<byte> mask = layout.GetMask(maskVersion);
            ref bool m = ref Unsafe.AsRef<bool>(mask.Pointer);
            ref MagicBit imageFirst = ref MemoryMarshal.GetReference(imageBits);
            ref bool dataFirst = ref MemoryMarshal.GetReference(encodedData);
            nint length = encodedData.Length;

            for (nint i = 0; i < length; i++) {
                nint mapOffset = info[i >> 3]->MapOffsets[i & 7];
                Unsafe.Add(ref imageFirst, mapOffset) = new MagicBit(MagicBitType.Expect, Unsafe.Add(ref dataFirst, i) ^ Unsafe.Add(ref m, mapOffset));
            }
        }

        [SkipLocalsInit]
        unsafe public static QRCode ImageArt(QRLayout layout, ReadOnlySpan<MagicBit> imageBits, QRMaskVersion maskVersion = QRMaskVersion.Version000) {
            if (imageBits.Length != layout.Map2D.Length) throw new ArgumentOutOfRangeException(nameof(imageBits));

            var imageBitsClone = GC.AllocateUninitializedArray<MagicBit>(imageBits.Length);
            imageBits.CopyTo(imageBitsClone);
            ref MagicBit imageFirst = ref MemoryMarshal.GetArrayDataReference(imageBitsClone);

            {
                UnmanagedArray<byte> mask = layout.GetMask(maskVersion);
                ref ulong m = ref Unsafe.AsRef<ulong>(mask.Pointer);
                ref ulong v = ref Unsafe.As<MagicBit, ulong>(ref imageFirst);
                nint i = 0;
                nint length = imageBitsClone.Length;
                for (; i < length; i += 8) {
                    Unsafe.AddByteOffset(ref v, i) ^= Unsafe.AddByteOffset(ref m, i);
                }
                for (; i < length; i++) {
                    Unsafe.Add(ref Unsafe.As<ulong, byte>(ref v), i) ^= Unsafe.Add(ref Unsafe.As<ulong, byte>(ref m), i);
                }
            }

            var blockTemplate = layout.DataBlockTemplate;
            int dataBits = blockTemplate[^1].Length * 8, eccBits = layout.EccBlockTemplate[^1].Length * 8;
            var dataTemplate = stackalloc MagicBit[dataBits];
            var eccTemplate = stackalloc MagicBit[eccBits];
            var resultData = stackalloc bool[dataBits];
            Span<byte> tempData = stackalloc byte[dataBits >> 3];
            Span<byte> tempEcc = stackalloc byte[eccBits >> 3];
            QRCode qr = new QRCode(layout, maskVersion);

            for (int i = 0; i < blockTemplate.Length; i++) {
                UnmanagedRefArray<QRDataInfo>.RawArray dataBlock = blockTemplate[i];
                for (int j = 0; j < dataBlock.Length; j++) {
                    QRDataInfo* info = dataBlock[j];
                    ref QRPointsInfo mapOffsets = ref info->MapOffsets;
                    nint offset = j << 3;
                    dataTemplate[offset + 0] = Unsafe.Add(ref imageFirst, mapOffsets.p0);
                    dataTemplate[offset + 1] = Unsafe.Add(ref imageFirst, mapOffsets.p1);
                    dataTemplate[offset + 2] = Unsafe.Add(ref imageFirst, mapOffsets.p2);
                    dataTemplate[offset + 3] = Unsafe.Add(ref imageFirst, mapOffsets.p3);
                    dataTemplate[offset + 4] = Unsafe.Add(ref imageFirst, mapOffsets.p4);
                    dataTemplate[offset + 5] = Unsafe.Add(ref imageFirst, mapOffsets.p5);
                    dataTemplate[offset + 6] = Unsafe.Add(ref imageFirst, mapOffsets.p6);
                    dataTemplate[offset + 7] = Unsafe.Add(ref imageFirst, mapOffsets.p7);
                }

                UnmanagedRefArray<QRDataInfo>.RawArray eccBlock = layout.EccBlockTemplate[i];
                for (int j = 0; j < eccBlock.Length; j++) {
                    QRDataInfo* info = eccBlock[j];
                    ref QRPointsInfo mapOffsets = ref info->MapOffsets;
                    nint offset = j << 3;
                    eccTemplate[offset + 0] = Unsafe.Add(ref imageFirst, mapOffsets.p0);
                    eccTemplate[offset + 1] = Unsafe.Add(ref imageFirst, mapOffsets.p1);
                    eccTemplate[offset + 2] = Unsafe.Add(ref imageFirst, mapOffsets.p2);
                    eccTemplate[offset + 3] = Unsafe.Add(ref imageFirst, mapOffsets.p3);
                    eccTemplate[offset + 4] = Unsafe.Add(ref imageFirst, mapOffsets.p4);
                    eccTemplate[offset + 5] = Unsafe.Add(ref imageFirst, mapOffsets.p5);
                    eccTemplate[offset + 6] = Unsafe.Add(ref imageFirst, mapOffsets.p6);
                    eccTemplate[offset + 7] = Unsafe.Add(ref imageFirst, mapOffsets.p7);
                }


                dataBits = dataBlock.Length * 8;

                ReadOnlySpan<MagicBit> templateData = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<MagicBit>(dataTemplate), dataBits);
                ReadOnlySpan<MagicBit> templateEcc = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<MagicBit>(eccTemplate), eccBits);
                Span<bool> outData = MemoryMarshal.CreateSpan(ref Unsafe.AsRef<bool>(resultData), dataBits);
                Match(templateData, templateEcc, outData);

                var subTempData = tempData[..dataBlock.Length];

                QRTools.ToByteArrayNoCheck(outData, subTempData);

                RSEncode.Encode(subTempData, tempEcc);

                qr.Write(dataBlock, subTempData);
                qr.Write(eccBlock, tempEcc);
            }

            qr.XorMask();
            return qr;
        }
    }
}
