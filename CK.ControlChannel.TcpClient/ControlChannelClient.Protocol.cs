using CK.ControlChannel.Abstractions;
using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelClient
    {
        private ConcurrentQueue<string> _pendingAckPub = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _pendingAckSub = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _pendingAckUnpub = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> _pendingAckUnsub = new ConcurrentQueue<string>();
        private ConcurrentQueue<ChannelMessage> _pendingAckMsg = new ConcurrentQueue<ChannelMessage>();

        private ConcurrentDictionary<string, ushort?> _pubChannels = new ConcurrentDictionary<string, ushort?>();
        private ConcurrentDictionary<string, ushort?> _subChannels = new ConcurrentDictionary<string, ushort?>();
        private ConcurrentDictionary<ushort, string> _subChannelsById = new ConcurrentDictionary<ushort, string>();

        /// <summary>
        /// Event fired after server acknowledges incoming channel registration.
        /// </summary>
        public event EventHandler<ChannelEventArgs> OnChannelRegistered;


        /// <summary>
        /// Perform authentication on the given stream.
        /// </summary>
        /// <returns>Awaitable task</returns>
        private async Task AuthenticateAsync( IActivityMonitor m )
        {
            Debug.Assert( this._dataStream != null && this._dataStream.CanRead && this._dataStream.CanWrite );
            m.Debug( () => $"Authenticating with protocol version {Protocol.PROTOCOL_VERSION}" );
            byte[] dataBuffer = ControlMessage.SerializeControlMessage( _authData );
            await _msgSemaphore.WaitAsync();
            try
            {
                _dataStream.WriteByte( Protocol.PROTOCOL_VERSION );
                _dataStream.WriteInt32( dataBuffer.Length );
                await _dataStream.WriteBufferAsync( dataBuffer );
            }
            finally
            {
                _msgSemaphore.Release();
            }
            int b = _dataStream.ReadByte();
            if( b < 0 ) { throw new IOException( $"Connection was closed during authentication" ); }
            if( b != Protocol.M_ACK ) { throw new ControlChannelException( $"Server refused authentication with {b}" ); }
            b = _dataStream.ReadByte();
            if( b != Protocol.PROTOCOL_VERSION )
            {
                m.Warn( $"Server responded with version {b}, expected {Protocol.PROTOCOL_VERSION}" );
            }
        }

        private void Listen( CancellationToken ct )
        {
            IActivityMonitor m = new ActivityMonitor();
            m.Debug( () => "Listening for data" );
            bool quit = false;
            ushort channelId;
            ushort? pendingChannelId;
            string channelName;
            int len;
            ChannelMessage msg;
            ClientChannelDataHandler handler;
            while( !ct.IsCancellationRequested && !quit )
            {
                int b = _dataStream.ReadByte();
                m.Debug( () => $"Receiving message {b}" );
                if( b < 0 ) { break; } // Disconnected
                else
                {
                    switch( b )
                    {
                        case Protocol.M_MSG_PUB:
                            m.Debug( () => "Receiving: M_MSG_PUB" );
                            channelId = _dataStream.ReadUInt16();
                            len = _dataStream.ReadInt32();
                            if( len > 0 )
                            {
                                byte[] buffer = _dataStream.ReadBuffer( len );
                                _msgSemaphore.Wait();
                                try
                                {
                                    m.Debug( () => "Writing ACK: M_MSG_PUB" );
                                    _dataStream.WriteAck( Protocol.M_MSG_PUB );
                                }
                                finally
                                {
                                    _msgSemaphore.Release();
                                }
                                if( _subChannelsById.TryGetValue( channelId, out channelName ) )
                                {
                                    m.Debug( () => $"Handling message with channel {channelId}: {channelName}" );
                                    if( _incomingChannelHandlers.TryGetValue( channelName, out handler ) )
                                    {
                                        handler( m, buffer );
                                    }
                                    else
                                    {
                                        m.Warn( () => $"No handler registered to handle channel {channelName}. Message is lost." );
                                    }
                                }
                                else
                                {
                                    m.Error( $"Could not locate incoming channel id {channelId}" );
                                    quit = true;
                                }
                            }
                            else
                            {
                                m.Error( $"Received invalid length {len}" );
                                quit = true;
                            }
                            break;
                        case Protocol.M_ACK:
                            int ackedHeader = _dataStream.ReadByte();
                            if( ackedHeader < 0 )
                            {
                                m.Debug( () => "ACK disconnecting" );
                                quit = true;
                                break;
                            } // Disconnected
                            switch( ackedHeader )
                            {
                                case Protocol.M_MSG_PUB:
                                    if( _pendingAckMsg.TryDequeue( out msg ) )
                                    {
                                        m.Debug( () => $"Received ACK for M_MSG_PUB" );
                                    }
                                    else
                                    {
                                        m.Error( $"Received ACK for M_MSG_PUB when there wasn't any pending" );
                                        quit = true;
                                    }
                                    break;
                                case Protocol.M_UNPUB_TOPIC:
                                    if( _pendingAckUnpub.TryDequeue( out channelName ) )
                                    {
                                        m.Debug( () => $"Received ACK for M_UNPUB_TOPIC" );
                                        if( _pubChannels.TryRemove( channelName, out pendingChannelId ) )
                                        {
                                            m.Debug( () => $"Removed outgoing channel {channelName} = {pendingChannelId}" );
                                        }
                                        else
                                        {
                                            m.Warn( $"Could not remove outgoing channel {channelName} from local registered channels" );
                                        }
                                    }
                                    else
                                    {
                                        m.Error( $"Received ACK for M_UNPUB_TOPIC when there wasn't any pending" );
                                        quit = true;
                                    }
                                    break;
                                case Protocol.M_UNSUB_TOPIC:
                                    if( _pendingAckUnsub.TryDequeue( out channelName ) )
                                    {
                                        m.Debug( () => $"Received ACK for M_UNSUB_TOPIC" );
                                        if( _subChannels.TryRemove( channelName, out pendingChannelId ) )
                                        {
                                            m.Debug( () => $"Removed incoming channel {channelName} = {pendingChannelId}" );
                                            _subChannelsById.TryRemove( pendingChannelId.Value, out channelName );
                                        }
                                        else
                                        {
                                            m.Warn( $"Could not remove incoming channel {channelName} from local registered channels" );
                                        }
                                    }
                                    else
                                    {
                                        m.Error( $"Received ACK for M_UNSUB_TOPIC when there wasn't any pending" );
                                        quit = true;
                                    }
                                    break;
                                case Protocol.M_PUB_TOPIC:
                                    if( _pendingAckPub.TryDequeue( out channelName ) )
                                    {
                                        channelId = _dataStream.ReadUInt16();
                                        m.Debug( () => $"Received ACK for M_PUB_TOPIC: Outgoing Channel {channelName} = {channelId}" );
                                        if( _pubChannels.TryUpdate( channelName, channelId, null ) )
                                        {
                                            m.Debug( () => $"Added outgoing channel {channelName} = {channelId}" );

                                            m.Debug( () => $"Outgoing channels changed: Processing queue in background" );
                                            Task.Run( () => ProcessQueueAsync( new ActivityMonitor() ) );
                                        }
                                        else
                                        {
                                            m.Warn( $"Could not add outgoing channel {channelName} to local registered channels" );
                                        }
                                    }
                                    else
                                    {
                                        m.Error( $"Received ACK for M_PUB_TOPIC when there wasn't any pending" );
                                        quit = true;
                                    }
                                    break;
                                case Protocol.M_SUB_TOPIC:
                                    if( _pendingAckSub.TryDequeue( out channelName ) )
                                    {
                                        channelId = _dataStream.ReadUInt16();
                                        m.Debug( () => $"Received ACK for M_SUB_TOPIC: Incoming Channel {channelName} = {channelId}" );
                                        if( _subChannels.TryUpdate( channelName, channelId, null ) )
                                        {
                                            m.Debug( () => $"Added incoming channel {channelName} = {channelId}" );
                                            _subChannelsById.TryAdd( channelId, channelName );
                                            if( OnChannelRegistered != null )
                                            {
                                                try
                                                {
                                                    OnChannelRegistered( this, new ChannelEventArgs( channelName ) );
                                                }
                                                catch( Exception ex )
                                                {
                                                    m.Error( "Caught during OnChannelRegistered event", ex );
                                                }
                                            }
                                        }
                                        else
                                        {
                                            m.Warn( $"Could not add incoming channel {channelName} to local registered channels" );
                                        }
                                    }
                                    else
                                    {
                                        m.Error( $"Received ACK for M_SUB_TOPIC when there wasn't any pending" );
                                        quit = true;
                                    }
                                    break;
                                default:
                                    m.Error( $"Unknown ACK for type {b}" );
                                    quit = true;
                                    break;
                            }
                            break;
                        default:
                            m.Error( $"Unknown message header {b}" );
                            quit = true;
                            break;
                    }
                }
            }
        }

        private void RegisterIncomingChannel( string channelName )
        {
            if( _subChannels.TryAdd( channelName, null ) )
            {
                Debug.Assert( _dataStream != null );
                _pendingAckSub.Enqueue( channelName );
                _dataStream.WriteByte( Protocol.M_SUB_TOPIC );
                _dataStream.WriteString( channelName, Protocol.TextEncoding );
            }
        }

        private void RegisterOutgoingChannel( string channelName )
        {
            if( _pubChannels.TryAdd( channelName, null ) )
            {
                Debug.Assert( _dataStream != null );
                _pendingAckPub.Enqueue( channelName );
                _dataStream.WriteByte( Protocol.M_PUB_TOPIC );
                _dataStream.WriteString( channelName, Protocol.TextEncoding );
            }
        }
    }
}
