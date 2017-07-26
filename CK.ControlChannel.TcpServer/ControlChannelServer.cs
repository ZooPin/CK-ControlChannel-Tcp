using System;
using System.Collections.Generic;
using System.Text;
using CK.ControlChannel.Abstractions;

namespace CK.ControlChannel.TcpServer
{
    public class ControlChannelServer : IControlChannelServer
    {
        public string Host => throw new NotImplementedException();

        public int Port => throw new NotImplementedException();

        public bool IsSecure => throw new NotImplementedException();

        public bool IsOpen => throw new NotImplementedException();

        public IEnumerable<IServerClientSession> ActiveSessions => throw new NotImplementedException();

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void RegisterChannelHandler( string channelName, ServerChannelDataHandler handler )
        {
            throw new NotImplementedException();
        }
    }
}
