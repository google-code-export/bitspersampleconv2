using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WWLanBenchmark {
    class XmitConnection {
        public TcpClient client;
        public NetworkStream stream;
        public BinaryWriter bw;
        public bool bUse;

        public void Initialize(TcpClient client) {
            this.client = client;
            stream = client.GetStream();
            bw = new BinaryWriter(stream);
            bUse = false;
        }

        public void Return() {
            bUse = false;
        }

        public void Close() {
            if (bw != null) {
                bw.Close();
                bw.Dispose();
                bw = null;
            }

            if (stream != null) {
                stream.Close();
                stream.Dispose();
                stream = null;
            }

            if (client != null) {
                client.Close();
                client = null;
            }
        }
    };
}
