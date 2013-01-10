using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.MessageHandlers;
using VSPC.Core;
using VSPC.Core.Messages;
using NLog;
using VSPC.Core.Messages.UI;
using VSPC.Core.Messages.FSD;
using Metacraft.Vatsim.Network;
using Metacraft.Vatsim.Network.PDU;
using System.Runtime.InteropServices;


namespace VSPC.FSD
{
    public class VSPCMessageHandler: AVSPCMessageHandler
    {

        private static Logger logger;
        private FSDSession fsdSession;
        VSPCContext context;
     

        Logger Logger
        {
            get
            {
                if (logger == null)
                    logger = LogManager.GetCurrentClassLogger(); 

                return logger;
            }
        }

        public void Init(MessageBroker broker)
        {
            base.Init(broker);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            broker.Subscribe(this, typeof(UserClickedLoginMessage));
            broker.Subscribe(this, typeof(UserClickedLogoffMessage));
        }

        
        public override void HandleMessage(AMessage message, VSPCContext context)
        {
            if (message is UserClickedLoginMessage)
                Connect((UserClickedLoginMessage)message, context);
            else if (message is UserClickedLogoffMessage)
                Disconnect((UserClickedLogoffMessage)message, context);
            else if (message is PositionReportMessage)
                SendPositionReportToFSDServer((PositionReportMessage)message, context);
            else if (message is ErrorMessage)
                Logger.Error("FSD Error received: {0} {1} {2}", ((ErrorMessage)message).ErrorCode, ((ErrorMessage)message).ErrorInfo, ((ErrorMessage)message).ErrorText);
            else if (message is VSPC.Core.Messages.FLSIM.FlightsimConnectedMessage)
                context.FlightsimIsConnected = true;
            else if (message is VSPC.Core.Messages.FLSIM.FlightsimDisconnectedMessage)
                context.FlightsimIsConnected = false;
            else if (message is TextMessageSend)
                SendTextMessage((TextMessageSend)message);
            else
                Logger.Error("Unexpected message type received in FSDMessageHandler: " + message.GetType().Name);
        }

        #region Connect/Disconnect methods

