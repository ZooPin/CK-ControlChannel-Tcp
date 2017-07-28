using CK.ControlChannel.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    internal class TcpClientHandler
    {
        private readonly Stream _s;

        public TcpClientHandler( Stream s )
        {
            Debug.Assert( s != null && s.CanRead && s.CanWrite );
            _s = s;
        }

    }

    public class ControlChannelServerException : Exception
    {
        internal ControlChannelServerException( string message ) : base( message )
        {

        }
    }
}
