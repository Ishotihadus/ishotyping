using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IshoTyping
{
    public class MusicList
    {
        public string name { get; set; }
        public string artist { get; set; }
        public string genre { get; set; }
        public string xmlpath { get; set; }
        public string musicpath { get; set; }

        public ushort level { get; set; }
        public string length { get; set; }

        public MusicList() { }

        public MusicList(string name, string artist, string genre, string xmlpath, string musicpath)
        {
            this.name = name;
            this.artist = artist;
            this.genre = genre;
            this.xmlpath = xmlpath;
            this.musicpath = musicpath;
        }

        public MusicList(string name, string artist, string genre, string xmlpath, string musicpath, ushort level, string length)
        {
            this.name = name;
            this.artist = artist;
            this.genre = genre;
            this.xmlpath = xmlpath;
            this.musicpath = musicpath;
            this.level = level;
            this.length = length;
        }
    }
}
