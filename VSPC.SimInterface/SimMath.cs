using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.SimInterface
{
    public class Waypoint
    {
        public double Latitude { get; set; }  // (PLANE LATITUDE, degrees) north positive
        public double Longitude { get; set; } // (PLANE LONGITUDE, degrees) east positive
        public double Altitude { get; set; }  // (PLANE ALTITUDE, Meters) 
        public double Pitch { get; set; }     // (PLANE PITCH DEGREES, radians)
        public double Bank { get; set; }      // (PLANE BANK DEGREES, radians)
        public double Heading { get; set; }   // (PLANE HEADING DEGREES TRUE, radians)
        public DateTime Timestamp { get; set; }
        public Int32 zulu_time { get; set; }         // (ZULU TIME, seconds) seconds since midnight UTC
        public double IASpeed { get; set; }     // forward speed, meters per second
        public double GroundSpeed { get; set; }     // forward speed, meters per second
    };


    /// <summary>
    /// Credits goes to Ian Forster-Lewis www.forsterlewis.com, who did all the hard math work
    /// </summary>
    public class SimMath
    {
        const double M_PI = Math.PI;
        const double EARTH_RAD = 6366710.0; // earth's radius in meters

        const double ini_pitch_offset = 0; // pitch adjustment to apply to AI aircraft
        const double ini_pitch_min = -0.3; // max low-speed pitch in radians (negative)
        const double ini_pitch_max = 0.1; // max high-speed pitch in radians (positive) 
        const double ini_pitch_v_zero = 30; // speed in m/s for pitch=0;
        const uint MINIMUM_GS_WITHOUT_RATELIMITS = 5;   // Below this speed, slew rates will be reduced

        #region Conversion methods

        public static double ConvertKnotsToMetersPerSecond(double speedKts)
        {
            return speedKts * 0.514444444;
        }


        // convert radians at the center of the earth to meters on the surface
        public static double rad2m(double rad)
        {
            return EARTH_RAD * rad;
        }

        // convert metres on earths surface to radians subtended at the centre
        public static double m2rad(double distance)
        {
            return distance / EARTH_RAD;
        }

        // convert degrees to radians
        public static double deg2rad(double deg)
        {
            return deg * (M_PI / 180.0);
        }

        // convert radians to degrees
        public static double rad2deg(double rad)
        {
            return rad * (180.0 / M_PI);
        }

        // convert meters to feet (for initposition)
        public static double m2ft(double m)
        {
            return m * 3.2808399;
        }

        #endregion


        // bearing (radians) from point 1 to point 2
        public static double bearing(double lat1d, double lon1d, double lat2d, double lon2d)
        {

            double lat1 = deg2rad(lat1d);
            double lon1 = deg2rad(lon1d);
            double lat2 = deg2rad(lat2d);
            double lon2 = deg2rad(lon2d);
            return (Math.Atan2(Math.Sin(lon2 - lon1) * Math.Cos(lat2), Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(lon2 - lon1)) + 2 * M_PI) %
                                (2 * M_PI);
        }

        // distance (m) on earth's surface from point 1 to point 2
        public static double distance(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1r = deg2rad(lat1);
            double lon1r = deg2rad(lon1);
            double lat2r = deg2rad(lat2);
            double lon2r = deg2rad(lon2);
            return Math.Acos(Math.Sin(lat1r) * Math.Sin(lat2r) + Math.Cos(lat1r) * Math.Cos(lat2r) * Math.Cos(lon2r - lon1r)) * EARTH_RAD;
        }

        // gives average bearing of middle point between three points
        // which gives a 'target' heading for the ai object to be at when it arrives at middle point
        public static double target_heading(double lat1, double lon1, double lat2, double lon2, double lat3, double lon3)
        {
            double avg_lat1 = (lat1 + lat2) / 2;
            double avg_lon1 = (lon1 + lon2) / 2;
            double avg_lat2 = (lat2 + lat3) / 2;
            double avg_lon2 = (lon2 + lon3) / 2;

            return bearing(avg_lat1, avg_lon1, avg_lat2, avg_lon2);
        }

        // +ve/-ve difference between two headings
        public static double heading_delta(double desired, double current)
        {
            double angle;
            angle = (desired - current + 2 * M_PI) % (2 * M_PI);
            return (angle > M_PI) ? angle - 2 * M_PI : angle;
        }

        //*********************************************************************************************
        // interp code - inject more ReplayPoints where IGC file timestep > 5  seconds
        //*********************************************************************************************

        // distance_and_bearing(...) returns a new lat/long a distance and bearing from lat1,lon1.
        // lat, longs in degrees, rbearng in radians, distance in meters
        public static Waypoint distance_and_bearing(Waypoint p, double distance, double rbearing)
        {
            double rlat1, rlong1, rdistance, rlat2, rlong2;
            Waypoint r = new Waypoint();
            rlat1 = deg2rad(p.Latitude);
            rlong1 = deg2rad(p.Longitude);
            rdistance = m2rad(distance);
            rlat2 = Math.Asin(Math.Sin(rlat1) * Math.Cos(rdistance) + Math.Cos(rlat1) * Math.Sin(rdistance) * Math.Cos(rbearing));
            if (Math.Cos(rlat2) == 0)
            {
                rlong2 = rlong1;      // endpoint a pole
            }
            else
            {
                rlong2 = ((rlong1 + Math.Asin(Math.Sin(rbearing) * Math.Sin(rdistance) / Math.Cos(rlat2)) + M_PI) % (2 * M_PI)) - M_PI;
            }
            r.Latitude = rad2deg(rlat2);
            r.Longitude = rad2deg(rlong2);

            return r;
        }

        // interp returns a RelayPoint step_time after point p1
        // with time/lat/lon/alt only
        public static Waypoint interp(Waypoint p0, Waypoint p1, Waypoint p2, Waypoint p3, Int32 step_time)
        {

            double correction_coefficient = 0.17;
            Int32 time_delta = p2.zulu_time - p1.zulu_time;

            //bearings in radians
            double bearing0 = bearing(p0.Latitude, p0.Longitude, p1.Latitude, p1.Longitude);
            double bearing1 = bearing(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);
            double bearing2 = bearing(p2.Latitude, p2.Longitude, p3.Latitude, p3.Longitude);

            double bearing_delta1 = heading_delta(bearing0, bearing1);
            double bearing_delta2 = heading_delta(bearing1, bearing2);

            double total_turn1 = bearing_delta1 + bearing_delta2;

            double heading_correction1 = correction_coefficient * total_turn1;

            double distance1 = distance(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);

            double speed1 = distance1 / time_delta;

            double new_heading1 = bearing1 + heading_correction1;
            double new_heading_delta1 = heading_delta(bearing1, new_heading1);
            double speed_correction1 = (1 + (1 - 2 / M_PI) * Math.Abs(new_heading_delta1));
            double distance_to_interp1 = speed1 * step_time * speed_correction1;

            Waypoint r = distance_and_bearing(p1, distance_to_interp1, new_heading1);
            r.zulu_time = p1.zulu_time + step_time;
            r.Altitude = p1.Altitude + ((double)step_time / (double)time_delta) * (p2.Altitude - p1.Altitude);
            return r;
        }

        //*********************************************************************************************
        // slew calibration functions

        public static uint slew_rotation_to_rate(double rotation, double current_groundspeed)
        { // rotation in radians / second
            // +ve rotate to port, port wing down, nose down
            // rotation rad/s = rate ^ 2 / 11240000

            var rate = (rotation < 0) ? (uint)-Math.Sqrt(-rotation * 11240000) : (uint)Math.Sqrt(rotation * 11240000);

            
            // reduce rates depending on the current groundspeed
            // without a limit, the plane makes rather hard turns/banks/pitches at low speeds

            if (current_groundspeed < MINIMUM_GS_WITHOUT_RATELIMITS)
            {
                rate = Math.Min(rate, 100);
                rate = (uint)Math.Max((int)rate, -100);
            }

            return rate;
        }

        public static uint slew_ahead_to_rate(double speed)
        { // speed in meters per second
            // +ve forwards
            // speed m/s = rate ^ 2 / 45678
            return (speed < 0) ? (uint)-Math.Sqrt(-speed * 45678) : (uint)Math.Sqrt(speed * 45678);
        }

        public static uint slew_alt_to_rate(double sink)
        {
            // +ve downwards
            return (sink < 0) ? (uint)-Math.Sqrt(-sink * 3084000) : (uint)Math.Sqrt(sink * 3084000);
        }

        //*********************************************************************************************
        // which heading should object be at to approach on correct target heading
        public static double desired_heading(double bearing_to_wp, double target_heading)
        {
            double heading;
            double coefficient;

            coefficient = 0.5;

            heading = bearing_to_wp - coefficient * heading_delta(target_heading, bearing_to_wp);
            heading = heading + 2 * M_PI;

            return heading % (2 * M_PI);
        }

        //*********************************************************************************************
        // calculate appropriate pitch based on speed and predict point
        // +ve pitch is nose DOWN
        public static double desired_pitch(double alt_delta, double dist, double time)
        {
            double zdist; // 3d diagonal distance for proper speed
            double speed;
            double slope_pitch;
            double speed_pitch;
            double pitch;

            //double coefficient;
            // check for safe values - worst case we can always give a pitch of zero
            if (time < 0.1) return 0;
            if (dist < 0.1) return 0;
            //coefficient = 0.35;

            zdist = Math.Sqrt(Math.Pow(alt_delta, 2) + Math.Pow(dist, 2));
            speed = zdist / time;

            // get pitch values due to (i) slope and (ii) speed and combine them
            slope_pitch = -Math.Atan(alt_delta / dist);

            //const double PITCH_MIN = -0.3; // pitch at min speed i.e. max pitch nose UP
            //const double PITCH_MAX = 0.18; // pitch at max speed

            double C = -2 * ini_pitch_min / M_PI;
            double X = Math.Tan(ini_pitch_max / C);

            speed_pitch = C * Math.Atan(X * (1 - ini_pitch_v_zero / speed)) + ini_pitch_offset;
            //speed_pitch = coefficient * atan ( 900 / pow(speed,2) - 1 );

            pitch = slope_pitch + speed_pitch;
            pitch = Math.Min(pitch, 1.5);
            pitch = Math.Max(pitch, -1.5);
            return pitch;
        }

        // what rate should object change heading at
        public static uint slew_turn_rate(double bearing_to_wp, double current_heading, double target_heading, double current_groundspeed)
        {
            double desired;
            double coefficient;

            coefficient = 0.65;

            desired = desired_heading(bearing_to_wp, target_heading);

            // note minus in front of coefficient - +ve turn reduces heading!
            return slew_rotation_to_rate(-coefficient * heading_delta(desired, current_heading), current_groundspeed);
        }


        // what rate to set ahead slew should object move to arrive at correct time
        public static uint slew_ahead_rate(double lat1, double lon1, double lat2, double lon2, double time_to_go)
        {
            double speed = distance(lat1, lon1, lat2, lon2) / time_to_go;
            return slew_ahead_to_rate(speed);
        }

        /*
        // calculate appropriate pitch/bank/heading/speed values for replaypoint[i]
        public static void ai_update_pbhs(Waypoint[] p, int i)
        {
            // pitch & speed
            if (i == 0) { p[i].Pitch = 0; p[i].Speed = 0; }
            else
            {
                double dist = distance(p[i - 1].Latitude,
                                              p[i - 1].Longitude,
                                              p[i].Latitude,
                                              p[i].Longitude);
                p[i].Pitch = desired_pitch(p[i].Altitude - p[i - 1].Altitude,
                                                dist,
                                                p[i].zulu_time - p[i - 1].zulu_time);
                p[i].Speed = dist / (p[i].zulu_time - p[i - 1].zulu_time);
            }

            // heading
            if (i == 1) p[0].Heading = bearing(p[0].Latitude, p[0].Longitude,
                                             p[1].Latitude, p[1].Longitude);
            else if (i > 1)
            {
                p[i - 1].Heading = target_heading(p[i - 2].Latitude, p[i - 2].Longitude,
                                             p[i - 1].Latitude, p[i - 1].Longitude,
                                             p[i].Latitude, p[i].Longitude);
                //if (debug) printf("updating p[%2d-%d] (%2.5f,%2.5f) p[%2d-%d].heading=%.3f\n",
                //	i, p[i].zulu_time,p[i].latitude, p[i].longitude,i-1,p[i-1].zulu_time,p[i-1].heading);
            }

            // bank
            if (i < 2) p[i].Bank = 0;
            else
            {
                double this_bearing = bearing(p[i - 1].Latitude,
                                              p[i - 1].Longitude,
                                              p[i].Latitude,
                                              p[i].Longitude);
                double prev_bearing = bearing(p[i - 2].Latitude,
                                              p[i - 2].Longitude,
                                              p[i - 1].Latitude,
                                              p[i - 1].Longitude);
                double bearing_delta = (this_bearing + 2 * M_PI - prev_bearing) % (2 * M_PI);
                if (bearing_delta > M_PI) bearing_delta = bearing_delta - 2 * M_PI;
                double turn_radians_per_second = bearing_delta / (p[i].zulu_time - p[i - 1].zulu_time);
                p[i].Bank = -turn_radians_per_second * 4;
                p[i].Bank = Math.Min(p[i].Bank, 1.5);
                p[i].Bank = Math.Max(p[i].Bank, -1.5);
            }
        }
        */
        public static bool AIAircraftIsParked(Waypoint currentWp, Waypoint newWp)
        {
            return distance(currentWp.Latitude, currentWp.Longitude, newWp.Latitude, newWp.Longitude) < 0.001;
        }
    }
}
