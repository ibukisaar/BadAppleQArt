using System;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    public static class QREncoder {
        interface IEncoder {
            EncodeType EncodeType { get; }
            int ModeBinary { get; }
            int BitsOfLength { get; }

            int GetBitCount(int dataLength);

            void Encode(ref QRBitArray.BufferedWriter writer, ReadOnlySpan<byte> data);
        }

        readonly struct NumericEncoder : IEncoder {
            public NumericEncoder(int version) {
                BitsOfLength = version switch {
                    <= 9 => 10,
                    <= 26 => 12,
                    _ => 14
                };
            }

            public EncodeType EncodeType => EncodeType.Numeric;

            public int ModeBinary => 0b0001;

            public int BitsOfLength { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Check(byte b) => b is >= 48 and <= 57;

            public void Encode(ref QRBitArray.BufferedWriter writer, ReadOnlySpan<byte> data) {
                ref byte first = ref MemoryMarshal.GetReference(data);
                int numberOf10Bits = Math.DivRem(data.Length, 3, out int remOf10Bits);
                int i = 0;
                for (; i < numberOf10Bits; i++) {
                    int n100 = Unsafe.Add(ref first, 3 * i) - '0';
                    int n10 = Unsafe.Add(ref first, 3 * i + 1) - '0';
                    int n1 = Unsafe.Add(ref first, 3 * i + 2) - '0';
                    writer.Write(n100 * 100 + n10 * 10 + n1, 10);
                }

                switch (remOf10Bits) {
                    case 1: {
                            int n1 = Unsafe.Add(ref first, 3 * i) - '0';
                            writer.Write(n1, 4);
                            break;
                        }
                    case 2: {
                            int n10 = Unsafe.Add(ref first, 3 * i) - '0';
                            int n1 = Unsafe.Add(ref first, 3 * i + 1) - '0';
                            writer.Write(n10 * 10 + n1, 7);
                            break;
                        }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetBitCount(int dataLength) {
                int numberOf10Bits = Math.DivRem(dataLength, 3, out int r);
                return numberOf10Bits * 10 + ((0x070400 >> (r << 3)) & 0xff);
            }
        }

        readonly struct AlphanumericEncoder : IEncoder {
            public static ReadOnlySpan<sbyte> AlphanumericTable => new sbyte[] {
                   36,   -1,   -1,   -1,   37,   38,   -1,   -1,   -1,   -1,   39,   40,   -1,   41,   42,   43,
                    0,    1,    2,    3,    4,    5,    6,    7,    8,    9,   44,   -1,   -1,   -1,   -1,   -1,
                   -1,   10,   11,   12,   13,   14,   15,   16,   17,   18,   19,   20,   21,   22,   23,   24,
                   25,   26,   27,   28,   29,   30,   31,   32,   33,   34,   35
            };

            public AlphanumericEncoder(int version) {
                BitsOfLength = version switch {
                    <= 9 => 9,
                    <= 26 => 11,
                    _ => 13
                };
            }

            public EncodeType EncodeType => EncodeType.Alphanumeric;

            public int ModeBinary => 0b0010;

            public int BitsOfLength { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Check(byte b) {
                ref sbyte first = ref MemoryMarshal.GetReference(AlphanumericTable);
                return b is >= 32 and < 91 && Unsafe.Add(ref first, b - 32) >= 0;
            }

            public void Encode(ref QRBitArray.BufferedWriter writer, ReadOnlySpan<byte> data) {
                ref byte first = ref MemoryMarshal.GetReference(data);
                ref sbyte table = ref MemoryMarshal.GetReference(AlphanumericTable);
                int numberOf10Bits = data.Length >> 1;
                int i = 0;
                for (; i < numberOf10Bits; i++) {
                    int vH = Unsafe.Add(ref table, Unsafe.Add(ref first, 2 * i) - 32);
                    int vL = Unsafe.Add(ref table, Unsafe.Add(ref first, 2 * i + 1) - 32);
                    writer.Write(vH * 45 + vL, 11);
                }
                if ((data.Length & 1) != 0) {
                    int v = Unsafe.Add(ref table, Unsafe.Add(ref first, 2 * i) - 32);
                    writer.Write(v, 6);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetBitCount(int dataLength) {
                return (dataLength >> 1) * 11 + (dataLength & 1) * 6;
            }
        }

        readonly struct ByteEncoder : IEncoder {
            public ByteEncoder(int version) {
                BitsOfLength = version switch {
                    <= 9 => 8,
                    _ => 16
                };
            }

            public EncodeType EncodeType => EncodeType.Byte;

            public int ModeBinary => 0b0100;

            public int BitsOfLength { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Check(byte _) => true;

            public void Encode(ref QRBitArray.BufferedWriter writer, ReadOnlySpan<byte> data) {
                for (int i = 0; i < data.Length; i++) {
                    writer.Write(data[i], 8);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int GetBitCount(int dataLength) => dataLength << 3;
        }

        enum EncodeType : int {
            Byte,
            Alphanumeric,
            Numeric,
        }

        struct EncodeSpan {
            public EncodeType Type;
            public int DataLength;
        }

        struct EncodedSpan {
            public EncodeType Type;
            public int DataLength;
            //public int Bits;
            //public int DataBits;
        }

        unsafe struct EncodeNode {
            public EncodeType Type;
            public int DataLength;
            public int Bits;
            public int DataBits;
            public EncodeNode* Parent;
        }

        unsafe struct EncodeTree {
            public int TotalBits;
            public int Depth;
            public EncodeNode* Root;
        }

        [SkipLocalsInit]
        unsafe static EncodedSpan[] BuildSpans(int version, ReadOnlySpan<byte> data, out int totalBits) {
            if (data.IsEmpty) {
                totalBits = 0;
                return Array.Empty<EncodedSpan>();
            }

            StackList<EncodeSpan> spans = new(stackalloc EncodeSpan[data.Length]);
            int prevIndex = -1;
            EncodeType prevType = default;

            for (int i = 0; i < data.Length; i++) {
                byte b = data[i];
                EncodeType type;
                if (NumericEncoder.Check(b)) {
                    type = EncodeType.Numeric;
                } else if (AlphanumericEncoder.Check(b)) {
                    type = EncodeType.Alphanumeric;
                } else {
                    type = EncodeType.Byte;
                }

                if (prevIndex >= 0) {
                    if (prevType != type) {
                        spans.Add(new EncodeSpan {
                            Type = prevType,
                            DataLength = i - prevIndex
                        });
                    } else {
                        continue;
                    }
                }
                prevIndex = i;
                prevType = type;
            }

            if (prevIndex >= 0) {
                spans.Add(new EncodeSpan {
                    Type = prevType,
                    DataLength = data.Length - prevIndex
                });
            }


            var nEncoder = new NumericEncoder(version);
            var aEncoder = new AlphanumericEncoder(version);
            var bEncoder = new ByteEncoder(version);

            EncodeNode* nodePool = stackalloc EncodeNode[spans.Count * 3];
            int nodeCount = 0;
            Span<EncodeTree> treePool = stackalloc EncodeTree[6];
            StackList<EncodeTree> trees = new(treePool[..3]);
            StackList<EncodeTree> tempTrees = new(treePool[3..]);

            for (int i = 0; i < spans.Count; i++) {
                int dataLength = spans[i].DataLength;
                switch (spans[i].Type) {
                    case EncodeType.Numeric:
                        AttachNode(nEncoder, dataLength);
                        AttachNode(aEncoder, dataLength);
                        AttachNode(bEncoder, dataLength);
                        UpdateTrees(3);
                        break;
                    case EncodeType.Alphanumeric:
                        AttachNode(aEncoder, dataLength);
                        AttachNode(bEncoder, dataLength);
                        UpdateTrees(2);
                        break;
                    default:
                        AttachNode(bEncoder, dataLength);
                        UpdateTrees(1);
                        break;
                }
            }

            ref EncodeTree minTree = ref trees[0];
            for (int i = 1; i < trees.Count; i++) {
                if (trees[i].TotalBits < minTree.TotalBits) {
                    minTree = ref trees[i];
                }
            }

            EncodeNode* node = minTree.Root;
            EncodedSpan[] result = GC.AllocateUninitializedArray<EncodedSpan>(minTree.Depth);
            ref EncodedSpan resultFirst = ref MemoryMarshal.GetArrayDataReference(result);
            for (int i = minTree.Depth - 1; i >= 0; i--, node = node->Parent) {
                ref EncodedSpan span = ref Unsafe.Add(ref resultFirst, i);
                span.Type = node->Type;
                span.DataLength = node->DataLength;
            }
            totalBits = minTree.TotalBits;
            return result;




            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            EncodeNode* NewNode() {
                return &nodePool[nodeCount++];
            }

            void AttachNode<T>(T encoder, int dataLength) where T : IEncoder {
                EncodeNode* node = NewNode();
                node->Type = encoder.EncodeType;
                ref EncodeTree tree = ref tempTrees[(int)encoder.EncodeType];
                tree.Root = node;

                if (trees.Count > 0) {
                    int minIndex = -1;
                    for (int i = 0; i < trees.Count; i++) {
                        bool isEquals = trees[i].Root->Type == encoder.EncodeType;
                        int newDataLength = isEquals ? trees[i].Root->DataLength + dataLength : dataLength;
                        int dataBits = encoder.GetBitCount(newDataLength);
                        int lastBits = 4 + encoder.BitsOfLength + dataBits;
                        int totalBits = trees[i].TotalBits + lastBits;
                        if (isEquals) totalBits -= trees[i].Root->Bits;

                        if (minIndex < 0 || tree.TotalBits > totalBits) {
                            minIndex = i;
                            tree.TotalBits = totalBits;
                            tree.Depth = trees[i].Depth + (isEquals ? 0 : 1);
                            node->DataLength = newDataLength;
                            node->Bits = lastBits;
                            node->DataBits = dataBits;
                            node->Parent = trees[i].Root;
                        }
                    }
                } else {
                    int dataBits = encoder.GetBitCount(dataLength);
                    node->DataLength = dataLength;
                    node->Bits = 4 + encoder.BitsOfLength + dataBits;
                    node->DataBits = dataBits;
                    node->Parent = null;
                    tree.TotalBits = node->Bits;
                    tree.Depth = 1;
                }
            }

            void UpdateTrees(int treeCount) {
                for (int i = 0; i < treeCount; i++) {
                    trees[i].TotalBits = tempTrees[i].TotalBits;
                    trees[i].Depth = tempTrees[i].Depth;
                    EncodeNode* node = tempTrees[i].Root;
                    if (node->Parent != null && node->Parent->Type == node->Type) {
                        node->Parent = node->Parent->Parent;
                    }
                    trees[i].Root = node;
                }
                trees.Count = treeCount;
            }
        }

        static EncodedSpan[] BuildSpans(int version, QRDataMode? mode, ReadOnlySpan<byte> data, out int totalBits) {
            int dataBits;
            switch (mode) {
                case QRDataMode.Numeric:
                    for (int i = 0; i < data.Length; i++) {
                        if (!NumericEncoder.Check(data[i])) {
                            throw new ArgumentOutOfRangeException(nameof(data), "包含无效字符");
                        }
                    }
                    var nEncoder = new NumericEncoder(version);
                    dataBits = nEncoder.GetBitCount(data.Length);
                    totalBits = 4 + nEncoder.BitsOfLength + dataBits;
                    return new[] { new EncodedSpan {
                        Type = EncodeType.Numeric,
                        DataLength = data.Length,
                        //Bits = totalBits,
                        //DataBits = dataBits,
                    } };
                case QRDataMode.Alphanumeric:
                    for (int i = 0; i < data.Length; i++) {
                        if (!AlphanumericEncoder.Check(data[i])) {
                            throw new ArgumentOutOfRangeException(nameof(data), "包含无效字符");
                        }
                    }
                    var aEncoder = new AlphanumericEncoder(version);
                    dataBits = aEncoder.GetBitCount(data.Length);
                    totalBits = 4 + aEncoder.BitsOfLength + dataBits;
                    return new[] { new EncodedSpan {
                        Type = EncodeType.Alphanumeric,
                        DataLength = data.Length,
                        //Bits = totalBits,
                        //DataBits = dataBits,
                    } };
                case QRDataMode.Byte:
                    var bEncoder = new ByteEncoder(version);
                    dataBits = bEncoder.GetBitCount(data.Length);
                    totalBits = 4 + bEncoder.BitsOfLength + dataBits;
                    return new[] { new EncodedSpan {
                        Type = EncodeType.Byte,
                        DataLength = data.Length,
                        //Bits = totalBits,
                        //DataBits = dataBits,
                    } };
                default:
                    return BuildSpans(version, data, out totalBits);
            }
        }

        static int GuassVersion(ReadOnlySpan<byte> data, QREcLevel ecLevel, QRDataMode? mode, out EncodedSpan[] encodedSpans, out int totalBits) {
            var tempEncodedSpans = BuildSpans(1, mode, data, out totalBits);
            for (int ver = 1; ver <= 9; ver++) {
                if (totalBits <= (QRHelper.GetDataCapacity(ver, ecLevel) << 3)) {
                    encodedSpans = tempEncodedSpans;
                    return ver;
                }
            }
            tempEncodedSpans = BuildSpans(10, mode, data, out totalBits);
            for (int ver = 10; ver <= 26; ver++) {
                if (totalBits <= (QRHelper.GetDataCapacity(ver, ecLevel) << 3)) {
                    encodedSpans = tempEncodedSpans;
                    return ver;
                }
            }
            tempEncodedSpans = BuildSpans(27, mode, data, out totalBits);
            for (int ver = 27; ver <= 40; ver++) {
                if (totalBits <= (QRHelper.GetDataCapacity(ver, ecLevel) << 3)) {
                    encodedSpans = tempEncodedSpans;
                    return ver;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(data), "数据过大");
        }

        unsafe private static void EncodeData(ReadOnlySpan<byte> data, int version, ReadOnlySpan<EncodedSpan> encodedSpans, Span<bool> result) {
            var nEncoder = new NumericEncoder(version);
            var aEncoder = new AlphanumericEncoder(version);
            var bEncoder = new ByteEncoder(version);

            fixed (bool* pResult = result) {
                var bitArray = new QRBitArray(pResult, result.Length);
                var writer = bitArray.CreateWriter();
                int offset = 0;
                for (int i = 0; i < encodedSpans.Length; i++) {
                    ref readonly EncodedSpan encodedSpan = ref encodedSpans[i];
                    switch (encodedSpan.Type) {
                        case EncodeType.Numeric:
                            writer.Write(nEncoder.ModeBinary, 4);
                            writer.Write(encodedSpan.DataLength, nEncoder.BitsOfLength);
                            nEncoder.Encode(ref writer, data.Slice(offset, encodedSpan.DataLength));
                            break;
                        case EncodeType.Alphanumeric:
                            writer.Write(aEncoder.ModeBinary, 4);
                            writer.Write(encodedSpan.DataLength, aEncoder.BitsOfLength);
                            aEncoder.Encode(ref writer, data.Slice(offset, encodedSpan.DataLength));
                            break;
                        default:
                            writer.Write(bEncoder.ModeBinary, 4);
                            writer.Write(encodedSpan.DataLength, bEncoder.BitsOfLength);
                            bEncoder.Encode(ref writer, data.Slice(offset, encodedSpan.DataLength));
                            break;
                    }
                    offset += encodedSpan.DataLength;
                }
                writer.Flush();
            }
        }

        public static bool[] Encode(ReadOnlySpan<byte> data, int version, QREcLevel ecLevel, QRDataMode? mode = null) {
            if (version is <= 0 or > 40) throw new ArgumentOutOfRangeException(nameof(version));

            EncodedSpan[] encodedSpans = BuildSpans(version, mode, data, out int totalBits);
            if (totalBits > QRHelper.GetDataCapacity(version, ecLevel) * 8) throw new ArgumentOutOfRangeException(nameof(data), "数据过大");

            bool[] result = GC.AllocateUninitializedArray<bool>(totalBits);
            EncodeData(data, version, encodedSpans, result);
            return result;
        }

        public static bool[] Encode(ReadOnlySpan<byte> data, QREcLevel ecLevel, QRDataMode? mode, out int version) {
            if (data.Length > QRHelper.MaxDataLength) throw new ArgumentOutOfRangeException(nameof(data), "数据过大");

            version = GuassVersion(data, ecLevel, mode, out EncodedSpan[] encodedSpans, out int totalBits);
            bool[] result = GC.AllocateUninitializedArray<bool>(totalBits);
            EncodeData(data, version, encodedSpans, result);
            return result;
        }

        public static int WriteTerminator(Span<bool> encodedBuffer, int encodedLength) {
            if (encodedBuffer.Length < encodedLength) throw new ArgumentOutOfRangeException(nameof(encodedLength));

            ref bool first = ref Unsafe.Add(ref MemoryMarshal.GetReference(encodedBuffer), encodedLength);
            switch (encodedBuffer.Length - encodedLength) {
                case 0:
                    return encodedLength;
                case 1:
                    first = false;
                    return encodedLength + 1;
                case 2:
                    Unsafe.As<bool, short>(ref first) = 0;
                    return encodedLength + 2;
                case 3:
                    Unsafe.As<bool, short>(ref first) = 0;
                    Unsafe.Add(ref first, 2) = false;
                    return encodedLength + 3;
                default:
                    Unsafe.As<bool, int>(ref first) = 0;
                    return encodedLength + 4;
            }
        }

        public static bool[] ToFinalEncodedData(QRLayout layout, bool[] encodedData) {
            int dataCapacity = layout.DataCapacity * 8;
            if (dataCapacity < encodedData.Length) throw new ArgumentOutOfRangeException(nameof(encodedData));
            int finalLength = Math.Min(dataCapacity, encodedData.Length + 4);
            bool[] result = GC.AllocateUninitializedArray<bool>(finalLength);
            encodedData.CopyTo(result.AsSpan());
            result.AsSpan(encodedData.Length).Clear();
            return result;
        }
    }
}
