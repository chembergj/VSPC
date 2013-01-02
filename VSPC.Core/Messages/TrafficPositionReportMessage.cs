using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    public class TrafficPositionReportMessage: AVSPCMessage
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Groundspeed { get; set; }
        public double Heading { get; set; }
        public double BankAngle { get; set; }
    }
}
