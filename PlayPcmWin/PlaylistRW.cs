namespace PlayPcmWin {

    class PlaylistTrackInfo {
        public string path;
        public string title;
        public int    trackId;   // TRACK 10 ==> 10
        public int    startTick; // *75 seconds 0: start of the file
        public int    endTick;   // -1: till the end of file

        public int    indexId;   // INDEX 00 ==> 0, INDEX 01 ==> 1
        public string performer;
        public bool readSeparatorAfter;

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

        public void CopyFrom(PlaylistTrackInfo rhs) {
            path = rhs.path;
            title = rhs.title;
            trackId = rhs.trackId;
            startTick = rhs.startTick;
            endTick = rhs.endTick;

            indexId = rhs.indexId;
            performer = rhs.performer;
            albumTitle = rhs.albumTitle;
            readSeparatorAfter = rhs.readSeparatorAfter;
        }
    }
    
    interface PlaylistReader {
        bool ReadFromFile(string path);
        PlaylistTrackInfo GetTrackInfo(int nth);
        int GetTrackInfoCount();
    }
}
