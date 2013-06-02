using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;

namespace BpsConvWin
{
    class RiffChunkDescriptor
    {
        public byte[] chunkId;
        public uint   chunkSize;
        public byte[] format;

        public bool Read(BinaryReader br)
        {
            chunkId = new byte[4];
            chunkId[0] = (byte)'R';
            chunkId[1] = (byte)'I';
            chunkId[2] = (byte)'F';
            chunkId[3] = (byte)'F';

            chunkSize = br.ReadUInt32();
            if (chunkSize < 36) {
                Console.WriteLine("E: chunkSize is too small {0}", chunkSize);
                return false;
            }

            format = br.ReadBytes(4);
            if (format[0] != 'W' || format[1] != 'A' || format[2] != 'V' || format[3] != 'E')
            {
                Console.WriteLine("E: RiffChunkDescriptor.format mismatch. \"{0}{1}{2}{3}\" should be \"WAVE\"",
                    (char)format[0], (char)format[1], (char)format[2], (char)format[3]);
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(chunkId);
            bw.Write(chunkSize);
            bw.Write(format);
        }
    }

    class FmtSubChunk
    {
        public byte[] subChunk1Id;
        public uint   subChunk1Size;
        public ushort audioFormat;
        public ushort numChannels;
        public uint   sampleRate;

        public uint   byteRate;
        public ushort blockAlign;
        public ushort bitsPerSample;
        public ushort validBitsPerSample;
        public bool   isSampleFloat;

        public int BitsPerFrame {
            get {
                return bitsPerSample * numChannels;
            }
        }

        public bool Read(BinaryReader br)
        {
            subChunk1Id = new byte[4];
            subChunk1Id[0] = (byte)'f';
            subChunk1Id[1] = (byte)'m';
            subChunk1Id[2] = (byte)'t';
            subChunk1Id[3] = (byte)' ';

            subChunk1Size = br.ReadUInt32();
            if (16 != subChunk1Size && 18 != subChunk1Size && 40 != subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.subChunk1Size != 16 {0} this file type is not supported", subChunk1Size);
                return false;
            }

            audioFormat = br.ReadUInt16();
            if (1 != audioFormat && 65534 != audioFormat) {
                Console.WriteLine("E: this wave file is not PCM format {0}. Cannot read this file", audioFormat);
                return false;
            }

            numChannels = br.ReadUInt16();
            Console.WriteLine("D: numChannels={0}", numChannels);

            sampleRate = br.ReadUInt32();
            Console.WriteLine("D: sampleRate={0}", sampleRate);

            byteRate = br.ReadUInt32();
            Console.WriteLine("D: byteRate={0}", byteRate);

            blockAlign = br.ReadUInt16();
            Console.WriteLine("D: blockAlign={0}", blockAlign);

            bitsPerSample = br.ReadUInt16();
            Console.WriteLine("D: bitsPerSample={0}", bitsPerSample);
            if (16 != bitsPerSample && 24 != bitsPerSample) {
                Console.WriteLine("E: bitsPerSample={0} this program only accepts 16bit or 24bit PCM WAV files so far.", bitsPerSample);
                return false;
            }

            ushort extensibleSize = 0;
            if (16 < subChunk1Size) {
                // cbSize 2bytes
                extensibleSize = br.ReadUInt16();
            }
            if (0 != extensibleSize && 22 != extensibleSize) {
                Console.WriteLine("E: FmtSubChunk.Read() cbSize != 0 nor 22 {0}", extensibleSize);
                return false;
            }

            if (22 == extensibleSize) {
                // WAVEFORMATEX(22 bytes)

                validBitsPerSample = br.ReadUInt16();
                uint channelMask = br.ReadUInt32();
                var formatGuid = br.ReadBytes(16);

                var pcmGuid   = Guid.Parse("00000001-0000-0010-8000-00aa00389b71");
                var pcmGuidByteArray = pcmGuid.ToByteArray();
                var floatGuid = Guid.Parse("00000003-0000-0010-8000-00aa00389b71");
                var floatGuidByteArray = floatGuid.ToByteArray();
                if (pcmGuidByteArray.SequenceEqual(formatGuid)) {
                    isSampleFloat = false;
                } else if (floatGuidByteArray.SequenceEqual(formatGuid)) {
                    isSampleFloat = true;
                } else {
                    Console.WriteLine("E: FmtSubChunk.Read() unknown format guid on WAVEFORMATEX.SubFormat.");
                    Console.WriteLine("{3:X2}{2:X2}{1:X2}{0:X2}-{5:X2}{4:X2}-{7:X2}{6:X2}-{9:X2}{8:X2}-{10:X2}{11:X2}{12:X2}{13:X2}{14:X2}{15:X2}",
                        formatGuid[0], formatGuid[1], formatGuid[2], formatGuid[3],
                        formatGuid[4], formatGuid[5], formatGuid[6], formatGuid[7],
                        formatGuid[8], formatGuid[9], formatGuid[10], formatGuid[11],
                        formatGuid[12], formatGuid[13], formatGuid[14], formatGuid[15]);
                    return false;
                }
            }

            if (byteRate != sampleRate * numChannels * bitsPerSample / 8) {
                Console.WriteLine("E: byteRate is wrong value. corrupted file?");
                return false;
            }

            if (blockAlign != numChannels * bitsPerSample / 8) {
                Console.WriteLine("E: blockAlign is wrong value. corrupted file?");
                return false;
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(subChunk1Id);
            bw.Write(16);
            bw.Write(audioFormat);
            bw.Write(numChannels);
            bw.Write(sampleRate);

            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
        }
    }

