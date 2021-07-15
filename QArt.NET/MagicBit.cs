using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace QArt.NET {
    public readonly struct MagicBit {
        private readonly byte bits;

        public bool Value => (bits & 1) != 0;

        public MagicBitType Type => (MagicBitType)(bits >> 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MagicBit(MagicBitType type, bool value) {
            bits = (byte)(((int)type << 1) | (value ? 1 : 0));
        }

        public override string ToString()
            => $"{Type}, {(Value ? 1 : 0)}";
    }
}
