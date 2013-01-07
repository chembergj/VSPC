using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FlightSimulator.SimConnect;
using VSPC.Core.MessageHandlers;
using System.Runtime.InteropServices;
using NLog;
using VSPC.Core.Messages;
using VSPC.Core;
using VSPC.Core.Messages.UI;
using VSPC.Core.Messages.FLSIM;
using System.Windows.Interop;
using System.Windows;
using VSPC.Core.Messages.FSD;

namespace VSPC.SimInterface
{
    public class AIPlane
    {
        public AIPlane()
        {
            Waypoints = new Queue<Waypoint>();
        }

        public uint SimConnectObjectId { get; set; }
        public string Callsign { get; set; }
        public Waypoint CurrentWaypoint { get; set; }
        public Queue<Waypoint> Waypoints { get; set; }
    }

    public class SimConnectInterface : AVSPCMessageHandler
    {
        private static Logger logger;
        IntPtr hWnd;

        Logger Logger
        {
            get
            {
                if (logger == null)
                    logger = LogManager.GetCurrentClassLogger();

                return logger;
            }
        }

        enum DEFINITIONS
        {
            PositionReportStruct,
            PositionUpdateStruct,
            ATCIdStruct,
        }

        enum SIMCONNECT_EVENTS
        {
            EVENTID_POSITIONREPORT,
            EVENTID_AISETATCID,
            EVENTID_POSITIONREPORT_FOR_AIUPDATE,

            // Slew events
            EVENTID_SLEW_ON,
            EVENTID_AXIS_SLEW_HEADING_SET,
            EVENTID_AXIS_SLEW_ALT_SET,
            EVENTID_AXIS_SLEW_BANK_SET,
            EVENTID_AXIS_SLEW_PITCH_SET,
            EVENTID_AXIS_SLEW_AHEAD_SET,
            EVENTID_DEFINITION_AI_MOVE,

            EVENTID_SETAIAC = 10000,
        };

        enum GROUP_PRIORITIES : uint
        {
            SIMCONNECT_GROUP_PRIORITY_HIGHEST = 1,
            SIMCONNECT_GROUP_PRIORITY_HIGHEST_MASKABLE = 10000000,
            SIMCONNECT_GROUP_PRIORITY_STANDARD = 1900000000,
            SIMCONNECT_GROUP_PRIORITY_DEFAULT = 2000000000,
            SIMCONNECT_GROUP_PRIORITY_LOWEST = 4000000000
        }

        // Counter for keeping track of request id for new AI traffic
        uint AICounter = 0;
        object AICounterLock = 0;

        // Dictionary of AI objects not yet assigned a simconnect object id (mapping from callsign to AICounter)
        readonly Dictionary<uint, string> AICounterToCallsignMap = new Dictionary<uint, string>();

        // Dictionary of AI objects with an assigned simconnect object id
        readonly Dictionary<string, AIPlane> CallsignToAIPlaneMap = new Dictionary<string, AIPlane>();

        // Struct used for position reporting
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct PositionReportStruct
        {
            // this is how you declare a fixed size string
            public double latitude;
            public double longitude;
            public double altitude;
            public double groundspeed;
            public double pitch;
            public double bank;
            public double heading;
            // public short transponder;
        };

        // Struct used for sending data from position reports to SimConnect
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct PositionUpdateStruct
        {
            // this is how you declare a fixed size string
            public double latitude;
            public double longitude;
            public double altitude;
            public double heading;
            //public double pitch;
            //public double bank;

            //public short transponder;
            // public double groundspeed;
        };

        // Struct used for sending data from position reports to SimConnect
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct ATCIDStruct
        {
            public string ATCId;
        };


        // User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;

        double zulu_clock = 0.0; // this is sim_logger's version of FSX 'ZULU TIME'

        /* TO BE PLACED IN MAIN PROGRAM 
         * 
         *  protected override void DefWndProc(ref Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    simconnect.ReceiveMessage();
                }
            }
            else
            {
                base.DefWndProc(ref m);
            }
        }
         * */

