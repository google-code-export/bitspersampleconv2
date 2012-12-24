using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PcmDataLib {

    /// <summary>
    /// ユーティリティー関数置き場。
    /// </summary>
    public class Util {
        /// <summary>
        /// readerのデータをcountバイトだけスキップする。
        /// </summary>
        public static void BinaryReaderSkip(BinaryReader reader, long count) {
            if (reader.BaseStream.CanSeek) {
                reader.BaseStream.Seek(count, SeekOrigin.Current);
            } else {
                for (long i = 0; i < count; ++i) {
                    reader.ReadByte();
                }
            }
        }

        public static bool BinaryReaderSeekFromBegin(BinaryReader reader, long offset) {
            if (!reader.BaseStream.CanSeek) {
                return false;
            }
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            return true;
        }

        /// <summary>
        /// FourCC形式のバイト列をsと比較する
        /// </summary>
        /// <param name="b">FourCC形式のバイト列を含むバッファ</param>
        /// <param name="bPos">バイト列先頭から注目位置までのオフセット</param>
        /// <param name="s">比較対象文字列 最大4文字</param>
        /// <returns></returns>
        public static bool FourCCHeaderIs(byte[] b, int bPos, string s)
        {
            System.Diagnostics.Debug.Assert(s.Length == 4);
            if (b.Length - bPos < 4)
            {
                return false;
            }

            /*
            System.Console.WriteLine("D: b={0}{1}{2}{3} s={4}",
                (char)b[0], (char)b[1], (char)b[2], (char)b[3], s);
            */

            return s[0] == b[bPos]
                && s[1] == b[bPos + 1]
                && s[2] == b[bPos + 2]
                && s[3] == b[bPos + 3];
        }
    }

    /// <summary>
    /// PCMデータ情報置き場。
    /// ・PCMフォーマット情報
    ///   ・チャンネル数
    ///   ・サンプルレート
    ///   ・量子化ビット数
    ///   ・サンプルデータ形式(整数、浮動小数点数)
    /// ・PCMデータ
    ///   ・PCMデータ配列
    ///   ・PCMデータフレーム数(フレーム＝サンプル x チャンネル)
    /// ・ファイル管理情報
    ///   ・連番
    ///   ・ファイルグループ番号
    ///   ・ファイル名(ディレクトリ名を除く)
    ///   ・フルパスファイル名
    ///   ・表示名
    ///   ・開始Tick
    ///   ・終了Tick
    /// </summary>
    public class PcmData {

        // PCMフォーマット情報 //////////////////////////////////////////////

        public enum ValueRepresentationType {
            SInt,
            SFloat
        };

        /// <summary>
        /// チャンネル数
        /// </summary>
        public int NumChannels { get; set; }

        /// <summary>
        /// サンプルレート
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// 1サンプル値のビット数(無効な0埋めビット含む)
        /// </summary>
        public int BitsPerSample { get; set; }

        /// <summary>
        /// 1サンプル値の有効なビット数
        /// </summary>
        public int ValidBitsPerSample { get; set; }

        /// <summary>
        /// サンプル値形式(int、float)
        /// </summary>
        public ValueRepresentationType
            SampleValueRepresentationType { get; set; }

        // PCMデータ ////////////////////////////////////////////////////////

        /// <summary>
        /// 総フレーム数(サンプル値の数÷チャンネル数)
        /// </summary>
        private long   m_numFrames;

        /// <summary>
        /// サンプル値配列。
        /// </summary>
        private byte[] m_sampleArray;

        // ファイル管理情報 /////////////////////////////////////////////////

        /// <summary>
        /// 識別番号 0から始まる番号
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 連番(再生順) 0から始まる番号
        /// </summary>
        public int Ordinal { get; set; }

        /// <summary>
        /// ファイルグループ番号。 0から始まる番号
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// ファイル名(ディレクトリ名を除く)
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// フルパスファイル名
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// 表示名。CUEシートから来る
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 開始Tick(75分の1秒=1)。0のとき、ファイルの先頭が開始Tick
        /// </summary>
        public int    StartTick { get; set; }

        /// <summary>
        /// 終了Tick(75分の1秒=1)。-1のとき、ファイルの終わりが終了Tick
        /// </summary>
        public int    EndTick { get; set; }

        /// <summary>
        /// アルバムタイトル
        /// </summary>
        public string AlbumTitle { get; set; }

        /// <summary>
        /// アーティスト
        /// </summary>
        public string ArtistName { get; set; }

        /// <summary>
        /// CUEシートから読んだ場合のINDEX番号(1==音声データ、0==無音)
        /// </summary>
        public int CueSheetIndex { get; set; }

        /// <summary>
        /// 画像バイト数
        /// </summary>
        public int PictureBytes { get; set; }

        /// <summary>
        /// 画像データバイト列
        /// </summary>
        public byte[] PictureData { get; set; }

        /// <summary>
        /// 長さ(秒)
        /// </summary>
        public int DurationSeconds {
            get {
                int seconds = (int)(NumFrames / SampleRate)
                        - StartTick / 75;
                if (0 <= EndTick) {
                    seconds = (EndTick - StartTick) / 75;
                }
                if (seconds < 0) {
                    seconds = 0;
                }
                return seconds;
            }
        }

        /// <summary>
        /// rhsの内容をコピーする。PCMデータ配列だけはコピーしない。(nullをセットする)
        /// PCMデータ配列は、SetSampleArrayで別途設定する。
        /// </summary>
        /// <param name="rhs">コピー元</param>
        private void CopyHeaderInfoFrom(PcmData rhs) {
            NumChannels   = rhs.NumChannels;
            SampleRate    = rhs.SampleRate;
            BitsPerSample = rhs.BitsPerSample;
            ValidBitsPerSample = rhs.ValidBitsPerSample;
            SampleValueRepresentationType = rhs.SampleValueRepresentationType;
            m_numFrames = rhs.m_numFrames;
            m_sampleArray = null;
            Id          = rhs.Id;
            Ordinal     = rhs.Ordinal;
            GroupId     = rhs.GroupId;
            FileName    = rhs.FileName;
            FullPath    = rhs.FullPath;
            DisplayName = rhs.DisplayName;
            StartTick   = rhs.StartTick;
            EndTick     = rhs.EndTick;
            AlbumTitle  = rhs.AlbumTitle;
            ArtistName   = rhs.ArtistName;
            CueSheetIndex = rhs.CueSheetIndex;
            PictureBytes = rhs.PictureBytes;
            PictureData = rhs.PictureData;
        }

        public PcmData() {
            NumChannels = 0;
            SampleRate = 0;
            BitsPerSample = 0;
            ValidBitsPerSample = 0;
            SampleValueRepresentationType = ValueRepresentationType.SInt;
            m_numFrames = 0;
            m_sampleArray = null;
            Id = -1;
            Ordinal = -1;
            GroupId = -1;
            FileName = null;
            FullPath = null;
            DisplayName = null;
            StartTick = 0;
            EndTick = -1;
            AlbumTitle = "";
            ArtistName = "";
            CueSheetIndex = 1;
            PictureBytes = 0;
            PictureData = null;
        }


        /// <summary>
        /// ヘッダー情報、サンプルデータ領域をクローンする。
        /// </summary>
        public void CopyFrom(PcmData rhs) {
            CopyHeaderInfoFrom(rhs);

            m_sampleArray = null;
            if (rhs.m_sampleArray != null) {
                m_sampleArray = (byte[])rhs.m_sampleArray.Clone();
            }
        }

        // プロパティIO /////////////////////////////////////////////////////

        /// <summary>
        /// 総フレーム数(サンプル値の数÷チャンネル数)
        /// </summary>
        public long NumFrames {
            get { return m_numFrames; }
        }

        /// <summary>
        /// 1フレームあたりのビット数(サンプルあたりビット数×総チャンネル数)
        /// </summary>
        public int BitsPerFrame {
            get { return BitsPerSample * NumChannels; }
        }

        /// <summary>
        /// サンプル値配列
        /// </summary>
        public byte[] GetSampleArray() {
            return m_sampleArray;
        }

        /// <summary>
        /// 総フレーム数(サンプル値の数÷チャンネル数)をセット
        /// </summary>
        public void SetNumFrames(long numFrames) {
            m_numFrames = numFrames;
        }

        /// <summary>
        /// サンプル配列を入れる。総フレーム数は別途セットする必要あり。
        /// </summary>
        /// <param name="sampleArray">サンプル配列</param>
        public void SetSampleArray(byte[] sampleArray) {
            m_sampleArray = null;
            m_sampleArray = sampleArray;
        }

        public void SetSampleArray(long numFrames, byte[] sampleArray) {
            m_numFrames = numFrames;
            m_sampleArray = null;
            m_sampleArray = sampleArray;
        }

        /// <summary>
        /// forget data part.
        /// PCMデータ配列を忘れる。
        /// サンプル数など、フォーマット情報は忘れない。
        /// </summary>
        public void ForgetDataPart() {
            m_sampleArray = null;
        }

        public void SetPicture(int bytes, byte[] data) {
            PictureBytes = bytes;
            PictureData = data;
        }

        /// <summary>
        /// PCMデータの形式を設定する。
        /// </summary>
        public void SetFormat(
            int numChannels,
            int bitsPerSample,
            int validBitsPerSample,
            int sampleRate,
            ValueRepresentationType sampleValueRepresentation,
            long numFrames) {
            NumChannels = numChannels;
            BitsPerSample = bitsPerSample;
            ValidBitsPerSample = validBitsPerSample;
            SampleRate = sampleRate;
            SampleValueRepresentationType = sampleValueRepresentation;
            m_numFrames = numFrames;

            m_sampleArray = null;
        }

        /// <summary>
        /// サンプリング周波数と量子化ビット数、有効なビット数、チャンネル数、データ形式が同じならtrue
        /// </summary>
        public bool IsSameFormat(PcmData other) {
            return BitsPerSample      == other.BitsPerSample
                && ValidBitsPerSample == other.ValidBitsPerSample
                && SampleRate    == other.SampleRate
                && NumChannels   == other.NumChannels
                && SampleValueRepresentationType == other.SampleValueRepresentationType;
        }

        /// <summary>
        /// StartTickとEndTickを見て、必要な部分以外をカットする。
        /// </summary>
        public void Trim() {
            if (StartTick < 0) {
                // データ壊れ。先頭を読む。
                StartTick = 0;
            }

            long startFrame = (long)(StartTick) * SampleRate / 75;
            long endFrame   = (long)(EndTick)   * SampleRate / 75;

            TrimInternal(startFrame, endFrame);
        }

        /// <summary>
        /// startFrameからendFrameまでの範囲にする。
        /// </summary>
        /// <param name="startFrame">0: 先頭 </param>
        /// <param name="endFrame">負: 最後まで。0以上: 範囲外の最初のデータoffset。 0の場合0サンプルとなる</param>
        public void TrimByFrame(long startFrame, long endFrame) {
            TrimInternal(startFrame, endFrame);
        }

        private void TrimInternal(long startFrame, long endFrame) {
            if (startFrame == 0 && endFrame < 0) {
                // データTrimの必要はない。
                return;
            }

            if (endFrame < 0 ||
                NumFrames < endFrame) {
                // 終了位置はファイルの終わり。
                endFrame = NumFrames;
            }

            if (endFrame < startFrame) {
                // 1サンプルもない。
                startFrame = endFrame;
            }

            long startBytes = startFrame * BitsPerFrame / 8;
            long endBytes   = endFrame   * BitsPerFrame / 8;

            Debug.Assert(0 <= startBytes);
            Debug.Assert(0 <= endBytes);
            Debug.Assert(startBytes <= endBytes);
            Debug.Assert(null != m_sampleArray);
            Debug.Assert(startBytes <= m_sampleArray.Length);
            Debug.Assert(endBytes <= m_sampleArray.Length);

            long newNumSamples = endFrame - startFrame;
            m_numFrames = newNumSamples;
            if (newNumSamples == 0 ||
                m_sampleArray.Length <= startBytes) {
                m_sampleArray = null;
                m_numFrames = 0;
            } else {
                byte[] newArray = new byte[endBytes - startBytes];
                Array.Copy(m_sampleArray, startBytes, newArray, 0, endBytes - startBytes);
                m_sampleArray = null;
                m_sampleArray = newArray;
            }
        }

        /// <summary>
        /// サンプル値取得。フォーマットがなんであっても使用可能。
        /// </summary>
        /// <param name="ch">チャンネル番号</param>
        /// <param name="pos">サンプル番号</param>
        /// <returns>サンプル値。-1.0～+1.0位</returns>
        public double GetSampleValueInDouble(int ch, long pos) {
            Debug.Assert(0 <= ch && ch < NumChannels);

            if (pos < 0 || NumFrames <= pos) {
                return 0.0;
            }

            long offset = pos * BitsPerFrame/8 + ch * BitsPerSample/8;
            Debug.Assert(offset <= 0x7fffffffL);

            byte [] data = new byte[BitsPerSample / 8];
            Array.Copy(m_sampleArray, offset, data, 0, BitsPerSample / 8);

            switch (BitsPerSample) {
            case 16:
                data = ConvI16toF64(data);
                break;
            case 24:
                data = ConvI24toF64(data);
                break;
            case 32:
                switch (SampleValueRepresentationType) {
                case ValueRepresentationType.SInt:
                    data = ConvI32toF64(data);
                    break;
                case ValueRepresentationType.SFloat:
                    data = ConvF32toF64(data);
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false);
                    break;
                }
                break;
            case 64:
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            return BitConverter.ToDouble(data, 0);
        }

        /// <summary>
        /// double サンプル値セット。フォーマットが64bit SFloatの場合のみ使用可能。
        /// </summary>
        public void SetSampleValueInDouble(int ch, long pos, double val) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 64);
            Debug.Assert(0 <= ch && ch < NumChannels);

            if (pos < 0 || NumFrames <= pos) {
                return;
            }

            long offset = pos * BitsPerFrame / 8 + ch * BitsPerSample / 8;
            Debug.Assert(offset <= 0x7fffffffL);

            var byteArray = BitConverter.GetBytes(val);
            Buffer.BlockCopy(byteArray, 0, m_sampleArray, (int)offset, 8);
        }

        /// <summary>
        /// float サンプル値取得。フォーマットが32bit floatの場合のみ使用可能。
        /// </summary>
        /// <param name="ch">チャンネル番号</param>
        /// <param name="pos">サンプル番号</param>
        /// <returns>サンプル値。-1.0～+1.0位</returns>
        public float GetSampleValueInFloat(int ch, long pos) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 32);
            Debug.Assert(0 <= ch && ch < NumChannels);

            if (pos < 0 || NumFrames <= pos) {
                return 0.0f;
            }

            long offset = pos * BitsPerFrame / 8 + ch * BitsPerSample / 8;
            Debug.Assert(offset <= 0x7fffffffL);

            return BitConverter.ToSingle(m_sampleArray, (int)offset);
        }

        /// <summary>
        /// サンプル値セット。フォーマットが32bit SFloatの場合のみ使用可能。
        /// </summary>
        public void SetSampleValueInFloat(int ch, long pos, float val) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 32);
            Debug.Assert(0 <= ch && ch < NumChannels);

            if (pos < 0 || NumFrames <= pos) {
                return;
            }

            long offset = pos * BitsPerFrame / 8 + ch * BitsPerSample / 8;
            Debug.Assert(offset <= 0x7fffffffL);

            var byteArray = BitConverter.GetBytes(val);
            Buffer.BlockCopy(byteArray, 0, m_sampleArray, (int)offset, 4);
        }

        /// <summary>
        /// doubleのバッファをスケーリングする。ダブルバッファとは関係ない
        /// </summary>
        public void ScaleDoubleBuffer(double scale) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 64);
            for (int i = 0; i < NumFrames * NumChannels; ++i) {
                double v = BitConverter.ToDouble(m_sampleArray, i * 8);
                v *= scale;
                var byteArray = BitConverter.GetBytes(v);
                Buffer.BlockCopy(byteArray, 0, m_sampleArray, i * 8, 8);
            }
        }

        /// <summary>
        /// floatのバッファをスケーリングする。
        /// </summary>
        public void ScaleFloatBuffer(float scale) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 32);
            for (int i = 0; i < NumFrames * NumChannels; ++i) {
                float v = BitConverter.ToSingle(m_sampleArray, i * 4);
                v *= scale;
                var byteArray = BitConverter.GetBytes(v);
                Buffer.BlockCopy(byteArray, 0, m_sampleArray, i * 4, 4);
            }
        }

        /// <summary>
        /// doubleのバッファで最大値、最小値を取得
        /// </summary>
        public void FindMaxMinValueOnDoubleBuffer(out double maxV, out double minV) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 64);
            maxV = 0.0;
            minV = 0.0;

            for (int i = 0; i < NumFrames * NumChannels; ++i) {
                double v = BitConverter.ToDouble(m_sampleArray, i * 8);
                if (v < minV) {
                    minV = v;
                }
                if (maxV < v) {
                    maxV = v;
                }
            }
        }

        private static readonly double SAMPLE_VALUE_MAX_DOUBLE =  1.0;
        private static readonly double SAMPLE_VALUE_MIN_DOUBLE = -1.0;
        private static readonly float  SAMPLE_VALUE_MAX_FLOAT  =  1.0f;
        private static readonly float  SAMPLE_VALUE_MIN_FLOAT  = -1.0f;

        /// <summary>
        /// floatのバッファで最大値、最小値を取得
        /// </summary>
        public void FindMaxMinValueOnFloatBuffer(out float maxV, out float minV) {
            Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
            Debug.Assert(ValidBitsPerSample == 32);
            maxV = 0.0f;
            minV = 0.0f;

            for (int i = 0; i < NumFrames * NumChannels; ++i) {
                float v = BitConverter.ToSingle(m_sampleArray, i * 4);
                if (v < minV) {
                    minV = v;
                }
                if (maxV < v) {
                    maxV = v;
                }
            }
        }

        /// <summary>
        /// doubleのバッファで音量制限する。
        /// </summary>
        /// <returns>スケーリングが行われた場合、スケールの倍数(1.0より小さい)。行われなかった場合1.0</returns>
        public double LimitLevelOnDoubleRange() {
            double maxV;
            double minV;
            FindMaxMinValueOnDoubleBuffer(out maxV, out minV);

            double scale = 1.0;
            if (SAMPLE_VALUE_MAX_DOUBLE < maxV) {
                scale = SAMPLE_VALUE_MAX_DOUBLE / maxV;
            }
            if (minV < SAMPLE_VALUE_MIN_DOUBLE && SAMPLE_VALUE_MIN_DOUBLE / minV < scale) {
                scale = SAMPLE_VALUE_MIN_DOUBLE / minV;
            }

            if (scale < 1.0) {
                ScaleDoubleBuffer(scale);
            }

            return scale;
        }

        /// <summary>
        /// floatのバッファで音量制限する。
        /// </summary>
        /// <returns>スケーリングが行われた場合、スケールの倍数。行われなかった場合1.0f</returns>
        public float LimitLevelOnFloatRange() {
            float maxV;
            float minV;
            FindMaxMinValueOnFloatBuffer(out maxV, out minV);

            float scale = 1.0f;
            if (SAMPLE_VALUE_MAX_FLOAT < maxV) {
                scale = SAMPLE_VALUE_MAX_FLOAT / maxV;
            }
            if (minV < SAMPLE_VALUE_MIN_FLOAT && SAMPLE_VALUE_MIN_FLOAT / minV < scale) {
                scale = SAMPLE_VALUE_MIN_FLOAT / minV;
            }

            if (scale < 1.0f) {
                ScaleFloatBuffer(scale);
            }

            return scale;
        }

        public PcmData MonoToStereo() {
            System.Diagnostics.Debug.Assert(NumChannels == 1);

            // サンプルあたりビット数が8の倍数でないとこのアルゴリズムは使えない
            System.Diagnostics.Debug.Assert((BitsPerSample & 7) == 0);

            byte [] newSampleArray = new byte[m_sampleArray.LongLength * 2];

            {
                int bytesPerSample = BitsPerSample / 8;

                // NumFramesは総フレーム数。sampleArrayのフレーム数はこれよりも少ないことがある。
                // 実際に存在するサンプル数sampleFramesだけ処理する。
                long sampleFrames = m_sampleArray.LongLength / bytesPerSample;
                long fromPosBytes = 0;
                for (long frame = 0; frame < sampleFrames; ++frame) {
                    for (int offs = 0; offs < bytesPerSample; ++offs) {
                        newSampleArray[fromPosBytes * 2 + offs] = m_sampleArray[fromPosBytes + offs];
                        newSampleArray[fromPosBytes * 2 + bytesPerSample + offs] = m_sampleArray[fromPosBytes + offs];
                    }
                    fromPosBytes += bytesPerSample;
                }
            }
            PcmData newPcmData = new PcmData();
            newPcmData.CopyHeaderInfoFrom(this);
            newPcmData.SetFormat(2, BitsPerSample, ValidBitsPerSample, SampleRate, SampleValueRepresentationType, NumFrames);
            newPcmData.SetSampleArray(newSampleArray);

            return newPcmData;
        }

        public class BitsPerSampleConvArgs {
            /// <summary>
            /// perform noise shaping if applicable
            /// </summary>
            public bool noiseShaping;

            /// <summary>
            /// [out] true if noise shaping is performed
            /// </summary>
            public bool noiseShapingPerformed;

            public BitsPerSampleConvArgs(bool noiseShaping) {
                this.noiseShaping = noiseShaping;
                noiseShapingPerformed = false;
            }
        };

        /// <summary>
        /// 量子化ビット数をbitsPerSampleに変更した、新しいPcmDataを戻す。
        /// 自分自身の内容は変更しない。
        /// </summary>
        /// <param name="newBitsPerSample">新しい量子化ビット数</param>
        /// <returns>量子化ビット数変更後のPcmData</returns>
        public PcmData BitsPerSampleConvertTo(int newBitsPerSample, ValueRepresentationType newValueRepType, BitsPerSampleConvArgs args) {
            byte [] newSampleArray        = null;

            bool noiseShaping = false;
            if (args != null) {
                noiseShaping = args.noiseShaping;
            }
            bool noiseShapingCapable = false;

            /// @todo 次に項目を増やすときは、2次元のテーブルにリファクタリングする
            if (newBitsPerSample == 64) {
                Debug.Assert(newValueRepType == ValueRepresentationType.SFloat);
                switch (BitsPerSample) {
                case 16:
                    newSampleArray = ConvI16toF64(GetSampleArray());
                    break;
                case 24:
                    newSampleArray = ConvI24toF64(GetSampleArray());
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        newSampleArray = ConvF32toF64(GetSampleArray());
                    } else {
                        newSampleArray = ConvI32toF64(GetSampleArray());
                    }
                    break;
                case 64:
                    Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
                    newSampleArray = (byte[])GetSampleArray().Clone();
                    break;
                default:
                    Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 32) {
                if (newValueRepType == ValueRepresentationType.SFloat) {
                    switch (BitsPerSample) {
                    case 16:
                        newSampleArray = ConvI16toF32(GetSampleArray());
                        break;
                    case 24:
                        newSampleArray = ConvI24toF32(GetSampleArray());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            newSampleArray = (byte[])GetSampleArray().Clone();
                        } else {
                            newSampleArray = ConvI32toF32(GetSampleArray());
                        }
                        break;
                    case 64:
                        Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
                        newSampleArray = ConvF64toF32(GetSampleArray());
                        break;
                    default:
                        Debug.Assert(false);
                        return null;
                    }
                } else if (newValueRepType == ValueRepresentationType.SInt) {
                    switch (BitsPerSample) {
                    case 16:
                        newSampleArray = ConvI16toI32(GetSampleArray());
                        break;
                    case 24:
                        newSampleArray = ConvI24toI32(GetSampleArray());
                        break;
                    case 32:
                        if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                            newSampleArray = ConvF32toI32(GetSampleArray());
                        } else {
                            newSampleArray = (byte[])GetSampleArray().Clone();
                        }
                        break;
                    case 64:
                        Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
                        newSampleArray = ConvF64toI32(GetSampleArray());
                        break;
                    default:
                        Debug.Assert(false);
                        return null;
                    }
                } else {
                    Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 24) {
                switch (BitsPerSample) {
                case 16:
                    newSampleArray = ConvI16toI24(GetSampleArray());
                    break;
                case 24:
                    newSampleArray = (byte[])GetSampleArray().Clone();
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        newSampleArray = ConvF32toI24(GetSampleArray());
                    } else {
                        newSampleArray = ConvI32toI24(GetSampleArray(), noiseShaping);
                        noiseShapingCapable = true;
                    }
                    break;
                case 64:
                    Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
                    newSampleArray = ConvF64toI24(GetSampleArray(), noiseShaping);
                    noiseShapingCapable = true;
                    break;
                default:
                    Debug.Assert(false);
                    return null;
                }
            } else if (newBitsPerSample == 16) {
                switch (BitsPerSample) {
                case 16:
                    newSampleArray = (byte[])GetSampleArray().Clone();
                    break;
                case 24:
                    newSampleArray = ConvI24toI16(GetSampleArray(), noiseShaping);
                    noiseShapingCapable = true;
                    break;
                case 32:
                    if (SampleValueRepresentationType == ValueRepresentationType.SFloat) {
                        newSampleArray = ConvF32toI16(GetSampleArray(), noiseShaping);
                        noiseShapingCapable = true;
                    } else {
                        newSampleArray = ConvI32toI16(GetSampleArray(), noiseShaping);
                        noiseShapingCapable = true;
                    }
                    break;
                case 64:
                    Debug.Assert(SampleValueRepresentationType == ValueRepresentationType.SFloat);
                    newSampleArray = ConvF64toI16(GetSampleArray(), noiseShaping);
                    noiseShapingCapable = true;
                    break;
                default:
                    Debug.Assert(false);
                    return null;
                }
            } else {
                Debug.Assert(false);
                return null;
            }

            if (args != null) {
                args.noiseShapingPerformed = noiseShaping && noiseShapingCapable;
            }

            // 有効なビット数の計算
            int newValidBitsPerSample = ValidBitsPerSample;
            if (newBitsPerSample < newValidBitsPerSample) {
                // 新しい量子化ビット数が、元の量子化ビット数よりも減った。
                newValidBitsPerSample = newBitsPerSample;
            }
            if (newBitsPerSample == 32 &&
                newValueRepType == ValueRepresentationType.SFloat) {
                // FLOAT32は、全てのビット(=32)を有効にしないと意味ないデータになると思われる。
                newValidBitsPerSample = 32;
            }
            if (newBitsPerSample == 64 &&
                newValueRepType == ValueRepresentationType.SFloat) {
                // FLOAT64は、全てのビット(=64)を有効にしないと意味ないデータになると思われる。
                newValidBitsPerSample = 64;
            }

            PcmData newPcmData = new PcmData();
            newPcmData.CopyHeaderInfoFrom(this);
            newPcmData.SetFormat(NumChannels, newBitsPerSample, newValidBitsPerSample, SampleRate, newValueRepType, NumFrames);
            newPcmData.SetSampleArray(newSampleArray);

            return newPcmData;
        }

        private byte[] ConvI16toI24(byte[] from) {
            int nSample = from.Length/2;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }
        private byte[] ConvI16toI32(byte[] from) {
            int nSample = from.Length/2;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI24toI32(byte[] from) {
            int nSample = from.Length/3;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                // 下位ビットは、0埋めする。
                to[toPos++] = 0;

                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
                to[toPos++] = from[fromPos++];
            }
            return to;
        }

        private byte[] ConvI24toI16(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 3;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                int [] err = new int[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch=0; ch < NumChannels; ++ch) {
                        int v = (short)(from[fromPos + 1] + (from[fromPos + 2] << 8));

                        v += err[ch]>>8;
                        if (32767 < v) {
                            v = 32767;
                        }
                        err[ch] -= ((err[ch])>>8)<<8;

                        err[ch] += from[fromPos];

                        to[toPos++] = (byte)(v&0xff);
                        to[toPos++] = (byte)((v>>8) & 0xff);

                        fromPos += 3;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    // 下位ビットの情報を切り捨てる。
                    ++fromPos;

                    to[toPos++] = from[fromPos++];
                    to[toPos++] = from[fromPos++];
                }
            }
            return to;
        }

        private byte[] ConvI32toI16(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                int [] err = new int[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch=0; ch < NumChannels; ++ch) {
                        int v = (short)(from[fromPos + 2] + (from[fromPos + 3] << 8));

                        v += err[ch] >> 16;
                        if (32767 < v) {
                            v = 32767;
                        }
                        err[ch] -= ((err[ch]) >> 16) << 16;

                        err[ch] += from[fromPos] + (from[fromPos + 1] << 8);

                        to[toPos++] = (byte)(v & 0xff);
                        to[toPos++] = (byte)((v >> 8) & 0xff);

                        fromPos += 4;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    // 下位ビットの情報を切り捨てる。
                    ++fromPos;
                    ++fromPos;

                    to[toPos++] = from[fromPos++];
                    to[toPos++] = from[fromPos++];
                }
            }
            return to;
        }

        private byte[] ConvI32toI24(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                int [] err = new int[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch=0; ch < NumChannels; ++ch) {
                        int v = (from[fromPos + 1] << 8) + (from[fromPos + 2] << 16) + (from[fromPos + 3] << 24);
                        v >>= 8;

                        v += err[ch]>>8;
                        if (8388607 < v) {
                            v = 8388607;
                        }
                        err[ch] -= ((err[ch]) >> 8) << 8;

                        err[ch] += from[fromPos];

                        to[toPos++] = (byte)(v & 0xff);
                        to[toPos++] = (byte)((v >> 8) & 0xff);
                        to[toPos++] = (byte)((v >> 16) & 0xff);

                        fromPos += 4;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    // 下位ビットの情報を切り捨てる。
                    ++fromPos;

                    to[toPos++] = from[fromPos++];
                    to[toPos++] = from[fromPos++];
                    to[toPos++] = from[fromPos++];
                }
            }

            return to;
        }

        ////////////////////////////////////////////////////////////////////
        // Clipped counter used for float to int conversion

        private static long mClippedCount;

        private static void IncrementClippedCounter() {
            Interlocked.Increment(ref mClippedCount);
        }

        /// <summary>
        /// clear clipped counter. call before conv start
        /// </summary>
        public static void ClearClippedCounter() {
            mClippedCount = 0;
        }

        /// <summary>
        /// read latest clipped counter value
        /// </summary>
        /// <returns>clipped counter</returns>
        public static long ReadClippedCounter() {
            return Thread.VolatileRead(ref mClippedCount);
        }

        ////////////////////////////////////////////////////////////////////
        // F32

        private static readonly float SAMPLE_VALUE_MAX_FLOAT_TO_I16 = 32767.0f / 32768.0f;
        private static readonly float SAMPLE_VALUE_MAX_FLOAT_TO_I24 = 8388607.0f / 8388608.0f; //< 0x3f7ffffe

        private byte[] ConvF32toI16(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                float [] err = new float[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch=0; ch < NumChannels; ++ch) {
                        float fv = System.BitConverter.ToSingle(from, fromPos);
                        if (SAMPLE_VALUE_MAX_FLOAT <= fv) {
                            fv = SAMPLE_VALUE_MAX_FLOAT_TO_I16;
                            IncrementClippedCounter();
                        }
                        if (fv < SAMPLE_VALUE_MIN_FLOAT) {
                            fv = SAMPLE_VALUE_MIN_FLOAT;
                            IncrementClippedCounter();
                        }
                        float fScale = fv * 32768.0f + err[ch];
                        int iv = (int)fScale;
                        err[ch] = fScale - (float)iv;

                        to[toPos++] = (byte)(iv & 0xff);
                        to[toPos++] = (byte)((iv >> 8) & 0xff);
                        fromPos += 4;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    float fv = System.BitConverter.ToSingle(from, fromPos);
                    if (SAMPLE_VALUE_MAX_FLOAT <= fv) {
                        fv = SAMPLE_VALUE_MAX_FLOAT_TO_I16;
                        IncrementClippedCounter();
                    }
                    if (fv < SAMPLE_VALUE_MIN_FLOAT) {
                        fv = SAMPLE_VALUE_MIN_FLOAT;
                        IncrementClippedCounter();
                    }
                    int iv = (int)(fv * 32768.0f);

                    to[toPos++] = (byte)(iv & 0xff);
                    to[toPos++] = (byte)((iv >> 8) & 0xff);
                    fromPos += 4;
                }
            }
            return to;
        }

        private byte[] ConvF32toI24(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos   = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                if (SAMPLE_VALUE_MAX_FLOAT <= fv) {
                    fv = SAMPLE_VALUE_MAX_FLOAT_TO_I24;
                    IncrementClippedCounter();
                }
                if (fv < SAMPLE_VALUE_MIN_FLOAT) {
                    fv = SAMPLE_VALUE_MIN_FLOAT;
                    IncrementClippedCounter();
                }
                int iv = (int)(fv * 8388608.0f);

                to[toPos++] = (byte)(iv & 0xff);
                to[toPos++] = (byte)((iv>>8) & 0xff);
                to[toPos++] = (byte)((iv>>16) & 0xff);
                fromPos += 4;
            }
            return to;
        }

        private byte[] ConvF32toI32(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos   = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                if (SAMPLE_VALUE_MAX_FLOAT <= fv) {
                    fv = SAMPLE_VALUE_MAX_FLOAT_TO_I24;
                    IncrementClippedCounter();
                }
                if (fv < SAMPLE_VALUE_MIN_FLOAT) {
                    fv = SAMPLE_VALUE_MIN_FLOAT;
                    IncrementClippedCounter();
                }
                int iv = (int)(fv * 8388608.0f);

                to[toPos++] = 0;
                to[toPos++] = (byte)(iv & 0xff);
                to[toPos++] = (byte)((iv>>8) & 0xff);
                to[toPos++] = (byte)((iv>>16) & 0xff);
                fromPos += 4;
            }
            return to;
        }

        private byte[] ConvI16toF32(byte[] from) {
            int nSample = from.Length / 2;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                short iv = (short)(from[fromPos]
                    + (from[fromPos+1]<<8));
                float fv = ((float)iv) * (1.0f / 32768.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 2;
            }
            return to;
        }

        private byte[] ConvI24toF32(byte[] from) {
            int nSample = from.Length / 3;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos]<<8)
                    + ((int)from[fromPos+1]<<16)
                    + ((int)from[fromPos+2]<<24);
                float fv = ((float)iv) * (1.0f / 2147483648.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 3;
            }
            return to;
        }

        private byte[] ConvI32toF32(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos+1]<<8)
                    + ((int)from[fromPos+2]<<16)
                    + ((int)from[fromPos+3]<<24);
                float fv = ((float)iv) * (1.0f / 2147483648.0f);

                byte [] b = System.BitConverter.GetBytes(fv);

                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 4;
            }
            return to;
        }

        ////////////////////////////////////////////////////////////////
        // F64

        private static readonly double SAMPLE_VALUE_MAX_DOUBLE_TO_I16 = 32767.0 / 32768.0;
        private static readonly double SAMPLE_VALUE_MAX_DOUBLE_TO_I24 = 8388607.0 / 8388608.0;

        private byte[] ConvF64toI16(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 8;
            byte[] to = new byte[nSample * 2];
            int fromPos = 0;
            int toPos = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                double [] err = new double[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch = 0; i < NumChannels; ++ch) {
                        double dv = System.BitConverter.ToDouble(from, fromPos);
                        if (SAMPLE_VALUE_MAX_DOUBLE <= dv) {
                            dv = SAMPLE_VALUE_MAX_DOUBLE_TO_I16;
                            IncrementClippedCounter();
                        }
                        if (dv < SAMPLE_VALUE_MIN_DOUBLE) {
                            dv = SAMPLE_VALUE_MIN_DOUBLE;
                            IncrementClippedCounter();
                        }
                        double dScale = dv * 32768.0f + err[ch];
                        int iv = (int)dScale;
                        err[ch] = dScale - (double)iv;

                        to[toPos++] = (byte)(iv & 0xff);
                        to[toPos++] = (byte)((iv >> 8) & 0xff);
                        fromPos += 8;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    double dv = System.BitConverter.ToDouble(from, fromPos);
                    if (SAMPLE_VALUE_MAX_DOUBLE <= dv) {
                        dv = SAMPLE_VALUE_MAX_DOUBLE_TO_I16;
                        IncrementClippedCounter();
                    }
                    if (dv < SAMPLE_VALUE_MIN_DOUBLE) {
                        dv = SAMPLE_VALUE_MIN_DOUBLE;
                        IncrementClippedCounter();
                    }
                    int iv = (int)(dv * 32768.0);

                    to[toPos++] = (byte)(iv & 0xff);
                    to[toPos++] = (byte)((iv >> 8) & 0xff);
                    fromPos += 8;
                }
            }
            return to;
        }

        private byte[] ConvF64toI24(byte[] from, bool noiseShaping) {
            int nSample = from.Length / 8;
            byte[] to = new byte[nSample * 3];
            int fromPos = 0;
            int toPos   = 0;

            if (noiseShaping) {
                int nFrame = nSample / NumChannels;
                double [] err = new double[NumChannels];

                for (int i = 0; i < nFrame; ++i) {
                    for (int ch = 0; i < NumChannels; ++ch) {
                        double dv = System.BitConverter.ToDouble(from, fromPos);
                        if (SAMPLE_VALUE_MAX_DOUBLE <= dv) {
                            dv = SAMPLE_VALUE_MAX_DOUBLE_TO_I24;
                            IncrementClippedCounter();
                        }
                        if (dv < SAMPLE_VALUE_MIN_DOUBLE) {
                            dv = SAMPLE_VALUE_MIN_DOUBLE;
                            IncrementClippedCounter();
                        }
                        double dScale = dv * 8388608.0 + err[ch];
                        int iv = (int)dScale;
                        err[ch] = dScale - (double)iv;

                        to[toPos++] = (byte)(iv & 0xff);
                        to[toPos++] = (byte)((iv >> 8) & 0xff);
                        to[toPos++] = (byte)((iv >> 16) & 0xff);
                        fromPos += 8;
                    }
                }
            } else {
                for (int i = 0; i < nSample; ++i) {
                    double dv = System.BitConverter.ToDouble(from, fromPos);
                    if (SAMPLE_VALUE_MAX_DOUBLE <= dv) {
                        dv = SAMPLE_VALUE_MAX_DOUBLE_TO_I24;
                        IncrementClippedCounter();
                    }
                    if (dv < SAMPLE_VALUE_MIN_DOUBLE) {
                        dv = SAMPLE_VALUE_MIN_DOUBLE;
                        IncrementClippedCounter();
                    }
                    int iv = (int)(dv * 8388608.0);
                    to[toPos++] = (byte)(iv & 0xff);
                    to[toPos++] = (byte)((iv >> 8) & 0xff);
                    to[toPos++] = (byte)((iv >> 16) & 0xff);
                    fromPos += 8;
                }
            }
            return to;
        }

        private byte[] ConvF64toI32(byte[] from) {
            int nSample = from.Length / 8;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos   = 0;
            for (int i = 0; i < nSample; ++i) {
                double dv = System.BitConverter.ToDouble(from, fromPos);

                int iv = 0;
                if ((long)Int32.MaxValue < (long)(dv * Int32.MaxValue)) {
                    iv = Int32.MaxValue;
                    IncrementClippedCounter();
                } else if ((long)(-dv * Int32.MinValue) < (long)Int32.MinValue) {
                    iv = Int32.MinValue;
                    IncrementClippedCounter();
                } else {
                    iv = (int)(-dv * Int32.MinValue);
                }

                to[toPos++] = (byte)((iv >> 0) & 0xff);
                to[toPos++] = (byte)((iv >> 8)  & 0xff);
                to[toPos++] = (byte)((iv >> 16) & 0xff);
                to[toPos++] = (byte)((iv >> 24) & 0xff);
                fromPos += 8;
            }
            return to;
        }

        private byte[] ConvF64toF32(byte[] from) {
            int nSample = from.Length / 8;
            byte[] to = new byte[nSample * 4];
            int fromPos = 0;
            int toPos   = 0;

            for (int i = 0; i < nSample; ++i) {
                double dv = System.BitConverter.ToDouble(from, fromPos);
                float fv = (float)dv;
                if (SAMPLE_VALUE_MAX_FLOAT <= fv) {
                    fv = SAMPLE_VALUE_MAX_FLOAT_TO_I24;
                    IncrementClippedCounter();
                }
                if (fv < SAMPLE_VALUE_MIN_FLOAT) {
                    fv = SAMPLE_VALUE_MIN_FLOAT;
                    IncrementClippedCounter();
                }
                byte [] b = System.BitConverter.GetBytes(fv);
                to[toPos++] = b[0];
                to[toPos++] = b[1];
                to[toPos++] = b[2];
                to[toPos++] = b[3];
                fromPos += 8;
            }
            return to;
        }

        private byte[] ConvI16toF64(byte[] from) {
            int nSample = from.Length / 2;
            byte[] to = new byte[nSample * 8];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                short iv = (short)(from[fromPos]
                    + (from[fromPos + 1] << 8));
                double dv = ((double)iv) * (1.0 / 32768.0);

                byte [] b = System.BitConverter.GetBytes(dv);

                for (int j=0; j < 8; ++j) {
                    to[toPos++] = b[j];
                }
                fromPos += 2;
            }
            return to;
        }

        private byte[] ConvI24toF64(byte[] from) {
            int nSample = from.Length / 3;
            byte[] to = new byte[nSample * 8];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos] << 8)
                    + ((int)from[fromPos + 1] << 16)
                    + ((int)from[fromPos + 2] << 24);
                double dv = ((double)iv) * (1.0 / 2147483648.0);

                byte [] b = System.BitConverter.GetBytes(dv);

                for (int j=0; j < 8; ++j) {
                    to[toPos++] = b[j];
                }
                fromPos += 3;
            }
            return to;
        }

        private byte[] ConvI32toF64(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 8];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                int iv = ((int)from[fromPos + 1] << 8)
                    + ((int)from[fromPos + 2] << 16)
                    + ((int)from[fromPos + 3] << 24);
                double dv = ((double)iv) * (1.0 / 2147483648.0);

                byte [] b = System.BitConverter.GetBytes(dv);

                for (int j=0; j < 8; ++j) {
                    to[toPos++] = b[j];
                }
                fromPos += 4;
            }
            return to;
        }

        private byte[] ConvF32toF64(byte[] from) {
            int nSample = from.Length / 4;
            byte[] to = new byte[nSample * 8];
            int fromPos = 0;
            int toPos = 0;
            for (int i = 0; i < nSample; ++i) {
                float fv = System.BitConverter.ToSingle(from, fromPos);
                double dv = (double)fv;

                byte [] b = System.BitConverter.GetBytes(dv);
                for (int j=0; j < 8; ++j) {
                    to[toPos++] = b[j];
                }
                fromPos += 4;
            }
            return to;
        }
    }
}
