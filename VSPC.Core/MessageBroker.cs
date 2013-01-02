using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VSPC.Core.MessageHandlers;
using VSPC.Core.Messages;

namespace VSPC.Core
{
    public class MessageBroker
    {
        Dictionary<Type, List<IVSPCMessageHandler>> messageHandlers = new Dictionary<Type, List<IVSPCMessageHandler>>();
        VSPCContext context = new VSPCContext();

        public void Publish(AMessage msg)
        {
            List<IVSPCMessageHandler> filteredHandlers;
            if (messageHandlers.TryGetValue(msg.GetType(), out filteredHandlers))
            {
                filteredHandlers.ForEach(h => h.HandleMessage(msg, context));
            }
        }

        List<IVSPCMessageHandler> GetOrCreateHandlerList(Type msgType)
        {
            List<IVSPCMessageHandler> handlers;
            if (!messageHandlers.TryGetValue(msgType, out handlers))
            {
                handlers = new List<IVSPCMessageHandler>();
                messageHandlers.Add(msgType, handlers);
            }

            return handlers;
        }

        public void Subscribe(IVSPCMessageHandler handler, Type msgType)
        {
            if (!msgType.IsSubclassOf(typeof(AMessage)))
                throw new ArgumentException("Invalid msgType. msgType must be a subclass of AVSPCMessage");

            var handlersForType = GetOrCreateHandlerList(msgType);
            handlersForType.Add(handler);
        }

        public void SubscribeSubclasses(IVSPCMessageHandler handler, Type msgType)
        {
            if (!msgType.IsSubclassOf(typeof(AMessage)))
                throw new ArgumentException("Invalid msgType. msgType must be a subclass of AVSPCMessage");

            foreach (var subtype in msgType.Assembly.GetTypes().Where(t => t.IsSubclassOf(msgType)))
            {
                var handlersForType = GetOrCreateHandlerList(subtype);
                handlersForType.Add(handler);
            }
        }
    }
}
