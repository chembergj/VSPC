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
        public AIPlane(string callsign)
        {
            Callsign = callsign;
        }

        public uint AICounter { get; set; }
        public uint SimConnectObjectId { get; set; }
        public string Callsign { get; protected set; }

        // True if we are currently moving, but next waypoint says GS=0
        public bool SlowingDownForParkingMode { get; set; } 

        // Present target waypoint the plane is aiming for
        public Waypoint TargetWaypoint { get; protected set; }
        
        // Remaining seconds, until we reach the Target Waypoint (approx., only in case network trf.pos. arrives at exact intervals)
        public double RemainingSecondsUntilTarget { get; set; }

        // Set new target and reset "timer"
        public void SetTargetWaypoint(Waypoint wp)
        {
            TargetWaypoint = wp;
            RemainingSecondsUntilTarget = SimConnectInterface.SECONDS_BETWEEN_POSITIONREPORTS_FROM_NETWORK;
        }

        public bool IsTargetWaypointStale()
        {
            return RemainingSecondsUntilTarget == 0;
        }
    }

    public class SimConnectInterface : AVSPCMessageHandler
    {
       
        #region enum definitions

        enum DEFINITIONS
        {
            PositionReportStruct,
            PositionUpdateStruct,
            AIPositionUpdateStruct,
            AIMoveStruct,
            AISetAltAboveGroundStruct
        }

        enum SIMCONNECT_EVENTS
        {
            EVENTID_POSITIONREPORT,
            EVENTID_AIRELEASEATC,

            // Slew events
            EVENTID_SLEW_ON,
            EVENTID_AXIS_SLEW_HEADING_SET,
            EVENTID_AXIS_SLEW_ALT_SET,
            EVENTID_AXIS_SLEW_BANK_SET,
            EVENTID_AXIS_SLEW_PITCH_SET,
            EVENTID_AXIS_SLEW_AHEAD_SET,
            EVENTID_DEFINITION_AI_MOVE,
            EVENTID_SLEW_RESET,

            // Light events
            EVENTID_STROBES_ON,
            EVENTID_TOGGLE_BEACON_LIGHTS,
            EVENTID_TOGGLE_TAXI_LIGHTS,
            EVENTID_TOGGLE_WING_LIGHTS,
    
            EVENTID_SETAIAC = 100000,

            EVENTID_POSITIONREPORT_FOR_AIUPDATE = 200000
        };

        enum GROUP_PRIORITIES : uint
        {
            SIMCONNECT_GROUP_PRIORITY_HIGHEST = 1,
            SIMCONNECT_GROUP_PRIORITY_HIGHEST_MASKABLE = 10000000,
            SIMCONNECT_GROUP_PRIORITY_STANDARD = 1900000000,
            SIMCONNECT_GROUP_PRIORITY_DEFAULT = 2000000000,
            SIMCONNECT_GROUP_PRIORITY_LOWEST = 4000000000
         }

        #endregion  

        #region fields

        // NLog logger
        private static Logger logger;

        // Window handle for message pump
        IntPtr hWnd;

        // Expected interval between traffic reports from vatsim
        public const int SECONDS_BETWEEN_POSITIONREPORTS_FROM_NETWORK = 5;

        // if speed exceeds this value, don't bother to slew the plane over there, just move him right away
        const int MAX_SPEED_BEFORE_WARP = 1543; // = 3000kts

        // Counter + lock for keeping track of request id for new AI traffic
        uint AICounter = 0;
        object AICounterLock = 0;

        // Dictionary of AI objects not yet assigned a simconnect object id (mapping from callsign to AICounter)
        readonly Dictionary<uint, string> AICounterToCallsignMap = new Dictionary<uint, string>();

        // Dictionary of AI objects with an assigned simconnect object id
        readonly Dictionary<string, AIPlane> CallsignToAIPlaneMap = new Dictionary<string, AIPlane>();

        // User-defined win32 event
        private const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;

        #endregion  

        #region Properties 

        

        Logger Logger
        {
            get
            {
                if (logger == null)
                    logger = LogManager.GetCurrentClassLogger();

                return logger;
            }
        }


        #endregion

        #region Struct definitions for receiving SimConnect data

        // Struct used for position reporting
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct PositionReportStruct
        {
            public double latitude;
            public double longitude;
            public double truealtitude;
            public double pressurealtitude;
            public double groundspeed;
            public double pitch;        // Degrees
            public double bank;         // Degrees
            public double heading;      // Degrees
            // public short transponder;
        };

        // Struct used for sending data from position reports to SimConnect
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct AIPositionReportStruct
        {
            public double latitude;
            public double longitude;
            public double truealtitude;
            public double groundspeed;
            public double pitch;        // Radians
            public double bank;         // Radians
            public double heading;      // Radians
            public double simOnGround;
            public double altAboveGround;      
        };

        // Struct used for moving AI plane
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct AIMoveStruct
        {
            public double latitude;
            public double longitude;
            public double truealtitude;
            public double pitch;        // Radians
            public double bank;         // Radians
            public double heading;      // Radians
            
        };

        // Struct used for handling AI plane on the ground
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct AIAltAboveGroundStruct
        {
            public double altAboveGround;
        };

        #endregion  


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
            var origCallsign = trafficPositionReportMessage.Sender;

            // for (int i = 1; i < 10; i++)
            {
                // trafficPositionReportMessage.Sender = origCallsign + ("-" + i);
                // trafficPositionReportMessage.Longitude += 0.0003;

                if (CallsignToAIPlaneMap.ContainsKey(trafficPositionReportMessage.Sender))
                {
                    UpdateExisitingAIData(trafficPositionReportMessage);
                }
                else
                {
                    CreateNewAIAircraft(trafficPositionReportMessage);

                }
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
                    Altitude = trafficPositionReportMessage.TrueAltitude,
                    Bank = -trafficPositionReportMessage.BankAngle,
                    Heading = trafficPositionReportMessage.Heading,
                    Latitude = trafficPositionReportMessage.Latitude,
                    Longitude = trafficPositionReportMessage.Longitude,
                    Pitch = -trafficPositionReportMessage.Pitch,
                    OnGround = 1,
                    Airspeed = 0
                };

                simconnect.AICreateNonATCAircraft(GetRepaintTitle(),  trafficPositionReportMessage.Sender, initpos, (SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_SETAIAC + counter));
                // simconnect.AICreateSimulatedObject(GetRepaintTitle(), initpos, (SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_SETAIAC + counter));

                var aiplane = new AIPlane(trafficPositionReportMessage.Sender);
                aiplane.SetTargetWaypoint(CreateWaypointFromTrafficPositionReportMsg(trafficPositionReportMessage));
                aiplane.AICounter = counter;
                CallsignToAIPlaneMap.Add(aiplane.Callsign, aiplane);
            }
            catch (COMException e)
            {
                Logger.Error("SimConnectInterface.CreateNewAIAircraft: " + e);
            }
        }

        // Much much much more work to be done here, but for now, let's make life easy
        private string GetRepaintTitle()
        {
            return "Cessna Skyhawk 172SP Paint1";
        }

        private void UpdateExisitingAIData(TrafficPositionReportMessage msg)
        {
            var AIAircraft = CallsignToAIPlaneMap[msg.Sender];

            // Sync AI data for this airplane with receival of position reports, 1) Cancel it 2) Restart it
            // simconnect.RequestDataOnSimObject((SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE + AIAircraft.AICounter), DEFINITIONS.AIPositionUpdateStruct, AIAircraft.SimConnectObjectId, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, uint.MaxValue, uint.MaxValue, 0);
            AIAircraft.SetTargetWaypoint(CreateWaypointFromTrafficPositionReportMsg(msg));
            // simconnect.RequestDataOnSimObject((SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE + AIAircraft.AICounter), DEFINITIONS.AIPositionUpdateStruct, AIAircraft.SimConnectObjectId, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 1, 0);

            // Nothing more to do here, the next AI position update event from SimConnect will use the new waypoint
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
        /// A new AI traffic position msg has been received from Vatsim
        /// Set the new waypoint and reset the timer-counter
        /// </summary>
        /// <param name="msg"></param>


        private void HandleAIPositionReport(uint AIPlaneSimConnectId, AIPositionReportStruct posreport)
        {
            var AIAircraft = CallsignToAIPlaneMap.Where(keyvalue => keyvalue.Value.SimConnectObjectId == AIPlaneSimConnectId).First().Value;
            var currentWp = CreateWaypointFromAIPositionReportStruct(ref posreport, DateTime.Now);
            var newWp = AIAircraft.TargetWaypoint;

            Logger.Debug(AIAircraft.Callsign + " pitch: " + posreport.pitch.ToString() + " on gnd: " + posreport.simOnGround.ToString() + " alt above gnd: " + posreport.altAboveGround.ToString());


            if (AIAircraft.IsTargetWaypointStale())
            {
                Logger.Debug(AIAircraft.Callsign + " has stale target waypoint");
            }
            else if (SimMath.AIAircraftIsParked(currentWp, newWp))
            {
                ResetRates(AIAircraft);
            }
            else
            {
                CalculateSlewAI(AIAircraft, currentWp, newWp);
            }
        }

        private void CalculateSlewAI(AIPlane AIAircraft, Waypoint currentWp, Waypoint newWp)
        {
            var period = AIAircraft.RemainingSecondsUntilTarget;
            AIAircraft.RemainingSecondsUntilTarget --;
            bool onGround = currentWp.OnGround && Math.Abs(currentWp.Altitude - newWp.Altitude) < 10;
          
            AIAircraft.SlowingDownForParkingMode = currentWp.GroundSpeed > 0 && newWp.GroundSpeed == 0 && onGround;
            double periodCorrectionForParking = period > 1 && AIAircraft.SlowingDownForParkingMode ? -1.0 : 0;

            Logger.Debug(string.Format("{0} SlowingDownForParkingMode: {1}, periodCorrectionForParking: {2}", AIAircraft.Callsign, periodCorrectionForParking, AIAircraft.SlowingDownForParkingMode));

            // now calculate steering deltas based on predict point

            double bearing_to_wp = SimMath.bearing(currentWp.Latitude, currentWp.Longitude,
                                           newWp.Latitude, newWp.Longitude);

            uint heading_rate = 0;

            uint ahead_rate = SimMath.slew_ahead_rate(currentWp.Latitude, currentWp.Longitude,
                                          newWp.Latitude, newWp.Longitude,
                                          period + periodCorrectionForParking);
            //uint ahead_rate = SimMath.slew_ahead_rate_experimental(AIAircraft.Callsign, currentWp, newWp, period);

            bool doFinalHardTurnBeforeParking = AIAircraft.SlowingDownForParkingMode && AIAircraft.RemainingSecondsUntilTarget == 1;
            heading_rate = SimMath.slew_turn_rate(bearing_to_wp, currentWp.Heading, newWp.Heading, currentWp.GroundSpeed, doFinalHardTurnBeforeParking);

            if (SimMath.AIAircraftIsPushingBack(currentWp, newWp, heading_rate))
            {
                Logger.Debug(string.Format("{0} assuming pushback, old bearing: {1}", AIAircraft.Callsign, bearing_to_wp));

                // Hard turn with low real GS, probably a pushback
                bearing_to_wp = SimMath.bearing(newWp.Latitude, newWp.Longitude,
                                           currentWp.Latitude, currentWp.Longitude);
                heading_rate = SimMath.slew_turn_rate(bearing_to_wp, currentWp.Heading, newWp.Heading, currentWp.GroundSpeed, doFinalHardTurnBeforeParking);
                ahead_rate = ((uint)-(int)ahead_rate);
               
            }
          

            double speed = SimMath.distance(currentWp.Latitude, currentWp.Longitude, newWp.Latitude, newWp.Longitude) / (period + periodCorrectionForParking);
            var desiredHdg = SimMath.desired_heading(bearing_to_wp, newWp.Heading, doFinalHardTurnBeforeParking ? 0.9 : 0.1);
            Logger.Debug(string.Format("Period: {0}  speed: {1}   ahead rt: {2}  bearing: {3} ({5})  desired heading: {4} ({6})", period, speed, SimMath.slew_ahead_to_rate(speed), bearing_to_wp, desiredHdg, RadianToDegree(bearing_to_wp), RadianToDegree(desiredHdg)));
            
            
           
             /*
            uint ahead_rate = SimMath.slew_ahead_rate_experimental(AIAircraft.Callsign, currentWp, newWp, period);
            if (Math.Abs((int)ahead_rate) > 16384)
            {
            }*/
            
            // On ground, and not on the way up or down? ignore pitch + bank

            uint bank_rate = onGround ? 0 : SimMath.slew_rotation_to_rate((newWp.Bank - currentWp.Bank) / period, currentWp.GroundSpeed);

            uint pitch_rate = onGround ? 0 : SimMath.slew_rotation_to_rate((newWp.Pitch - currentWp.Pitch) / period, currentWp.GroundSpeed);

            uint alt_rate = SimMath.slew_alt_to_rate(SimMath.ft2m((currentWp.Altitude - newWp.Altitude)) / period);


            Logger.Debug(string.Format("CURRENT WP: La {0}, Lo {1}, Alt {2}, Pi {3}, Ba {4}, Hdg {5} ({7}), GS: {6}",
                               currentWp.Latitude.ToString("0000.00000000"),
                               currentWp.Longitude.ToString("0000.00000000"),
                               currentWp.Altitude.ToString("00000"),
                               currentWp.Pitch.ToString("0000.00000000"),
                               currentWp.Bank.ToString("0000.00000000"),
                               currentWp.Heading.ToString("0000.00000000"),
                               currentWp.GroundSpeed.ToString("000.0000"),
                               RadianToDegree(currentWp.Heading)
                               ));

            Logger.Debug(string.Format("NEW WP    : La {0}, Lo {1}, Alt {2}, Pi {3}, Ba {4}, Hdg {5} ({11}), GS {11}, Ahead rt {6}, Alt rt {7}, Pi rt {8}, Ba rt {9}, Hdg rt {10}",
                                newWp.Latitude.ToString("0000.00000000"),
                                newWp.Longitude.ToString("0000.00000000"),
                                newWp.Altitude.ToString("00000"),
                                newWp.Pitch.ToString("0000.00000000"),
                                newWp.Bank.ToString("0000.00000000"),
                                newWp.Heading.ToString("0000.00000000"),
                                (int)ahead_rate,
                                (int)alt_rate,
                                (int)pitch_rate,
                                (int)bank_rate,
                                (int)heading_rate,
                                newWp.GroundSpeed.ToString("000.0000"),
                                RadianToDegree(newWp.Heading)
                                ));
            
            

            // send the actual slew adjustments

            if (speed <= MAX_SPEED_BEFORE_WARP)
            {
                TransmitSlewEvents(AIAircraft, heading_rate, ahead_rate, bank_rate, pitch_rate, alt_rate);
                if(onGround)
                {
                    AIAltAboveGroundStruct s = new AIAltAboveGroundStruct() { altAboveGround = 0 };
                    // simconnect.SetDataOnSimObject(DEFINITIONS.AISetAltAboveGroundStruct, AIAircraft.SimConnectObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, s); 
                }
            }
            else
                MoveAI(AIAircraft, newWp);

            // send gear up/down as necessary
            // TODO: ai_gear(ai_index, i, pos);
        }

    

        private void MoveAI(AIPlane AIAircraft, Waypoint newWp)
        {
            simconnect.TransmitClientEvent(AIAircraft.SimConnectObjectId,
                                SIMCONNECT_EVENTS.EVENTID_AXIS_SLEW_AHEAD_SET,
                                0,
                                GROUP_PRIORITIES.SIMCONNECT_GROUP_PRIORITY_HIGHEST,
                                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);

            var aimove = new AIMoveStruct()
            {
                latitude = newWp.Latitude,
                longitude = newWp.Longitude,
                truealtitude = newWp.Altitude,
                pitch = newWp.Pitch,
                bank = newWp.Bank,
                heading = newWp.Heading,
                
            };

            Logger.Trace(AIAircraft.Callsign +  " doing WARP SPEED");
            simconnect.SetDataOnSimObject(DEFINITIONS.AIMoveStruct, AIAircraft.SimConnectObjectId, SIMCONNECT_DATA_SET_FLAG.DEFAULT, aimove);
        }

        private void TransmitSlewEvents(AIPlane AIAircraft, uint heading_rate, uint ahead_rate, uint bank_rate, uint pitch_rate, uint alt_rate)
        {
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
        }

        private void ResetRates(AIPlane AIAircraft)
        {
            TransmitSlewEvents(AIAircraft, 0, 0, 0, 0, 0);
        }

       

        private static Waypoint CreateWaypointFromAIPositionReportStruct(ref AIPositionReportStruct aiposreport, DateTime timestamp)
        {
            var currentWp = new Waypoint()
            {
                Altitude = aiposreport.truealtitude,
                Longitude = aiposreport.longitude,
                Latitude = aiposreport.latitude,
                GroundSpeed = SimMath.knotsToMetersPerSecond(aiposreport.groundspeed), 
                Pitch = aiposreport.pitch,
                Bank = aiposreport.bank,
                Heading = aiposreport.heading, 
                Timestamp = timestamp,
                OnGround = aiposreport.simOnGround != 0
            };
            return currentWp;
        }

        public static Waypoint CreateWaypointFromTrafficPositionReportMsg(TrafficPositionReportMessage msg)
        {
            var currentWp = new Waypoint()
            {
                Altitude = msg.TrueAltitude,
                Bank = SimMath.deg2rad(-msg.BankAngle),
                Heading = SimMath.deg2rad(msg.Heading),
                Latitude = msg.Latitude,
                Longitude = msg.Longitude,
                Pitch = SimMath.deg2rad(-msg.Pitch),
                GroundSpeed = SimMath.knotsToMetersPerSecond(msg.Groundspeed),
                Timestamp = msg.ReceiveTime 
            };
            return currentWp;
        }


        #region comment stuff

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

        #endregion

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

                // define a data structure for position reports for own plane
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Pressure Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                // simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "TRANSPONDER CODE:1", "", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.RegisterDataDefineStruct<PositionReportStruct>(DEFINITIONS.PositionReportStruct);

                // define a data structure for updating position of AI planes
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "Plane Pitch Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "PLANE BANK DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "PLANE HEADING DEGREES TRUE", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "SIM ON GROUND", "", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIPositionUpdateStruct, "PLANE ALT ABOVE GROUND", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                
                //simconnect.AddToDataDefinition(DEFINITIONS.PositionUpdateStruct, "TRANSPONDER CODE:1", "", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                simconnect.RegisterDataDefineStruct<AIPositionReportStruct>(DEFINITIONS.AIPositionUpdateStruct);

                // define a data structure for moving AI
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "Plane Altitude", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "Plane Pitch Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "PLANE BANK DEGREES", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.AIMoveStruct, "PLANE HEADING DEGREES TRUE", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.RegisterDataDefineStruct<AIMoveStruct>(DEFINITIONS.AIMoveStruct);


                simconnect.AddToDataDefinition(DEFINITIONS.AISetAltAboveGroundStruct, "Plane alt above ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.RegisterDataDefineStruct<AIAltAboveGroundStruct>(DEFINITIONS.AISetAltAboveGroundStruct);

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
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_SLEW_RESET, "SLEW_RESET");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_STROBES_ON, "STROBES_ON");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_TOGGLE_BEACON_LIGHTS, "TOGGLE_BEACON_LIGHTS");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_TOGGLE_TAXI_LIGHTS, "TOGGLE_TAXI_LIGHTS");
                simconnect.MapClientEventToSimEvent(SIMCONNECT_EVENTS.EVENTID_TOGGLE_WING_LIGHTS, "TOGGLE_WING_LIGHTS");
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
                try
                {
                    simconnect.ReceiveMessage();
                }
                catch (COMException e)
                {
                    if ((uint)e.ErrorCode == 0xC000020D)
                    {
                        Logger.Error("SimConnect connection reset");
                        CloseConnection();
                        broker.Publish(new FlightsimDisconnectedMessage());
                    }
                }
                finally
                {
                    handled = true;
                }
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
                AICounterToCallsignMap.Clear();
                CallsignToAIPlaneMap.Clear();
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
            if(data.dwRequestID >= (uint)SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE)
                HandleAIPositionReport(data.dwObjectID, (AIPositionReportStruct)data.dwData[0]);
            else
            {
                switch ((SIMCONNECT_EVENTS)data.dwRequestID)
                {
                    case SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT:
                        var posreport = (PositionReportStruct)data.dwData[0];
                        var positionReportMsg = new PositionReportMessage()
                        {
                            TrueAltitude = posreport.truealtitude,
                            PressureAltitude = posreport.pressurealtitude,
                            Longitude = posreport.longitude,
                            Latitude = posreport.latitude,
                            Groundspeed = posreport.groundspeed,
                            Pitch = -posreport.pitch,
                            Bank = -posreport.bank,
                            Heading = posreport.heading,
                        };
                        broker.Publish(positionReportMsg);
                        break;
                }
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
                // Remember simconnect object id

                var aicounter = data.dwRequestID - (uint)SIMCONNECT_EVENTS.EVENTID_SETAIAC;
                var planeObjectId = data.dwObjectID;
                var callsign = AICounterToCallsignMap[aicounter];

                CallsignToAIPlaneMap[callsign].SimConnectObjectId = data.dwObjectID;

                // Set AI plane in slew mode and subscribe to 1 second pos.reps
                simconnect.AIReleaseControl(planeObjectId, SIMCONNECT_EVENTS.EVENTID_AIRELEASEATC);
                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_SLEW_ON, 1, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_STROBES_ON, 0, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_TOGGLE_BEACON_LIGHTS, 0, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_TOGGLE_TAXI_LIGHTS, 0, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                simconnect.TransmitClientEvent(planeObjectId, SIMCONNECT_EVENTS.EVENTID_TOGGLE_WING_LIGHTS, 0, null, SIMCONNECT_EVENT_FLAG.DEFAULT);
                simconnect.RequestDataOnSimObject((SIMCONNECT_EVENTS)((uint)SIMCONNECT_EVENTS.EVENTID_POSITIONREPORT_FOR_AIUPDATE + aicounter), DEFINITIONS.AIPositionUpdateStruct, planeObjectId, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 1, 0);
                AICounterToCallsignMap.Remove(aicounter);
            }
        }
    }
}