using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PlayPcmWin {
    class M3uTrackInfo {
        public string path;

        public void Clear() {
            path = string.Empty;
        }

        public void CopyFrom(M3uTrackInfo rhs) {
            path   = rhs.path;
        }

        public void Debug() {
            Console.WriteLine("    path={0}", path);
        }

        public M3uTrackInfo(string aPath) {
            path = aPath;
        }
    };

    class M3uReader {
        List<M3uTrackInfo> mTrackInfoList;
        string mDirPath;

        public M3uReader() {
        }

        public bool ReadFromFile(string path) {
            // 1パスで読む。

            mTrackInfoList = new List<M3uTrackInfo>();
            mDirPath = System.IO.Path.GetDirectoryName(path) + "\\";

            bool result = true;
            try {
                using (StreamReader sr = new StreamReader(path, Encoding.UTF8)) {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        ParseOneLine(line);
                    }
                }
            } catch (IOException ex) {
                Console.WriteLine(ex);
                result = false;
            }
            return result;
        }

        const string SUPPORTED_EXTENSION_REGEX = @"(\.WAV|\.WAVE|\.FLAC|\.AIF|\.AIFF|\.AIFC|.AIFFC";

        private void ParseOneLine(string line) {
            if (line.StartsWith("#")) {
                // 飛ばす
                return;
            }

            if (!Regex.IsMatch(Path.GetExtension(line).ToUpperInvariant(), SUPPORTED_EXTENSION_REGEX)) {
                // 飛ばす
                return;
            }

            if (Regex.IsMatch(line, @"^http:\/\/.*")) {
                // 飛ばす
                return;
            }

            if (Regex.IsMatch(line, @"^[A-Za-z]:\.*")) {
                // ドライブレターから始まるフルパス指定。
                mTrackInfoList.Add(new M3uTrackInfo(line.Trim()));
                return;
            }

            if (Regex.IsMatch(line, @"\\[A-Za-z][A-Za-z0-9\-\.]+[A-Za-z0-9]\.*")) {
                // UNC名。
                mTrackInfoList.Add(new M3uTrackInfo(line.Trim()));
                return;
            }

            // 相対パス
            var sb = new StringBuilder(mDirPath);
            sb.Append(line.Trim());
            mTrackInfoList.Add(new M3uTrackInfo(sb.ToString()));
        }

    }
}