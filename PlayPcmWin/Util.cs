using System.Windows.Media;

namespace PlayPcmWin {
    class Util {

        private Util() {
        }

        public static Color ColorFromArgb(long argb) {
            return Color.FromArgb(
                (byte)((argb & 0xff000000U) >> 24),
                (byte)((argb & 0x00ff0000U) >> 16),
                (byte)((argb & 0x0000ff00U) >> 8),
                (byte)((argb & 0x000000ffU) >> 0));
        }
    }
}
