﻿using Autofac;
using BusinessObjects;
using log4net;
using NetCoreServer;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FinCore
{
    class SMessageSession : WssSession
    {
        private ILog log;
        private ISignalHandler handler;
        private IMessagingServer mServer;
        public SMessageSession(WssServer server, ILog l, ISignalHandler signalHandler) : base(server)
        {
            log = l;
            handler = signalHandler;
            mServer = (IMessagingServer)server;
        }

        public override void OnWsConnected(HttpRequest request)
        {
            log.Info($"WebSocket sessionId {Id} connected!");
        }

        public override void OnWsDisconnected()
        {
            log.Info($"WebSocket sessionId {Id} disconnected!");
        }

        public override void OnWsReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
#if DEBUG
            log.Debug("Incoming: " + message);
#endif
            WsMessage wsMessage = JsonConvert.DeserializeObject<WsMessage>(message);
            if (wsMessage != null)
            {
                handler.ProcessMessage(wsMessage, mServer);
            }
        }

        protected override void OnError(SocketError error)
        {
            log.Info($"WebSocket session caught an error with code {error}");
        }
    }

    public class SMessagingServer : WssServer, IMessagingServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SMessagingServer));

        public SMessagingServer(SslContext ctx, IPAddress address, int port)
            : base(ctx, address, port) { }

        protected override SslSession CreateSession()
        {
            if (Program.Container == null)
                return null;
            ISignalHandler handler = Program.Container.Resolve<ISignalHandler>();
            return new SMessageSession(this, log, handler);
        }

        protected override void OnError(SocketError error)
        {
            log.Error($"WebSocket server caught an error with code {error}");
        }
    }

}
