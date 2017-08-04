using System;

namespace CK.ControlChannel.Tcp
{
    public class ChannelEventArgs : EventArgs
    {
        public string ChannelName { get; }
        public ChannelEventArgs(string channelName)
        {
            ChannelName = channelName;
        }
    }
}
