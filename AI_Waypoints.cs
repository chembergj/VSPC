//Copyright (c) Microsoft Corporation.  All rights reserved. 
//
//
// C# Creating AI Objects sample
//
// Click on Connect to try and connect to a running version of ESP
// Click on Set User Location to SeaTac to move the user aircraft to the same location as the AI objects
// Click on Create AI objects once to create two aircraft and a fuel truck
// Click on Send AI Waypoints once to send waypoints to the two AI objects
// Watch the Extra 300S and fuel truck go into motion
// Click on Send smoke request to see a smoke trail from the Extra 300S
// Click on Disconnect to close the connection, and then you will
// be able to click on Connect and restart the process
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

// Add these two statements to all SimConnect clients
using Microsoft.ESP.SimConnect;
using System.Runtime.InteropServices;

namespace Managed_AI_Waypoints
{
    public partial class AI_Waypoints : Form
    {

        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;

        // Object IDs
        uint Extra300SID = 0;
        uint TruckID = 0;
        uint DouglasID = 0;

        // structure used to set/receive LLAPBH data
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct PositionData
        {
            public double latitude;
            public double longitude;
            public double altitude;
            public double pitch;
            public double bank;
            public double heading;
        };

        enum DEFINITIONS
        {
            Extra300SWaypoints,
            FuelTruckWaypoints,
            ExtraSmoke,
            PositionData,
        }

        enum DATA_REQUESTS
        {
            REQUEST_DOUGLAS,
            REQUEST_Extra300S,
            REQUEST_TRUCK,
            REQUEST_RELEASE_AI_AIRCRAFT,
        };

        enum EVENTS
        {
            PAUSED,
            SEND_UNPAUSE,
            EVENT_FREEZE_POSITION,
            EVENT_FREEZE_ALTITUDE,
            EVENT_FREEZE_ATTITUDE,
        };

        enum GROUPID
        {
            FLAG = 2000000000,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct ExtraSmoke
        {
            public bool smokeEnable;
            // Add more data here if necessary
          
        };

        // Declare the actual structure
        ExtraSmoke es;
        
        public AI_Waypoints()
        {
            InitializeComponent();

            setButtons(true, false, false, false, false);

            es.smokeEnable = false;
        }
        // Simconnect client will send a win32 message when there is 
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.

        protected override void DefWndProc(ref Message m)
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

        private void setButtons(bool bConnect, bool bCreate, bool bSend, bool bSmoke, bool bDisconnect)
        {
            buttonConnect.Enabled = bConnect;
            buttonSetUserLocation.Enabled = bCreate;
            buttonCreateAIObjects.Enabled = bCreate;
            buttonSendWaypoints.Enabled = bSend;
            buttonSmoke.Enabled = bSmoke;
            buttonDisconnect.Enabled = bDisconnect;
        }

        private void closeConnection()
        {
            if (simconnect != null)
            {
                // Dispose serves the same purpose as SimConnect_Close()
                simconnect.Dispose();
                simconnect = null;
                es.smokeEnable = false;
                displayText("Connection closed");
            }
        }

        // Set up the SimConnect event handlers
        private void initComms()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // catch the assigned object IDs
                simconnect.OnRecvAssignedObjectId += new SimConnect.RecvAssignedObjectIdEventHandler(simconnect_OnRecvAssignedObjectId);

                // set up the data definiton for the Extra smoke
                simconnect.AddToDataDefinition(DEFINITIONS.ExtraSmoke, "SMOKE ENABLE", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<ExtraSmoke>(DEFINITIONS.ExtraSmoke);

                // Subscribe to system event Pause
                simconnect.SubscribeToSystemEvent(EVENTS.PAUSED, "Pause");
                simconnect.OnRecvEvent += new SimConnect.RecvEventEventHandler(simconnect_OnRecvEvent);

                // Map an event to the EventID: PAUSE_OFF
                simconnect.MapClientEventToSimEvent(EVENTS.SEND_UNPAUSE, "PAUSE_OFF");

                // define & register PositionData, used for LLAPBH updates on Sim Objects
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "PLANE ALTITUDE", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "Plane Pitch Degrees", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "Plane Bank Degrees", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.PositionData, "Plane Heading Degrees True", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            }
            catch (COMException ex)
            {
                displayText(ex.Message);
            }
        }

        // The simulation will pause each time a key is selected in the addon, so unpause the sim each time this happens

