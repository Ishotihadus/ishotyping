using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IshoTyping
{
    [Serializable()]
    public class HighScoreData
    {
        public string xmlpath { get; set; }
        public string hashcode { get; set; } // これにはSHA256を適用

        public int points { get; set; }
        public int combo { get; set; }

        public int typedmillisecond { get; set; }
        public int firstmillisecond { get; set; }
        
        public int correct { get; set; }
        public int miss { get; set; }

        public int complete { get; set; }
        public int failed { get; set; }
        public int nothingfailed { get; set; }

        public DateTime dt { get; set; }


        public HighScoreData()
        {
        }

        public HighScoreData(string xmlpath, string hashcode)
        {
            this.xmlpath = xmlpath;
            this.hashcode = hashcode;
            dt = DateTime.Now;
        }

        public bool memory(int _points, int _combo, int _typed, int _first, int _correct, int _miss, int _complete, int _failed, int _nf)
        {
            if (_points < points)
            {
                return false;
            }
            else
            {
                points = _points;
                combo = _combo;
                typedmillisecond = _typed;
                firstmillisecond = _first;
                correct = _correct;
                miss = _miss;
                complete = _complete;
                failed = _failed;
                nothingfailed = _nf;
                dt = DateTime.Now;
                return true;
            }
        }

    }
}
