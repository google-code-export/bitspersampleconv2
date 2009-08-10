﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

namespace BpsConvWin2
{
    struct RiffChunkDescriptor
    {
        public byte[] chunkId;
        public uint   chunkSize;
        public byte[] format;

        public void Create(uint chunkSize)
        {
            chunkId = new byte[4];
            chunkId[0] = (byte)'R';
            chunkId[1] = (byte)'I';
            chunkId[2] = (byte)'F';
            chunkId[3] = (byte)'F';

            Debug.Assert(36 <= chunkSize);
            this.chunkSize = chunkSize;

            format = new byte[4];
            format[0] = (byte)'W';
            format[1] = (byte)'A';
            format[2] = (byte)'V';
            format[3] = (byte)'E';
        }

        public bool Read(BinaryReader br)
        {
            chunkId = br.ReadBytes(4);
            if (chunkId[0] != 'R' ||
                chunkId[1] != 'I' ||
                chunkId[2] != 'F' ||
                chunkId[3] != 'F') {
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
            if (format[0] != 'W' ||
                format[1] != 'A' ||
                format[2] != 'V' ||
                format[3] != 'E') {
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

    struct RiffFmtSubChunk
    {
        public byte[] subChunk1Id;
        public uint   subChunk1Size;
        public ushort audioFormat;
        public ushort numChannels;
        public uint   sampleRate;

        public uint   byteRate;
        public ushort blockAlign;
        public ushort bitsPerSample;

        public void Create(int numChannels, int sampleRate, int bitsPerSample)
        {
            subChunk1Id = new byte[4];
            subChunk1Id[0] = (byte)'f';
            subChunk1Id[1] = (byte)'m';
            subChunk1Id[2] = (byte)'t';
            subChunk1Id[3] = (byte)' ';

            subChunk1Size      = 16;
            audioFormat        = 1;
            this.numChannels   = (ushort)numChannels;
            this.sampleRate    = (uint)sampleRate;

            byteRate           = (uint)(sampleRate * numChannels * bitsPerSample / 8);
            blockAlign         = (ushort)(numChannels * bitsPerSample / 8);
            this.bitsPerSample = (ushort)bitsPerSample;
        }

        public bool Read(BinaryReader br)
        {
            subChunk1Id = br.ReadBytes(4);
            if (subChunk1Id[0] != 'f' ||
                subChunk1Id[1] != 'm' ||
                subChunk1Id[2] != 't' ||
                subChunk1Id[3] != ' ') {
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

    struct RiffDataSubChunk
    {
        public byte[] subChunk2Id;
        public uint   subChunk2Size;

        public void Create(uint subChunk2Size)
        {
            subChunk2Id = new byte[4];
            subChunk2Id[0] = (byte)'d';
            subChunk2Id[1] = (byte)'a';
            subChunk2Id[2] = (byte)'t';
            subChunk2Id[3] = (byte)'a';

            this.subChunk2Size = subChunk2Size;
        }

        public bool ReadHeader(BinaryReader br)
        {
            subChunk2Id = br.ReadBytes(4);
            if (subChunk2Id[0] != 'd' ||
                subChunk2Id[1] != 'a' ||
                subChunk2Id[2] != 't' ||
                subChunk2Id[3] != 'a') {
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

            return true;
        }

        public void WriteHeader(BinaryWriter bw)
        {
            bw.Write(subChunk2Id);
            bw.Write(subChunk2Size);
        }
    }

    class WavRW
    {
        public RiffChunkDescriptor chunkDescriptor;
        public RiffFmtSubChunk     fmtSubChunk;
        public RiffDataSubChunk    dataSubChunk;

        public bool ReadHeader(BinaryReader br)
        {
            if (!chunkDescriptor.Read(br)) {
                return false;
            }
            if (!fmtSubChunk.Read(br)) {
                return false;
            }
            if (!dataSubChunk.ReadHeader(br)) {
                return false;
            }

            return true;
        }

        public SampledData ReadAll(BinaryReader br)
        {
            if (!ReadHeader(br)) {
                return null;
            }
            HashSet<short> variation = new HashSet<short>();

            SampledData sd = new SampledData(fmtSubChunk.sampleRate, fmtSubChunk.numChannels);
            byte[] data = br.ReadBytes((int)dataSubChunk.subChunk2Size);
            switch (fmtSubChunk.bitsPerSample) {
            case 16:
                for (int i=0; i < data.Length / 2 / fmtSubChunk.numChannels; ++i) {
                    for (int ch=0; ch < fmtSubChunk.numChannels; ++ch) {
                        short v = (short)(data[i * 2] + ((ushort)(data[i * 2 + 1]) << 8));
                        variation.Add(v);

                        sd.Channel(ch).SampleAdd((double)v * (1.0f / 32768.0f));
                    }
                }
                sd.ValueVariation = variation.Count;
                break;
            default:
                Console.WriteLine("E: unknown bitsPerSample {0}", fmtSubChunk.bitsPerSample);
                return null;
            }
            return sd;
        }

    }
}
