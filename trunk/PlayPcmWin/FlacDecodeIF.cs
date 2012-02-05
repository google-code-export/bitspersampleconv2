﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;

namespace PlayPcmWin {
    class FlacDecodeIF {
        public struct FlacCuesheetTrackIndexInfo {
            public int indexNr;
            public long offsetSamples;
        }

        public class FlacCuesheetTrackInfo {
            public int trackNr;
            public long offsetSamples;
            public List<FlacCuesheetTrackIndexInfo> indices = new List<FlacCuesheetTrackIndexInfo>();
        };

        private Process m_childProcess;
        private BinaryReader m_br;
        private AnonymousPipeServerStream m_pss;
        private int m_bytesPerFrame;
        private long m_numFrames;
        private int m_pictureBytes;
        private byte[] m_pictureData;
        private int m_numFramesPerBlock;

        public static string ErrorCodeToStr(int ercd) {
            switch (ercd) {
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Success:
                return "Success";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Completed:
                return "Completed";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.DataNotReady:
                return "Data not ready (internal error)";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.WriteOpenFailed:
                return "Could not open specified file";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecoderNewFailed:
                return "FlacStreamDecoder create failed";

            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecoderInitFailed:
                return "FlacStreamDecoder init failed";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FlacStreamDecorderProcessFailed:
                return "FlacStreamDecoder returns fail";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.LostSync:
                return "Lost sync while decoding (file corrupted)";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.BadHeader:
                return "Bad header";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.FrameCrcMismatch:
                return "CRC mismatch. (File corrupted)";

            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.Unparseable:
                return "Unparsable data";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.NumFrameIsNotAligned:
                return "NumFrame is not aligned (internal error)";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.RecvBufferSizeInsufficient:
                return "Recv bufer size is insufficient (internal error)";
            case (int)FlacDecodeCS.FlacDecode.DecodeResultType.OtherError:
            default:
                return "Other error";
            }
        }

        private void SendString(string s) {
            m_childProcess.StandardInput.WriteLine(s);
        }

        private void SendBase64(string s) {
            byte[] b = new byte[s.Length * 2];

            for (int i = 0; i < s.Length; ++i) {
                char c = s[i];
                b[i * 2 + 0] = (byte)((c >> 0) & 0xff);
                b[i * 2 + 1] = (byte)((c >> 8) & 0xff);
            }
            string sSend = Convert.ToBase64String(b);
            m_childProcess.StandardInput.WriteLine(sSend);
        }

        private void StartChildProcess() {
            System.Diagnostics.Debug.Assert(null == m_childProcess);

            m_childProcess = new Process();
            m_childProcess.StartInfo.FileName = "FlacDecodeCS.exe";

            m_pss = new AnonymousPipeServerStream(
                PipeDirection.In, HandleInheritability.Inheritable);

            m_childProcess.StartInfo.Arguments = m_pss.GetClientHandleAsString();
            m_childProcess.StartInfo.UseShellExecute = false;
            m_childProcess.StartInfo.CreateNoWindow = true;
            m_childProcess.StartInfo.RedirectStandardInput = true;
            m_childProcess.StartInfo.RedirectStandardOutput = false;
            m_childProcess.Start();

            m_pss.DisposeLocalCopyOfClientHandle();
            m_br = new BinaryReader(m_pss);
        }

        private int StopChildProcess() {
            System.Diagnostics.Debug.Assert(null != m_childProcess);
            m_childProcess.WaitForExit();
            int exitCode = m_childProcess.ExitCode;
            m_childProcess.Close();
            m_childProcess = null;

            m_pss.Close();
            m_pss = null;

            m_br.Close();
            m_br = null;

            return exitCode;
        }

        enum ReadMode {
            Header,
            HeadereAndData,
        };

