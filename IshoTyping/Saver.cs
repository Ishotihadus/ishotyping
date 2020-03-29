using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IshoTyping
{
    [Serializable()]
    public class Saver
    {

        private List<HighScoreData> _highscores;
        private byte[] _romajisetting;
        private double _fontsize;
        private double _volume;
        private bool _kpmswitch;
        private string _accesstoken;
        private string _accesstokensecret;
        private uint _experimentalvalue;
        private int _settingoffset;

        public List<HighScoreData> highscores
        {
            get { return _highscores; }
            set { _highscores = value; }
        }

        public byte[] romajisetting
        {
            get { return _romajisetting; }
            set { _romajisetting = value; }
        }

        public double fontsize
        {
            get { return _fontsize; }
            set { _fontsize = value; }
        }

        public double volume
        {
            get { return _volume; }
            set { _volume = value; }
        }

        public bool kpmswitch
        {
            get { return _kpmswitch; }
            set { _kpmswitch = value; }
        }

        public string accestoken
        {
            get { return _accesstoken; }
            set { _accesstoken = value; }
        }

        public string accesstokensecret
        {
            get { return _accesstokensecret; }
            set { _accesstokensecret = value; }
        }

        public uint experimentalvalue
        {
            get { return _experimentalvalue; }
            set { _experimentalvalue = value; }
        }

        public int settingoffset
        {
            get { return _settingoffset; }
            set { _settingoffset = value; }
        }

        public Saver(List<HighScoreData> highscores, byte[] romajisetting, double fontsize,
            double volume, bool kpmswitch, string accesstoken, string accesstokensecret, uint experimentalvalue, int settingoffset)
        {
            _highscores = highscores;
            _romajisetting = romajisetting;
            _fontsize = fontsize;
            _volume = volume;
            _kpmswitch = kpmswitch;
            _accesstoken = accesstoken;
            _accesstokensecret = accesstokensecret;
            _experimentalvalue = experimentalvalue;
            _settingoffset = settingoffset;
        }

    }
}
