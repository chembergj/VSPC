using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FSD
{
    public class InfoRequestMessage: AFSDMessage
    {
        public const string reqTypeCapabilities = "CAPS";
        public const string reqTypeRealname = "RN";
       
        public string RequestType { get; set; }
    }
}
