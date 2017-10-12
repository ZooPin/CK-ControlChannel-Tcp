using CK.ControlChannel.Abstractions;
using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelServer : IControlChannelServer, IDisposable
    {
        public static readonly CKTrait ClientLogTag = ActivityMonitor.Tags.Register( "ControlChannelServer.Client" );

        private readonly string _host;
        private readonly int _port;
        private readonly X509Certificate2 _serverCertificate;
        private readonly IAuthorizationHandler _authHandler;
        private readonly ConcurrentDictionary<string, TcpServerClientSession> _activeSessions = new ConcurrentDictionary<string, TcpServerClientSession>();
        private readonly ManualResetEvent _connectEvent = new ManualResetEvent( false );
        private readonly Dictionary<string, ServerChannelDataHandler> _channelHandlers = new Dictionary<string, ServerChannelDataHandler>();
        private readonly RemoteCertificateValidationCallback _userCertificateValidationCallback;

        private CancellationTokenSource _cts;
        private TcpListener _tcpListener;
        private Task _listenTask;
        private IPEndPoint _ep;
        private bool _isOpen;

        /// <summary>
        /// Creates a new instance of <see cref="ControlChannelServer"/>.
        /// </summary>
        /// <param name="host">IP adress to bind to</param>
        /// <param name="port">Port to bind to</param>
        /// <param name="authHandler">Client authentication handler</param>
        /// <param name="serverCertificate">Server SSL certificate; a non-null value enables SSL</param>
        /// <param name="userCertificateValidationCallback">User certficate validation callback; a non-null value forces client to present a certificate</param>
        public ControlChannelServer(
            string host,
            int port,
            IAuthorizationHandler authHandler,
            X509Certificate2 serverCertificate = null,
            RemoteCertificateValidationCallback userCertificateValidationCallback = null
            )
        {
            _host = host;
            _port = port;
            _authHandler = authHandler;
            _serverCertificate = serverCertificate;
            _userCertificateValidationCallback = userCertificateValidationCallback;
        }

        public string Host => _host;

        public int Port => _port;

        public bool IsSecure => _serverCertificate != null;

        public bool IsOpen => _isOpen;

        public bool IsDisposed { get; private set; }

        public IEnumerable<IServerClientSession> ActiveSessions => _activeSessions.Values;

        public void Close()
        {
            if( _isOpen )
            {
                _cts.Cancel();
                foreach( var s in _activeSessions )
                {
                    s.Value.Stream.Dispose();
                }
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
#pragma warning disable CS4014 // We don't want to lock
                    Task.Run( () => AcceptClientAsync( c ), _cts.Token );
#pragma warning restore CS4014 
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
            m.AutoTags = ClientLogTag;
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
                    using( SslStream ssl = new SslStream(
                        ns,
                        false,
                        _userCertificateValidationCallback,
                        null,
                        EncryptionPolicy.RequireEncryption
                        ) )
                    {
                        m.Debug( () => $"Using SSL stream with server certificate: {_serverCertificate.Describe()}" );
                        await ssl.AuthenticateAsServerAsync(
                            _serverCertificate,
                            _userCertificateValidationCallback != null,
                            SslProtocols.Tls12,
                            true
                            );
                        if( m.ShouldLogLine( LogLevel.Debug ) && ssl.RemoteCertificate != null )
                        {
                            m.Debug( () => $"Client connected with certificate: {ssl.RemoteCertificate.Describe()}" );
                        }
                        await HandleClientStreamAsync( m, ssl, c );
                    }
                }
                else
                {
                    m.Warn( "Using unencrypted stream" );
                    await HandleClientStreamAsync( m, ns, c );
                }
                m.Debug( () => "Client disconnected" );
            }
        }

        public void RegisterChannelHandler( string channelName, ServerChannelDataHandler handler )
        {
            _channelHandlers[ channelName ] = handler;
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
