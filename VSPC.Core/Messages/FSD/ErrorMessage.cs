using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FSD
{
    public class ErrorMessage: AFSDMessage
    {
        public int ErrorCode { get; set; }
        public string ErrorInfo { get; set; }
        public string ErrorText { get; set; }
    }
}
