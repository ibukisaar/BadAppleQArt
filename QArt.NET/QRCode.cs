using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QArt.NET {
    unsafe public sealed class QRCode : IDisposable {
        public QRLayout Layout { get; }
        public int Version => Layout.Version;
        public QREcLevel EcLevel => Layout.EcLevel;
        public int Size => Layout.Size;
        public QRMaskVersion MaskVersion { get; }
        public UnmanagedArray<QRValue> Values { get; }

        public ref QRValue this[int x, int y] => ref Values[y * Size + x];

        public QRCode(ReadOnlySpan<byte> data, int version, QREcLevel ecLevel, QRDataMode? mode, QRMaskVersion maskVersion) {
            if (version is < 0 or > 40) throw new ArgumentOutOfRangeException(nameof(version));
            if (data.Length > QRHelper.MaxDataLength) throw new ArgumentOutOfRangeException(nameof(data), "数据过大");

            bool[] encodedData = version == 0 ?
                QREncoder.Encode(data, ecLevel, mode, out version) :
                QREncoder.Encode(data, version, ecLevel, mode);
            int encodedBits = encodedData.Length;

            Layout = QRLayout.GetLayout(version, ecLevel);
            MaskVersion = maskVersion;
            Values = new UnmanagedArray<QRValue>(Layout.Map2D.Length);

            // 此处的Values当临时空间使用
            Span<bool> finalEncodedBuffer = MemoryMarshal.CreateSpan(ref Unsafe.AsRef<bool>(Values.Pointer), Layout.DataCapacity * 8);
            encodedData.CopyTo(finalEncodedBuffer);
            encodedBits = QREncoder.WriteTerminator(finalEncodedBuffer, encodedBits);

            WriteAll(finalEncodedBuffer[..encodedBits]);
        }

        public QRCode(ReadOnlySpan<byte> data) : this(data, 0, QREcLevel.L, null, QRMaskVersion.Version000) { }

        public QRCode(ReadOnlySpan<bool> finalEncodedData, int version, QREcLevel ecLevel, QRMaskVersion maskVersion) {
            if (version is < 0 or > 40) throw new ArgumentOutOfRangeException(nameof(version));

            Layout = QRLayout.GetLayout(version, ecLevel);
            MaskVersion = maskVersion;
            Values = new UnmanagedArray<QRValue>(Layout.Map2D.Length);

            WriteAll(finalEncodedData);
        }

        internal QRCode(QRLayout layout, QRMaskVersion maskVersion) {
            Layout = layout;
            MaskVersion = maskVersion;
            Values = new UnmanagedArray<QRValue>(Layout.Map2D.Length);

            CopyLayoutValues();
            WriteFormat(QRHelper.GetFormatBits(EcLevel, MaskVersion));
            WriteVersion(QRHelper.GetVersionBits(Version));
        }

        [SkipLocalsInit]
        private void WriteAll(ReadOnlySpan<bool> finalEncodedData) {
            Span<byte> finalEncodedByteArray = stackalloc byte[QRTools.GetByteCount(finalEncodedData.Length)];
            QRTools.ToByteArrayNoCheck(finalEncodedData, finalEncodedByteArray);

            byte* buffer = stackalloc byte[Layout.BufferCapacity];
            var (dataArray, eccArray) = Layout.AllocDataEccArray(buffer);
            Layout.WriteDataEccArray(finalEncodedByteArray, dataArray, eccArray);

            CopyLayoutValues();
            WriteFormat(QRHelper.GetFormatBits(EcLevel, MaskVersion));
            WriteVersion(QRHelper.GetVersionBits(Version));
            WriteDataArray(dataArray);
            WriteEccArray(eccArray);
            XorMask();
        }



        internal void CopyLayoutValues() {
            const int flags = 1 << (int)QRType.FinderPattern
                | 1 << (int)QRType.Separator
                | 1 << (int)QRType.TimingPatterns
                | 1 << (int)QRType.AlignmentPatterns
                | 1 << (int)QRType.OtherPatterns;

            QRMapInfo** layoutMap2D = Layout.Map2D.Pointer;
            QRValue* valueMap2D = Values.Pointer;
            for (nint i = 0, len = Values.Length; i < len; i++) {
                if (((1 << (int)layoutMap2D[i]->Type) & flags) != 0) {
                    valueMap2D[i] = layoutMap2D[i]->Value;
                }
            }
        }

        internal void WriteFormat(int formatBits) {
            QRMapInfo** formatInformation1 = Layout.FormatInformation1.Pointer;
            QRMapInfo** formatInformation2 = Layout.FormatInformation2.Pointer;
            QRValue* values = Values.Pointer;

            for (int i = 0; i < 15; i++) {
                QRValue value = ((1 << i) & formatBits) != 0;
                values[formatInformation1[i]->MapOffset] = values[formatInformation2[i]->MapOffset] = value;
            }
        }

        internal void WriteVersion(int versionBits) {
            if (Version < 7) return;

            QRMapInfo** versionInformation1 = Layout.VersionInformation1!.Pointer;
            QRMapInfo** versionInformation2 = Layout.VersionInformation2!.Pointer;
            QRValue* values = Values.Pointer;

            for (int i = 0; i < 18; i++) {
                QRValue value = ((1 << i) & versionBits) != 0;
                values[versionInformation1[i]->MapOffset] = values[versionInformation2[i]->MapOffset] = value;
            }
        }

        private void WriteArray(UnmanagedArray<UnmanagedRefArray<QRDataInfo>.RawArray> templateArray, UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray inputArray) {
            QRValue* values = Values.Pointer;
            for (int r = 0; r < inputArray.Length; r++) {
                byte* rowData = inputArray[r].NativeArray;
                nint length = inputArray[r].Length;
                UnmanagedRefArray<QRDataInfo>.RawArray templateRow = templateArray[r];
                for (nint c = 0; c < length; c++) {
                    ref QRPointsInfo ps = ref templateRow[c]->MapOffsets;
                    int v = rowData[c]; // byte
                    values[ps.p0] = (QRValue)(v >> 7);
                    values[ps.p1] = (QRValue)((v >> 6) & 1);
                    values[ps.p2] = (QRValue)((v >> 5) & 1);
                    values[ps.p3] = (QRValue)((v >> 4) & 1);
                    values[ps.p4] = (QRValue)((v >> 3) & 1);
                    values[ps.p5] = (QRValue)((v >> 2) & 1);
                    values[ps.p6] = (QRValue)((v >> 1) & 1);
                    values[ps.p7] = (QRValue)(v & 1);
                }
            }
        }

        internal void Write(UnmanagedRefArray<QRDataInfo>.RawArray template, ReadOnlySpan<byte> input) {
            QRValue* values = Values.Pointer;
            for (int i = 0; i < input.Length; i++) {
                ref QRPointsInfo ps = ref template[i]->MapOffsets;
                int v = input[i];
                values[ps.p0] = (QRValue)(v >> 7);
                values[ps.p1] = (QRValue)((v >> 6) & 1);
                values[ps.p2] = (QRValue)((v >> 5) & 1);
                values[ps.p3] = (QRValue)((v >> 4) & 1);
                values[ps.p4] = (QRValue)((v >> 3) & 1);
                values[ps.p5] = (QRValue)((v >> 2) & 1);
                values[ps.p6] = (QRValue)((v >> 1) & 1);
                values[ps.p7] = (QRValue)(v & 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteDataArray(UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray dataArray) {
            WriteArray(Layout.DataBlockTemplate, dataArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteEccArray(UnmanagedArray<UnmanagedArray<byte>.RawArray>.RawArray eccArray) {
            WriteArray(Layout.EccBlockTemplate, eccArray);
        }

        internal void XorMask() {
            UnmanagedArray<byte> mask = Layout.GetMask(MaskVersion);
            nint length = mask.Length;
            ref ulong m = ref Unsafe.AsRef<ulong>(mask.Pointer);
            ref ulong v = ref Unsafe.AsRef<ulong>(Values.Pointer);
            nint i = 0;
            for (; i < length; i += 8) {
                Unsafe.AddByteOffset(ref v, i) ^= Unsafe.AddByteOffset(ref m, i);
            }
            for (; i < length; i++) {
                Unsafe.Add(ref Unsafe.As<ulong, byte>(ref v), i) ^= Unsafe.Add(ref Unsafe.As<ulong, byte>(ref m), i);
            }
        }

        public override string ToString() {
            int size = Size;
            var sb = new StringBuilder((size + 2) * size);
            nint i = 0;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    sb.Append(Values[i++] ? '■' : '□');
                }
                if (y != size - 1) sb.AppendLine();
            }
            return sb.ToString();
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                Values.Dispose();
            }
        }

        public void Dispose() {
            Dispose(disposing: true);
        }
    }
}
