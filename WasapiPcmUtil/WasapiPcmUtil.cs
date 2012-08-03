using Wasapi;
using PcmDataLib;

namespace WasapiPcmUtil {
    public enum RenderThreadTaskType {
        None,
        Audio,
        ProAudio,
        Playback
    };

    public enum BitsPerSampleFixType {
        Variable,
        Sint16,
        Sint32,
        Sfloat32,
        Sint24,

        Sint32V24,
        VariableSint16Sint24,
        VariableSint16Sint32V24,
        AutoSelect
    }

    public enum WasapiSharedOrExclusive {
        Shared,
        Exclusive
    };

    public enum WasapiDataFeedMode {
        EventDriven,
        TimerDriven
    };

    public struct SampleFormatInfo {
        public int bitsPerSample;
        public int validBitsPerSample;
        public WasapiCS.BitFormatType bitFormatType;

        static public WasapiCS.BitFormatType
        VrtToBft(PcmDataLib.PcmData.ValueRepresentationType vrt) {
            switch (vrt) {
            case PcmDataLib.PcmData.ValueRepresentationType.SInt:
                return WasapiCS.BitFormatType.SInt;
            case PcmDataLib.PcmData.ValueRepresentationType.SFloat:
                return WasapiCS.BitFormatType.SFloat;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.BitFormatType.SInt;
            }
        }

        public WasapiCS.SampleFormatType GetSampleFormatType() {
            if (bitFormatType == WasapiCS.BitFormatType.SFloat) {
                System.Diagnostics.Debug.Assert(bitsPerSample == 32);
                System.Diagnostics.Debug.Assert(validBitsPerSample == 32);
                return WasapiCS.SampleFormatType.Sfloat;
            }

            switch (bitsPerSample) {
            case 16:
                return WasapiCS.SampleFormatType.Sint16;
            case 24:
                return WasapiCS.SampleFormatType.Sint24;
            case 32:
                if (validBitsPerSample == 24) {
                    return WasapiCS.SampleFormatType.Sint32V24;
                }
                return WasapiCS.SampleFormatType.Sint32;
            default:
                System.Diagnostics.Debug.Assert(false);
                return WasapiCS.SampleFormatType.Sint16;
            }
        }

        /// <summary>
        /// フォーマット設定から、
        /// Setup()に設定されうるビットフォーマットの候補の数を数えて戻す。
        /// </summary>
        /// <returns>Setup()に設定されうるビットフォーマットの候補の数</returns>
        static public int GetSetupSampleFormatCandidateNum(
                WasapiSharedOrExclusive sharedOrExclusive,
                BitsPerSampleFixType bitsPerSampleFixType,
                int pcmDataValidBitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType vrt) {
            if (bitsPerSampleFixType != BitsPerSampleFixType.AutoSelect ||
                pcmDataValidBitsPerSample == 16 ||
                (pcmDataValidBitsPerSample == 32 && vrt == PcmData.ValueRepresentationType.SInt) ||
                sharedOrExclusive == WasapiSharedOrExclusive.Shared) {
                return 1;
            }

            // AutoSelect 24bit 排他モードの場合Sint32V24とSint24を試す。
            return 2;
        }

