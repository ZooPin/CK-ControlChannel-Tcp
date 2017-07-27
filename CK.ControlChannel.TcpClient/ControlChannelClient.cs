using CK.ControlChannel.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    public class ControlChannelClient : IControlChannelClient
    {
        public string Host => throw new NotImplementedException();

        public int Port => throw new NotImplementedException();

        public bool IsSecure => throw new NotImplementedException();

        public bool IsOpen => throw new NotImplementedException();

        public void RegisterChannelHandler( string channelName, ClientChannelDataHandler handler )
        {
            throw new NotImplementedException();
        }

        public void Send( string channelName, byte[] data )
        {
            throw new NotImplementedException();
        }
    }
}
