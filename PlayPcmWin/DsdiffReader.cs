// 日本語UTF-8

using System;
using System.IO;
using System.Threading.Tasks;

namespace PlayPcmWin {

    class DsdiffReader {
        public enum ResultType {
            Success,
            NotDsf,
            HeaderError,
            NotSupportFormatType,
            FormVersionChunkSizeError,
            PropertyChunkSizeError,
            NotSupportPropertyType,
            SampleRateChunkSizeError,
            NotSupportSampleRate,
            ChannelsChunkSizeError,
            CompressionTypeChunkSizeError,
            NotSupportCompressionType,
            NotSupportFormatVersion,
            NotSupportFileTooLarge,
            NotSupportNumChannels,
            NotSupportSampleFrequency,
            ReadError
        }

        public int NumChannels { get; set; }

        /// <summary>
        /// 2822400 (2.8MHz)
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 1フレーム=16ビット(2バイト) x チャンネル数とする
        /// </summary>
        public long NumFrames { get; set; }

        /// <summary>
        /// stream data offset from the start of the file
        /// </summary>
        private const int STREAM_DATA_OFFSET = 92;

        private ResultType ReadDsdChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes < 4 || 0x7fffffff < chunkBytes) {
                return ResultType.NotSupportFileTooLarge;
            }

