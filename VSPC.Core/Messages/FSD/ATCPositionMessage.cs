using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Core.Messages.FSD
{
    public enum FacilityType
    {
        OBS = 0,
        FSS = 1,
        DEL = 2,
        GND = 3,
        TWR = 4,
        APP = 5,
        CTR = 6
    };

    public enum AtcRating
    {
        ATC_UNKNOWN = -1,
        ATC_NOTALLOWED = 0, /// No use in FSD yet
        ATC_OBS = 1, /// Observer
        ATC_S1 = 2, /// Student 1
        ATC_S2 = 3, /// Student 2
        ATC_S3 = 4, /// Student 3
        ATC_C1 = 5, /// Controller 1
        ATC_C2 = 6, /// Controller 2
        ATC_C3 = 7, /// Controller 3
        ATC_I1 = 8, /// Instructor 1
        ATC_I2 = 9, /// Instructor 2
        ATC_I3 = 10 /// Instructor 3
    };

	public class ATCPositionMessage: AFSDMessage
	{
        public string Callsign { get; set; }
        public int Frequency { get; set; }
        public FacilityType Facilitytype { get; set; }
        public int VisualRange { get; set; }
        public AtcRating Rating { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Altitude { get; set; }

        public override string ToString()
        {
            return string.Format("ATCPos: {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}", Callsign, Frequency, Facilitytype, VisualRange, Rating, Latitude, Longitude, Altitude);
        }
	}
}