        private void Connect(UserClickedLoginMessage msg, VSPCContext context)
        {
            this.context = context;
            fsdSession = new FSDSession(new ClientProperties("Claus Joergensen Client", new Version(1, 0), 0x5820, "163f6a324730ed0aa1ba30b29148687c"), msg);
            fsdSession.ServerIdentificationReceived += fsdSession_ServerIdentificationReceived;
            fsdSession.ClientQueryReceived += fsdSession_ClientQueryReceived;
            fsdSession.TextMessageReceived += fsdSession_TextMessageReceived;
            fsdSession.ATCPositionReceived += fsdSession_ATCPositionReceived;
            fsdSession.PilotPositionReceived += fsdSession_PilotPositionReceived;
            fsdSession.NetworkError += fsdSession_NetworkError;
            fsdSession.ProtocolErrorReceived += fsdSession_ProtocolErrorReceived;
            fsdSession.KillRequestReceived += fsdSession_KillRequestReceived;
            fsdSession.IgnoreUnknownPackets = true;
            fsdSession.Connect(msg.Server, 6809);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern bool GetVolumeInformation(string Volume, StringBuilder VolumeName,
            uint VolumeNameSize, out uint SerialNumber, out uint SerialNumberLength,
            out uint flags, StringBuilder fs, uint fs_size);



        void fsdSession_ServerIdentificationReceived(object sender, DataReceivedEventArgs<PDUServerIdentification> e)
        {
            var msg = (UserClickedLoginMessage)e.UserData;

            uint serialNum, serialNumLength, flags;
            StringBuilder volumename = new StringBuilder(256);
            StringBuilder fstype = new StringBuilder(256); 

            bool ok = GetVolumeInformation("C:\\", volumename, 	(uint)volumename.Capacity - 1, out serialNum, out serialNumLength, 	out flags, fstype, (uint)fstype.Capacity - 1);

            fsdSession.SendPDU(new PDUClientIdentification(msg.Callsign, fsdSession.ClientProperties.ClientID, fsdSession.ClientProperties.Name, fsdSession.ClientProperties.Version.Major, fsdSession.ClientProperties.Version.Minor, msg.Cid, ((int)serialNum).ToString(), null));
            fsdSession.SendPDU(new PDUAddPilot(msg.Callsign, msg.Cid, msg.Password, NetworkRating.OBS, ProtocolRevision.VatsimAuth, SimulatorType.MSFS95, msg.Realname));
            context.FSDIsConnected = true;
            context.Callsign = msg.Callsign;
            context.Realname = msg.Realname;
            context.CID = msg.Cid;
            broker.Publish(new FSDConnectedMessage());
            broker.Subscribe(this, typeof(PositionReportMessage));
            broker.Subscribe(this, typeof(TextMessageSend));
            Logger.Trace("Connect done");
        }

        void fsdSession_KillRequestReceived(object sender, DataReceivedEventArgs<PDUKillRequest> e)
        {
            
        }

        void fsdSession_PilotPositionReceived(object sender, DataReceivedEventArgs<PDUPilotPosition> e)
        {
            Logger.Info(string.Format("Pilot pos received: Lat, Long: {0},{1}, GS: {2}, hdg: {3}, bank: {4}, pitch: {5}, press.alt: {6}, truealt: {7}", e.PDU.Lat.ToString("####0.00000000"), e.PDU.Lon.ToString("####0.00000000"), e.PDU.GroundSpeed, e.PDU.Heading, e.PDU.Bank, e.PDU.Pitch, e.PDU.PressureAltitude, e.PDU.TrueAltitude));
            var msg = new TrafficPositionReportMessage() { Sender = e.PDU.From, Altitude = e.PDU.TrueAltitude, BankAngle = e.PDU.Bank, Groundspeed = e.PDU.GroundSpeed, Heading = e.PDU.Heading, Latitude = e.PDU.Lat, Longitude = e.PDU.Lon, Pitch = e.PDU.Pitch };
            broker.Publish(msg);
        }

        void fsdSession_ProtocolErrorReceived(object sender, DataReceivedEventArgs<PDUProtocolError> e)
        {
            
        }

        void fsdSession_NetworkError(object sender, NetworkErrorEventArgs e)
        {
            
        }

        void fsdSession_NetworkConnected(object sender, NetworkEventArgs e)
        {
           
        }


        private void Disconnect(UserClickedLogoffMessage msg, VSPCContext context)
        {
            context.FSDIsConnected = false;
            fsdSession.SendPDU(new PDUDeletePilot(msg.Callsign, context.CID));
            fsdSession.Disconnect();
            broker.Publish(new FSDDisconnectedMessage());
        }

        #endregion

        #region Text message methods

        private void SendTextMessage(TextMessageSend textMessageSend)
        {
            fsdSession.SendPDU(new PDUTextMessage(textMessageSend.Sender, textMessageSend.Receiver, textMessageSend.Text));
        }

        void fsdSession_TextMessageReceived(object sender, DataReceivedEventArgs<PDUTextMessage> e)
        {
            broker.Publish(new TextMessageReceive()
            {
                Sender = e.PDU.From,
                Receiver = e.PDU.To,
                Text = e.PDU.Message
            });
        }

        #endregion


        #region Helper methods regaring Facilitytypes/ratings

        FacilityType NWFacilityTypeToVSPCFacilityType(NetworkFacility nf)
        {
            switch (nf)
            {
                case NetworkFacility.APP:
                    return FacilityType.APP;
                case NetworkFacility.CTR:
                    return FacilityType.CTR;
                case NetworkFacility.DEL:
                    return FacilityType.DEL;
                case NetworkFacility.FSS:
                    return FacilityType.FSS;
                case NetworkFacility.GND:
                    return FacilityType.GND;
                case NetworkFacility.OBS:
                    return FacilityType.OBS;
                case NetworkFacility.TWR:
                    return FacilityType.TWR;
                default:
                    Logger.Error("Unexpected network facility: " + nf);
                    return FacilityType.OBS;
            }
        }

        AtcRating NWAtcRatingToVSPCAtcRating(NetworkRating rating)
        {
            switch(rating)
            {
                case NetworkRating.OBS:
                    return AtcRating.ATC_OBS;
                case NetworkRating.S1:
                    return AtcRating.ATC_S1;
                case NetworkRating.S2:
                    return AtcRating.ATC_S2;
                case NetworkRating.S3:
                    return AtcRating.ATC_S3;
                case NetworkRating.C1:
                    return AtcRating.ATC_C1;
                case NetworkRating.C2:
                    return AtcRating.ATC_C2;
                case NetworkRating.C3:
                    return AtcRating.ATC_C3;
                case NetworkRating.I1:
                    return AtcRating.ATC_I1;
                case NetworkRating.I2:
                    return AtcRating.ATC_I2;
                case NetworkRating.I3:
                    return AtcRating.ATC_I3;
                case NetworkRating.SUP:
                    return AtcRating.ATC_SUP;
                case NetworkRating.ADM:
                    return AtcRating.ATC_ADM;
                default:
                    Logger.Error("Unexpected network rating: " + rating);
                    return AtcRating.ATC_OBS;
	        }
        }

        NetworkRating VSPCAtcRatingToNWAtcRating(AtcRating rating)
        {
            switch (rating)
            {
                case AtcRating.ATC_OBS:
                    return NetworkRating.OBS;
                case AtcRating.ATC_S1:
                    return NetworkRating.S1;
                case AtcRating.ATC_S2:
                    return NetworkRating.S2;
                case AtcRating.ATC_S3:
                    return NetworkRating.S3;
                case AtcRating.ATC_C1:
                    return NetworkRating.C1;
                case AtcRating.ATC_C2:
                    return NetworkRating.C2;
                case AtcRating.ATC_C3:
                    return NetworkRating.C3;
                case AtcRating.ATC_I1:
                    return NetworkRating.I1;
                case AtcRating.ATC_I2:
                    return NetworkRating.I2;
                case AtcRating.ATC_I3:
                    return NetworkRating.I3;
                case AtcRating.ATC_SUP:
                    return NetworkRating.SUP;
                case AtcRating.ATC_ADM:
                    return NetworkRating.ADM;
                default:
                    Logger.Error("Unexpected network rating: " + rating);
                    return NetworkRating.OBS;
            }
        }

        #endregion


        private void SendPositionReportToFSDServer(PositionReportMessage positionReportMessage, VSPCContext context)
        {
            if (context.FSDIsConnected && fsdSession.Connected)
                // TODO: TrueAlt + PressureAlt
                fsdSession.SendPDU(new PDUPilotPosition(context.Callsign, positionReportMessage.Transponder, positionReportMessage.SquawkingCharlie, positionReportMessage.Identing, NetworkRating.OBS, positionReportMessage.Latitude, positionReportMessage.Longitude, (int)Math.Round(positionReportMessage.Altitude), (int)Math.Round(positionReportMessage.Altitude), (int)Math.Round(positionReportMessage.Groundspeed), (int)Math.Round(positionReportMessage.Pitch), (int)Math.Round(positionReportMessage.Bank), (int)Math.Round(positionReportMessage.Heading)));
        }

        void fsdSession_ATCPositionReceived(object sender, DataReceivedEventArgs<PDUATCPosition> e)
        {
            broker.Publish(new ATCPositionMessage()
            {
                Callsign = e.PDU.From,
                Frequency = e.PDU.Frequency,
                Facilitytype = NWFacilityTypeToVSPCFacilityType(e.PDU.Facility),
                VisualRange = e.PDU.VisibilityRange,
                Rating = NWAtcRatingToVSPCAtcRating(e.PDU.Rating),
                Latitude = e.PDU.Lat,
                Longitude = e.PDU.Lon
            });
        }

        #region Client query methods

        void fsdSession_ClientQueryReceived(object sender, DataReceivedEventArgs<PDUClientQuery> e)
        {
            switch(e.PDU.QueryType)
            {
                case ClientQueryType.Capabilities:
                   SendCapsInfoResponse(e);
                   break;
                case ClientQueryType.RealName:
                   SendRealnameInfoResponse(e);
                   break;
                default:
                   break;
            }
        }

        private void SendRealnameInfoResponse(DataReceivedEventArgs<PDUClientQuery> e)
        {
            var payload = new List<string>();
            payload.Add(context.Realname);
            // TODO: USER:1 ? FSINN: NONE:1 
            payload.Add("NONE");
            payload.Add("1");
            fsdSession.SendPDU(new PDUClientQueryResponse(context.Callsign, e.PDU.From, ClientQueryType.RealName, payload)); 
        }

        private void SendCapsInfoResponse(DataReceivedEventArgs<PDUClientQuery> e)
        {
            var payload = new List<string>();
            payload.Add(context.Realname);
            // TODO: CAPS?
            payload.Add("MODELDESC=1");
            payload.Add("ATCINFO=1");
            //payload.Add("INTERIMPOS=1");
            fsdSession.SendPDU(new PDUClientQueryResponse(context.Callsign, e.PDU.From, ClientQueryType.Capabilities, payload));
        }

        #endregion

    }
}