        private int ReadStartCommon(ReadMode mode, string flacFilePath, long skipFrames, long wantFrames, out PcmDataLib.PcmData pcmData_return, out List<FlacCuesheetTrackInfo> cueSheet_return) {
            pcmData_return = new PcmDataLib.PcmData();
            cueSheet_return = new List<FlacCuesheetTrackInfo>();
            
            StartChildProcess();

            switch (mode) {
            case ReadMode.Header:
                SendString("H");
                SendBase64(flacFilePath);
                break;
            case ReadMode.HeadereAndData:
                SendString("A");
                SendBase64(flacFilePath);
                SendString(skipFrames.ToString());
                SendString(wantFrames.ToString());
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            int rv = m_br.ReadInt32();
            if (rv != 0) {
                return rv;
            }

            int nChannels     = m_br.ReadInt32();
            int bitsPerSample = m_br.ReadInt32();
            int sampleRate    = m_br.ReadInt32();

            m_numFrames         = m_br.ReadInt64();
            m_numFramesPerBlock = m_br.ReadInt32();

            string titleStr = m_br.ReadString();
            string albumStr = m_br.ReadString();
            string artistStr = m_br.ReadString();

            m_pictureBytes = m_br.ReadInt32();
            m_pictureData = new byte[0];
            if (0 < m_pictureBytes) {
                m_pictureData = m_br.ReadBytes(m_pictureBytes);
            }

            {
                int numCuesheetTracks = m_br.ReadInt32();
                for (int trackId=0; trackId < numCuesheetTracks; ++trackId) {
                    var cti = new FlacCuesheetTrackInfo();
                    cti.trackNr = m_br.ReadInt32();
                    cti.offsetSamples = m_br.ReadInt64();

                    int numCuesheetTrackIndices = m_br.ReadInt32();
                    for (int indexId=0; indexId < numCuesheetTrackIndices; ++indexId) {
                        var indexInfo = new FlacCuesheetTrackIndexInfo();
                        indexInfo.indexNr = m_br.ReadInt32();
                        indexInfo.offsetSamples = m_br.ReadInt64();
                        cti.indices.Add(indexInfo);
                    }
                    cueSheet_return.Add(cti);
                }
            }

            pcmData_return.SetFormat(
                nChannels,
                bitsPerSample,
                bitsPerSample,
                sampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                m_numFrames);

            pcmData_return.DisplayName = titleStr;
            pcmData_return.AlbumTitle = albumStr;
            pcmData_return.ArtistName = artistStr;

            pcmData_return.SetPicture(m_pictureBytes, m_pictureData);
            return 0;
        }

        public int ReadHeader(string flacFilePath, out PcmDataLib.PcmData pcmData_return, out List<FlacCuesheetTrackInfo> cueSheet_return) {
            int rv = ReadStartCommon(ReadMode.Header, flacFilePath, 0, 0, out pcmData_return, out cueSheet_return);
            StopChildProcess();
            if (rv != 0) {
                return rv;
            }

            return 0;
        }

        /// <summary>
        /// FLACファイルからPCMデータを取り出し開始。
        /// </summary>
        /// <param name="flacFilePath">読み込むファイルパス。</param>
        /// <param name="skipFrames">ファイルの先頭からのスキップするフレーム数。0以外の値を指定するとMD5のチェックが行われなくなる。</param>
        /// <param name="wantFrames">取得するフレーム数。</param>
        /// <param name="pcmData">出てきたデコード後のPCMデータ。</param>
        /// <returns></returns>
        public int ReadStreamBegin(string flacFilePath, long skipFrames, long wantFrames, out PcmDataLib.PcmData pcmData_return) {
            List<FlacCuesheetTrackInfo> cti;
            int rv = ReadStartCommon(ReadMode.HeadereAndData, flacFilePath, skipFrames, wantFrames, out pcmData_return, out cti);
            if (rv != 0) {
                StopChildProcess();
                m_bytesPerFrame = 0;
                return rv;
            }

            m_bytesPerFrame = pcmData_return.BitsPerFrame / 8;
            return 0;
        }

        /// <summary>
        /// PCMサンプルを読み出す。
        /// </summary>
        /// <returns>読んだサンプルデータ。サイズはpreferredFramesよりも少ない場合がある。(preferredFramesよりも多くはない。)</returns>
        public byte [] ReadStreamReadOne(long preferredFrames)
        {
            System.Diagnostics.Debug.Assert(0 < m_bytesPerFrame);

            int frameCount = m_br.ReadInt32();
            // System.Console.WriteLine("ReadStreamReadOne() frameCount={0}", frameCount);

            if (frameCount == 0) {
                return new byte[0];
            }

            byte [] sampleArray = m_br.ReadBytes(frameCount * m_bytesPerFrame);

            if (preferredFrames < frameCount) {
                // 欲しいフレーム数よりも多くのサンプルデータが出てきた。CUEシートの場合などで起こる。
                // データの後ろをtruncateする。
                Array.Resize(ref sampleArray, (int)preferredFrames * m_bytesPerFrame);
                frameCount = (int)preferredFrames;
            }

            return sampleArray;
        }

        public int BytesPerFrame {
            get { return m_bytesPerFrame; }
        }

        public long NumFrames {
            get { return m_numFrames; }
        }

        public int PictureBytes {
            get { return m_pictureBytes; }
        }

        public byte[] GetPictureData() {
            return m_pictureData;
        }

        public int ReadStreamEnd()
        {
            int exitCode = StopChildProcess();

            m_bytesPerFrame = 0;

            return exitCode;
        }

        public void ReadStreamAbort() {
            System.Diagnostics.Debug.Assert(null != m_childProcess);
            m_pss.Close();
            m_pss = null;

            m_childProcess.Close();
            m_childProcess = null;

            m_br.Close();
            m_br = null;
        }

    }
}