        void simconnect_OnRecvEvent(SimConnect sender, SIMCONNECT_RECV_EVENT data)
        {
            displayText("Pause event received");

            switch ((EVENTS)data.uEventID)
            {
                case EVENTS.PAUSED:

                    simconnect.TransmitClientEvent((uint) SimConnect.SIMCONNECT_OBJECT_ID_USER, EVENTS.SEND_UNPAUSE, (uint) 0, GROUPID.FLAG, SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
                    displayText("Unpause request sent...");
                    break;

            }
        }

        void simconnect_OnRecvAssignedObjectId(SimConnect sender, SIMCONNECT_RECV_ASSIGNED_OBJECT_ID data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_DOUGLAS:

                    DouglasID = (uint) (DATA_REQUESTS)data.dwObjectID;
                    displayText("Received Douglas ID");
                    break;

                case DATA_REQUESTS.REQUEST_Extra300S:

                    Extra300SID = (uint) (DATA_REQUESTS)data.dwObjectID;
                    displayText("Received Extra 300S ID");
                    break;

                case DATA_REQUESTS.REQUEST_TRUCK:
                    TruckID = (uint) (DATA_REQUESTS)data.dwObjectID;
                    displayText("Received Truck ID");
                    break;

                default:
                    displayText("Unknown Request ID received: " + (DATA_REQUESTS)data.dwRequestID);
                    break;

            }
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            displayText("Connected to ESP");
        }

        // The case where the user closes ESP
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            displayText("ESP has exited");
            closeConnection();
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            displayText("Exception received: " + data.dwException);
        }

