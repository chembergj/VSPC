using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    /// <summary>
    /// Reporting of own position, speed and alt
    /// </summary>
    public class PositionReportMessage: AVSPCMessage
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Groundspeed { get; set; }

        public override string ToString()
        {
            return string.Format("Lat: {0:##.000000}  Long: {1:##.000000}  Alt: {2:#0000}  Groundspeed: {3:000}", Latitude, Longitude, Altitude, Groundspeed);
        }
    }
}
