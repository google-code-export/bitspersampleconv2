using System.Windows.Media;
using System.IO;

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

        public static ushort ReadBigU16(BinaryReader br) {
            ushort result = (ushort)(((int)br.ReadByte() << 8) + br.ReadByte());
            return result;
        }

        public static uint ReadBigU32(BinaryReader br) {
            uint result = 
                (uint)((uint)br.ReadByte() << 24) +
                (uint)((uint)br.ReadByte() << 16) +
                (uint)((uint)br.ReadByte() << 8) +
                (uint)((uint)br.ReadByte() << 0);
            return result;
        }
    }
}
