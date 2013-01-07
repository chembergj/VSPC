using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.Messages.FSD;

namespace VSPC.Core.Messages.FSD
{
    /// <summary>
    /// Reporting of other traffic nearby online traffics position, speed, alt etc.
    /// </summary>
    public class TrafficPositionReportMessage: AFSDMessage
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Groundspeed { get; set; }
        public double Heading { get; set; }
        public double BankAngle { get; set; }
        public int Pitch { get; set; }
    }
}
