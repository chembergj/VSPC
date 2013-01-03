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

namespace VSPC.SimInterface
{
    public class SimConnectInterface: AVSPCMessageHandler
    {
    	private static Logger logger;
        IntPtr hWnd;

    	Logger Logger
    	{
    		get
    		{
    			if(logger == null)
					logger = LogManager.GetCurrentClassLogger();

    			return logger;
    		}
    	}

        enum DEFINITIONS
        {
            PositionReportStruct,
        }

        enum DATA_REQUESTS
        {
            REQUEST_POSITIONREPORT,
        };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
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
            public bool onground;
        	public short transponder;
        };

        // User-defined win32 event
        public const int WM_USER_SIMCONNECT = 0x0402;

        // SimConnect object
        SimConnect simconnect = null;


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

        public override void HandleMessage(Core.Messages.AMessage message, VSPCContext context)
        {
            if (message is UserClickedLoginMessage)
                InitSimConnect(context);
            else if (message is UserClickedLogoffMessage)
                CloseConnection();
            else
                Logger.Error("Unexpected message type received in SimConnectInterface: " + message.GetType().Name);
        }

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
                simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
				simconnect.AddToDataDefinition(DEFINITIONS.PositionReportStruct, "TRANSPONDER CODE:1", "", SIMCONNECT_DATATYPE.INT32, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                
                simconnect.RegisterDataDefineStruct<PositionReportStruct>(DEFINITIONS.PositionReportStruct);

                // catch a simobject data request
                simconnect.OnRecvSimobjectData += new SimConnect.RecvSimobjectDataEventHandler(simconnect_OnRecvSimobjectData);
                simconnect.RequestDataOnSimObject(DATA_REQUESTS.REQUEST_POSITIONREPORT, DEFINITIONS.PositionReportStruct, SimConnect.SIMCONNECT_OBJECT_ID_USER, SIMCONNECT_PERIOD.SECOND, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 5, 0);
            }
            catch (COMException ex)
            {
                Logger.Error(ex.Message);
                broker.Publish(new SimCommErrorMessage());
            }
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
            Logger.Error("Exception received: " + data.dwException);
        }

        private double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }

        void simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_POSITIONREPORT:
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
                        OnGround = posreport.onground,

                    };
                    broker.Publish(positionReportMsg);
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
    }
}
