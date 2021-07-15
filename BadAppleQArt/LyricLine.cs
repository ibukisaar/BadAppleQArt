using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BadAppleQArt {
    public sealed class LyricLine {
        public TimeSpan Time { get; }
        public string Text { get; }

        public LyricLine(TimeSpan time, string text) {
            Time = time;
            Text = text;
        }

        public override string ToString() {
            return $"[{Time:hh\\:mm\\:ss}]{Text}";
        }
    }
}
