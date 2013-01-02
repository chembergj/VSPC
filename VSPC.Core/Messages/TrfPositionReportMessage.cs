using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    /// <summary>
    /// Reporting of other traffic nearby online traffics position, speed, alt etc.
    /// </summary>
    public class TrafficPositionReportMessage: AMessage
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Groundspeed { get; set; }
        public double Heading { get; set; }
        public double BankAngle { get; set; }
    }
}
