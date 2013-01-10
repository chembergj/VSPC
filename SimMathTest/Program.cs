using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.SimInterface;

namespace SimMathTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var currentWp = new Waypoint() 
            {
                Latitude = 47.43135000,
                Longitude = -122.30805000,
                Bank = 0,
                Altitude = 437.408073162284,
                Pitch = -0.00349065847694874,
                Heading = 0,
                GroundSpeed = 0
            };

            var newWp = new Waypoint() 
            {
                Latitude = 47.43140000,
                Longitude = -122.30806000,
                Bank = 0,
                Altitude = 436,
                Pitch = 0.0174532925199433,
                Heading = 6.19591884457987,
                GroundSpeed = 8
            };

            double bearing_to_wp = SimMath.bearing(currentWp.Latitude, currentWp.Longitude,
                                               newWp.Latitude, newWp.Longitude);

            uint heading_rate = SimMath.slew_turn_rate(bearing_to_wp, currentWp.Heading, newWp.Heading);

            var period = (newWp.Timestamp - currentWp.Timestamp).TotalSeconds;

            uint ahead_rate = SimMath.slew_ahead_rate(currentWp.Latitude, currentWp.Longitude,
                                           newWp.Latitude, newWp.Longitude,
                                           period);

            uint bank_rate = SimMath.slew_rotation_to_rate((newWp.Bank - currentWp.Bank) / period);

            uint pitch_rate = SimMath.slew_rotation_to_rate((newWp.Pitch - currentWp.Pitch) / period);

            uint alt_rate = SimMath.slew_alt_to_rate((currentWp.Altitude - newWp.Altitude) / period);

            //debug - print lat longs for excel analysis
            // time,lat,lon,alt,pitch,bank,heading,ahead rate, alt rate, pitch rate, bank rate, heading rate

            Console.WriteLine(string.Format("CURRENT WP: La {0}, Lo {1}, Alt {2}, Pi {3}, Ba {4}, Hdg {5}, GS: {6}",
                               currentWp.Latitude.ToString("####0.00000000"),
                               currentWp.Longitude.ToString("####0.00000000"),
                               currentWp.Altitude,
                               currentWp.Pitch,
                               currentWp.Bank,
                               currentWp.Heading,
                               currentWp.GroundSpeed
                               ));

            Console.WriteLine(string.Format("NEW WP: La {0}, Lo {1}, Alt {2}, Pi {3}, Ba {4}, Hdg {5}, GS {11}, Ahead rt {6}, Alt rt {7}, Pi rt {8}, Ba rt {9}, Hdg rt {10}",
                                newWp.Latitude.ToString("####0.00000000"),
                                newWp.Longitude.ToString("####0.00000000"),
                                newWp.Altitude,
                                newWp.Pitch,
                                newWp.Bank,
                                newWp.Heading,
                                ahead_rate,
                                alt_rate,
                                pitch_rate,
                                bank_rate,
                                heading_rate,
                                newWp.GroundSpeed
                                ));

        }
    }
}
