using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FSD
{
    public class CommErrorMessage: AMessage
    {
        public string ErrorMessage { get; set; }
    }
}
