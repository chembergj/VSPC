using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages
{
    /// <summary>
    /// Reporting of own position, speed and alt
    /// </summary>
    public class PositionReportMessage: AMessage
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Groundspeed { get; set; }
        public double Pitch { get; set; }
        public double Bank { get; set; }
        public double Heading { get; set; }
		public short Transponder { get; set; }
        public bool SquawkingCharlie { get; set; }
        public bool Identing { get; set; }


        public override string ToString()
        {
            return string.Format("PositionReportMessage Lat: {0:##.000000}  Long: {1:##.000000}  Alt: {2:#0000}  Groundspeed: {3:000} Pitch: {4}  Bank: {5}  Heading: {6}  Transponder: {7}", Latitude, Longitude, Altitude, Groundspeed, Pitch, Bank, Heading, Transponder);
        }
    }
}
