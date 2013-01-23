using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

using VSPC.Core.MessageHandlers;
using VSPC.Core.Messages;
using Moq;
using VSPC.Core.Messages.FSD;

namespace VSPC.Core.Test
{
    [TestFixture]
    public class MessageBrokerTestFixture
    {
        MessageBroker broker;
        readonly VSPCContext context = new VSPCContext() { Callsign = "OY-TST", Realname = "MessageBrokerTestFixture" };

        [SetUp]
        public void SetUp()
        {
            broker = new MessageBroker();
        }

        [Test]
        public void TestReceiveSubscribed()
        {
            var msg = new TrafficPositionReportMessage() { TrueAltitude = 1234 };
            var msg2 = new TrafficPositionReportMessage() { TrueAltitude = 4567 };
            
            var handler = new Mock<IVSPCMessageHandler>();
            broker.Subscribe(handler.Object, typeof(TrafficPositionReportMessage));
            broker.Publish(msg);
            handler.Verify(h => h.HandleMessage(msg, context), Times.Once());
        }

        [Test]
        public void TestReceiveSubscribedMultipleReceivers()
        {
            var msg = new TrafficPositionReportMessage() { TrueAltitude = 1234 };

            var handler1 = new Mock<IVSPCMessageHandler>();
            var handler2 = new Mock<IVSPCMessageHandler>();
            broker.Subscribe(handler1.Object, typeof(TrafficPositionReportMessage));
            broker.Subscribe(handler2.Object, typeof(TrafficPositionReportMessage));
            broker.Publish(msg);
            handler1.Verify(h => h.HandleMessage(msg, context), Times.Once());
            handler2.Verify(h => h.HandleMessage(msg, context), Times.Once());
        }

        [Test]
        public void TestNoReceiveOnNotSubscribed()
        {
            var msg = new TextMessageSend();
            var handler = new Mock<IVSPCMessageHandler>();
            
            broker.Subscribe(handler.Object, typeof(TrafficPositionReportMessage));
            broker.Publish(msg);
            handler.Verify(h => h.HandleMessage(msg, context), Times.Never());
        }
    }
}
