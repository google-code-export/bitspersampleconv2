using System.Runtime.InteropServices;

namespace WWFlacRWCS {
    public class Metadata {
        public int          sampleRate;
        public int          channels;
        public int          bitsPerSample;
        public int          pictureBytes;

        public long         totalSamples;

        public string titleStr;
        public string artistStr;
        public string albumStr;
        public string albumArtistStr;
        public string genreStr;

        public string dateStr;
        public string trackNumberStr;
        public string discNumberStr;
        public string pictureMimeTypeStr;

        public string pictureDescriptionStr;

        public byte [] md5sum;
    };

    public class FlacRW {
        public int DecodeAll(string path) {
            return NativeMethods.WWFlacRW_DecodeAll(path);
        }

        public int GetDecodedMetadata(int id, out Metadata meta) {
            NativeMethods.Metadata nMeta;
            int result = NativeMethods.WWFlacRW_GetDecodedMetadata(id, out nMeta);
            meta = new Metadata();
            if (0 <= result) {
                meta.sampleRate     = nMeta.sampleRate;
                meta.channels       = nMeta.channels;
                meta.bitsPerSample  = nMeta.bitsPerSample;
                meta.pictureBytes   = nMeta.pictureBytes;
                meta.totalSamples   = nMeta.totalSamples;
                meta.titleStr       = nMeta.titleStr;
                meta.artistStr      = nMeta.artistStr;
                meta.albumArtistStr = nMeta.albumArtistStr;
                meta.genreStr       = nMeta.genreStr;
                meta.dateStr        = nMeta.dateStr;
                meta.trackNumberStr = nMeta.trackNumberStr;
                meta.discNumberStr  = nMeta.discNumberStr;
                meta.pictureMimeTypeStr    = nMeta.pictureMimeTypeStr;
                meta.pictureDescriptionStr = nMeta.pictureDescriptionStr;
                meta.md5sum = nMeta.md5sum;
            }
            return result;
        }

        public int GetDecodedPicture(int id, out byte [] pictureReturn, int pictureBytes) {
            pictureReturn = new byte[pictureBytes];
            return NativeMethods.WWFlacRW_GetDecodedPicture(id, pictureReturn, pictureReturn.Length);
        }

        public long GetDecodedPcmBytes(int id, int channel, long startBytes, out byte[] pcmReturn, long pcmBytes) {
            pcmReturn = new byte[pcmBytes];
            return NativeMethods.WWFlacRW_GetDecodedPcmBytes(id, channel, startBytes, pcmReturn, pcmReturn.LongLength);
        }

        public int DecodeEnd(int id) {
            return NativeMethods.WWFlacRW_DecodeEnd(id);
        }

        public int EncodeInit(Metadata meta) {
            var nMeta = new NativeMethods.Metadata();
            nMeta.sampleRate = meta.sampleRate;
            nMeta.channels = meta.channels;
            nMeta.bitsPerSample = meta.bitsPerSample;
            nMeta.pictureBytes = meta.pictureBytes;
            nMeta.totalSamples = meta.totalSamples;
            nMeta.titleStr = meta.titleStr;
            nMeta.artistStr = meta.artistStr;
            nMeta.albumArtistStr = meta.albumArtistStr;
            nMeta.genreStr = meta.genreStr;
            nMeta.dateStr = meta.dateStr;
            nMeta.trackNumberStr = meta.trackNumberStr;
            nMeta.discNumberStr = meta.discNumberStr;
            nMeta.pictureMimeTypeStr = meta.pictureMimeTypeStr;
            nMeta.pictureDescriptionStr = meta.pictureDescriptionStr;
            nMeta.md5sum = meta.md5sum;
            return NativeMethods.WWFlacRW_EncodeInit(nMeta);
        }

        public int EncodeSetPicture(int id, byte[] pictureData) {
            return NativeMethods.WWFlacRW_EncodeSetPicture(id, pictureData, pictureData.Length);
        }

        public int EncodeAddPcm(int id, int channel, byte[] pcmData) {
            return NativeMethods.WWFlacRW_EncodeAddPcm(id, channel, pcmData, pcmData.LongLength);
        }

        public int EncodeRun(int id, string path) {
            return NativeMethods.WWFlacRW_EncodeRun(id, path);
        }
        
        public int EncodeEnd(int id) {
            return NativeMethods.WWFlacRW_EncodeEnd(id);
        }
    }

    internal static class NativeMethods {
        const int WWFLAC_TEXT_STRSZ = 256;
        const int WWFLAC_MD5SUM_BYTES = 16;

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct Metadata {
            public int          sampleRate;
            public int          channels;
            public int          bitsPerSample;
            public int          pictureBytes;

            public long         totalSamples;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string titleStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string artistStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string albumStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string albumArtistStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string genreStr;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string dateStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string trackNumberStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string discNumberStr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string pictureMimeTypeStr;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WWFLAC_TEXT_STRSZ)]
            public string pictureDescriptionStr;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2560)]
            public byte [] md5sum;
        };

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_DecodeAll(string path);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_GetDecodedMetadata(int id, out Metadata metaReturn);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_GetDecodedPicture(int id, byte[] pictureReturn, int pictureBytes);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        long WWFlacRW_GetDecodedPcmBytes(int id, int channel, long startBytes, byte[] pcmReturn, long pcmBytes);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_DecodeEnd(int id);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_EncodeInit(Metadata meta);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_EncodeSetPicture(int id, byte[] pictureData, int pictureBytes);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_EncodeAddPcm(int id, int channel, byte[] pcmData, long pcmBytes);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_EncodeRun(int id, string path);

        [DllImport("WWFlacRW.dll", CharSet = CharSet.Unicode)]
        internal extern static
        int WWFlacRW_EncodeEnd(int id);
    }
}