    class DataSubChunk
    {
        public byte[] subChunk2Id;
        public uint   subChunk2Size;
        public byte[] data;

        public void Setup(uint bytes, byte[] dataFrom) {
            subChunk2Id = new byte[4];
            subChunk2Id[0] = (byte)'d';
            subChunk2Id[1] = (byte)'a';
            subChunk2Id[2] = (byte)'t';
            subChunk2Id[3] = (byte)'a';
            subChunk2Size = bytes;
            data = new byte[bytes];
            dataFrom.CopyTo(data, 0);
        }

        public bool Read(BinaryReader br)
        {
            subChunk2Id = new byte[4];
            subChunk2Id[0] = (byte)'d';
            subChunk2Id[1] = (byte)'a';
            subChunk2Id[2] = (byte)'t';
            subChunk2Id[3] = (byte)'a';

            subChunk2Size = br.ReadUInt32();
            Console.WriteLine("D: subChunk2Size={0}", subChunk2Size);
            if (0x80000000 <= subChunk2Size) {
                Console.WriteLine("E: file too large to handle. {0} bytes", subChunk2Size);
                return false;
            }

            data = br.ReadBytes((int)subChunk2Size);
            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(subChunk2Id);
            bw.Write(subChunk2Size);
            bw.Write(data);
        }

        public short GetSampleValue16(int pos) {
            return (short)(data[pos] + (data[pos + 1] << 8));
        }

        public void SetSampleValue16(int pos, short v) {
            data[pos] = (byte)(v & 0xff);
            data[pos+1] = (byte)((v>>8) & 0xff);
        }

        public int GetSampleValue24(int pos) {
            int v = (data[pos]<<8) + (data[pos + 1] << 16) + (data[pos+2]<<24);
            return v >> 8;
        }

        public void SetSampleValue24(int pos, int v) {
            data[pos] = (byte)(v & 0xff);
            data[pos + 1] = (byte)((v >> 8) & 0xff);
            data[pos + 2] = (byte)((v >> 16) & 0xff);
        }

    }

    public sealed class BpsConv
    {
        public class ConvertParams {
            public int newQuantizationBitrate;

            public enum DitherType {
                Truncate,
                RpdfDither,
                GaussianDither,
                NoiseShaping,
                NoiseShaping2,
                NoiseShapingMash2,
            };

            public DitherType ditherType;

            public int order;
            public double [] filter;
        };

        RiffChunkDescriptor mRcd;
        FmtSubChunk mFsc;
        DataSubChunk mDsc;

        public int BitsPerSample {
            get { return mFsc.bitsPerSample; }
        }

        private void SkipUnknownChunk(BinaryReader br) {
            var bytes = br.ReadUInt32();
            bytes = (uint)((bytes +1) & ~1);
            for (int i=0; i<bytes; ++i) {
                br.ReadByte();
            }
        }

        public bool ReadFromFile(BinaryReader br) {
            mRcd = null;
            mFsc = null;
            mDsc = null;

            try {
                while (true) {
                    var subChunk1Id = br.ReadInt32();
                    switch (subChunk1Id) {
                    case ((int)('R')) + (((int)('I')) << 8) + (((int)('F')) << 16) + (((int)('F')) << 24):
                        mRcd = new RiffChunkDescriptor();
                        if (!mRcd.Read(br)) {
                            return false;
                        }
                        break;

                    case ((int)('f')) + (((int)('m')) << 8) + (((int)('t')) << 16) + (((int)(' ')) << 24):
                        mFsc = new FmtSubChunk();
                        if (!mFsc.Read(br)) {
                            return false;
                        }
                        break;
                    case ((int)('d')) + (((int)('a')) << 8) + (((int)('t')) << 16) + (((int)('a')) << 24):
                        mDsc = new DataSubChunk();
                        if (!mDsc.Read(br)) {
                            return false;
                        }
                        break;
                    default:
                        SkipUnknownChunk(br);
                        break;
                    }
                }
            } catch (EndOfStreamException ex) {
                // 正常終了ｗｗｗｗ
            }

            return mRcd != null && mFsc != null && mDsc != null;
        }

