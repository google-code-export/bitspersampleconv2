using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;

namespace BpsConvWin
{
    struct RiffChunkDescriptor
    {
        public byte[] chunkId;
        public uint   chunkSize;
        public byte[] format;

        public bool Read(BinaryReader br)
        {
            chunkId = br.ReadBytes(4);
            if (chunkId[0] != 'R' || chunkId[1] != 'I' || chunkId[2] != 'F' || chunkId[3] != 'F') {
                Console.WriteLine("E: RiffChunkDescriptor.chunkId mismatch. \"{0}{1}{2}{3}\" should be \"RIFF\"",
                    (char)chunkId[0], (char)chunkId[1], (char)chunkId[2], (char)chunkId[3]);
                return false;
            }

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

    struct FmtSubChunk
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
            subChunk1Id = br.ReadBytes(4);
            if (subChunk1Id[0] != 'f' || subChunk1Id[1] != 'm' || subChunk1Id[2] != 't' || subChunk1Id[3] != ' ') {
                Console.WriteLine("E: FmtSubChunk.subChunk1Id mismatch. \"{0}{1}{2}{3}\" should be \"fmt \"",
                    (char)subChunk1Id[0], (char)subChunk1Id[1], (char)subChunk1Id[2], (char)subChunk1Id[3]);
                return false;
            }

            subChunk1Size = br.ReadUInt32();
            if (16 != subChunk1Size && 18 != subChunk1Size && 40 != subChunk1Size) {
                Console.WriteLine("E: FmtSubChunk.subChunk1Size != 16 {0} this file type is not supported", subChunk1Size);
                return false;
            }

            audioFormat = br.ReadUInt16();
            if (1 != audioFormat) {
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
            if (16 != bitsPerSample) {
                Console.WriteLine("E: bitsPerSample={0} this program only accepts 16bps PCM WAV files so far.", bitsPerSample);
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

    struct DataSubChunk
    {
        public byte[] subChunk2Id;
        public uint   subChunk2Size;
        public byte[] data;

        public bool Read(BinaryReader br)
        {
            subChunk2Id = br.ReadBytes(4);
            if (subChunk2Id[0] != 'd' || subChunk2Id[1] != 'a' || subChunk2Id[2] != 't' || subChunk2Id[3] != 'a') {
                Console.WriteLine("E: DataSubChunk.subChunk2Id mismatch. \"{0}{1}{2}{3}\" should be \"data\"",
                    (char)subChunk2Id[0], (char)subChunk2Id[1], (char)subChunk2Id[2], (char)subChunk2Id[3]);
                return false;
            }

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
        public struct ConvertParams {
            public int newQuantizationBitrate;
            public bool addDither;
            public bool noiseShaping;
        };

        RiffChunkDescriptor rcd;
        FmtSubChunk fsc;
        DataSubChunk dsc;

        public bool Convert(BinaryReader br, BinaryWriter bw,
                ConvertParams args) {
            rcd = new RiffChunkDescriptor();
            if (!rcd.Read(br)) {
                return false;
            }

            fsc = new FmtSubChunk();
            if (!fsc.Read(br)) {
                return false;
            }

            dsc = new DataSubChunk();
            if (!dsc.Read(br)) {
                return false;
            }

            if (!ReduceBitsPerSample(args)) {
                return false;
            }

            rcd.Write(bw);
            fsc.Write(bw);
            dsc.Write(bw);

            return true;
        }

        public bool ReduceBitsPerSample(ConvertParams args) {
            uint mask = 0xffffffff << (16 - args.newQuantizationBitrate);
            uint maskError = ~mask;
            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
            byte[] randomNumber = new byte[2];

            Console.WriteLine("D: maskErr={0:X}", maskError);

            int bytesPerFrame = fsc.numChannels * fsc.bitsPerSample / 8;
            int numFrames = dsc.data.Length / bytesPerFrame;

            var errorAcc = new uint[fsc.numChannels];
            
            int pos = 0;
            for (int i=0; i < numFrames; ++i) {
                for (int ch=0; ch < fsc.numChannels; ++ch) {
                    int sample = dsc.GetSampleValue16(pos);

                    uint error = (uint)sample & maskError;
                    errorAcc[ch] += error;

                    sample = (int)(sample - error);

                    if (args.noiseShaping) {
                        if (maskError <= errorAcc[ch]) {
                            errorAcc[ch] -= maskError;
                            sample += (int)maskError;
                        }
                    }

                    if (args.addDither) {
                        gen.GetBytes(randomNumber);
                        ushort randDither = (ushort)((ushort)((randomNumber[0] << 8) + randomNumber[1]) & (ushort)(~mask));
                        sample += randDither;
                    }

                    if (0x7fff < sample) {
                        sample = 0x7fff;
                    }
                    if (sample < -0x8000) {
                        sample = -0x8000;
                    }

                    dsc.SetSampleValue16(pos, (short)sample);

                    pos += fsc.bitsPerSample / 8;
                }
            }

            return true;
        }

    }
}
