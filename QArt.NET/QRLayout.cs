using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    unsafe public sealed class QRLayout {
        private readonly int version;
        private readonly QREcLevel ecLevel;
        private readonly int size;
        private readonly int dataCapacity;
        private readonly (int EccPerBlock, int Blocks1, int Codewords1, int Blocks2, int Codewords2) blockInfo;

        private readonly UnmanagedArray<QRMapInfo> map2dEntities;
        private readonly UnmanagedArray<QRDataInfo>[] dataBlocksRef, eccBlocksRef;
        private UnmanagedRefArray<QRDataInfo>[] _dataBlocks, _eccBlocks;

        private readonly UnmanagedRefArray<QRMapInfo> map2D;
        private UnmanagedRefArray<QRMapInfo> formatInformation1 = null!, formatInformation2 = null!;
        private UnmanagedRefArray<QRMapInfo>? versionInformation1, versionInformation2;
        private readonly UnmanagedRefArray<QRDataInfo> dataInfo;
        private UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray> dataBlocks, eccBlocks;

        private readonly UnmanagedArray<byte>?[] templateMasks = new UnmanagedArray<byte>?[8];


        public int Version => version;
        public QREcLevel EcLevel => ecLevel;
        public int Size => size;
        public int DataCapacity => dataCapacity;
        public ref readonly (int EccPerBlock, int Blocks1, int Codewords1, int Blocks2, int Codewords2) BlockInfo => ref blockInfo;

        public UnmanagedRefArray<QRMapInfo> Map2D => map2D;
        public UnmanagedRefArray<QRMapInfo> FormatInformation1 => formatInformation1;
        public UnmanagedRefArray<QRMapInfo> FormatInformation2 => formatInformation2;
        public UnmanagedRefArray<QRMapInfo>? VersionInformation1 => versionInformation1;
        public UnmanagedRefArray<QRMapInfo>? VersionInformation2 => versionInformation2;
        public UnmanagedRefArray<QRDataInfo> DataTemplate => dataInfo;
        public UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray> DataBlockTemplate => dataBlocks;
        public UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray> EccBlockTemplate => eccBlocks;

        public int BufferCapacity {
            get {
                var dataRawArray = dataBlocks.Raw;
                var eccRawArray = eccBlocks.Raw;
                int size = sizeof(UnmanagedRefArray<byte>.RawArray) * (dataRawArray.Length + eccRawArray.Length);
                for (int i = 0; i < dataRawArray.Length; i++) {
                    size += dataRawArray[i].Length;
                }
                for (int i = 0; i < eccRawArray.Length; i++) {
                    size += eccRawArray[i].Length;
                }
                return size;
            }
        }


        public ref readonly QRMapInfo this[int x, int y] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                if (x < -size || x >= size) throw new IndexOutOfRangeException(nameof(x));
                if (y < -size || y >= size) throw new IndexOutOfRangeException(nameof(y));
                return ref map2dEntities[AbsoluteOffset(y) * size + AbsoluteOffset(x)];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AbsoluteOffset(int offset) {
            return offset >= 0 ? offset : offset + size;
        }

        static readonly byte[,] finderPatternImage = new byte[7, 7] {
            { 1, 1, 1, 1, 1, 1, 1 },
            { 1, 0, 0, 0, 0, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 0, 0, 0, 0, 1 },
            { 1, 1, 1, 1, 1, 1, 1 },
        };

        static readonly byte[,] alignmentPatternImage = new byte[5, 5] {
            { 1, 1, 1, 1, 1 },
            { 1, 0, 0, 0, 1 },
            { 1, 0, 1, 0, 1 },
            { 1, 0, 0, 0, 1 },
            { 1, 1, 1, 1, 1 },
        };

        static readonly int[][] alignmentPatternsPoints = {
            null!,
            null!,
            new[]{ 6, 18 },
            new[]{ 6, 22 },
            new[]{ 6, 26 },
            new[]{ 6, 30 },
            new[]{ 6, 34 },
            new[]{ 6, 22, 38 },// 7
			new[]{ 6, 24, 42 },
            new[]{ 6, 26, 46 },
            new[]{ 6, 28, 50 },
            new[]{ 6, 30, 54 },
            new[]{ 6, 32, 58 },
            new[]{ 6, 34, 62 },
            new[]{ 6, 26, 46, 66 }, // 14
			new[]{ 6, 26, 48, 70 },
            new[]{ 6, 26, 50, 74 },
            new[]{ 6, 30, 54, 78 },
            new[]{ 6, 30, 56, 82 },
            new[]{ 6, 30, 58, 86 },
            new[]{ 6, 34, 62, 90 },
            new[]{ 6, 28, 50, 72, 94 }, // 21
			new[]{ 6, 26, 50, 74, 98 },
            new[]{ 6, 30, 54, 78, 102 },
            new[]{ 6, 28, 54, 80, 106 },
            new[]{ 6, 32, 58, 84, 110 },
            new[]{ 6, 30, 58, 86, 114 },
            new[]{ 6, 34, 62, 90, 118 },
            new[]{ 6, 26, 50, 74, 98, 122 }, // 28
			new[]{ 6, 30, 54, 78, 102, 126 },
            new[]{ 6, 26, 52, 78, 104, 130 },
            new[]{ 6, 30, 56, 82, 108, 134 },
            new[]{ 6, 34, 60, 86, 112, 138 },
            new[]{ 6, 30, 58, 86, 114, 142 },
            new[]{ 6, 34, 62, 90, 118, 146 },
            new[]{ 6, 30, 54, 78, 102, 126, 150 }, // 35
			new[]{ 6, 24, 50, 76, 102, 128, 154 },
            new[]{ 6, 28, 54, 80, 106, 132, 158 },
            new[]{ 6, 32, 58, 84, 110, 136, 162 },
            new[]{ 6, 26, 54, 82, 110, 138, 166 },
            new[]{ 6, 30, 58, 86, 114, 142, 170 },
        };

        static readonly (int X, int Y)[] formatInformationPoints1 = { (8, 0), (8, 1), (8, 2), (8, 3), (8, 4), (8, 5), (8, 7), (8, 8), (7, 8), (5, 8), (4, 8), (3, 8), (2, 8), (1, 8), (0, 8) };
        static readonly (int X, int Y)[] formatInformationPoints2 = { (-1, 8), (-2, 8), (-3, 8), (-4, 8), (-5, 8), (-6, 8), (-7, 8), (-8, 8), (8, -7), (8, -6), (8, -5), (8, -4), (8, -3), (8, -2), (8, -1) };

        private QRLayout(int version, QREcLevel ecLevel) {
            this.version = version;
            this.ecLevel = ecLevel;
            size = QRHelper.GetSize(version);
            dataCapacity = QRHelper.GetDataCapacity(version, ecLevel);
            blockInfo = QRHelper.GetBlockInfo(version, ecLevel);

            map2dEntities = new UnmanagedArray<QRMapInfo>(size * size);
            map2D = new UnmanagedRefArray<QRMapInfo>(map2dEntities);
            for (int i = 0; i < map2dEntities.Length; i++) {
                map2dEntities[i].Type = QRType.None;
            }

            dataInfo = new UnmanagedRefArray<QRDataInfo>(dataCapacity);
            dataBlocksRef = new UnmanagedArray<QRDataInfo>[blockInfo.Blocks1 + blockInfo.Blocks2];
            eccBlocksRef = new UnmanagedArray<QRDataInfo>[dataBlocksRef.Length];
            _dataBlocks = new UnmanagedRefArray<QRDataInfo>[dataBlocksRef.Length];
            _eccBlocks = new UnmanagedRefArray<QRDataInfo>[dataBlocksRef.Length];
            dataBlocks = new UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray>(dataBlocksRef.Length);
            eccBlocks = new UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray>(dataBlocksRef.Length);

            SetFinderPattern();
            SetSeparator();
            SetAlignmentPatterns();
            SetTimingPatterns();
            SetOtherPatterns();
            SetFormatInformation();
            SetVersionInformation();
            SetDataEcc();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref QRMapInfo NewMapInfo(QRType type, int x, int y) {
            nint mapOffset = y * size + x;
            ref QRMapInfo info = ref map2dEntities[mapOffset];
            info.MapOffset = mapOffset;
            info.Type = type;
            info.X = x;
            info.Y = y;
            return ref info;
        }

        private void SetImage(int dstX, int dstY, byte[,] img, QRType type) {
            int w = img.GetLength(1);
            int h = img.GetLength(0);

            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    ref QRMapInfo info = ref NewMapInfo(type, x + dstX, y + dstY);
                    info.Value = img[y, x] != 0;
                }
            }
        }

        private void SetFinderPattern() {
            SetImage(0, 0, finderPatternImage, QRType.FinderPattern);
            SetImage(0, size - 7, finderPatternImage, QRType.FinderPattern);
            SetImage(size - 7, 0, finderPatternImage, QRType.FinderPattern);
        }

        private void SetSeparator() {
            void Walk(int startX, int startY, params (int Count, int OffsetX, int OffsetY)[] paths) {
                int x = startX, y = startY;
                NewMapInfo(QRType.Separator, x, y).Value = QRValue.White;
                foreach (var (count, offsetX, offsetY) in paths) {
                    for (int i = 0; i < count; i++) {
                        x += offsetX;
                        y += offsetY;
                        NewMapInfo(QRType.Separator, x, y).Value = QRValue.White;
                    }
                }
            }

            Walk(0, 7, (Count: 7, OffsetX: 1, OffsetY: 0), (Count: 7, OffsetX: 0, OffsetY: -1));
            Walk(size - 1, 7, (Count: 7, OffsetX: -1, OffsetY: 0), (Count: 7, OffsetX: 0, OffsetY: -1));
            Walk(0, size - 8, (Count: 7, OffsetX: 1, OffsetY: 0), (Count: 7, OffsetX: 0, OffsetY: 1));
        }

        private void SetAlignmentPatterns() {
            if (version < 2) return;

            int[] points = alignmentPatternsPoints[version];
            for (int i = 0; i < points.Length; i++) {
                for (int j = 0; j < points.Length; j++) {
                    if (i == 0 && j == 0) continue;
                    if (i == 0 && j == points.Length - 1) continue;
                    if (i == points.Length - 1 && j == 0) continue;
                    SetImage(points[i] - 2, points[j] - 2, alignmentPatternImage, QRType.AlignmentPatterns);
                }
            }
        }

        private void SetTimingPatterns() {
            bool val = QRValue.Black;
            for (int x = 8; x < size - 8; x++) {
                nint mapOffset = 6 * size + x;
                ref QRMapInfo info = ref map2dEntities[mapOffset];
                if (info.Type is QRType.None) {
                    info.MapOffset = mapOffset;
                    info.Type = QRType.TimingPatterns;
                    info.X = x;
                    info.Y = 6;
                    info.Value = val;
                }
                val = !val;
            }

            val = QRValue.Black;
            for (int y = 8; y < size - 8; y++) {
                nint mapOffset = y * size + 6;
                ref QRMapInfo info = ref map2dEntities[mapOffset];
                if (info.Type is QRType.None) {
                    info.MapOffset = mapOffset;
                    info.Type = QRType.TimingPatterns;
                    info.X = 6;
                    info.Y = y;
                    info.Value = val;
                }
                val = !val;
            }
        }

        private void SetOtherPatterns() {
            NewMapInfo(QRType.OtherPatterns, 8, size - 8).Value = QRValue.Black;
        }

        private void SetFormatInformation() {
            UnmanagedRefArray<QRMapInfo> Set((int X, int Y)[] formatInformationPoints) {
                var formatInformation = new UnmanagedRefArray<QRMapInfo>(15);
                for (int i = 0; i < formatInformationPoints.Length; i++) {
                    int x = AbsoluteOffset(formatInformationPoints[i].X);
                    int y = AbsoluteOffset(formatInformationPoints[i].Y);
                    ref QRMapInfo info = ref NewMapInfo(QRType.FormatInformation, x, y);
                    info.Offset = i;
                    formatInformation[i] = (QRMapInfo*)Unsafe.AsPointer(ref info);
                }
                return formatInformation;
            }

            formatInformation1 = Set(formatInformationPoints1);
            formatInformation2 = Set(formatInformationPoints2);
        }

        private void SetVersionInformation() {
            if (version < 7) return;

            UnmanagedRefArray<QRMapInfo> Set(Func<int, int, int> getX, Func<int, int, int> getY) {
                var versionInformation = new UnmanagedRefArray<QRMapInfo>(18);
                for (int j = 0; j < 6; j++) {
                    for (int i = 0; i < 3; i++) {
                        int offset = j * 3 + i;
                        int x = getX(i, j);
                        int y = getY(i, j);
                        ref QRMapInfo info = ref NewMapInfo(QRType.VersionInformation, x, y);
                        info.Offset = offset;
                        versionInformation[offset] = (QRMapInfo*)Unsafe.AsPointer(ref info);
                    }
                }
                return versionInformation;
            }

            versionInformation1 = Set(getX: (i, _) => AbsoluteOffset(-11 + i), getY: (_, j) => j);
            versionInformation2 = Set(getX: (_, j) => j, getY: (i, _) => AbsoluteOffset(-11 + i));
        }

        private void SetDataEcc() {
            int blockCount = blockInfo.Blocks1 + blockInfo.Blocks2;
            int byteOffset = 0;
            for (int r = 0; r < blockInfo.Blocks1; r++) {
                FillBlock(r, blockInfo.Codewords1);
            }
            for (int r = blockInfo.Blocks1; r < blockCount; r++) {
                FillBlock(r, blockInfo.Codewords2);
            }

            void FillBlock(int row, int blockCodewords) {
                var dataBlock = new UnmanagedArray<QRDataInfo>(blockCodewords);
                for (int c = 0; c < blockCodewords; c++, byteOffset++) {
                    ref QRDataInfo info = ref dataBlock[c];
                    info.Type = QRType.Data;
                    info.ByteOffset = byteOffset;
                    info.BlockRow = row;
                    info.BlockColumn = c;
                    dataInfo[byteOffset] = (QRDataInfo*)Unsafe.AsPointer(ref info);
                }
                dataBlocksRef[row] = dataBlock;
                _dataBlocks[row] = new UnmanagedRefArray<QRDataInfo>(dataBlock);
                this.dataBlocks[row] = _dataBlocks[row].Raw;
            }

            byteOffset = 0;
            for (int r = 0; r < eccBlocksRef.Length; r++) {
                var eccBlock = new UnmanagedArray<QRDataInfo>(blockInfo.EccPerBlock);
                for (int c = 0; c < blockInfo.EccPerBlock; c++) {
                    ref QRDataInfo info = ref eccBlock[c];
                    info.Type = QRType.Ecc;
                    info.ByteOffset = byteOffset;
                    info.BlockRow = r;
                    info.BlockColumn = c;
                }
                eccBlocksRef[r] = eccBlock;
                _eccBlocks[r] = new UnmanagedRefArray<QRDataInfo>(eccBlock);
                this.eccBlocks[r] = _eccBlocks[r].Raw;
            }

            int totalBytes = dataCapacity + blockCount * blockInfo.EccPerBlock;
            using var dataEcc = new UnmanagedRefArray<QRDataInfo>(totalBytes);
            ref var dataBlocks = ref MemoryMarshal.GetArrayDataReference(dataBlocksRef);
            ref var eccBlocks = ref MemoryMarshal.GetArrayDataReference(eccBlocksRef);
            int i = 0;
            for (int c = 0; c < blockInfo.Codewords1; c++) {
                for (int r = 0; r < blockCount; r++) {
                    dataEcc[i++] = (QRDataInfo*)Unsafe.AsPointer(ref Unsafe.Add(ref dataBlocks, r)[c]);
                }
            }
            for (int c = blockInfo.Codewords1; c < blockInfo.Codewords2; c++) {
                for (int r = blockInfo.Blocks1; r < blockCount; r++) {
                    dataEcc[i++] = (QRDataInfo*)Unsafe.AsPointer(ref Unsafe.Add(ref dataBlocks, r)[c]);
                }
            }
            for (int c = 0; c < blockInfo.EccPerBlock; c++) {
                for (int r = 0; r < blockCount; r++) {
                    dataEcc[i++] = (QRDataInfo*)Unsafe.AsPointer(ref Unsafe.Add(ref eccBlocks, r)[c]);
                }
            }

            int totalBits = totalBytes << 3;
            int bitOffset = 0;
            int divide = (size - 7) >> 1;
            int halfSize = size >> 1;

            for (int x0 = 0; x0 < halfSize; x0++) {
                for (int y0 = 0; y0 < size; y0++) {
                    int beginX = size - 1 - (x0 << 1) - (x0 >= divide ? 1 : 0);
                    int y = (x0 & 1) != 0 ? y0 : size - 1 - y0;
                    for (int x = beginX; x >= beginX - 1; x--) {
                        nint mapOffset = y * size + x;
                        ref QRMapInfo info = ref map2dEntities[mapOffset];
                        if (info.Type == QRType.None) {
                            if (bitOffset < totalBits) {
                                QRDataInfo* byteInfo = dataEcc[bitOffset >> 3];
                                byteInfo->MapOffsets[bitOffset & 7] = mapOffset;

                                info.MapOffset = mapOffset;
                                info.Type = byteInfo->Type;
                                info.X = x;
                                info.Y = y;
                                info.BitIndex = bitOffset & 7;
                                info.ByteInfo = byteInfo;
                            } else {
                                info.MapOffset = mapOffset;
                                info.Type = QRType.Padding;
                                info.X = x;
                                info.Y = y;
                            }
                            bitOffset++;
                        }
                    }
                }
            }
        }

        static readonly Func<int, int, bool>[] MaskGenerators = {
            (x, y) => (x + y) % 2 == 0,
            (x, y) => y % 2 == 0,
            (x, y) => x % 3 == 0,
            (x, y) => (x + y) % 3 == 0,
            (x, y) => (x / 3 + y / 2) % 2 == 0,
            (x, y) => (x * y) % 2 + (x * y) % 3 == 0,
            (x, y) => ((x * y) % 2 + (x * y) % 3) % 2 == 0,
            (x, y) => ((x + y) % 2 + (x * y) % 3) % 2 == 0,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedArray<byte> GetMask(QRMaskVersion maskVersion) {
            return templateMasks[(int)maskVersion] ??= CreateMask(MaskGenerators[(int)maskVersion]);

            UnmanagedArray<byte> CreateMask(Func<int, int, bool> g) {
                const int flags = 1 << (int)QRType.Data
                    | 1 << (int)QRType.Ecc
                    | 1 << (int)QRType.Padding;

                var result = new UnmanagedArray<byte>(map2D.Length, fillValue: 0);
                for (int y = 0; y < size; y++) {
                    for (int x = 0; x < size; x++) {
                        int offset = y + size * x;
                        if (((1 << (int)map2D[offset]->Type) & flags) != 0 && g(x, y)) {
                            result[offset] = 1;
                        }
                    }
                }
                return result;
            }
        }

        public (UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray DataArray, UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray EccArray) AllocDataEccArray(byte* buffer) {
            var dataRawArray = dataBlocks.Raw;
            var eccRawArray = eccBlocks.Raw;
            Unsafe.SkipInit(out UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray dataArray);
            Unsafe.SkipInit(out UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray eccArray);
            dataArray.NativeArray = (UnmanagedArray<byte>.RawArray*)buffer;
            dataArray.Length = dataRawArray.Length;
            eccArray.NativeArray = (UnmanagedArray<byte>.RawArray*)buffer + dataRawArray.Length;
            eccArray.Length = eccRawArray.Length;

            byte* dataPointer = (byte*)(eccArray.NativeArray + eccArray.Length);
            for (int i = 0; i < dataRawArray.Length; i++) {
                dataArray[i].NativeArray = dataPointer;
                dataPointer += dataArray[i].Length = dataRawArray[i].Length;
            }
            for (int i = 0; i < eccRawArray.Length; i++) {
                eccArray[i].NativeArray = dataPointer;
                dataPointer += eccArray[i].Length = eccRawArray[i].Length;
            }
            return (dataArray, eccArray);
        }

        public void WriteDataEccArray(ReadOnlySpan<byte> finalEncodedData, UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray dataArray, UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray eccArray, bool fillPadding = true) {
            if (finalEncodedData.Length > dataCapacity)
                throw new ArgumentOutOfRangeException(nameof(finalEncodedData), "数据过大");

            var dataBlocksArray = dataBlocks.Raw;
            var eccBlocksArray = eccBlocks.Raw;
            CheckLengths(dataBlocksArray, dataArray);
            CheckLengths(eccBlocksArray, eccArray);

            nint offset = 0;
            QRDataInfo** infos = dataInfo.Pointer;
            nint length = finalEncodedData.Length;
            int row, col;
            ref byte data = ref MemoryMarshal.GetReference(finalEncodedData);

            for (; offset < length; offset++) {
                row = infos[offset]->BlockRow;
                col = infos[offset]->BlockColumn;
                dataArray[row][col] = Unsafe.Add(ref data, offset);
            }

            if (fillPadding) {
                nint dataCapacity = this.dataCapacity;
                while (offset < dataCapacity) {
                    row = infos[offset]->BlockRow;
                    col = infos[offset]->BlockColumn;
                    dataArray[row][col] = 0b11101100;
                    offset++;
                    if (offset < dataCapacity) {
                        row = infos[offset]->BlockRow;
                        col = infos[offset]->BlockColumn;
                        dataArray[row][col] = 0b00010001;
                        offset++;
                    }
                }
            }

            for (int i = 0; i < dataBlocksArray.Length; i++) {
                ReadOnlySpan<byte> msg = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<byte>(dataArray[i].NativeArray), dataArray[i].Length);
                Span<byte> outEcc = MemoryMarshal.CreateSpan(ref Unsafe.AsRef<byte>(eccArray[i].NativeArray), eccArray[i].Length);
                RSEncode.Encode(msg, outEcc);
            }


            static void CheckLengths(UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray>.RawArray templateArray, UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray inputArray) {
                if (inputArray.Length != templateArray.Length) throw new ArgumentOutOfRangeException(nameof(inputArray));
                for (int i = 0; i < templateArray.Length; i++) {
                    if (inputArray[i].Length != templateArray[i].Length) {
                        throw new ArgumentOutOfRangeException(nameof(inputArray));
                    }
                }
            }
        }


        private static readonly QRLayout?[,] templateLayouts = new QRLayout[40, 4];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static QRLayout GetLayout(int version, QREcLevel ecLevel) {
            if (version is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(version));
            if ((int)ecLevel is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(ecLevel));
            return templateLayouts[version - 1, (int)ecLevel] ??= new QRLayout(version, ecLevel);
        }
    }
}
