using System;
using System.Collections.Generic;
using System.Text;
using CK.ControlChannel.Abstractions;
using System.Security.Cryptography.X509Certificates;

namespace CK.ControlChannel.TcpServer
{
    public class ControlChannelServer : IControlChannelServer, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly List<TcpServerClientSession> _activeSessions = new List<TcpServerClientSession>();
        private readonly X509Certificate2 _serverCertificate;

        public ControlChannelServer( string host, int port, X509Certificate2 serverCertificate = null )
        {
            _host = host;
            _port = port;
            _serverCertificate = serverCertificate;
        }

        public string Host => _host;

        public int Port => _port;

        public bool IsSecure => _serverCertificate != null;

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