        /// <summary>
        /// PcmDataの形式と、(共有・排他)、フォーマット固定設定から、
        /// デバイスに設定されるビットフォーマットを取得。
        /// 
        /// これは、内容的にテーブルなので、テーブルにまとめたほうが良い。
        /// </summary>
        /// <returns>デバイスに設定されるビットフォーマット</returns>
        static public SampleFormatInfo CreateSetupSampleFormat(
                WasapiSharedOrExclusive sharedOrExclusive,
                BitsPerSampleFixType bitsPerSampleFixType,
                int bitsPerSample,
                int validBitsPerSample,
                PcmDataLib.PcmData.ValueRepresentationType vrt,
                int candidateId) {
            SampleFormatInfo sf = new SampleFormatInfo();

            if (sharedOrExclusive == WasapiSharedOrExclusive.Shared) {
                // 共有モード
                sf.bitsPerSample = bitsPerSample;
                sf.validBitsPerSample = validBitsPerSample;
                sf.bitFormatType = SampleFormatInfo.VrtToBft(vrt);
                return sf;
            }

            // 排他モード
            switch (bitsPerSampleFixType) {
            case BitsPerSampleFixType.Sint16:
                sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                sf.bitsPerSample = 16;
                sf.validBitsPerSample = 16;
                break;
            case BitsPerSampleFixType.Sint24:
                sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                sf.bitsPerSample = 24;
                sf.validBitsPerSample = 24;
                break;
            case BitsPerSampleFixType.Sint32:
                sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                sf.bitsPerSample = 32;
                sf.validBitsPerSample = 32;
                break;
            case BitsPerSampleFixType.Sint32V24:
                sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                sf.bitsPerSample = 32;
                sf.validBitsPerSample = 24;
                break;
            case BitsPerSampleFixType.Sfloat32:
                sf.bitFormatType = WasapiCS.BitFormatType.SFloat;
                sf.bitsPerSample = 32;
                sf.validBitsPerSample = 32;
                break;
            case BitsPerSampleFixType.Variable:
                if (validBitsPerSample != 16) {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 32;
                    sf.validBitsPerSample = validBitsPerSample;
                } else {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 16;
                    sf.validBitsPerSample = 16;
                }
                break;
            case BitsPerSampleFixType.VariableSint16Sint32V24:
                if (validBitsPerSample != 16) {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 32;
                    sf.validBitsPerSample = validBitsPerSample;
                    if (24 < validBitsPerSample) {
                        sf.validBitsPerSample = 24;
                    }
                } else {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 16;
                    sf.validBitsPerSample = 16;
                }
                break;
            case BitsPerSampleFixType.VariableSint16Sint24:
                if (validBitsPerSample != 16) {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 24;
                    sf.validBitsPerSample = 24;
                } else {
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = 16;
                    sf.validBitsPerSample = 16;
                }
                break;
            case BitsPerSampleFixType.AutoSelect:
                if (validBitsPerSample == 16 ||
                    (validBitsPerSample == 32 && vrt == PcmData.ValueRepresentationType.SInt)) {
                    // Sint32や16の場合、1通りしか無い。
                    sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                    sf.bitsPerSample = validBitsPerSample;
                    sf.validBitsPerSample = validBitsPerSample;
                } else {
                    // Sint32V24とSint24を試す。
                    switch (candidateId) {
                    case 0:
                        sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                        sf.bitsPerSample = 32;
                        sf.validBitsPerSample = 24;
                        break;
                    case 1:
                        sf.bitFormatType = WasapiCS.BitFormatType.SInt;
                        sf.bitsPerSample = 24;
                        sf.validBitsPerSample = 24;
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                    }
                }
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }

            return sf;
        }
    };

    public class PcmUtil {

        /// <summary>
        /// 量子化ビット数を、もし必要なら変更する。
        /// </summary>
        /// <param name="pd">入力PcmData</param>
        /// <returns>変更後PcmData</returns>
        static public PcmData BitsPerSampleConvAsNeeded(PcmData pd, WasapiCS.SampleFormatType fmt) {
            switch (fmt) {
            case WasapiCS.SampleFormatType.Sfloat:
                // System.Console.WriteLine("Converting to Sfloat32bit...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SFloat);
                pd.ValidBitsPerSample = 32;
                break;
            case WasapiCS.SampleFormatType.Sint16:
                // System.Console.WriteLine("Converting to SInt16bit...");
                pd = pd.BitsPerSampleConvertTo(16, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 16;
                break;
            case WasapiCS.SampleFormatType.Sint24:
                // System.Console.WriteLine("Converting to SInt24...");
                pd = pd.BitsPerSampleConvertTo(24, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 24;
                break;
            case WasapiCS.SampleFormatType.Sint32V24:
                // System.Console.WriteLine("Converting to SInt32V24...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 24;
                break;
            case WasapiCS.SampleFormatType.Sint32:
                // System.Console.WriteLine("Converting to SInt32bit...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 32;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            return pd;
        }

        /// <summary>
        /// 量子化ビット数を、もし必要なら変更する。
        /// </summary>
        /// <param name="pd">入力PcmData</param>
        /// <returns>変更後PcmData</returns>
        static public PcmData BitsPerSampleConvAsNeeded(PcmData pd, byte [] sampleArray, WasapiCS.SampleFormatType fmt) {
            switch (fmt) {
            case WasapiCS.SampleFormatType.Sfloat:
                // System.Console.WriteLine("Converting to Sfloat32bit...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SFloat);
                pd.ValidBitsPerSample = 32;
                break;
            case WasapiCS.SampleFormatType.Sint16:
                // System.Console.WriteLine("Converting to SInt16bit...");
                pd = pd.BitsPerSampleConvertTo(16, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 16;
                break;
            case WasapiCS.SampleFormatType.Sint24:
                // System.Console.WriteLine("Converting to SInt24...");
                pd = pd.BitsPerSampleConvertTo(24, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 24;
                break;
            case WasapiCS.SampleFormatType.Sint32V24:
                // System.Console.WriteLine("Converting to SInt32V24...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 24;
                break;
            case WasapiCS.SampleFormatType.Sint32:
                // System.Console.WriteLine("Converting to SInt32bit...");
                pd = pd.BitsPerSampleConvertTo(32, PcmData.ValueRepresentationType.SInt);
                pd.ValidBitsPerSample = 32;
                break;
            default:
                System.Diagnostics.Debug.Assert(false);
                break;
            }
            return pd;
        }
    }
}