        public override void Init(MessageBroker broker)
        {
            base.Init(broker);
            SetupWindowsMessaging();
            broker.Subscribe(this, typeof(UserClickedLoginMessage));
            broker.Subscribe(this, typeof(UserClickedLogoffMessage));
            broker.Subscribe(this, typeof(TrafficPositionReportMessage));
        }

        public override void HandleMessage(Core.Messages.AMessage message, VSPCContext context)
        {
            if (message is UserClickedLoginMessage)
                InitSimConnect(context);
            else if (message is UserClickedLogoffMessage)
                CloseConnection();
            else if (message is TrafficPositionReportMessage)
                HandleTrafficPositionReport((TrafficPositionReportMessage)message);
            else
                Logger.Error("Unexpected message type received in SimConnectInterface: " + message.GetType().Name);
        }

        private void HandleTrafficPositionReport(TrafficPositionReportMessage trafficPositionReportMessage)
        {
            AIPlane plane;
            if (CallsignToAIPlaneMap.TryGetValue(trafficPositionReportMessage.Sender, out plane))
            {
                UpdateExisitingAIData(trafficPositionReportMessage);
            }
            else
            {
                CreateNewAIAircraft(trafficPositionReportMessage);

            }
        }

        private void CreateNewAIAircraft(TrafficPositionReportMessage trafficPositionReportMessage)
        {
            uint counter;

            lock (AICounterLock)
            {
                counter = AICounter++;
            }

            AICounterToCallsignMap.Add(counter, trafficPositionReportMessage.Sender);

            try
            {
                // TODO: OnGround + airspeed
                var initpos = new SIMCONNECT_DATA_INITPOSITION()
                {
                    Altitude = trafficPositionReportMessage.Altitude,
                    Bank = -trafficPositionReportMessage.BankAngle,
                    Heading = trafficPositionReportMessage.Heading,
                    Latitude = trafficPositionReportMessage.Latitude,
                    Longitude = trafficPositionReportMessage.Longitude,
                    Pitch = -trafficPositionReportMessage.Pitch,
                    OnGround = 1,
                    Airspeed = 0
                };

                simconnect.AICreateSimulatedObject("Beech King Air 350 Paint1", initpos, (SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_SETAIAC + counter));

                var aiplane = new AIPlane()
                {
                    Callsign = trafficPositionReportMessage.Sender,
                    CurrentWaypoint = new Waypoint()
                    {
                        Altitude = trafficPositionReportMessage.Altitude,
                        Bank = -trafficPositionReportMessage.BankAngle,
                        Heading = trafficPositionReportMessage.Heading,
                        Latitude = trafficPositionReportMessage.Latitude,
                        Longitude = trafficPositionReportMessage.Longitude,
                        Pitch = -trafficPositionReportMessage.Pitch,
                        Timestamp = DateTime.Now
                    }
                };

                CallsignToAIPlaneMap.Add(aiplane.Callsign, aiplane);
            }
            catch (COMException e)
            {
                Logger.Error("SimConnectInterface.CreateNewAIAircraft: " + e);
            }
        }


     
        /*
        void move_ai(int ai_index, Waypoint r)
        {
            AIMoveStruct ai_move_data;
            ai_move_data.latitude = r.Latitude;
            ai_move_data.longitude = r.Longitude;
            ai_move_data.altitude = r.Altitude;
            ai_move_data.pitch = r.Pitch;
            ai_move_data.bank = r.Bank;
            ai_move_data.heading = r.Heading;

            Logger.Debug(string.Format("Moving ai({0}) to {1},{2}", ai_index, r.Latitude, r.Longitude));

            // set LLAPBH
            simconnect.SetDataOnSimObject(SIMCONNECT_EVENTS.EVENTID_DEFINITION_AI_MOVE, ai_info[ai_index].id, 0, ai_move_data);

            // send slew command to stop
            simconnect.TransmitClientEvent(ai_info[ai_index].id, SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_AHEAD_SET,
                                0, // zero ahead rate => stop
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
        }
        */
        /*
        void create_ai(int ai_index)
        {
            Logger.Debug(string.Format("Creating AI {0}", ai_info[ai_index].title));

            SIMCONNECT_DATA_INITPOSITION ai_init;

            ai_init.Altitude = SimMath.m2ft(replay[ai_index][0].altitude) + 10; // feet Altitude of Sea-tac is 433 feet
            ai_init.Latitude = SimMath.replay[ai_index][0].latitude;    // Degrees Convert from 47 25.90 N
            ai_init.Longitude = SimMath.replay[ai_index][0].longitude;   // Degrees Convert from 122 18.48 W
            ai_init.Pitch = SimMath.rad2deg(replay[ai_index][0].pitch);       // Degrees
            ai_init.Bank = SimMath.rad2deg(replay[ai_index][0].bank);        // Degrees
            ai_init.Heading = SimMath.rad2deg(replay[ai_index][0].heading);     // Degrees
            ai_init.OnGround = 0;                               // 1=OnGround, 0 = airborne
            ai_init.Airspeed = 0;                               // Knots

            // now create ai object
            if (!ai_info[ai_index].created) hr = SimConnect_AICreateSimulatedObject(hSimConnect,
                                                    ai_info[ai_index].title,
                                                    ai_init,
                                                    (UINT)REQUEST_AI_CREATE + ai_index);
            //if (debug) printf("create_ai %s\n", (hr==S_OK) ? "OK" : "FAIL");
        }
        */

