using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace QArt.NET {
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(StackList<>.DebugView))]
    unsafe internal struct StackList<T> where T : unmanaged {
        public readonly T* Buffer { get; }

        public int Count { readonly get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StackList(Span<T> buffer) {
            Buffer = (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));
            Count = 0;
        }

        public readonly ref T this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T value) {
            Buffer[Count++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<T> AsReadOnlySpan(int start, int length) {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<T>(Buffer + start), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ReadOnlySpan<T> AsReadOnlySpan(int start) {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef<T>(Buffer + start), Count - start);
        }

        private class DebugView {
            private readonly T[] values;

            public DebugView(StackList<T> list) {
                values = new T[list.Count];
                for (int i = 0; i < values.Length; i++) {
                    values[i] = list[i];
                }
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T[] Items => values;
        }
    }
}
