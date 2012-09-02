// 日本語 UTF-8
// CUEシートを読む。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace PlayPcmWin {

    /// <summary>
    /// 1曲の情報
    /// </summary>
    class CueSheetTrackInfo {
        public string path;
        public string title;
        public int    trackId;   // TRACK 10 ==> 10
        public int    startTick; // *75 seconds
        public int    endTick;   // -1: till the end of file
 
        public int    indexId;   // INDEX 00 ==> 0, INDEX 01 ==> 1
        public string performer;
        public bool readSeparatorAfter;

        // 複数アルバムが単一のCUEシートに入っている場合、
        // アルバム情報が全体で1個というわけにはいかない。曲情報として扱う。
        public string albumTitle;

        public void Clear() {
            path = "";
            title = string.Empty;
            trackId = 0;
            startTick = 0;
            endTick = -1;

            indexId = -1;
            performer = string.Empty;
            albumTitle = string.Empty;
            readSeparatorAfter = false;
        }

        public void CopyFrom(CueSheetTrackInfo rhs) {
            path      = rhs.path;
            title     = rhs.title;
            trackId   = rhs.trackId;
            startTick = rhs.startTick;
            endTick   = rhs.endTick;

            indexId   = rhs.indexId;
            performer = rhs.performer;
            albumTitle = rhs.albumTitle;
            readSeparatorAfter = rhs.readSeparatorAfter;
        }

        private static bool TimeStrToInt(string timeStr, out int timeInt) {
            string s = String.Copy(timeStr);
            if (2 <= s.Length && s[0] == '0') {
                s = s.Substring(1);
            }
            return int.TryParse(s, out timeInt);
        }

        public static int TickStrToInt(string tickStr) {
            string[] msf = tickStr.Split(':');
            if (msf.Length != 3) {
                return -1;
            }

            int m;
            if (!TimeStrToInt(msf[0], out m)) {
                return -1;
            }
            int s;
            if (!TimeStrToInt(msf[1], out s)) {
                return -1;
            }
            int f;
            if (!TimeStrToInt(msf[2], out f)) {
                return -1;
            }

            return f + s * 75 + m * 75 * 60;
        }

        public static string TickIntToStr(int tick) {
            if (tick < 0) {
                return "--:--:--";
            }

            int f = tick % 75;
            int s =((tick - f) / 75) % 60;
            int m = (tick - s * 75 - f)/75/60;

            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", m, s, f);
        }

        public void Debug() {
            Console.WriteLine("    path={0}\n    Track={1} Index={2} start={3}({4}) end={5}({6}) performer={7} albumTitle={8} rsa={9}\n    title={10}",
                path, trackId, indexId, startTick, TickIntToStr(startTick),
                endTick, TickIntToStr(endTick), performer, albumTitle, readSeparatorAfter, title);
        }
    };

    /// <summary>
    /// CUEシートを書き込むクラス。
    /// </summary>
    class CueSheetWriter {
        private List<CueSheetTrackInfo> m_trackInfoList = new List<CueSheetTrackInfo>();
        private string m_albumTitle     = string.Empty;
        private string m_albumPerformer = string.Empty;

        public void AddTrackInfo(CueSheetTrackInfo a) {
            m_trackInfoList.Add(a);
        }

        public void SetAlbumTitle(string s) {
            m_albumTitle = s;
        }

        public void SetAlbumPerformer(string s) {
            m_albumPerformer = s;
        }

        /// <summary>
        /// throws IOException, ArgumentException, UnauthorizedException
        /// </summary>
        public bool WriteToFile(string path) {
            if (m_trackInfoList.Count() == 0) {
                return false;
            }

            using (StreamWriter sw = new StreamWriter(path, false, Encoding.Default)) {
                // アルバムタイトル
                if (null != m_albumTitle && 0 < m_albumTitle.Length) {
                    sw.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "TITLE \"{0}\"", m_albumTitle));
                } else {
                    sw.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "TITLE \"\""));
                }

                // アルバム演奏者。
                if (null != m_albumPerformer && 0 < m_albumPerformer.Length) {
                    sw.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "PERFORMER \"{0}\"", m_albumPerformer));
                }

                // 曲情報出力
                int trackCount = 1;
                for (int i = 0; i < m_trackInfoList.Count(); ++i) {
                    CueSheetTrackInfo cti = m_trackInfoList[i];

                    if (0 == string.CompareOrdinal(Path.GetDirectoryName(path),
                            Path.GetDirectoryName(cti.path))) {
                        sw.WriteLine("FILE \"{0}\" WAVE", Path.GetFileName(cti.path));
                    } else {
                        sw.WriteLine("FILE \"{0}\" WAVE", cti.path);
                    }

                    sw.WriteLine("  TRACK {0:D2} AUDIO", trackCount++);

                    sw.WriteLine("    TITLE \"{0}\"", cti.title);

                    if (null != cti.performer && 0 < cti.performer.Length) {
                        sw.WriteLine("    PERFORMER \"{0}\"", cti.performer);
                    }

                    // INDEX ?? で曲情報が確定するので、その前にREM KOKOMADEを入れる。
                    if (!(0 <= cti.endTick &&
                        (i == m_trackInfoList.Count() - 1)) &&
                        cti.readSeparatorAfter) {
                        sw.WriteLine("    REM KOKOMADE");
                    }

                    sw.WriteLine("    INDEX {0} {1}",
                        cti.indexId,
                        CueSheetTrackInfo.TickIntToStr(cti.startTick));

                    if (0 <= cti.endTick
                            && ((i == m_trackInfoList.Count() -1)
                            || (0 == string.CompareOrdinal(m_trackInfoList[i + 1].path, m_trackInfoList[i].path)
                                && m_trackInfoList[i+1].startTick != m_trackInfoList[i].endTick))) {
                        sw.WriteLine("  TRACK {0:D2} AUDIO", trackCount++);
                        sw.WriteLine("    TITLE \" gap \"");
                        sw.WriteLine("    INDEX 00 {0}",
                            CueSheetTrackInfo.TickIntToStr(cti.endTick));
                    }
                }
            }
            return true;
        }
    };

    /// <summary>
    /// CUEシートを読むクラス
    /// </summary>
    class CueSheetReader {
        private List<CueSheetTrackInfo> m_trackInfoList;
        private CueSheetTrackInfo m_currentTrackInfo;
        private string m_dirPath;

        private string m_albumTitle;
        private string m_albumPerformer;

        private bool m_bAlbumInfoParsing;

        public int GetTrackInfoCount() {
            return m_trackInfoList.Count;
        }

        public CueSheetTrackInfo GetTrackInfo(int nth) {
            return m_trackInfoList[nth];
        }

        /// <summary>
        /// タイトルがファイルに書いてない場合、string.Emptyがもどる。
        /// </summary>
        public string GetAlbumTitle() {
            return m_albumTitle;
        }

        /// <summary>
        /// 演奏者がファイルに書いてない場合、string.Emptyが戻る。
        /// </summary>
        /// <returns></returns>
        public string GetAlbumPerformer() {
            return m_albumPerformer;
        }

        /// <summary>
        /// if file read is failed IOException or ArgumentException or UnauthrizedAccessException occurs
        /// </summary>
        public bool ReadFromFile(string path) {
            // 2パス処理する。
            // パス1…ファイルから読み込んでm_trackInfoListに骨格を作る。
            // パス2…m_trackInfoListを走査して、前後関係によって判明する情報を埋める。
            //          現在のtrackInfoと1個後のトラックが同一ファイル名で
            //          cur.endTickが埋まっていない場合
            //          cur.endTick←next.startTickする。

            m_trackInfoList = new List<CueSheetTrackInfo>();
            m_dirPath = System.IO.Path.GetDirectoryName(path) + "\\";

            m_currentTrackInfo = new CueSheetTrackInfo();
            m_currentTrackInfo.Clear();

            m_albumTitle     = string.Empty;

            m_bAlbumInfoParsing = true;

            // Pass 1の処理
            bool result = false;
            
            using (StreamReader sr = new StreamReader(path, Encoding.Default)) {
                string line;
                int lineno = 0;
                while ((line = sr.ReadLine()) != null) {
                    ++lineno;
                    result = ParseOneLine(line, lineno);
                    if (!result) {
                        break;
                    }
                }
            }
            if (!result) {
                return false;
            }

            /*
            Console.WriteLine("after Pass1 =================================");
            Console.WriteLine("trackInfoList.Count={0}", m_trackInfoList.Count);
            for (int i = 0; i < m_trackInfoList.Count; ++i) {
                Console.WriteLine("trackInfo {0}", i);
                m_trackInfoList[i].Debug();
            }
            */

            // Pass 2の処理
            for (int i = 0; i < m_trackInfoList.Count-1; ++i) {
                CueSheetTrackInfo cur = m_trackInfoList[i];
                CueSheetTrackInfo next = m_trackInfoList[i+1];

                if (0 == string.CompareOrdinal(cur.path, next.path) &&
                    cur.endTick < 0) {
                    cur.endTick = next.startTick;
                }

                if (0 <= cur.endTick &&
                    0 <= cur.startTick &&
                    cur.endTick < cur.startTick) {
                    Console.WriteLine("track {0}: startTick{1} points newer time than endTick{2}",
                        cur.trackId, cur.startTick, cur.endTick);
                    return false;
                }
            }

            /*
            Console.WriteLine("after Pass2 =================================");
            Console.WriteLine("trackInfoList.Count={0}", m_trackInfoList.Count);
            for (int i = 0; i < m_trackInfoList.Count; ++i) {
                Console.WriteLine("trackInfo {0}", i);
                m_trackInfoList[i].Debug();
            }
            */

            return true;
        }

        private static List<string> Tokenize(string line) {
            int quoteStartPos = -1;
            int lastWhiteSpacePos = -1;
            List<string> tokenList = new List<string>();

            // 最後の文字がホワイトスペースではない場合の処理が面倒なので、
            // 最後にホワイトスペースをつける。
            line = line + " ";

            for (int i = 0; i < line.Length; ++i) {
                if (0 <= quoteStartPos) {
                    // ダブルクォートの中の場合。
                    // 次にダブルクォートが出てきたらトークンが完成。クォーティングモードを終わる。
                    if (line[i] == '\"') {
                        string token = line.Substring(quoteStartPos, i - quoteStartPos);
                        tokenList.Add(token);
                        quoteStartPos = -1;
                        lastWhiteSpacePos = i;
                    }
                } else {
                    // ダブルクォートの中ではない場合。
                    if (line[i] == '\"') {
                        // トークン開始。
                        quoteStartPos = i + 1;
                    } else {
                        // ダブルクォートの中でなく、ダブルクォートでない場合。

                        // トークンは、ホワイトスペース的な物で区切られる。
                        if (line[i] == ' ' || line[i] == '\t' || line[i] == '\r' || line[i] == '\n') {
                            if (lastWhiteSpacePos + 1 == i) {
                                // 左隣の文字がホワイトスペース。
                                lastWhiteSpacePos = i;
                            } else {
                                // 左隣の文字がホワイトスペースではなかった。
                                // トークンが完成。
                                string token = line.Substring(lastWhiteSpacePos + 1, i - (lastWhiteSpacePos + 1));
                                tokenList.Add(token);
                                lastWhiteSpacePos = i;
                            }
                        }
                    }
                }
            }

            /*
            Console.WriteLine("tokenList.Count={0}", tokenList.Count);
            for (int i = 0; i < tokenList.Count; ++i) {
                Console.WriteLine("{0} : \"{1}\"", i, tokenList[i]);
            }
            */

            return tokenList;
        }

        private bool ParseOneLine(string line, int lineno) {
            line = line.Trim();

            List<string> tokenList = Tokenize(line);
            if (tokenList.Count == 0) {
                return true;
            }

            switch (tokenList[0].ToUpperInvariant()) {
            case "PERFORMER":
                m_currentTrackInfo.performer = string.Empty;
                if (2 <= tokenList.Count && 0 < tokenList[1].Trim().Length) {
                    if (m_bAlbumInfoParsing) {
                        m_albumPerformer = tokenList[1];
                    } else {
                        m_currentTrackInfo.performer = tokenList[1];
                    }
                }

                break;
            case "TITLE":
                m_currentTrackInfo.title = string.Empty;
                if (2 <= tokenList.Count && 0 < tokenList[1].Trim().Length) {
                    if (m_bAlbumInfoParsing) {
                        m_albumTitle = tokenList[1];
                    } else {
                        m_currentTrackInfo.title = tokenList[1];
                    }
                }
                break;
            case "REM":
                if (2 <= tokenList.Count
                        && 0 == string.Compare(tokenList[1], "KOKOMADE", StringComparison.OrdinalIgnoreCase)) {
                    m_currentTrackInfo.readSeparatorAfter = true;
                }
                break;
            case "FILE":
                if (tokenList.Count < 2) {
                    Console.WriteLine("Error on line {0}: FILE directive error: filename is not specified", lineno);
                    return true;
                }
                if (3 <= tokenList[1].Length &&
                    ((tokenList[1][0] == '\\' && tokenList[1][1] == '\\') ||
                    ((tokenList[1][1] == ':')))) {
                    // フルパス。
                    m_currentTrackInfo.path = tokenList[1];
                } else {
                    // 相対パス。
                    m_currentTrackInfo.path = m_dirPath + tokenList[1];
                }

                // file tag has come; End album info.
                m_bAlbumInfoParsing = false;
                m_currentTrackInfo.Debug();

                break;
            case "TRACK":
                if (tokenList.Count < 2) {
                    Console.WriteLine("Error on line {0}: track number is not specified", lineno);
                    return true;
                }
                if (!int.TryParse(tokenList[1], out m_currentTrackInfo.trackId)) {
                    Console.WriteLine("Error on line {0}: track number TryParse failed", lineno);
                    return true;
                }
                m_currentTrackInfo.Debug();
                break;
            case "INDEX":
                if (tokenList.Count < 3) {
                    Console.WriteLine("Error on line {0}: index number tick format err", lineno);
                    return true;
                }
                if (!int.TryParse(tokenList[1], out m_currentTrackInfo.indexId)) {
                    m_currentTrackInfo.indexId = 1;
                }

                m_currentTrackInfo.startTick = CueSheetTrackInfo.TickStrToInt(tokenList[2]);
                if (m_currentTrackInfo.startTick < 0) {
                    Console.WriteLine("Error on line {0}: index {1} time format error ({2})",
                        lineno, m_currentTrackInfo.indexId, tokenList[2]);
                    return true;
                }

                if (m_currentTrackInfo.indexId == 0 ||
                    m_currentTrackInfo.indexId == 1) {
                    CueSheetTrackInfo newTrackInfo = new CueSheetTrackInfo();
                    newTrackInfo.CopyFrom(m_currentTrackInfo);
                    m_trackInfoList.Add(newTrackInfo);
                    m_currentTrackInfo.Debug();

                    // 揮発要素はここでリセットする。
                    m_currentTrackInfo.startTick = -1;
                    m_currentTrackInfo.readSeparatorAfter = false;
                    m_currentTrackInfo.performer = string.Empty;
                    m_currentTrackInfo.albumTitle = string.Empty;
                }
                break;

            default:
                Console.WriteLine("D: skipped {0}", tokenList[0]);
                break;
            }

            return true;
        }
    }
}
