using System;
using System.Collections.Generic;
using System.Text;
using CK.ControlChannel.Abstractions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using CK.Core;
using System.Net.Security;
using System.IO;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelServer : IControlChannelServer, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly List<TcpServerClientSession> _activeSessions = new List<TcpServerClientSession>();
        private readonly X509Certificate2 _serverCertificate;
        private readonly ManualResetEvent _connectEvent = new ManualResetEvent( false );
        private CancellationTokenSource _cts;
        private TcpListener _tcpListener;
        private Task _listenTask;
        private IPEndPoint _ep;
        private bool _isOpen;

        public ControlChannelServer( string host, int port, X509Certificate2 serverCertificate = null )
        {
            _host = host;
            _port = port;
            _serverCertificate = serverCertificate;
        }

        public string Host => _host;

        public int Port => _port;

        public bool IsSecure => _serverCertificate != null;

        public bool IsOpen => _isOpen;

        public bool IsDisposed { get; private set; }

        public IEnumerable<IServerClientSession> ActiveSessions => _activeSessions;

        public void Close()
        {
            if( _isOpen )
            {
                _cts.Cancel();
                _tcpListener.Stop();
                _tcpListener = null;
                _isOpen = false;
            }
        }

        public void Open()
        {
            if( _isOpen )
            {
                throw new InvalidOperationException( "Cannot re-open an already open server." );
            }
            _cts = new CancellationTokenSource();
            _ep = new IPEndPoint( IPAddress.Parse( Host ), Port );
            _tcpListener = new TcpListener( IPAddress.Any, Port );
            _tcpListener.Start();

            _listenTask = Task.Factory.StartNew( DoListen, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default ).Unwrap();
            _isOpen = true;
        }

        private async Task DoListen()
        {
            IActivityMonitor m = new ActivityMonitor();
            try
            {
                var token = _cts.Token;
                while( !token.IsCancellationRequested )
                {
                    TcpClient c = await _tcpListener.AcceptTcpClientAsync();
                    var clientTask = Task.Factory.StartNew( () => AcceptClientAsync( c ), _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default ).Unwrap();
                }

            }
            catch( ObjectDisposedException odex ) when( odex.ObjectName == typeof( Socket ).FullName )
            {
                m.Debug( "TcpListener is shutting down" );
            }
            catch( Exception ex )
            {
                m.Error( "Closing TcpListener following error", ex );
                Close();
            }
        }

        private async Task AcceptClientAsync( TcpClient c )
        {
            IActivityMonitor m = new ActivityMonitor();
            m.Debug( () =>
            {
                IPEndPoint ep = c.Client.RemoteEndPoint as IPEndPoint;
                string ip;
                if( ep != null )
                {
                    ip = ep.ToString();
                }
                else
                {
                    ip = c.Client.RemoteEndPoint.Serialize().ToString();
                }
                return $"Opening connection from {ip}";
            } );
            using( NetworkStream ns = c.GetStream() )
            {
                if( _serverCertificate != null )
                {
                    using( SslStream ssl = new SslStream( ns, false ) )
                    {
                        m.Debug( () => "Using SSL stream" );
                        await ssl.AuthenticateAsServerAsync( _serverCertificate );
                        await HandleClientStreamAsync( m, ssl, c );
                    }
                }
                else
                {
                    m.Warn( "Using unencrypted stream" );
                    await HandleClientStreamAsync( m, ns, c );
                }
            }
        }

        private async Task HandleClientStreamAsync( IActivityMonitor m, Stream s, TcpClient c )
        {
            var version = await ReadProtocolVersionAsync( s );
            if( version != ProtocolVersion )
            {
                m.Error( $"Client gave version {version}, but current version is {ProtocolVersion}" );
                // Stream will be disposed in AcceptClientAsync
            }
            else
            {
                m.Debug( () => $"Using version {version}" );
            }
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
