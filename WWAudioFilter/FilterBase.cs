using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WWAudioFilter {
    public enum FilterType {
        Gain
    }

    public class FilterBase {
        public FilterType FilterType { get; set; }

        private static int msFilterId = 0;

        public int FilterId { get; set; }

        public FilterBase(FilterType type) {
            FilterType = type;

            FilterId = msFilterId++;
        }

        public virtual string ToDescriptionText() {
            return "Do nothing.";
        }

        public virtual string ToSaveText() {
            return "";
        }
    }
}
