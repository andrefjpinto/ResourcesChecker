using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResourcesChecker.Models
{
    public class FilePath
    {
        public string Path { get; set; }
        public bool IsJavascript { get; set; }
        public string Content { get; set; }
    }
}
