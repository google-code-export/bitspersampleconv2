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

        public PlaylistTrackInfo ConvertToPlaylistTrackInfo() {
            var plti = new PlaylistTrackInfo();
            plti.path = path;
            plti.title = string.Empty;
            plti.trackId = 0;
            plti.startTick = 0;
            plti.endTick = -1;

            plti.indexId = 0;
            plti.performer = string.Empty;
            plti.albumTitle = string.Empty;
            plti.readSeparatorAfter = false;

            return plti;
        }
    };

    class M3uReader : PlaylistReader {
        List<M3uTrackInfo> mTrackInfoList;
        string mDirPath;

        public M3uReader() {
        }

        public PlaylistTrackInfo GetTrackInfo(int nth) {
            return mTrackInfoList[nth].ConvertToPlaylistTrackInfo();
        }

        public int GetTrackInfoCount() {
            return mTrackInfoList.Count;
        }

        public bool ReadFromFile(string path) {
            // 1パスで読む。

            mTrackInfoList = new List<M3uTrackInfo>();
            mDirPath = System.IO.Path.GetDirectoryName(path) + "\\";

            Encoding encoding = Encoding.Default;
            if (0 == string.CompareOrdinal(Path.GetExtension(path).ToUpperInvariant(), ".M3U8")) {
                encoding = Encoding.UTF8;
            }

            bool result = true;
            try {
                using (StreamReader sr = new StreamReader(path, encoding)) {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        ParseOneLine(line);
                    }
                }
            } catch (IOException ex) {
                Console.WriteLine(ex);
                result = false;
            } catch (ArgumentException ex) {
                Console.WriteLine(ex);
                result = false;
            }
            return result;
        }

        const string SUPPORTED_EXTENSION_REGEX = @"(\.WAV|\.WAVE|\.FLAC|\.AIF|\.AIFF|\.AIFC|\.AIFFC)";

        private void ParseOneLine(string line) {
            if (line.StartsWith("#", StringComparison.Ordinal)) {
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