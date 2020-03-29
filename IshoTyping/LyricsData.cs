using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace IshoTyping
{
    public class LyricsData
    {
        public string yomigana { get; set; }
        public string kanji { get; set; }

        public string typed { get; set; }

        public int begintime { get; set; }
        public int interval { get; set; }

        public int firsttime { get; set; }
        public int typingtime { get; set; }

        public List<int> misscursor { get; set; }
        public string remain { get; set; }

        public Color foregroundcolor { get; set; }
        public Color waitingcolor { get; set; }


        public LyricsData()
        {
        }

        public LyricsData(string yomigana, string kanji, int interval, int begintime)
        {
            this.yomigana = yomigana;
            this.kanji = kanji;
            this.interval = interval;
            this.begintime = begintime;
            foregroundcolor = Colors.Black;
            waitingcolor = Colors.SlateGray;
            this.typed = "";
            misscursor = new List<int>();
            remain = "";
        }

        public LyricsData(string yomigana, string kanji, int interval, int begintime, Color foregroundcolor, Color waitingcolor)
        {
            this.yomigana = yomigana;
            this.kanji = kanji;
            this.interval = interval;
            this.begintime = begintime;
            this.foregroundcolor = foregroundcolor;
            this.waitingcolor = waitingcolor;
            this.typed = "";
            misscursor = new List<int>();
            remain = "";
        }

        public int typedcount()
        {
            return typed.Length;
        }

        /// <summary>
        /// 実際に打った時のkpm
        /// </summary>
        /// <returns></returns>
        public double kpm()
        {
            return typed.Length * 60000 / typingtime;
        }

        public int endtime()
        {
            return begintime + interval;
        }

        public Boolean isuntypeline()
        {
            if (yomigana.Length == 0)
                return true;
            else
                return false;
        }

        public Boolean isunvisibleline()
        {
            if (kanji.Length == 0)
                return true;
            else
                return false;
        }

    }
}
