using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace WavRWLib2
{
    class RiffChunkDescriptor
    {
        private byte[] m_chunkId;

        /// <summary>
        /// チャンクサイズはあてにならない。
        /// </summary>
        public uint ChunkSize { get; set; }
        private byte[] m_format;

        public void Create(uint chunkSize)
        {
            m_chunkId = new byte[4];
            m_chunkId[0] = (byte)'R';
            m_chunkId[1] = (byte)'I';
            m_chunkId[2] = (byte)'F';
            m_chunkId[3] = (byte)'F';

            ChunkSize = chunkSize;

            m_format = new byte[4];
            m_format[0] = (byte)'W';
            m_format[1] = (byte)'A';
            m_format[2] = (byte)'V';
            m_format[3] = (byte)'E';
        }

        public long Read(BinaryReader br, byte[] chunkId)
        {
            m_chunkId = chunkId;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_chunkId, 0, "RIFF") &&
                !PcmDataLib.Util.FourCCHeaderIs(m_chunkId, 0, "RF64")) {
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }

            ChunkSize = br.ReadUInt32();
            if (ChunkSize < 36) {
                Console.WriteLine("E: chunkSize is too small {0}", ChunkSize);
                return 0;
            }

            m_format = br.ReadBytes(4);
            if (!PcmDataLib.Util.FourCCHeaderIs(m_format, 0, "WAVE")) {
                Console.WriteLine("E: RiffChunkDescriptor.format mismatch. \"{0}{1}{2}{3}\" should be \"WAVE\"",
                    (char)m_format[0], (char)m_format[1], (char)m_format[2], (char)m_format[3]);
                return 0;
            }

            return 8;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(m_chunkId);
            bw.Write(ChunkSize);
            bw.Write(m_format);
        }
    }

    class FmtSubChunk
    {
        private byte[] m_subChunk1Id;
        private uint m_subChunk1Size;
        private ushort m_audioFormat;
        public ushort NumChannels { get; set; }
        public uint SampleRate { get; set; }

        public PcmDataLib.PcmData.ValueRepresentationType SampleValueRepresentationType { get; set; }

        private uint   m_byteRate;
        private ushort m_blockAlign;
        public ushort BitsPerSample { get; set; }
        public ushort ValidBitsPerSample { get; set; }
        public uint ChannelMask { get; set; }

        public bool Create(
                int numChannels, int sampleRate, int bitsPerSample, int validBitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType sampleValueRepresentation)
        {
            m_subChunk1Id = new byte[4];
            m_subChunk1Id[0] = (byte)'f';
            m_subChunk1Id[1] = (byte)'m';
            m_subChunk1Id[2] = (byte)'t';
            m_subChunk1Id[3] = (byte)' ';

            m_subChunk1Size = 16;

            m_audioFormat = 1;

            System.Diagnostics.Debug.Assert(0 < numChannels);
            NumChannels = (ushort)numChannels;

            SampleRate = (uint)sampleRate;
            m_byteRate = (uint)(sampleRate * numChannels * bitsPerSample / 8);
            m_blockAlign = (ushort)(numChannels * bitsPerSample / 8);

            BitsPerSample = (ushort)bitsPerSample;
            ValidBitsPerSample = (ushort)validBitsPerSample;
            ChannelMask = 0;

            SampleValueRepresentationType = sampleValueRepresentation;
            if (sampleValueRepresentation == PcmDataLib.PcmData.ValueRepresentationType.SInt) {
                m_audioFormat = 1;
            } else if (sampleValueRepresentation == PcmDataLib.PcmData.ValueRepresentationType.SFloat) {
                m_audioFormat = 3;
            } else {
                System.Diagnostics.Debug.Assert(false);
            }

            return true;
        }
        
        public long Read(BinaryReader br, byte[] fourcc)
        {
            m_subChunk1Id = fourcc;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_subChunk1Id, 0, "fmt ")) {
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }

            m_subChunk1Size = br.ReadUInt32();

            if (40 == m_subChunk1Size) {
                // Console.WriteLine("D: FmtSubChunk.Read() WAVEFORMATEXTENSIBLE\n");
            } else if (16 != m_subChunk1Size && 18 != m_subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.Read() subChunk1Size!=16 {0} this file type is not supported", m_subChunk1Size);
                return 0;
            }

            m_audioFormat = br.ReadUInt16();
            if (1 == m_audioFormat) {
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;
            } else if (3 == m_audioFormat) {
                SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SFloat;
            } else if (0xfffe == m_audioFormat) {
                // WAVEFORMATEXTENSIBLE
            } else {
                Console.WriteLine("E: this wave file is not PCM format {0}. Cannot read this file", m_audioFormat);
                return 0;
            }

            NumChannels = br.ReadUInt16();
            SampleRate = br.ReadUInt32();
            m_byteRate = br.ReadUInt32();
            m_blockAlign = br.ReadUInt16();
            BitsPerSample = br.ReadUInt16();
            ValidBitsPerSample = BitsPerSample;

            ushort extensibleSize = 0;
            if (16 < m_subChunk1Size) {
                // cbSize 2bytes
                extensibleSize = br.ReadUInt16();
            }

            if (0 != extensibleSize && 22 != extensibleSize) {
                Console.WriteLine("E: FmtSubChunk.Read() cbSize != 0 nor 22 {0}", extensibleSize);
                return 0;
            }

            if (22 == extensibleSize) {
                // WAVEFORMATEX(22 bytes)

                ValidBitsPerSample = br.ReadUInt16();
                ChannelMask = br.ReadUInt32();
                var formatGuid = br.ReadBytes(16);

                var pcmGuid   = Guid.Parse("00000001-0000-0010-8000-00aa00389b71");
                var pcmGuidByteArray = pcmGuid.ToByteArray();
                var floatGuid = Guid.Parse("00000003-0000-0010-8000-00aa00389b71");
                var floatGuidByteArray = floatGuid.ToByteArray();
                if (pcmGuidByteArray.SequenceEqual(formatGuid)) {
                    SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SInt;
                } else if (floatGuidByteArray.SequenceEqual(formatGuid)) {
                    SampleValueRepresentationType = PcmDataLib.PcmData.ValueRepresentationType.SFloat;
                } else {
                    Console.WriteLine("E: FmtSubChunk.Read() unknown format guid on WAVEFORMATEX.SubFormat.");
                    Console.WriteLine("{3:X2}{2:X2}{1:X2}{0:X2}-{5:X2}{4:X2}-{7:X2}{6:X2}-{9:X2}{8:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}",
                        formatGuid[0], formatGuid[1], formatGuid[2], formatGuid[3],
                        formatGuid[4], formatGuid[5], formatGuid[6], formatGuid[7],
                        formatGuid[8], formatGuid[9], formatGuid[10], formatGuid[11],
                        formatGuid[12], formatGuid[13], formatGuid[14], formatGuid[15]);
                    return 0;
                }
            }

            if (m_byteRate != SampleRate * NumChannels * BitsPerSample / 8) {
                Console.WriteLine("E: byteRate is wrong value. corrupted file?");
                return 0;
            }

            if (m_blockAlign != NumChannels * BitsPerSample / 8) {
                Console.WriteLine("E: blockAlign is wrong value. corrupted file?");
                return 0;
            }

            return m_subChunk1Size + 4;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(m_subChunk1Id);
            bw.Write(m_subChunk1Size);
            bw.Write(m_audioFormat);
            bw.Write(NumChannels);
            bw.Write(SampleRate);

            bw.Write(m_byteRate);
            bw.Write(m_blockAlign);
            bw.Write(BitsPerSample);
        }
    }
    
    class DS64Chunk {
        private byte[] m_chunkId;
        private uint m_chunkSize;
        public long RiffSize { get; set; }
        public long DataSize { get; set; }
        public long SampleCount { get; set; }
        private List<long> m_table;

        public void Clear() {
            m_chunkId = new byte[4];
            m_chunkId[0] = (byte)'d';
            m_chunkId[1] = (byte)'s';
            m_chunkId[2] = (byte)'6';
            m_chunkId[3] = (byte)'4';

            RiffSize = 0;
            DataSize = 0;
            SampleCount = 0;
            m_table = new List<long>();
        }

        // 大体100テラぐらい
        const long INT64_DATA_SIZE_LIMIT = 0x0000ffffffffffffL;

        public long Read(BinaryReader br) {
            // chunkIdは、既に読んでいる。

            m_chunkSize = br.ReadUInt32();
            if (m_chunkSize < 0x1c) {
                Console.WriteLine("E: ds64 chunk too small.");
                return 0;
            }
            RiffSize = br.ReadInt64();
            DataSize = br.ReadInt64();
            SampleCount = br.ReadInt64();

            if (RiffSize < 0 || INT64_DATA_SIZE_LIMIT < RiffSize ||
                DataSize < 0 || INT64_DATA_SIZE_LIMIT < DataSize ||
                SampleCount < 0 || INT64_DATA_SIZE_LIMIT < SampleCount) {
                Console.WriteLine("E: DS64 content size info too large to handle.");
                return 0;
            }

            m_table = new List<long>();
            uint tableLength = br.ReadUInt32();
            for (uint i=0; i < tableLength; ++i) {
                // 何に使うのかわからないが、取っておく。
                byte[] id = br.ReadBytes(4);
                long v = br.ReadInt64();
                m_table.Add(v);
            }

            // chunkSize情報自体のサイズ4バイトを足す
            return m_chunkSize + 4;
        }
    }



    class DataSubChunk
    {
        private byte[] m_chunkId;
        public uint ChunkSize { get; set; }

        private byte[] m_rawData;

        /// <summary>
        /// ファイル先頭から、このデータチャンクのPCMデータ先頭までのオフセット
        /// </summary>
        public long Offset { get; set; }
        public long NumFrames { get; set; }

        public byte[] GetSampleArray() {
            return m_rawData;
        }

        public void Clear() {
            m_chunkId = null;
            ChunkSize = 0;
            m_rawData = null;
            NumFrames = 0;
        }

        public void SetRawData(long numSamples, byte[] rawData) {
            NumFrames = numSamples;
            m_rawData = rawData;
        }

        public void Create(long numSamples, byte[] rawData) {
            SetRawData(numSamples, rawData);
            m_chunkId = new byte[4];
            m_chunkId[0] = (byte)'d';
            m_chunkId[1] = (byte)'a';
            m_chunkId[2] = (byte)'t';
            m_chunkId[3] = (byte)'a';
            if (UInt32.MaxValue < rawData.LongLength) {
                // RF64形式。別途ds64チャンクを用意して、そこにdata chunkのバイト数を入れる。
                ChunkSize = UInt32.MaxValue;
            } else {
                ChunkSize = (uint)rawData.LongLength;
            }
        }

        public void TrimRawData(long newNumSamples, long startBytes, long endBytes) {
            System.Diagnostics.Debug.Assert(0 <= startBytes);
            System.Diagnostics.Debug.Assert(0 <= endBytes);
            System.Diagnostics.Debug.Assert(startBytes <= endBytes);

            NumFrames = newNumSamples;
            if (newNumSamples == 0 ||
                m_rawData.Length <= startBytes) {
                m_rawData = null;
                NumFrames = 0;
            } else {
                byte[] newArray = new byte[endBytes - startBytes];
                Array.Copy(m_rawData, startBytes, newArray, 0, endBytes - startBytes);
                m_rawData = null;
                m_rawData = newArray;
            }
        }

        /// <summary>
        /// dataチャンクのヘッダ部分だけを読む。
        /// 4バイトしか進まない。
        /// </summary>
        public long ReadHeader(BinaryReader br, long offset, byte[] fourcc, int numChannels, int bitsPerSample) {
            m_chunkId = fourcc;
            if (!PcmDataLib.Util.FourCCHeaderIs(m_chunkId, 0, "data")) {
                System.Diagnostics.Debug.Assert(false);
                return 0;
            }

            ChunkSize = br.ReadUInt32();

            Offset = offset + 4;

            int frameBytes = bitsPerSample / 8 * numChannels;
            NumFrames = ChunkSize / frameBytes;

            m_rawData = null;
            return 4;
        }

        /// <summary>
        /// PCMデータを無加工で読み出す。
        /// </summary>
        /// <param name="startFrame">0を指定すると最初から。</param>
        /// <param name="endFrame">負の値を指定するとファイルの最後まで。</param>
        /// <returns>false: ファイルの読み込みエラーなど</returns>
        public bool ReadRaw(BinaryReader br, int numChannels, int bitsPerSample,
            long startFrame, long endFrame) {
            br.BaseStream.Seek(Offset, SeekOrigin.Begin);

            // endBytesがファイルの終わり指定(負の値)の場合の具体的位置を設定する。
            // startBytesとendBytesがファイルの終わり以降を指していたら修正する。
            // ・endBytesがファイルの終わり以降…ファイルの終わりを指す。
            // ・startBytesがファイルの終わり以降…サイズ0バイトのWAVファイルにする。

            int frameBytes = bitsPerSample / 8 * numChannels;
            long startBytes = startFrame * frameBytes;
            long endBytes   = endFrame   * frameBytes;

            System.Diagnostics.Debug.Assert(0 <= startBytes);

            if (endBytes < 0 ||
                (NumFrames * frameBytes) < endBytes) {
                // 終了位置はファイルの終わり。
                    endBytes = NumFrames * frameBytes;
            }

            long newNumFrames = (endBytes - startBytes) / frameBytes;
            if (newNumFrames <= 0 ||
                NumFrames * frameBytes <= startBytes ||
                endBytes <= startBytes) {
                // サイズが0バイトのWAV。
                m_rawData = null;
                NumFrames = 0;
                return true;
            }

            if (0 < startBytes) {
                PcmDataLib.Util.BinaryReaderSkip(br, startBytes);
            }

            m_rawData = br.ReadBytes((int)newNumFrames * frameBytes);
            NumFrames = newNumFrames;
            return true;
        }

        public void Write(BinaryWriter bw) {
            bw.Write(m_chunkId);
            bw.Write(ChunkSize);
            bw.Write(m_rawData);
        }
    }

    public class WavData
    {
        private RiffChunkDescriptor m_rcd;
        private FmtSubChunk         m_fsc;
        private List<DataSubChunk>  m_dscList = new List<DataSubChunk>();
        private DS64Chunk           m_ds64;

        public PcmDataLib.PcmData.ValueRepresentationType SampleValueRepresentationType {
            get { return m_fsc.SampleValueRepresentationType; }
            set { m_fsc.SampleValueRepresentationType = value; }
        }

        private enum ReadMode {
            HeaderAndPcmData,
            OnlyHeader
        }

        /// <summary>
        /// StartTickとEndTickを見て、DSCヘッダ以降の必要な部分だけ読み込む。
        /// </summary>
        private bool ReadPcmDataInternal(BinaryReader br, long startFrame, long endFrame) {
            if (startFrame < 0) {
                // データ壊れ。先頭を読む。
                startFrame = 0;
            }

            if (0 <= endFrame && endFrame < startFrame) {
                // 1サンプルもない。
                startFrame = endFrame;
            }

            if (m_dscList.Count != 1) {
                // この読み込み方法は、複数個のデータチャンクが存在する場合には対応しない。
                Console.WriteLine("multi data chunk wav is not supported");
                return false;
            }

            return m_dscList[0].ReadRaw(br, m_fsc.NumChannels, m_fsc.BitsPerSample, startFrame, endFrame);
        }

        private long SkipUnknownChunk(BinaryReader br, byte[] fourcc) {
            // Console.WriteLine("D: SkipUnknownChunk skip \"{0}{1}{2}{3}\"", (char)fourcc[0], (char)fourcc[1], (char)fourcc[2], (char)fourcc[3]);

            long chunkSize = br.ReadUInt32();
            if (chunkSize == 0) {
                Console.WriteLine("E: SkipUnknownChunk chunk \"{0}{1}{2}{3}\" corrupted. chunkSize={4}",
                    (char)fourcc[0], (char)fourcc[1], (char)fourcc[2], (char)fourcc[3], chunkSize);
                return 0;
            }
            // PADの処理。chunkSizeが奇数の場合、1バイト読み進める。
            long skipBytes = (chunkSize + 1) & (~1L);
            PcmDataLib.Util.BinaryReaderSkip(br, skipBytes);
            return skipBytes + 4;
        }

        private long ReadListChunk(BinaryReader br) {
            uint listHeaderBytes = br.ReadUInt32();
            if (listHeaderBytes < 4) {
                Console.WriteLine("LIST header size is too short {0}", listHeaderBytes);
                return 0;
            }
            if (65535 < listHeaderBytes) {
                Console.WriteLine("LIST header size is too large {0}", listHeaderBytes);
                return 0;
            }

            byte[] data = br.ReadBytes((int)listHeaderBytes);
            long result = 4 + listHeaderBytes;
            if (1 == (listHeaderBytes & 1)) {
                // PADの処理。listHeaderBytesが奇数の場合、1バイト読み進める
                br.ReadBytes(1);
                ++result;
            }

            if (!PcmDataLib.Util.FourCCHeaderIs(data, 0, "INFO")) {
                Console.WriteLine("LIST header does not follows INFO");
                return result;
            }

            int pos = 4;
            while (pos+8 < data.Length) {
                if (data[pos+6] != 0 || data[pos+7] != 0) {
                    Console.WriteLine("LIST header contains very long text. parse aborted");
                    return 0;
                }
                int bytes = data[pos+4] + 256 * data[pos+5];
                if (0 < bytes) {
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "INAM")) {
                        Title = JapaneseTextByteArrayToString(data, pos + 8, bytes);
                    }
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "IART")) {
                        ArtistName = JapaneseTextByteArrayToString(data, pos + 8, bytes);
                    }
                    if (PcmDataLib.Util.FourCCHeaderIs(data, pos, "IPRD")) {
                        AlbumName = JapaneseTextByteArrayToString(data, pos + 8, bytes);
                    }
                }
                pos += 8 + ((bytes+1)&(~1));
            }
            return result;
        }

        private string JapaneseTextByteArrayToString(byte[] bytes, int index, int count) {
            string result = "不明";

            // 最後の'\0'を削る
            while (0 < count && bytes[index + count - 1] == 0) {
                --count;
            }
            if (0 == count) {
                return result;
            }

            var part = new byte[count];
            Buffer.BlockCopy(bytes, index, part, 0, count);

            var encoding = JCodeInspect.DetectEncoding(part);
            if (encoding == System.Text.Encoding.GetEncoding(932)) {
                // SJIS
                result = System.Text.Encoding.GetEncoding(932).GetString(part);
            } else {
                // UTF-8
                result = System.Text.Encoding.UTF8.GetString(part);
            }
            
            return result;
        }

        private bool Read(BinaryReader br, ReadMode mode, long startFrame, long endFrame) {
            bool firstHeader = true;

            long offset = 0;
            try {
                do {
                    var fourcc = br.ReadBytes(4);
                    if (fourcc.Length < 4) {
                        // ファイルの終わりに達した。
                        break;
                    }
                    offset += 4;

                    long advance = 0;

                    if (firstHeader) {
                        if (!PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "RIFF") &&
                            !PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "RF64")) {
                            // ファイルの先頭がRF64でもRIFFもない。WAVではない。
                            return false;
                        }

                        m_rcd = new RiffChunkDescriptor();
                        advance = m_rcd.Read(br, fourcc);

                        firstHeader = false;
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "fmt ")) {
                        m_fsc = new FmtSubChunk();
                        advance = m_fsc.Read(br, fourcc);
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "LIST")) {
                        advance = ReadListChunk(br);
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "ds64")) {
                        m_ds64 = new DS64Chunk();
                        advance = m_ds64.Read(br);
                    } else if (PcmDataLib.Util.FourCCHeaderIs(fourcc, 0, "data")) {
                        if (m_fsc == null) {
                            Console.WriteLine("Format subchunk missing.");
                            return false;
                        }
                        if (m_ds64 != null && 0 < m_dscList.Count) {
                            Console.WriteLine("multiple data chunk in RF64. not supported format");
                            return false;
                        }
                        
                        int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;

                        var dsc = new DataSubChunk();

                        advance = dsc.ReadHeader(br, offset, fourcc, m_fsc.NumChannels, m_fsc.BitsPerSample);

                        if (m_ds64 != null) {
                            // ds64チャンクが存在する場合(RF64形式)
                            // dsc.ChunkSizeは正しくないので、そこから算出するdsc.NumFramesも正しくない。
                            // dsc.NumFrameをds64の値で上書きする。
                            dsc.NumFrames = m_ds64.DataSize / frameBytes;
                        } else {
                            const long MASK = UInt32.MaxValue;
                            if (MASK < (br.BaseStream.Length -8) && m_rcd.ChunkSize != MASK) {
                                // RIFF chunkSizeが0xffffffffではないのにファイルサイズが4GB+8(8==RIFFチャンクのヘッダサイズ)以上ある。
                                // このファイルはdsc.ChunkSize情報の信憑性が薄い。
                                // dsc.ChunkSizeの上位ビットが桁あふれによって消失している可能性があるので、
                                // dsc.ChunkSizeの上位ビットをファイルサイズから類推して付加し、
                                // dsc.NumFrameを更新する。

                                long remainBytes = br.BaseStream.Length - (offset + advance);
                                long maskedRemainBytes =  remainBytes & 0xffffffffL;
                                if (maskedRemainBytes <= dsc.ChunkSize && m_rcd.ChunkSize - maskedRemainBytes < 4096) {
                                    long realChunkSize = dsc.ChunkSize;
                                    while (realChunkSize + 0x100000000L <= remainBytes) {
                                        realChunkSize += 0x100000000L;
                                    }
                                    dsc.NumFrames = realChunkSize / frameBytes;
                                }
                            }
                        }

                        // マルチデータチャンク形式の場合、data chunkの後にさらにdata chunkが続いたりするので、
                        // 読み込みを続行する。
                        long skipBytes = (dsc.NumFrames * frameBytes + 1) & (~1L);
                        PcmDataLib.Util.BinaryReaderSkip(br, skipBytes);

                        if (br.BaseStream.Length < (offset + advance) + skipBytes) {
                            // ファイルが途中で切れている。
                            dsc.NumFrames = (br.BaseStream.Length - (offset + advance)) / frameBytes;
                        }

                        if (0 < dsc.NumFrames) {
                            m_dscList.Add(dsc);
                        } else {
                            // ファイルがDSCヘッダ部分を最後に切れていて、サンプルデータが1フレーム分すらも無いとき。
                        }

                        advance += skipBytes;
                    } else {
                        advance = SkipUnknownChunk(br, fourcc);
                    }

                    if (0 == advance) {
                        return false;
                    }
                    offset += advance;
                } while (true);
            } catch (Exception ex) {
                Console.WriteLine("E: WavRWLib2.WavData.Read() {0}", ex);
            }

            if (mode == ReadMode.HeaderAndPcmData) {
                if (!ReadPcmDataInternal(br, startFrame, endFrame)) {
                    return false;
                }
                return true;
            }

            return m_rcd != null && m_fsc != null && m_dscList.Count != 0;
        }

        /// <summary>
        /// read only header part.
        /// NumChannels
        /// BitsPerSample
        /// SampleRate
        /// NumSamples
        /// </summary>
        public bool ReadHeader(BinaryReader br) {
            return Read(br, ReadMode.OnlyHeader, 0, -1);
        }

        public bool ReadHeaderAndSamples(BinaryReader br, long startFrame, long endFrame) {
            return Read(br, ReadMode.HeaderAndPcmData, startFrame, endFrame);
        }

        public void Write(BinaryWriter bw)
        {
            m_rcd.Write(bw);
            m_fsc.Write(bw);
            foreach (var dsc in m_dscList) {
                dsc.Write(bw);
            }
        }

        public int NumChannels
        {
            get { return m_fsc.NumChannels; }
        }

        public int BitsPerSample
        {
            get { return m_fsc.BitsPerSample; }
        }

        public int ValidBitsPerSample {
            get { return m_fsc.ValidBitsPerSample; }
        }

        public long NumFrames
        {
            get {
                long result = 0;
                foreach (var dsc in m_dscList) {
                    result += dsc.NumFrames;
                }
                return result;
            }
        }

        public int SampleRate
        {
            get { return (int)m_fsc.SampleRate; }
        }

        public byte[] GetSampleArray() {
            if (m_dscList.Count != 1) {
                Console.WriteLine("multi data chunk wav. not supported");
                return null;
            }
            return m_dscList[0].GetSampleArray();
        }

        public string Title { get; set; }
        public string ArtistName { get; set; }
        public string AlbumName { get; set; }

        public bool Set(
                int numChannels,
                int bitsPerSample,
                int validBitsPerSample,
                int sampleRate,
                PcmDataLib.PcmData.ValueRepresentationType sampleValueRepresentation,
                long numFrames,
                byte[] sampleArray) {
            m_rcd = new RiffChunkDescriptor();

            if (0xffffffffL < sampleArray.LongLength + 36) {
                System.Diagnostics.Debug.Assert(false);
                return false;
            }
            m_rcd.Create((uint)(36 + sampleArray.LongLength));

            m_fsc = new FmtSubChunk();
            m_fsc.Create(numChannels, sampleRate, bitsPerSample, validBitsPerSample, sampleValueRepresentation);

            var dsc = new DataSubChunk();
            dsc.Create(numFrames, sampleArray);
            m_dscList.Clear();
            m_dscList.Add(dsc);
            return true;
        }

        int m_currentDsc = -1;
        long m_dscPosFrame = 0;

        public bool ReadStreamBegin(BinaryReader br, out PcmDataLib.PcmData pcmData) {
            if (!ReadHeader(br)) {
                pcmData = new PcmDataLib.PcmData();
                return false;
            }

            pcmData = new PcmDataLib.PcmData();
            pcmData.SetFormat(m_fsc.NumChannels, m_fsc.BitsPerSample,
                m_fsc.BitsPerSample, (int)m_fsc.SampleRate,
                m_fsc.SampleValueRepresentationType, NumFrames);

            m_currentDsc = -1;
            m_dscPosFrame = 0;

            // 最初のDSCまでシークする。
            return ReadStreamSkip(br, 0);
        }


        public bool ReadStreamSkip(BinaryReader br, long skipFrames) {
            int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;

            for (int i=0; i<m_dscList.Count; ++i) {
                var dsc = m_dscList[i];

                if (skipFrames < dsc.NumFrames) {
                    // 開始フレームはこのdscにある。
                    m_currentDsc = i;
                    m_dscPosFrame = skipFrames;
                    PcmDataLib.Util.BinaryReaderSeekFromBegin(br, dsc.Offset + frameBytes * skipFrames);
                    return true;
                } else {
                    skipFrames -= dsc.NumFrames;
                }
            }

            // 開始フレームが見つからない
            return false;
        }

        /// <summary>
        /// 読めるデータ量は少ないことがある
        /// </summary>
        public byte[] ReadStreamReadOne(BinaryReader br, long preferredFrames) {
            if (m_currentDsc < 0 || m_dscList.Count <= m_currentDsc) {
                return null;
            }

            var dsc = m_dscList[m_currentDsc];

            // 現dscの残りデータ量
            long readFrames = dsc.NumFrames - m_dscPosFrame;
            if (preferredFrames < readFrames) {
                // 多すぎるので、減らす。
                readFrames = preferredFrames;
            }
            if (readFrames == 0) {
                return null;
            }

            int frameBytes = m_fsc.BitsPerSample / 8 * m_fsc.NumChannels;
            var result = br.ReadBytes((int)(readFrames * frameBytes));

            m_dscPosFrame += readFrames;

            if (dsc.NumFrames <= m_dscPosFrame && (m_currentDsc + 1) < m_dscList.Count) {
                // 次のdscに移動する
                // 8 == data chunk id + data chunk sizeのバイト数
                PcmDataLib.Util.BinaryReaderSkip(br, 8);
                ++m_currentDsc;
                m_dscPosFrame = 0;
            }
            return result;
        }

        public void ReadStreamEnd() {
            m_currentDsc = -1;
        }
    }
}
