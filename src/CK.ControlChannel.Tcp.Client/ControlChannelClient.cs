using CK.ControlChannel.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using System.Net.Sockets;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelClient : IControlChannelClient, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _isSecure;
        private readonly IReadOnlyDictionary<string, string> _authData;
        private readonly RemoteCertificateValidationCallback _serverCertificateValidationCallback;
        private readonly LocalCertificateSelectionCallback _localCertificateSelectionCallback;
        private readonly int _connectionRetryDelayMs;
        private readonly ConcurrentDictionary<string, ClientChannelDataHandler> _incomingChannelHandlers = new ConcurrentDictionary<string, ClientChannelDataHandler>();
        private readonly ConcurrentQueue<ChannelMessage> _pendingMessages = new ConcurrentQueue<ChannelMessage>();
        private readonly SemaphoreSlim _msgSemaphore = new SemaphoreSlim( 1, 1 );
        private readonly SemaphoreSlim _connectSemaphore = new SemaphoreSlim( 1, 1 );
        private CancellationTokenSource _cts;
        private TcpClient _tcpClient;
        private Stream _dataStream;
        private Exception _error = null;
        private Task _listenTask;
        private DateTime _nextConnectionRetry = DateTime.MinValue;

        /// <summary>
        /// Creates  a new instance of <see cref="ControlChannelClient"/>.
        /// </summary>
        /// <param name="host">The hostname of the server to connect to.</param>
        /// <param name="port">The port of the server to connect to.</param>
        /// <param name="authenticationData">The authentication data sent to the server when connecting.</param>
        /// <param name="isSecure">True is the connection should be made using SSL.</param>
        /// <param name="serverCertificateValidationCallback">The server certificate validation callback. If null, the default will be used.</param>
        /// <param name="localCertificateSelectionCallback">The local certificate selection callback. If null, no user certificate will be sent when connecting.</param>
        /// <param name="connectionRetryDelayMs">The delay to wait before trying to reconnect after a failed connection, in milliseconds.</param>
        public ControlChannelClient(
            string host,
            int port,
            IReadOnlyDictionary<string, string> authenticationData,
            bool isSecure,
            RemoteCertificateValidationCallback serverCertificateValidationCallback = null,
            LocalCertificateSelectionCallback localCertificateSelectionCallback = null,
            int connectionRetryDelayMs = 10*1000 // 10 sec
            )
        {
            _host = host;
            _port = port;
            _isSecure = isSecure;
            _authData = authenticationData;
            _serverCertificateValidationCallback = serverCertificateValidationCallback;
            _localCertificateSelectionCallback = localCertificateSelectionCallback;
            _connectionRetryDelayMs = connectionRetryDelayMs;
        }

        private async Task ConnectAndAuthenticateAsync( IActivityMonitor m )
        {
            if( _tcpClient != null ) { throw new InvalidOperationException( "A TcpClient already exists." ); }
            _tcpClient = new TcpClient( AddressFamily.InterNetwork );
            await _tcpClient.ConnectAsync( _host, _port );

            NetworkStream ns = _tcpClient.GetStream();
            Stream writeStream;
            if( _isSecure )
            {
                var ssl = new SslStream(
                    ns,
                    false,
                    _serverCertificateValidationCallback,
                    _localCertificateSelectionCallback,
                    EncryptionPolicy.RequireEncryption
                    );
                await ssl.AuthenticateAsClientAsync( _host );
                writeStream = ssl;
            }
            else
            {
                m.Warn( () => $"ControlChannelClient is using an unsecure connection" );
                writeStream = ns;
            }
            _dataStream = writeStream;
            await AuthenticateAsync( m );
        }

        public void Close()
        {
            if( _cts != null )
            {
                _cts.Cancel();
            }
            if( _dataStream != null )
            {
                _dataStream.Dispose();
                _dataStream = null;
            }
            if( _tcpClient != null )
            {
                _tcpClient.Dispose();
                _tcpClient = null;
            }
            _pendingAckPub = new ConcurrentQueue<string>();
            _pendingAckSub = new ConcurrentQueue<string>();
            _pendingAckUnpub = new ConcurrentQueue<string>();
            _pendingAckUnsub = new ConcurrentQueue<string>();
            _pendingAckMsg = new ConcurrentQueue<ChannelMessage>();
            _pubChannels.Clear();
            _subChannels.Clear();
            _subChannelsById.Clear();
        }

        public async Task OpenAsync( IActivityMonitor m = null )
        {
            if( m == null ) { m = new ActivityMonitor(); }
            try
            {
                await _connectSemaphore.WaitAsync();
                if( IsOpen || DateTime.Now < _nextConnectionRetry )
                {
                    return;
                }
                _cts = new CancellationTokenSource();
                await ConnectAndAuthenticateAsync( m );
                if( _dataStream != null )
                {
                    _listenTask = Task.Factory.StartNew( () => Listen( _cts.Token ), _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default );

                    // Register all incoming channels (outgoing channels will be registered when sending messages)
                    foreach( var p in _incomingChannelHandlers )
                    {
                        RegisterIncomingChannel( p.Key );
                    }
                }
                _nextConnectionRetry = DateTime.Now;

            }
            catch( ControlChannelException ex )
            {
                m.Error( "Error", ex );
                _error = ex;
                Close();
                _nextConnectionRetry = DateTime.Now + TimeSpan.FromMilliseconds( _connectionRetryDelayMs );
            }
            catch( Exception ex )
            {
                m.Warn( "Connection error", ex );
                Close();
                _nextConnectionRetry = DateTime.Now + TimeSpan.FromMilliseconds( _connectionRetryDelayMs );
            }
            finally
            {
                _connectSemaphore.Release();
            }
        }

        public string Host => _host;

        public int Port => _port;

        public bool IsSecure => _isSecure;

        public bool IsOpen => _tcpClient != null && _tcpClient.Connected;

        public bool CanUse => _error == null;

        /// <summary>
        /// Registers a channel handler, without waiting for server ACK.
        /// </summary>
        /// <param name="channelName">Channel name to register</param>
        /// <param name="handler">Handler action</param>
        public void RegisterChannelHandler( string channelName, ClientChannelDataHandler handler )
        {
            if( _incomingChannelHandlers.TryAdd( channelName, handler ) )
            {
                if( IsOpen )
                {
                    RegisterIncomingChannel( channelName );
                }
            }
            else
            {
                throw new InvalidOperationException( $"Channel already registered: {channelName}" );
            }
        }

        public async Task SendAsync( string channelName, byte[] data )
        {
            IActivityMonitor m = new ActivityMonitor();
            _pendingMessages.Enqueue( new ChannelMessage( channelName, data ) );
            if( IsOpen )
            {
                await InternalSendAsync( m, channelName, data );
            }
            else
            {
                await OpenAsync( m );
                await InternalSendAsync( m, channelName, data );
            }
        }

        private async Task InternalSendAsync( IActivityMonitor m, string channelName, byte[] data )
        {
            if( IsOpen )
            {
                RegisterOutgoingChannel( channelName );
                await ProcessQueueAsync( m );
            }
        }

        private async Task ProcessQueueAsync( IActivityMonitor m )
        {
            Debug.Assert( IsOpen && _dataStream != null );
            ChannelMessage msg;
            ushort? channelId;
            Queue<ChannelMessage> pendingMsg = new Queue<ChannelMessage>();
            await _msgSemaphore.WaitAsync();
            try
            {
                while( _pendingMessages.TryDequeue( out msg ) )
                {
                    if( _pubChannels.TryGetValue( msg.ChannelName, out channelId ) )
                    {
                        if( channelId.HasValue )
                        {
                            _pendingAckMsg.Enqueue( msg );
                            _dataStream.WriteByte( Protocol.M_MSG_PUB );
                            _dataStream.WriteUInt16( channelId.Value );
                            _dataStream.WriteInt32( msg.MessageContents.Length );
                            await _dataStream.WriteBufferAsync( msg.MessageContents );
                        }
                        else
                        {
                            pendingMsg.Enqueue( msg );
                        }
                    }
                    else
                    {
                        m.Warn( $"Channel {msg.ChannelName} was not registered" );
                        pendingMsg.Enqueue( msg );
                    }
                }
                // Put back messages that could not be sent
                foreach( var pmsg in pendingMsg ) { _pendingMessages.Enqueue( pmsg ); }

            }
            finally
            {
                _msgSemaphore.Release();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing )
        {
            if( !disposedValue )
            {
                if( disposing )
                {
                    Close();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose( true );
        }
        #endregion
    }
}
