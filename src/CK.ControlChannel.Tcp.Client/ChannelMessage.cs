using System;
using System.Collections.Generic;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    public struct ChannelMessage
    {
        public string ChannelName { get; }
        public byte[] MessageContents { get; }
        public ChannelMessage( string channel, byte[] message )
        {
            ChannelName = channel;
            MessageContents = message;
        }
    }
}