        /// <summary>
        /// This is where we do the predict-point following stuff
        /// Called each time we get a traffic position report from the server
        /// </summary>
        /// <param name="msg"></param>
        private void UpdateExisitingAIData(TrafficPositionReportMessage msg)
        {
            const double PREDICT_PERIOD = 5; // predict replay position 5 seconds ahead
            const double AI_WARP_TIME = 30; // if current AI point is 30 seconds old, then MOVE not SLEW


            // TODO: is it necessary to get the actual AI position here?

            // Yes, if the diff. in time is too large

            var newWp = new Waypoint() { 
                Altitude = msg.Altitude, 
                Bank = SimMath.deg2rad(msg.BankAngle), 
                Heading = SimMath.deg2rad(msg.Heading), 
                Latitude = msg.Latitude, 
                Longitude = msg.Longitude, 
                Pitch = SimMath.deg2rad(msg.Pitch), 
                Speed = msg.Groundspeed, 
                Timestamp = DateTime.Now 
            };

            var AIAircraft = CallsignToAIPlaneMap[msg.Sender];
            AIAircraft.Waypoints.Enqueue(newWp);

            /*
            // test to see if zulu_time of current AI position is so old we should MOVE not SLEW - or perhaps get the actual AI position??
            if ((newWp.Timestamp - oldWp.Timestamp).TotalSeconds > AI_WARP_TIME)
            {
                Logger.Trace(string.Format("UpdateExisitingAIData: {0} MOVE due to missing trf.pos, old TS: {1}, new TS: {2}", AIAircraft.Callsign, oldWp.Timestamp, newWp.Timestamp));
            }
            else if(AIAircraft.Waypoints.Count > 1)
            {
                // Somehow, we have been slower than the trafficposition updates, what to do?
                Logger.Trace(string.Format("UpdateExisitingAIData: {0} no of waypoints in line > 1: {1}", AIAircraft.Callsign, AIAircraft.Waypoints.Count));
            }
            */

            simconnect.RequestDataOnSimObject(SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE, DEFINITIONS.PositionReportStruct, AIAircraft.SimConnectObjectId, SIMCONNECT_PERIOD.ONCE, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);
        }