        // The case where the user closes the client
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeConnection();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (simconnect == null)
            {
                try
                {
                    // the constructor is similar to SimConnect_Open in the native API
                    simconnect = new SimConnect("C# Creating AI Objects", this.Handle, WM_USER_SIMCONNECT, null, 0);

                    setButtons(false, true, false, false, true);

                    initComms();

                }
                catch
                {
                    displayText("Unable to connect to ESP!");
                }
            }
            else
            {
                displayText("Error - try again!");
                closeConnection();

                setButtons(true, false, false, false, false);
            }
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            closeConnection();
            setButtons(true, false, false, false, false);
        }

        private void buttonSetUserLocation_Click(object sender, EventArgs e)
        {
            // Update the user location to the same lat/lon at the ai object we create
            PositionData initPositionData;
            initPositionData.latitude = 47.4315501972979;
            initPositionData.longitude = -122.308007293086;
            initPositionData.altitude = 0;
            initPositionData.heading = 360.0;
            initPositionData.pitch = 0;
            initPositionData.bank = 0;

            // Set the user aircraft location.
            simconnect.SetDataOnSimObject(DEFINITIONS.PositionData, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_DATA_SET_FLAG.DEFAULT, initPositionData);
        }

        private void buttonSmoke_Click(object sender, EventArgs e)
        {
            if (Extra300SID != 0)
            {
                // Toggle the smoke
                es.smokeEnable = !es.smokeEnable;

                simconnect.SetDataOnSimObject(DEFINITIONS.ExtraSmoke, Extra300SID, 0, es);

                if (es.smokeEnable)
                {
                    displayText("Smoke on requested...");
                }
                else
                {
                    displayText("Smoke off requesteed...");
                }
            }
            else
            {
                displayText("Extra 300S ID not set!");
            }
        }

        private void buttonSendWaypoints_Click(object sender, EventArgs e)
        {
            if (Extra300SID != 0 && TruckID != 0)
            {
                SIMCONNECT_DATA_WAYPOINT[] wp = new SIMCONNECT_DATA_WAYPOINT[3];
                SIMCONNECT_DATA_WAYPOINT[] ft = new SIMCONNECT_DATA_WAYPOINT[2];

                simconnect.AddToDataDefinition(DEFINITIONS.Extra300SWaypoints, "AI WAYPOINT LIST", "number", SIMCONNECT_DATATYPE.WAYPOINT, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.FuelTruckWaypoints, "AI WAYPOINT LIST", "number", SIMCONNECT_DATATYPE.WAYPOINT, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // Extra300S aircraft should fly in circles across the North end of the runway

                wp[0].Flags = (uint)SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED;
                wp[0].Altitude = 800;
                wp[0].Latitude = 47 + (27.79 / 60);
                wp[0].Longitude = -122 - (18.46 / 60);
                wp[0].ktsSpeed = 100;

                wp[1].Flags = (uint)SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED;
                wp[1].Altitude = 600;
                wp[1].Latitude = 47 + (27.79 / 60);
                wp[1].Longitude = -122 - (17.37 / 60);
                wp[1].ktsSpeed = 100;

                wp[2].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.WRAP_TO_FIRST | SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED);
                wp[2].Altitude = 800;
                wp[2].Latitude = 47 + (27.79 / 60);
                wp[2].Longitude = -122 - (19.92 / 60);
                wp[2].ktsSpeed = 100;

                // Create a polymorphic array

                Object[] objv1 = new Object[wp.Length];
                wp.CopyTo(objv1, 0);

                // Send the three waypoints to the Extra300S
                simconnect.SetDataOnSimObject(DEFINITIONS.Extra300SWaypoints, Extra300SID, 0, objv1);

                // Truck goes down the runway
                ft[0].Flags = (uint)SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED;
                ft[0].Altitude = 433;
                ft[0].Latitude = 47 + (25.93 / 60);
                ft[0].Longitude = -122 - (18.46 / 60);
                ft[0].ktsSpeed = 75;

                ft[1].Flags = (uint)(SIMCONNECT_WAYPOINT_FLAGS.WRAP_TO_FIRST | SIMCONNECT_WAYPOINT_FLAGS.SPEED_REQUESTED);
                ft[1].Altitude = 433;
                ft[1].Latitude = 47 + (26.25 / 60);
                ft[1].Longitude = -122 - (18.46 / 60);
                ft[1].ktsSpeed = 55;

                // Create a polymorphic array

                Object[] objv2 = new Object[ft.Length];
                ft.CopyTo(objv2, 0);

                // Send the waypoints to the fuel truck
                simconnect.SetDataOnSimObject(DEFINITIONS.FuelTruckWaypoints, TruckID, 0, objv2);

                displayText("Waypoint lists sent...");
                setButtons(false, false, false, true, true);
            }
            else
            {
                displayText("Extra 300S or Truck IDs not set!");
            }
        }

        private void buttonCreateAIObjects_Click(object sender, EventArgs e)
        {
            SIMCONNECT_DATA_INITPOSITION Init;

            // Add a parked museum aircraft, just west of the runway

            Init.Altitude = 433.0;				    // Altitude of Sea-tac is 433 feet
            Init.Latitude = 47 + (25.97 / 60);		// Convert from 47 25.97 N
            Init.Longitude = -122 - (18.51 / 60);	// Convert from 122 18.51 W
            Init.Pitch = 0.0;
            Init.Bank = 0.0;
            Init.Heading = 90.0;
            Init.OnGround = 1;
            Init.Airspeed = 0;
            simconnect.AICreateSimulatedObject("Douglas DC-3", Init, DATA_REQUESTS.REQUEST_DOUGLAS);
            
            // Initialize Extra300S aircraft just in front of user aircraft, at 47 25.89 N, 122 18.48 W

            Init.Altitude = 433.0;				    // Altitude of Sea-tac is 433 feet
            Init.Latitude = 47 + (25.91 / 60);		// Convert from 47 25.90 N
            Init.Longitude = -122 - (18.48 / 60);	// Convert from 122 18.48 W
            Init.Pitch = 0.0;
            Init.Bank = 0.0;
            Init.Heading = 360.0;
            Init.OnGround = 1;
            Init.Airspeed = 1;

            simconnect.AICreateNonATCAircraft("Extra 300S", "N1001", Init, DATA_REQUESTS.REQUEST_Extra300S);
            
            // Initialize truck just in front of user aircraft
            // User aircraft is at 47 25.89 N, 122 18.48 W

            Init.Altitude = 433.0;				    // Altitude of Sea-tac is 433 feet
            Init.Latitude = 47 + (25.91 / 60);		// Convert from 47 25.90 N
            Init.Longitude = -122 - (18.47 / 60);	// Convert from 122 18.48 W
            Init.Pitch = 0.0;
            Init.Bank = 0.0;
            Init.Heading = 360.0;
            Init.OnGround = 1;
            Init.Airspeed = 0;

            simconnect.AICreateSimulatedObject("VEH_jetTruck", Init, DATA_REQUESTS.REQUEST_TRUCK);

            displayText("Request to create objects sent...");
            setButtons(false, false, true, true, true);
        }

        // Response number
        int response = 1;

        // Output text - display a maximum of 10 lines
        string output = "\n\n\n\n\n\n\n\n\n\n";

        void displayText(string s)
        {
            // remove first string from output
            output = output.Substring(output.IndexOf("\n") + 1);

            // add the new string
            output += "\n" + response++ + ": " + s;

            // display it
            richResponse.Text = output;
        }
    }
}
// End of sample
