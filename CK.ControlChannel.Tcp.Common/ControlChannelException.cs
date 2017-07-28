using System;
using System.Collections.Generic;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    public class ControlChannelException : Exception
    {
        public ControlChannelException( string message )
            : base( message )
        {
        }
        public ControlChannelException( string message, Exception innerException )
            : base( message, innerException )
        {
        }
    }
}
