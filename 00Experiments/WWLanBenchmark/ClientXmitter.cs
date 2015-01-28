using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WWLanBenchmark {
    class ClientXmitter {
        List<XmitTask> mXmitTaskList;
        private byte[] mSendDataHash;

        public byte[] SendDataHash() {
            return mSendDataHash;
        }

        private List<XmitConnection> mConnectionList = new List<XmitConnection>();

        private XmitConnection GetAvailableConnection() {
            lock (mConnectionList) {
                foreach (var xc in mConnectionList) {
                    if (!xc.bUse) {
                        xc.bUse = true;
                        return xc;
                    }
                }
            }
            return null;
        }

        public void CloseConnections() {
            foreach (var xc in mConnectionList) {
                xc.Close();
            }
            mConnectionList.Clear();
        }

        public void Prepare(string server, int xmitPort, int xmitConnectionCount, long continuousSendBytes, int xmitFragmentBytes) {
            // Xmit用TCP接続を確立
            EstablishConnections(server, xmitPort, xmitConnectionCount);

            // XmitTaskのリストを準備。
            SetupXmitTasks(continuousSendBytes, xmitFragmentBytes);
        }

        private void EstablishConnections(string server, int xmitPort, int xmitConnectionCount) {
            for (int i = 0; i < xmitConnectionCount; ++i) {
                var xc = new XmitConnection();
                xc.Initialize(new TcpClient(server, xmitPort));
                mConnectionList.Add(xc);
            }
        }

        /// <param name="totalBytes">総送出バイト数</param>
        /// <param name="xmitFragmentBytes">各スレッドの送出バイト数</param>
        private void SetupXmitTasks(long totalBytes, int xmitFragmentBytes) {
            mXmitTaskList = new List<XmitTask>();

            // XmitTaskのリストを作成。
            long pos = 0;
            do {
                int bytes = xmitFragmentBytes;
                if (totalBytes < pos + xmitFragmentBytes) {
                    bytes = (int)(totalBytes - pos);
                }

                var xt = new XmitTask(pos, bytes);
                mXmitTaskList.Add(xt);
                pos += bytes;
            } while (pos < totalBytes);

            // 送出データの準備。
            Parallel.For(0, mXmitTaskList.Count, i => {
                using (var rng = new RNGCryptoServiceProvider()) {
                    XmitTask xt;
                    lock (mXmitTaskList) {
                        xt = mXmitTaskList[i];
                    }
                    xt.xmitData = new byte[xt.sizeBytes];
                    rng.GetBytes(xt.xmitData);
                }
            });

            // SHA256を計算。
            using (var hash = new SHA256CryptoServiceProvider()) {
                foreach (var xt in mXmitTaskList) {
                    hash.TransformBlock(xt.xmitData, 0, xt.xmitData.Length, xt.xmitData, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                mSendDataHash = hash.Hash;
            }
        }

        public bool Xmit() {
            bool result = true;

            var doneEventArray = new ManualResetEvent[mXmitTaskList.Count];
            for (int i = 0; i < mXmitTaskList.Count; ++i) {
                doneEventArray[i] = mXmitTaskList[i].doneEvent;
            }

            ThreadPool.SetMaxThreads(mConnectionList.Count, mConnectionList.Count);
            for (int i = 0; i < mXmitTaskList.Count; ++i) {
                ThreadPool.QueueUserWorkItem(ThreadPoolCallback, mXmitTaskList[i]);
            }
            WaitHandle.WaitAll(doneEventArray);

            for (int i = 0; i < mXmitTaskList.Count; ++i) {
                if (!mXmitTaskList[i].result) {
                    result = false;
                }
                mXmitTaskList[i].End();
            }
            mXmitTaskList.Clear();
            doneEventArray = null;
            return result;
        }

        private void ThreadPoolCallback(Object threadContext) {
            var xt = threadContext as XmitTask;
            var xc = GetAvailableConnection();

            xc.bw.Write(xt.startPos);
            xc.bw.Write(xt.sizeBytes);
            xc.bw.Write(xt.xmitData);
            xc.bw.Flush();

            xc.stream.ReadByte();

            xc.Return();

            xt.Done();
        }
    }
}
