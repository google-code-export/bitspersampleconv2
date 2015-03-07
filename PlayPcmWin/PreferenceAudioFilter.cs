
namespace PlayPcmWin {
    /// <summary>
    /// WWAudioFilterTypeと同じ順番で並べる
    /// </summary>
    public enum PreferenceAudioFilterType {
        PolarityInvert,
        Monaural,

        NUM
    };

    public class PreferenceAudioFilter {
        public PreferenceAudioFilterType Type { get; set; }

        public PreferenceAudioFilter(PreferenceAudioFilterType t) {
            Type = t;
        }

        public override string ToString() {
            return Type.ToString();
        }

        public PreferenceAudioFilter Copy() {
            var p = new PreferenceAudioFilter(Type);

            // 個別フィルターパラメーターのコピーをすること。
            
            return p;
        }

        // ここに個別フィルターパラメーターを並べる。
    }
}