        private void CalculateSlewAI(uint AIPlaneSimConnectId, PositionReportStruct posreport)
        {
            var AIAircraft = CallsignToAIPlaneMap.Where(keyvalue => keyvalue.Value.SimConnectObjectId == AIPlaneSimConnectId).First().Value;
            var currentWp = CreateWaypointFromPositionReportStruct(ref posreport, AIAircraft.CurrentWaypoint.Timestamp);
            var newWp = AIAircraft.Waypoints.Dequeue();

            /*
            double progress = 1;
            predict_point.Latitude = r[j - 1].latitude + progress * (r[j].latitude - r[j - 1].latitude);
            predict_point.Longitude = r[j - 1].longitude + progress * (r[j].longitude - r[j - 1].longitude);
            // include alt_offset in alt calc
            predict_point.Altitude = r[j - 1].altitude + progress * (r[j].altitude - r[j - 1].altitude) + ai_info[ai_index].alt_offset;
            predict_point.Heading = SimMath.bearing(r[j - 1].latitude, r[j - 1].longitude, r[j].latitude, r[j].longitude);
            
            if (pos.sim_on_ground)
            {
                // ON GROUND, so we can calibrate the IGC file alts with an offset
                // temporarily disabled while I think about the issues...
                //ai_info[ai_index].alt_offset = pos.altitude - r[j-1].altitude;
                //if (debug) printf("%s alt_offset %.1f\n",ai_info[ai_index].atc_id, ai_info[ai_index].alt_offset);
                predict_point.Pitch = 0;
                predict_point.Bank = 0;
            }
            else
            {
                predict_point.Bank = r[j - 1].bank + progress * (r[j].bank - r[j - 1].bank);
                predict_point.Pitch = r[j - 1].pitch + progress * (r[j].pitch - r[j - 1].pitch);
            }
             */
            // now calculate steering deltas based on predict point
            double bearing_to_wp = SimMath.bearing(currentWp.Latitude, currentWp.Longitude,
                                           newWp.Latitude, newWp.Longitude);

            double desired = SimMath.desired_heading(bearing_to_wp, newWp.Heading);

            double delta = SimMath.heading_delta(desired, currentWp.Heading);

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

            Logger.Debug(string.Format("{0},{1},{2},{3},{4},{5}, {6}, {7}, {8}, {9}, {10}",
                                newWp.Latitude,
                                newWp.Longitude,
                                newWp.Altitude,
                                newWp.Pitch,
                                newWp.Bank,
                                newWp.Heading,
                                ahead_rate,
                                alt_rate,
                                pitch_rate,
                                bank_rate,
                                heading_rate
                                ));

            // send the actual slew adjustments

            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_AHEAD_SET,
                                ahead_rate,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_HEADING_SET,
                                heading_rate,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_ALT_SET,
                                alt_rate,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_BANK_SET,
                                bank_rate,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_PITCH_SET,
                                pitch_rate,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            // send gear up/down as necessary
            // TODO: ai_gear(ai_index, i, pos);
            AIAircraft.CurrentWaypoint = newWp;
        }

        private static Waypoint CreateWaypointFromPositionReportStruct(ref PositionReportStruct posreport, DateTime timestamp)
        {
            var currentWp = new Waypoint()
            {
                Altitude = posreport.altitude,
                Longitude = posreport.longitude,
                Latitude = posreport.latitude,
                Speed = posreport.groundspeed,  // TODO: ?
                Pitch = SimMath.deg2rad(posreport.pitch),
                Bank = SimMath.deg2rad(posreport.bank),
                Heading = SimMath.deg2rad(posreport.heading), 
                Timestamp = timestamp
            };
            return currentWp;
        }

        //*****************************************************************************************
        //***********************        update_ai()   ********************************************
        //*****************************************************************************************
        //**********************  ************

