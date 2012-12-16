using System;
using System.IO;
using System.Security.Cryptography;

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

        public bool Read(BinaryReader br)
        {
            subChunk1Id = br.ReadBytes(4);
            if (subChunk1Id[0] != 'f' || subChunk1Id[1] != 'm' || subChunk1Id[2] != 't' || subChunk1Id[3] != ' ') {
                Console.WriteLine("E: FmtSubChunk.subChunk1Id mismatch. \"{0}{1}{2}{3}\" should be \"fmt \"",
                    (char)subChunk1Id[0], (char)subChunk1Id[1], (char)subChunk1Id[2], (char)subChunk1Id[3]);
                return false;
            }

            subChunk1Size = br.ReadUInt32();
            if (16 != subChunk1Size) {
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
            bw.Write(subChunk1Size);
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

        public bool ReduceBitsPerSample(int newBitsPerSample, bool addDither, bool noiseShaping)
        {
            uint mask = 0xffffffff << (16 - newBitsPerSample);
            uint maskError = ~mask;
            RNGCryptoServiceProvider gen = new RNGCryptoServiceProvider();
            byte[] randomNumber = new byte[2];

            Console.WriteLine("D: maskErr={0:X}", maskError);

            uint error = 0;
            uint errorAcc = 0;
            for (int i=0; i < data.Length / 2; ++i) {
                int sample = (short)(data[i * 2] + (data[i * 2 + 1] << 8));
                error = (uint)sample & maskError;
                sample = (int)(sample - error);
                errorAcc += error;

                if (addDither) {
                    gen.GetBytes(randomNumber);
                    ushort randDither = (ushort)((ushort)((randomNumber[0] << 8) + randomNumber[1]) & (ushort)(~mask));
                    sample += randDither;
                }

                if (noiseShaping) {
                    if (maskError <= errorAcc) {
                        errorAcc -= maskError;
                        sample += (int)maskError;
                    }
                }

                if (0x7fff < sample) {
                    sample = 0x7fff;
                }
                if (sample < -0x8000) {
                    sample = -0x8000;
                }

                data[i * 2]     = (byte)(0xff & sample);
                data[i * 2 + 1] = (byte)(0xff & (sample>>8));
            }

            return true;
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(subChunk2Id);
            bw.Write(subChunk2Size);
            bw.Write(data);
        }
    }

    public sealed class BpsConv
    {
        // prevent instanciation (FxCop CA1053)
        private BpsConv()
        {
        }

        public struct ConvertParams {
            public int newQuantizationBitrate;
            public bool addDither;
            public bool noiseShaping;
        };

        public static bool Convert(BinaryReader br, BinaryWriter bw,
                ConvertParams args) {
            RiffChunkDescriptor rcd = new RiffChunkDescriptor();
            if (!rcd.Read(br)) {
                return false;
            }

            FmtSubChunk fsc = new FmtSubChunk();
            if (!fsc.Read(br)) {
                return false;
            }

            DataSubChunk dsc = new DataSubChunk();
            if (!dsc.Read(br)) {
                return false;
            }

            if (!dsc.ReduceBitsPerSample(args.newQuantizationBitrate, args.addDither, args.noiseShaping)) {
                return false;
            }

            rcd.Write(bw);
            fsc.Write(bw);
            dsc.Write(bw);

            return true;
        }
    }
}