        public void Convert(ConvertParams args, BinaryWriter bw) {
            var toDsc = new DataSubChunk();
            toDsc.Setup(mDsc.subChunk2Size, mDsc.data);

            switch (mFsc.bitsPerSample) {
            case 16:
                switch (args.ditherType) {
                case ConvertParams.DitherType.NoiseShaping:
                    args.order = 1;
                    args.filter = new double[] {1, -1};
                    ReduceBitsPerSample16Ns2(args, toDsc);
                    break;
                case ConvertParams.DitherType.NoiseShaping2:
                    args.order = 2;
                    args.filter = new double[] {1, -2, 1};
                    ReduceBitsPerSample16Ns2(args, toDsc);
                    break;
                case ConvertParams.DitherType.NoiseShapingMash2:
                    // not implemented
                    throw new NotImplementedException();
                default:
                    args.order = 2;
                    args.filter = new double[] {1, -2, 1};
                    ReduceBitsPerSample16Other(args, toDsc);
                    break;
                }
                break;
            case 24:
                switch (args.ditherType) {
                case ConvertParams.DitherType.NoiseShaping:
                    args.order = 1;
                    args.filter = new double[] {1, -1};
                    ReduceBitsPerSample24Ns2(args, toDsc);
                    break;
                case ConvertParams.DitherType.NoiseShaping2:
                    args.order = 2;
                    args.filter = new double[] {1, -2, 1};
                    ReduceBitsPerSample24Ns2(args, toDsc);
                    break;
                case ConvertParams.DitherType.NoiseShapingMash2:
                    ReduceBitsPerSample24Mash2(args, toDsc);
                    break;
                default:
                    ReduceBitsPerSample24Other(args, toDsc);
                    break;
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            mRcd.Write(bw);
            mFsc.Write(bw);
            toDsc.Write(bw);
        }

        private void ReduceBitsPerSample16Ns2(ConvertParams args, DataSubChunk toDsc) {
            NoiseShaper2 [] ns = new NoiseShaper2[mFsc.numChannels];
            for (int ch=0; ch<mFsc.numChannels; ++ch) {
                ns[ch] = new NoiseShaper2(args.order, args.filter);
            }

            int bytesPerFrame = mFsc.numChannels * mFsc.bitsPerSample / 8;
            int numFrames = toDsc.data.Length / bytesPerFrame;

            int readPos = 0;
            int writePos = 0;
            for (int i=0; i < numFrames; ++i) {
                for (int ch=0; ch < mFsc.numChannels; ++ch) {
                    short sample = 0;

                    sample = ns[ch].Filter16(toDsc.GetSampleValue16(readPos), args.newQuantizationBitrate);
                    readPos += mFsc.bitsPerSample / 8;

                    toDsc.SetSampleValue16(writePos, sample);
                    writePos += mFsc.bitsPerSample / 8;
                }
            }
        }

        private void ReduceBitsPerSample24Ns2(ConvertParams args, DataSubChunk toDsc) {
            NoiseShaper2 [] ns = new NoiseShaper2[mFsc.numChannels];
            for (int ch=0; ch < mFsc.numChannels; ++ch) {
                ns[ch] = new NoiseShaper2(args.order, args.filter);
            }

            int bytesPerFrame = mFsc.numChannels * mFsc.bitsPerSample / 8;
            int numFrames = toDsc.data.Length / bytesPerFrame;

            int readPos = 0;
            int writePos = 0;
            for (int i=0; i < numFrames; ++i) {
                for (int ch=0; ch < mFsc.numChannels; ++ch) {
                    int sample = 0;

                    sample = ns[ch].Filter24(toDsc.GetSampleValue24(readPos), args.newQuantizationBitrate);
                    readPos += mFsc.bitsPerSample / 8;

                    toDsc.SetSampleValue24(writePos, sample);
                    writePos += mFsc.bitsPerSample / 8;
                }
            }
        }

        private void ReduceBitsPerSample24Mash2(ConvertParams args, DataSubChunk toDsc) {
            NoiseShaperMash [] mash = new NoiseShaperMash[mFsc.numChannels];
            for (int ch=0; ch < mFsc.numChannels; ++ch) {
                mash[ch] = new NoiseShaperMash(args.newQuantizationBitrate);
            }

            int bytesPerFrame = mFsc.numChannels * mFsc.bitsPerSample / 8;
            int numFrames = toDsc.data.Length / bytesPerFrame;

            int readPos = 0;
            int writePos = 0;

            // 1サンプル遅延するので…。
            for (int i=0; i < numFrames + 1; ++i) {
                for (int ch=0; ch < mFsc.numChannels; ++ch) {
                    int sample = 0;

                    if (i < numFrames) {
                        sample = mash[ch].Filter24(toDsc.GetSampleValue24(readPos));
                        readPos += mFsc.bitsPerSample / 8;
                    } else {
                        sample = mash[ch].Filter24(0);
                    }

                    if (1 <= i) {
                        toDsc.SetSampleValue24(writePos, sample);
                        writePos += mFsc.bitsPerSample / 8;
                    }
                }
            }
        }

        private void ReduceBitsPerSample16Other(ConvertParams args, DataSubChunk toDsc) {
            uint mask = 0xffffffff << (16 - args.newQuantizationBitrate);
            uint maskError = ~mask;
            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
            byte[] randomNumber = new byte[2];

            int noiseMagnitude = (int)Math.Pow(2, (16 - args.newQuantizationBitrate))/2;

            GaussianNoiseGenerator gng = new GaussianNoiseGenerator();

            Console.WriteLine("D: maskErr={0:X}", maskError);

            int bytesPerFrame = mFsc.numChannels * mFsc.bitsPerSample / 8;
            int numFrames = toDsc.data.Length / bytesPerFrame;

            int pos = 0;
            for (int i=0; i < numFrames; ++i) {
                for (int ch=0; ch < mFsc.numChannels; ++ch) {
                    double sample = toDsc.GetSampleValue16(pos);

                    sample = ((int)sample & mask);

                    switch (args.ditherType) {
                    case ConvertParams.DitherType.Truncate:
                        break;
                    case ConvertParams.DitherType.RpdfDither:
                        gen.GetBytes(randomNumber);
                        ushort randDither = (ushort)((ushort)((randomNumber[0] << 8) + randomNumber[1]) & (ushort)(~mask));
                        sample += randDither;
                        break;
                    case ConvertParams.DitherType.GaussianDither:
                        float noise = gng.NextFloat();
                        noise *= noiseMagnitude;
                        sample += (int)noise;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                    }


                    if (0x7fff < sample) {
                        sample = 0x7fff;
                    }
                    if (sample < -0x8000) {
                        sample = -0x8000;
                    }

                    toDsc.SetSampleValue16(pos, (short)sample);

                    pos += mFsc.bitsPerSample / 8;
                }
            }
        }

        private void ReduceBitsPerSample24Other(ConvertParams args, DataSubChunk toDsc) {
            uint mask = 0xffffffff << (24 - args.newQuantizationBitrate);
            uint maskError = ~mask;
            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
            GaussianNoiseGenerator gng = new GaussianNoiseGenerator();
            byte[] randomNumber = new byte[3];
            int noiseMagnitude = (int)Math.Pow(2, (24 - args.newQuantizationBitrate))/2;

            Console.WriteLine("D: maskErr={0:X}", maskError);

            int bytesPerFrame = mFsc.numChannels * mFsc.bitsPerSample / 8;
            int numFrames = toDsc.data.Length / bytesPerFrame;

            int pos = 0;
            for (int i=0; i < numFrames; ++i) {
                for (int ch=0; ch < mFsc.numChannels; ++ch) {
                    double sample = toDsc.GetSampleValue24(pos);
                    uint error = (uint)sample & maskError;
                    sample -= error;

                    switch (args.ditherType) {
                    case ConvertParams.DitherType.Truncate:
                        break;
                    case ConvertParams.DitherType.RpdfDither:
                        gen.GetBytes(randomNumber);
                        int randDither = (int)((randomNumber[0]) + (randomNumber[1] << 8) + (randomNumber[2]<<16) & ~mask);
                        sample += randDither;
                        break;
                    case ConvertParams.DitherType.GaussianDither:
                        float noise = gng.NextFloat();
                        noise *= noiseMagnitude;
                        sample += (int)noise;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                    }

                    if (0x7fffff < sample) {
                        sample = 0x7fffff;
                    }
                    if (sample < -0x800000) {
                        sample = -0x800000;
                    }

                    toDsc.SetSampleValue24(pos, (int)sample);

                    pos += mFsc.bitsPerSample / 8;
                }
            }
        }
    }
}