        // update the positions of the ai object i
        // called each time the actual ai position is returned from FSX
        /*
        void update_ai(int ai_index, AIStruct pos)
        {
           

            int i = 1;
            Waypoint r = replay[ai_index]; // the array of ReplayPoints for current tracklog
            Waypoint predict_point = new Waypoint(); // a ReplayPoint for the predicted position

            bool found = false;
            // scan the loaded IGC file until you find current time position
            //debug - this could be more efficient if we assume monotonic time
            while (i < ai_info[ai_index].logpoint_count - 2)
            {
                if (zulu_clock > r[i].zulu_time) i++;
                else
                {
                    found = true;
                    break;
                }
            }
            // now r[i] is first ReplayPoint AFTER current sim zulu_clock
            if (!found)
            {
                remove_ai(ai_index);
                return;
            }

            // test to see if zulu_time of current AI position is so old we should MOVE not SLEW
            if (zulu_clock - r[ai_info[ai_index].next_logpoint].zulu_time > AI_WARP_TIME)
            {
                if (debug) printf("zulu_clock: %.1f, next point: %d(%d), current: %d(%d)\n",
                    zulu_clock, i, r[i].zulu_time, ai_info[ai_index].next_logpoint, r[ai_info[ai_index].next_logpoint].zulu_time);
                move_ai(ai_index, r[i]);
                ai_info[ai_index].next_logpoint = i;
                return;
            }

            // OK, the next tracklog position is not too far away, so we'll aim for predict point
            ai_info[ai_index].next_logpoint = i;

            // now search forwards again for the NEXT point after the predict_point
            found = false;
            // PREDICT where the object would be in 4 seconds time
            double predict_time = zulu_clock + PREDICT_PERIOD;
            int j = i;
            while (j < ai_info[ai_index].logpoint_count - 2)
            {
                if (predict_time > r[j].zulu_time) j++;
                else
                {
                    found = true;
                    break;
                }
            }
            if (found)
            { // i.e. we have also found the predict point
                // now r[j] is first ReplayPoint AFTER predict_time
                // progress is fraction of forward progress beyond found replay point
                double progress = (predict_time - r[j - 1].zulu_time) / (r[j].zulu_time - r[j - 1].zulu_time);
                progress = Math.Max(progress, 0); // don't extrapolate *before* r[j-1]
                predict_point.Latitude = r[j - 1].latitude + progress * (r[j].latitude - r[j - 1].latitude);
                predict_point.Longitude = r[j - 1].longitude + progress * (r[j].longitude - r[j - 1].longitude);
                // include alt_offset in alt calc
                predict_point.Altitude = r[j - 1].altitude + progress * (r[j].altitude - r[j - 1].altitude) + ai_info[ai_index].alt_offset;
                predict_point.Heading = SimMath.bearing(r[j - 1].latitude, r[j - 1].longitude, r[j].latitude, r[j].longitude);
                if (pos.sim_on_ground)
                {
                    // ON GROUND, so we can calibrate the IGC file alts with an offset
                    // temporarily disabled while I think about the issues...
                    //ai_info[ai_index].alt_offset = pos.altitude - r[j-1].altitude;
                    //if (debug) printf("%s alt_offset %.1f\n",ai_info[ai_index].atc_id, ai_info[ai_index].alt_offset);
                    predict_point.Pitch = 0;
                    predict_point.Bank = 0;
                }
                else
                {
                    predict_point.Bank = r[j - 1].bank + progress * (r[j].bank - r[j - 1].bank);
                    predict_point.Pitch = r[j - 1].pitch + progress * (r[j].pitch - r[j - 1].pitch);
                }
                // now calculate steering deltas based on predict point
                double bearing_to_wp = SimMath.bearing(pos.latitude, pos.longitude,
                                               predict_point.Latitude, predict_point.Longitude);

                double desired = SimMath.desired_heading(bearing_to_wp, predict_point.Heading);

                double delta = SimMath.heading_delta(desired, pos.heading);

                uint heading_rate = SimMath.slew_turn_rate(bearing_to_wp, pos.heading, predict_point.Heading);

                uint ahead_rate = SimMath.slew_ahead_rate(pos.latitude, pos.longitude,
                                               predict_point.Latitude, predict_point.Longitude,
                                               PREDICT_PERIOD);

                uint bank_rate = SimMath.slew_rotation_to_rate((predict_point.Bank - pos.bank) / PREDICT_PERIOD);

                uint pitch_rate = SimMath.slew_rotation_to_rate((predict_point.Pitch - pos.pitch) / PREDICT_PERIOD);

                uint alt_rate = SimMath.slew_alt_to_rate((pos.altitude - predict_point.Altitude) / PREDICT_PERIOD);

                //debug - print lat longs for excel analysis
                // time,lat,lon,alt,pitch,bank,heading,ahead rate, alt rate, pitch rate, bank rate, heading rate

                Logger.Debug(string.Format("{0},{1},{2},{3},{4},{5}, {6}, {7}, {8}, {9}, {10}, {11}",
                                    zulu_clock,
                                    pos.latitude,
                                    pos.longitude,
                                    pos.altitude,
                                    pos.pitch,
                                    pos.bank,
                                    pos.heading,
                                    ahead_rate,
                                    alt_rate,
                                    pitch_rate,
                                    bank_rate,
                                    heading_rate
                                    ));
                // target time,lat,lon,alt,pitch,bank,heading,||
                Logger.Debug(string.Format("target:,{0},{1},{2},{3},{4},{5},{6}\n",
                                    r[i].zulu_time,
                                    r[i].latitude,
                                    r[i].longitude,
                                    r[i].altitude,
                                    r[i].pitch,
                                    r[i].bank,
                                    r[i].heading));

                // send the actual slew adjustments

                simconnect.TransmitClientEvent(
                                    ai_info[ai_index].id,
                                    SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_AHEAD_SET,
                                    ahead_rate,
                                    GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                    SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

                simconnect.TransmitClientEvent(
                                    ai_info[ai_index].id,
                                    SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_HEADING_SET,
                                    heading_rate,
                                    GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                    SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

                simconnect.TransmitClientEvent(
                                    ai_info[ai_index].id,
                                    SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_ALT_SET,
                                    alt_rate,
                                    GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                    SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

                simconnect.TransmitClientEvent(
                                    ai_info[ai_index].id,
                                    SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_BANK_SET,
                                    bank_rate,
                                    GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                    SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

                simconnect.TransmitClientEvent(
                                    ai_info[ai_index].id,
                                    SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_PITCH_SET,
                                    pitch_rate,
                                    GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                    SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

                // send gear up/down as necessary
                ai_gear(ai_index, i, pos);
            }
        }
        */
        // Set up all the SimConnect related event handlers
        private void InitSimConnect(VSPCContext context)
        {
            try
            {
                if (simconnect == null)
                {
                    try
                    {
                        Logger.Trace("Connecting to FSX...");
                        simconnect = new SimConnect("Managed Client Events", hWnd, WM_USER_SIMCONNECT, null, 0);
                        Logger.Trace("Connection established");
                    }
                    catch (COMException ex)
                    {
                        Logger.Error("Unable to connect to FSX: " + ex);
                        throw;
                    }
                }

                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure for position reports
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "TRANSPONDER CODE:1", "", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.RegisterDataDefineStruct<PositionReportStruct>(DEFINITIONS.PositionReportStruct);

                // define a data structure for updating position 
                simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "Plane Pitch Degrees", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                //simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "TRANSPONDER CODE:1", "", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.RegisterDataDefineStruct<PositionUpdateStruct>(DEFINITIONS.PositionUpdateStruct);

                // define a data structure for updating ATC Id
                simconnect.AddToDataDefinition(DEFINITIONS.ATCIdStruct, "ATC ID", null, SIMCONNECT_DATATYPE.STRING32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.RegisterDataDefineStruct<ATCIDStruct>(DEFINITIONS.ATCIdStruct);

                // catch a simobject data request
                simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
                simconnect.RequestDataOnSimObject(SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT, DEFINITIONS.PositionReportStruct, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 5, 0);

                // catch the assigned object IDs 
                simconnect.OnRecvAssignedObjectId += new SimConnect.RecvAssignedObjectIdEventHandler(simconnect_OnRecvAssignedObjectId);

                // client event id's
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_SLEW_ON, "SLEW_ON");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_HEADING_SET, "AXIS_SLEW_HEADING_SET");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_ALT_SET, "AXIS_SLEW_ALT_SET");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_BANK_SET, "AXIS_SLEW_BANK_SET");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_PITCH_SET, "AXIS_SLEW_PITCH_SET");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_AHEAD_SET, "AXIS_SLEW_AHEAD_SET");
            }
            catch (COMException ex)
            {
                Logger.Error(ex.Message);
                broker.Publish(new SimCommErrorMessage());
            }
        }


        void SetupWindowsMessaging()
        {
            var wih = new WindowInteropHelper(Application.Current.MainWindow);
            hWnd = wih.Handle;
            HwndSource hs = HwndSource.FromHwnd(hWnd);
            hs.AddHook(new HwndSourceHook(ProcessSimConnectWin32Events));
        }

        private IntPtr ProcessSimConnectWin32Events(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            handled = false;
            if (msg == SimConnectInterface.WM_USER_SIMCONNECT && simconnect != null)
            {
                simconnect.ReceiveMessage();
                handled = true;
            }

            return (IntPtr)0;
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Logger.Trace("Flightsim started: " + data.szApplicationName);
            broker.Publish(new FlightsimConnectedMessage() { ApplicationName = data.szApplicationName });
        }

        // The case where the user closes FSX
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Logger.Trace("FSX has exited");
            CloseConnection();
            broker.Publish(new FlightsimDisconnectedMessage());
        }

        private void CloseConnection()
        {
            if (simconnect != null)
            {
                simconnect.Dispose();
                simconnect = null;
                Logger.Trace("Connection closed");
                broker.Publish(new FlightsimDisconnectedMessage());
            }
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Logger.Error(string.Format("Simconnect exception received: dwException {0}, dwIndex: {1}", data.dwException, data.dwIndex));
        }

        private double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        private double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            switch ((SIMCONNECT_EVENTS)data.dwRequestID)
            {
                case SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT:
                    var posreport = (PositionReportStruct)data.dwData[0];
                    var positionReportMsg = new PositionReportMessage()
                    {
                        Altitude = posreport.altitude,
                        Longitude = posreport.longitude,
                        Latitude = posreport.latitude,
                        Groundspeed = posreport.groundspeed,
                        Pitch = -posreport.pitch,
                        Bank = -posreport.bank,
                        Heading = posreport.heading,
                    };
                    broker.Publish(positionReportMsg);
                    break;
                case SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE:
                    CalculateSlewAI(data.dwObjectID, (PositionReportStruct)data.dwData[0]);
                    break;
            }

        }



        /// <summary>
        /// Handle Windows Message
        /// </summary>
        /// <param name="m"></param>
        /// <returns>true if message is handled, false if default message handling should take place</returns>
        public bool GetWindowsMsg(System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT && simconnect != null)
            {
                simconnect.ReceiveMessage();
                return true;
            }

            return false;
        }

        void simconnect_OnRecvAssignedObjectId(SimConnect sender, SIMCONNECT_RECV_ASSIGNED_OBJECT_ID data)
        {
            if (data.dwRequestID >= (int)SIMCONNECT_EVENTS.EVENTID_SETAIAC)
            {
                var aicounter = data.dwRequestID - (uint)SIMCONNECT_EVENTS.EVENTID_SETAIAC;
                var planeObjectId = data.dwObjectID;
                var callsign = AICounterToCallsignMap[aicounter];

                CallsignToAIPlaneMap[callsign].SimConnectObjectId = data.dwObjectID;

                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_SLEW_ON, 1, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                var setAtcIdStruct = new ATCIDStruct() { ATCId = callsign };
                simconnect.SetDataOnSimObject(SIMCONNECT_EVENTS.EVENTID_AISETATCID, planeObjectId, 0, setAtcIdStruct);
                AICounterToCallsignMap.Remove(aicounter);
            }
        }
    }
}