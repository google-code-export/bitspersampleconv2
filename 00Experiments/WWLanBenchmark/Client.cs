using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace WWLanBenchmark {
    class Client {
        private const int ONE_GIGA = 1000 * 1000 * 1000;

        private List<byte[]> mSendData;
        private byte [] mSendDataHash;

        private BackgroundWorker mBackgroundWorker;

        public void Run(BackgroundWorker backgroundWorker, String server, int port, int continuousSendGiB, int testIterationCount) {
            mBackgroundWorker = backgroundWorker;

            try {
                using (var client = new TcpClient(server, port)) {
                    using (var stream = client.GetStream()) {
                        using (var bw = new BinaryWriter(stream)) {
                            mBackgroundWorker.ReportProgress(1, "Connected to Server.\nPreparing send data...");
                            SendSettings(bw, continuousSendGiB, testIterationCount);
                            PrepareSendData(continuousSendGiB);
                            mBackgroundWorker.ReportProgress(1, "done.\n");

                            for (int i = 0; i < testIterationCount; ++i) {
                                SendData(stream, bw, i, testIterationCount);
                            }
                            mBackgroundWorker.ReportProgress(100, "Done.\n");
                        }
                    }
                }
            } catch (ArgumentNullException e) {
                mBackgroundWorker.ReportProgress(0, string.Format("Error: ArgumentNullException: {0}\n", e));
            } catch (SocketException e) {
                mBackgroundWorker.ReportProgress(0, string.Format("Error: SocketException: {0}\n", e));
            }
        }

        private void SendSettings(BinaryWriter bw, int continuousSendGiB, int testIterationCount) {
            bw.Write(continuousSendGiB);
            bw.Write(testIterationCount);
        }

        private int StreamReadBytes(NetworkStream stream, out byte[] output, int bytes) {
            output = new byte[bytes];
            int readBytes = 0;
            do {
                readBytes += stream.Read(output, readBytes, bytes - readBytes);
            } while (readBytes < bytes);

            return readBytes;
        }

        private long StreamReadInt64(NetworkStream stream) {
            byte [] data;
            StreamReadBytes(stream, out data, 8);
            var ms = new MemoryStream(data);
            var br = new BinaryReader(ms);
            return br.ReadInt64();
        }

        private void PrepareSendData(int continuousSendGiB) {
            mSendData = new List<byte[]>();
            Parallel.For(0, continuousSendGiB, i => {
                using (var rng = new RNGCryptoServiceProvider()) {
                    var buff = new byte[ONE_GIGA];
                    rng.GetBytes(buff);
                    lock (mSendData) {
                        mSendData.Add(buff);
                    }
                }
            });

            using (var hash = new SHA256CryptoServiceProvider()) {
                foreach (var buff in mSendData) {
                    hash.TransformBlock(buff, 0, ONE_GIGA, buff, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                mSendDataHash = hash.Hash;
            }
        }

        private void SendData(NetworkStream stream, BinaryWriter bw, int idx, int total) {
            mBackgroundWorker.ReportProgress(10, string.Format("({0} / {1}) Sending {2}GB stream...\n",
                idx+1, total, mSendData.Count));

            bw.Write(idx);
            bw.Write(mSendDataHash);
            foreach (var buff in mSendData) {
                bw.Write(buff);
            }

            bw.Flush();

            mBackgroundWorker.ReportProgress(100, "    Waiting server response...\n");
            long elapsedMillisec = StreamReadInt64(stream);

            int sendGB = mSendData.Count;

            mBackgroundWorker.ReportProgress(1, string.Format("    Xmit {0}GB in {1} seconds. {2:0.###}Gbps\n",
                sendGB, elapsedMillisec / 1000.0,
                (double)sendGB * 8 / (elapsedMillisec / 1000.0)));
        }
    }
}
