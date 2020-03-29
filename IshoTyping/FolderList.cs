using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IshoTyping
{
    public class FolderList
    {
        public string name { set; get; }
        public string directory;

        public FolderList() { }
        public FolderList(string name, string directory) {
            this.name = name;
            this.directory = directory;
        }
    }
}
