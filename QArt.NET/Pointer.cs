using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace QArt.NET {
    [DebuggerTypeProxy(typeof(Pointer<>.DebugView))]
    [DebuggerDisplay("{ToString(),nq,raw}")]
    [StructLayout(LayoutKind.Sequential)]
    unsafe public readonly struct Pointer<T> where T : unmanaged {
        private readonly T* pointer;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ref T Ref {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef<T>(pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T*(Pointer<T> @this) => *(T**)Unsafe.AsPointer(ref @this);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Pointer<T>(T* pointer) => Unsafe.AsRef<Pointer<T>>(&pointer);

        public override string ToString() {
            if (pointer == null) return "null";

            var sb = new StringBuilder();
            if (sizeof(void*) == 8) {
                sb.Append(((ulong)pointer).ToString("x16"));
            } else {
                sb.Append(((uint)pointer).ToString("x8"));
            }
            sb.Append(" {").Append((*pointer).ToString()).Append('}');
            return sb.ToString();
        }

        private class DebugView {
            readonly Pointer<T> pointer;

            public DebugView(Pointer<T> pointer) {
                this.pointer = pointer;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public T? Value => (void*)pointer == null ? null : pointer.Ref;
        }
    }
}