            byte[] formType = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(formType, 0, "DSD ")) {
                return ResultType.NotSupportFormatType;
            }

            return ResultType.Success;
        }

        private ResultType ReadFormVersionChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes != 4) {
                return ResultType.FormVersionChunkSizeError;
            }

            uint version = Util.ReadBigU32(br);
            if (0x01000000 != (version & 0xff000000)) {
                return ResultType.NotSupportFormatVersion;
            }

            return ResultType.Success;
        }

        private ResultType ReadPropertyChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes < 4) {
                return ResultType.PropertyChunkSizeError;
            }

            byte[] propType = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(propType, 0, "SND ")) {
                return ResultType.NotSupportPropertyType;
            }

            return ResultType.Success;
        }

        private ResultType ReadSampleRateChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes != 4) {
                return ResultType.SampleRateChunkSizeError;
            }

            SampleRate = (int)Util.ReadBigU32(br);
            if (2822400 != SampleRate) {
                return ResultType.NotSupportSampleRate;
            }

            return ResultType.Success;
        }

        private ResultType ReadChannelsChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes < 6) {
                return ResultType.ChannelsChunkSizeError;
            }

            NumChannels = (int)Util.ReadBigU16(br);

            // skip channel ID's
            PcmDataLib.Util.BinaryReaderSkip(br, (long)(chunkBytes - 2));

            return ResultType.Success;
        }

        private ResultType ReadCompressionTypeChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes < 5) {
                return ResultType.CompressionTypeChunkSizeError;
            }

            byte[] propType = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(propType, 0, "DSD ")) {
                return ResultType.NotSupportCompressionType;
            }

            // skip compression name
            PcmDataLib.Util.BinaryReaderSkip(br, (int)(chunkBytes - 4+1) & (~1));

            return ResultType.Success;
        }

        private ResultType ReadSoundDataChunkHeader(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes == 0 || 0x7fffffff < chunkBytes) {
                return ResultType.NotSupportFileTooLarge;
            }

            NumFrames = (long)chunkBytes / 2 / NumChannels;

            return ResultType.Success;
        }

        private ResultType SkipUnknownChunk(BinaryReader br) {
            ulong chunkBytes = Util.ReadBigU64(br);
            if (chunkBytes == 0 || 0x7fffffff < chunkBytes) {
                return ResultType.NotSupportFileTooLarge;
            }

            // skip
            PcmDataLib.Util.BinaryReaderSkip(br, (int)(chunkBytes+1) & (~1));

            return ResultType.Success;
        }

        public string TitleName { get { return ""; } }
        public string AlbumName { get { return ""; } }
        public string ArtistName { get { return ""; } }

        /// <summary>
        /// 画像データバイト数(無いときは0)
        /// </summary>
        public int PictureBytes { get { return 0; } }

        /// <summary>
        /// 画像データ
        /// </summary>
        public byte[] PictureData { get { return new byte[0]; } }

        enum ReadHeaderMode {
            AllHeaders,
            ReadStopBeforeSoundData,
        };

        const int FOURCC_FRM8 = 0x384d5246;
        const int FOURCC_FVER = 0x52455646;
        const int FOURCC_PROP = 0x504f5250;
        const int FOURCC_FS =   0x20205346;
        const int FOURCC_SND =  0x20444e53;
        const int FOURCC_CHNL = 0x4c4e4843;
        const int FOURCC_CMPR = 0x52504d43;
        const int FOURCC_DSD =  0x20445344;

        private ResultType ReadHeader1(BinaryReader br, out PcmDataLib.PcmData pcmData, ReadHeaderMode mode) {
            pcmData = new PcmDataLib.PcmData();
            bool done = false;

            try {
                while (!done) {
                    ResultType rt = ResultType.Success;
                    uint fourCC = br.ReadUInt32();
                    switch (fourCC) {
                    case FOURCC_FRM8:
                        rt = ReadDsdChunk(br);
                        break;
                    case FOURCC_FVER:
                        rt = ReadFormVersionChunk(br);
                        break;
                    case FOURCC_PROP:
                        rt = ReadPropertyChunk(br);
                        break;
                    case FOURCC_FS:
                        rt = ReadSampleRateChunk(br);
                        break;
                    case FOURCC_CHNL:
                        rt = ReadChannelsChunk(br);
                        break;
                    case FOURCC_CMPR:
                        rt = ReadCompressionTypeChunk(br);
                        break;
                    case FOURCC_DSD:
                        rt = ReadSoundDataChunkHeader(br);
                        done = true;
                        break;
                    default:
                        rt = SkipUnknownChunk(br);
                        break;
                    }
                    if (rt != ResultType.Success) {
                        return rt;
                    }
                }
            } catch (EndOfStreamException ex) {
                // unexpected end of stream
                System.Console.WriteLine(ex);
            }

            if (!done ||
                2822400 != SampleRate ||
                2 != NumChannels) {
                return ResultType.ReadError;
            }

            pcmData.SetFormat(
                NumChannels,
                24,
                24,
                176400,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                NumFrames);
            pcmData.IsDsdOverPcm = true;

            return 0;
        }

        public ResultType ReadHeader(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            return ReadHeader1(br, out pcmData, ReadHeaderMode.AllHeaders);
        }

        // 1フレームは
        // DSFファイルを読み込む時 16ビット x チャンネル数
        // DoPデータとしては 24ビット x チャンネル数
        private long mPosFrame;

        public ResultType ReadStreamBegin(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            ResultType rt = ResultType.Success;
            rt = ReadHeader1(br, out pcmData, ReadHeaderMode.ReadStopBeforeSoundData);
            mPosFrame = 0;

            return rt;
        }

        /// <summary>
        /// フレーム指定でスキップする。
        /// </summary>
        /// <param name="skipFrames">スキップするフレーム数。負の値は指定できない。</param>
        /// <returns>実際にスキップできたフレーム数。</returns>
        public long ReadStreamSkip(BinaryReader br, long skipFrames) {
            if (skipFrames < 0) {
                System.Diagnostics.Debug.Assert(false);
            }

            if (NumFrames < mPosFrame + skipFrames) {
                // 最後に移動。
                skipFrames = NumFrames - mPosFrame;
            }
            if (skipFrames == 0) {
                return 0;
            }

            // DSDIFFの1フレーム=16ビット(2バイト) x チャンネル数
            PcmDataLib.Util.BinaryReaderSkip(br, skipFrames * 2 * NumChannels);
            mPosFrame += skipFrames;
            return skipFrames;
        }

        /// <summary>
        /// preferredFramesフレームぐらい読み出す。
        /// 1Mフレームぐらいにすると効率が良い。
        /// </summary>
        /// <returns>読みだしたフレーム</returns>
        public byte[] ReadStreamReadOne(BinaryReader br, int preferredFrames) {
            int readFrames = preferredFrames;
            if (NumFrames < mPosFrame + readFrames) {
                readFrames = (int)(NumFrames - mPosFrame);
            }

            if (readFrames == 0) {
                // 1バイトも読めない。
                return new byte[0];
            }

            // DoPの1フレーム == 24bit * NumChannels
            int streamBytes = 3 * readFrames * NumChannels;
            byte [] stream = new byte[streamBytes];

            int writePos = 0;
            for (int i=0; i < readFrames; ++i) {
                byte [] dsdData = br.ReadBytes(NumChannels * 2);
                for (int ch=0; ch < NumChannels; ++ch) {
                    stream[writePos + 0] = dsdData[ch + NumChannels];
                    stream[writePos + 1] = dsdData[ch];
                    stream[writePos + 2] = (byte)(0 != (i & 1) ? 0xfa : 0x05);
                    writePos += 3;
                }
            }
            mPosFrame += readFrames;

            return stream;
        }

        public void ReadStreamEnd() {
            mPosFrame = 0;
        }
    }
}
