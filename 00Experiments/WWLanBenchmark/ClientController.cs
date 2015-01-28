using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;

namespace WWLanBenchmark {
    class ClientController {
        private const long ONE_GIGA = 1000 * 1000 * 1000;
        private BackgroundWorker mBackgroundWorker;
        private ClientXmitter mClientXmitter = new ClientXmitter();

        public void Run(BackgroundWorker backgroundWorker, String server, int controlPort, int dataPort,
                int xmitConnectionCount, int xmitFragmentBytes, long totalBytes) {
            mBackgroundWorker = backgroundWorker;

            try {
                using (var client = new TcpClient(server, controlPort)) {
                    using (var stream = client.GetStream()) {
                        using (var bw = new BinaryWriter(stream)) {
                            // 1. 設定情報を送出。
                            mBackgroundWorker.ReportProgress(1, "Connected to Server.\nPreparing xmit data...");
                            if (!SendSettings(stream, bw, xmitFragmentBytes, totalBytes)) {
                                mBackgroundWorker.ReportProgress(100, "Error: Unexpected server response. Exit.");
                                return;
                            }

                            // 2. 送出データを準備し、Xmit用TCP接続をxmitConnectionCount個確立する。
                            mClientXmitter.Prepare(server, dataPort, xmitConnectionCount, totalBytes, xmitFragmentBytes);
                            mBackgroundWorker.ReportProgress(1, "done.\n");

                            mBackgroundWorker.ReportProgress(10, string.Format("Sending {0}GB stream...\n",
                                totalBytes / ONE_GIGA));

                            // 3. xmitConnectionCount個のTCP接続を使用してcontinuousSendGiB ギガバイト送出。
                            long elapsedMillisec = Xmit(stream, bw);

                            mClientXmitter.CloseConnections();

                            mBackgroundWorker.ReportProgress(1, string.Format("    Xmit {0}GB in {1} seconds. {2:0.###}Gbps\n",
                                totalBytes / ONE_GIGA, elapsedMillisec / 1000.0,
                                (double)totalBytes * 8 / ONE_GIGA / (elapsedMillisec / 1000.0)));
                        }
                    }
                }
            } catch (ArgumentNullException e) {
                mBackgroundWorker.ReportProgress(0, string.Format("Error: ArgumentNullException: {0}\n", e));
            } catch (SocketException e) {
                mBackgroundWorker.ReportProgress(0, string.Format("Error: SocketException: {0}\n", e));
            }
            mBackgroundWorker.ReportProgress(100, "Done.\n");
        }

        private static bool SendSettings(NetworkStream stream, BinaryWriter bw, long xmitFragmentBytes, long totalBytes) {
            bw.Write(xmitFragmentBytes);
            bw.Write(totalBytes);
            bw.Flush();

            // サーバーからのACKを待つ。
            int ack = stream.ReadByte();
            if (ack != 0) {
                return false;
            }
            return true;
        }

        private long Xmit(NetworkStream stream, BinaryWriter bw) {
            bw.Write(mClientXmitter.SendDataHash());
            bw.Flush();

            bool result = mClientXmitter.Xmit();
            if (!result) {
                mBackgroundWorker.ReportProgress(1, "Error: ClientXmitter.Xmit failed!\n");
            }

            mBackgroundWorker.ReportProgress(100, "    Waiting server response...\n");
            long elapsedMillisec = Utility.StreamReadInt64(stream);
            return elapsedMillisec;
        }

    }
}
