using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FLSIM
{
    public class FlightsimConnectedMessage: AMessage
    {
        public string ApplicationName { get; set; }
    }
}
