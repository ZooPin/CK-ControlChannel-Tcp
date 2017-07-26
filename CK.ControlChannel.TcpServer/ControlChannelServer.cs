using System;
using System.Collections.Generic;
using System.Text;
using CK.ControlChannel.Abstractions;

namespace CK.ControlChannel.TcpServer
{
    public class ControlChannelServer : IControlChannelServer, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly List<TcpServerClientSession> _activeSessions = new List<TcpServerClientSession>();

        public ControlChannelServer( string host, int port )
        {
            _host = host;
            _port = port;
        }

        public string Host => _host;

        public int Port => _port;

        public bool IsSecure => false;

        public bool IsOpen => false;

        public bool IsDisposed { get; private set; }

        public IEnumerable<IServerClientSession> ActiveSessions => _activeSessions;

        public void Close()
        {
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void RegisterChannelHandler( string channelName, ServerChannelDataHandler handler )
        {
            throw new NotImplementedException();
        }

        #region IDisposable Support

        protected virtual void Dispose( bool disposing )
        {
            if( !IsDisposed )
            {
                if( disposing )
                {
                    Close();
                }
                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose( true );
        }
        #endregion
    }
}
