using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [DebuggerTypeProxy(typeof(UnmanagedRefArray<>.DebugView))]
    [DebuggerDisplay("{ToString(),raw}")]
    unsafe public sealed class UnmanagedRefArray<T> : IDisposable where T : unmanaged {
        public static UnmanagedRefArray<T> Empty { get; } = new UnmanagedRefArray<T>(0);

        private readonly RawArray raw;

        public ref readonly RawArray Raw => ref raw;

        public T** Pointer => raw.NativeArray;

        public int Length => raw.Length;

        public ref T* this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref raw[index];
        }

        /// <summary>
        /// 此索引器在x64中可以避免零扩展（前提是<paramref name="index"/>本身就是<see cref="IntPtr"/>类型，不能有任何类型转换）
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ref T* this[nint index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref raw[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedRefArray(int length) {
            raw.NativeArray = (T**)Marshal.AllocHGlobal(length * sizeof(void*));
            raw.Length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedRefArray(int length, T* fillValue) : this(length) {
            AsSpan().Fill(fillValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedRefArray(ReadOnlySpan<Pointer<T>> span) : this(span.Length) {
            span.CopyTo(AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedRefArray(UnmanagedArray<T> unmanagedArray) : this(unmanagedArray.Length) {
            for (int i = 0; i < raw.Length; i++) {
                raw.NativeArray[i] = (T*)Unsafe.AsPointer(ref unmanagedArray[i]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Pointer<T>> AsSpan() {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<Pointer<T>>(raw.NativeArray), raw.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Pointer<T>> AsSpan(int start) {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<Pointer<T>>(raw.NativeArray + start), raw.Length - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Pointer<T>> AsSpan(int start, int length) {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<Pointer<T>>(raw.NativeArray + start), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<Pointer<T>>(UnmanagedRefArray<T> @this) => @this.AsSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<Pointer<T>>(UnmanagedRefArray<T> @this) => @this.AsSpan();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:删除未使用的参数", Justification = "<挂起>")]
        private void Dispose(bool disposing) {
            Marshal.FreeHGlobal((IntPtr)raw.NativeArray);
        }

        ~UnmanagedRefArray() {
            Dispose(disposing: false);
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T*[] ToArray() {
            T*[] result = new T*[Length];
            for (int i = 0; i < result.Length; i++) {
                result[i] = raw.NativeArray[i];
            }
            return result;
        }

        public override string ToString() {
            return $"UnmanagedRefArray<{typeof(T).Name}>[{Length}]";
        }

        [DebuggerDisplay("{ToString(),raw}")]
        [DebuggerTypeProxy(typeof(UnmanagedRefArray<>.RawArray.DebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public struct RawArray {
            public T** NativeArray { get; internal set; }
            public int Length { get; internal set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawArray(T** pointer, int length) {
                NativeArray = pointer;
                Length = length;
            }

            public readonly ref T* this[int index] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref NativeArray[index];
            }

            public readonly ref T* this[nint index] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref NativeArray[index];
            }

            public override string ToString() {
                return $"UnmanagedRefArray<{typeof(T).Name}>.RawArray[{Length}]";
            }

            private class DebugView {
                private readonly Pointer<T>[] values;

                public DebugView(RawArray array) {
                    values = new Pointer<T>[array.Length];
                    for (int i = 0; i < array.Length; i++) {
                        values[i] = array.NativeArray[i];
                    }
                }

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public Pointer<T>[] Items => values;
            }
        }

        public ref struct Enumerator {
            readonly T** nativeArray;
            readonly int length;
            int index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(UnmanagedRefArray<T> source) {
                nativeArray = source.raw.NativeArray;
                length = source.raw.Length;
                index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                int nextIndex = index + 1;
                if (nextIndex < length) {
                    index = nextIndex;
                    return true;
                }
                return false;
            }

            public readonly ref T* Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref nativeArray[index];
            }
        }

        private class DebugView {
            private readonly Pointer<T>[] values;

            public DebugView(UnmanagedRefArray<T> array) {
                values = array.AsSpan().ToArray();
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public Pointer<T>[] Items => values;
        }
    }
}
