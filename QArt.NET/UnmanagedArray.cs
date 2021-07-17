using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [DebuggerTypeProxy(typeof(UnmanagedArray<>.DebugView))]
    [DebuggerDisplay("{ToString(),raw}")]
    unsafe public sealed class UnmanagedArray<T> : IList<T>, IReadOnlyList<T>, IDisposable where T : unmanaged {
        static class EmptyArray {
            public static readonly UnmanagedArray<T> Empty = new(length: 0);
        }

        public static UnmanagedArray<T> Empty => EmptyArray.Empty;


        private readonly RawArray raw;

        public ref readonly RawArray Raw => ref raw;

        public T* Pointer => raw.NativeArray;

        public int Length => raw.Length;

        int ICollection<T>.Count => raw.Length;

        int IReadOnlyCollection<T>.Count => raw.Length;

        bool ICollection<T>.IsReadOnly => false;

        T IList<T>.this[int index] { get => this[index]; set => this[index] = value; }

        T IReadOnlyList<T>.this[int index] { get => this[index]; }

        public ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref raw[index];
        }

        /// <summary>
        /// 此索引器在x64中可以避免零扩展（前提是<paramref name="index"/>本身就是<see cref="IntPtr"/>类型，不能有任何类型转换）
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ref T this[nint index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref raw[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedArray(int length) {
            raw.NativeArray = (T*)Marshal.AllocHGlobal(length * sizeof(T));
            raw.Length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedArray(int length, T fillValue) : this(length) {
            AsSpan().Fill(fillValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedArray(ReadOnlySpan<T> span) : this(span.Length) {
            span.CopyTo(AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<T>(raw.NativeArray), raw.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int start) {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<T>(raw.NativeArray + start), raw.Length - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int start, int length) {
            return MemoryMarshal.CreateSpan(ref Unsafe.AsRef<T>(raw.NativeArray + start), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<T>(UnmanagedArray<T> @this) => @this.AsSpan();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(UnmanagedArray<T> @this) => @this.AsSpan();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:删除未使用的参数", Justification = "<挂起>")]
        private void Dispose(bool disposing) {
            Marshal.FreeHGlobal((IntPtr)raw.NativeArray);
        }

        ~UnmanagedArray() {
            Dispose(disposing: false);
        }

        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public int IndexOf(T item) {
            for (int i = 0; i < raw.Length; i++) {
                if (EqualityComparer<T>.Default.Equals(raw.NativeArray[i], item)) {
                    return i;
                }
            }
            return -1;
        }

        void IList<T>.Insert(int index, T item) {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index) {
            throw new NotSupportedException();
        }

        void ICollection<T>.Add(T item) {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear() {
            throw new NotSupportedException();
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int arrayIndex) {
            AsSpan().CopyTo(array.AsSpan(arrayIndex));
        }

        bool ICollection<T>.Remove(T item) {
            throw new NotSupportedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new EnumeratorClass(this);

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ToArray() => AsSpan().ToArray();

        public override string ToString() {
            return $"UnmanagedArray<{typeof(T).Name}>[{Length}]";
        }


        sealed class EnumeratorClass : IEnumerator<T> {
            readonly T* nativeArray;
            readonly int length;
            int index;

            public EnumeratorClass(UnmanagedArray<T> source) {
                nativeArray = source.raw.NativeArray;
                length = source.raw.Length;
                index = -1;
            }

            public T Current => nativeArray[index];

            object IEnumerator.Current => Current;

            public void Dispose() {

            }

            public bool MoveNext() {
                int nextIndex = index + 1;
                if (nextIndex < length) {
                    index = nextIndex;
                    return true;
                }
                return false;
            }

            public void Reset() {
                index = -1;
            }
        }

        [DebuggerDisplay("{ToString(),raw}")]
        [DebuggerTypeProxy(typeof(UnmanagedArray<>.RawArray.DebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public struct RawArray {
            public T* NativeArray { get; internal set; }
            public int Length { get; internal set; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RawArray(T* pointer, int length) {
                NativeArray = pointer;
                Length = length;
            }

            public readonly ref T this[int index] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref NativeArray[index];
            }

            public readonly ref T this[nint index] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref NativeArray[index];
            }

            private class DebugView {
                private readonly T[] values;

                public DebugView(RawArray array) {
                    values = new T[array.Length];
                    for (int i = 0; i < array.Length; i++) {
                        values[i] = array.NativeArray[i];
                    }
                }

                [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
                public T[] Items => values;
            }

            public override string ToString() {
                return $"UnmanagedArray<{typeof(T).Name}>.RawArray[{Length}]";
            }
        }

        public ref struct Enumerator {
            readonly T* nativeArray;
            readonly int length;
            int index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(UnmanagedArray<T> source) {
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

            public readonly ref T Current {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref nativeArray[index];
            }
        }

        private class DebugView {
            private readonly T[] values;

            public DebugView(UnmanagedArray<T> array) {
                values = array.ToArray();
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items => values;
        }
    }
}
