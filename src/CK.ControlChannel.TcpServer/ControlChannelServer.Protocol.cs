using CK.ControlChannel.Abstractions;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelServer
    {
        TcpServerClientSession AuthenticateClient( IActivityMonitor m, Stream s )
        {
            byte version = (byte)s.ReadByte();
            if( version != Protocol.PROTOCOL_VERSION )
            {
                m.Error( $"Invalid PROTOCOL_VERSION {version}" );
                return null;
            }

            IReadOnlyDictionary<string, string> authControl = s.ReadControl();

            TcpServerClientSession session = new TcpServerClientSession(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                authControl,
                s
                );
            if( _authHandler.OnAuthorizeSession( session ) )
            {
                session.IsAuthenticated = true;
                if( !_activeSessions.TryAdd( session.SessionId, session ) )
                {
                    throw new InvalidOperationException( $"Could not add session {session.SessionId} to active sessions" );
                }
                s.WriteAck( Protocol.PROTOCOL_VERSION ); // Reply with the version
                return session;
            }
            else
            {
                s.WriteByte( Protocol.M_AUTH_FAIL );
                return null;
            }
        }

        private async Task HandleClientStreamAsync( IActivityMonitor m, Stream s, TcpClient c )
        {
            TcpServerClientSession session = null;
            try
            {
                session = AuthenticateClient( m, s );
                if( session != null )
                {
                    await HandleClientMessage( m, session );
                }
            }
            catch( Exception ex )
            {
                m.Error( ex );
                s.Error( Protocol.E_INTERNAL_ERROR );
            }
            finally
            {
                if( session != null )
                {
                    m.Debug( $"Disconnecting client gracefully" );
                    session.IsConnected = false;
                    if( !_activeSessions.TryRemove( session.SessionId, out session ) )
                    {
                        throw new InvalidOperationException( $"Could not remove session {session.SessionId} from active sessions" );
                    }
                }
            }
        }

        private Task HandleClientMessage( IActivityMonitor m, TcpServerClientSession session )
        {
            Debug.Assert( session != null && session.IsAuthenticated );
            bool quit = false;
            Stream s = session.Stream;
            ushort channelId;
            string channelName;
            byte[] payload;
            int len;
            ServerChannelDataHandler handler;
            while( !quit )
            {
                byte header = (byte)s.ReadByte();
                switch( header )
                {
                    case Protocol.M_PUB_TOPIC:
                        channelName = s.ReadString( Protocol.TextEncoding );
                        channelId = session.RegisterOrGetIncomingChannel( channelName );
                        m.Debug( () => $"Registered incoming channel {channelName} to {channelId}" );
                        s.WriteAck( Protocol.M_PUB_TOPIC );
                        s.WriteUInt16( channelId );
                        break;
                    case Protocol.M_UNPUB_TOPIC:
                        channelId = s.ReadUInt16();
                        session.UnsubscribeIncomingChannel( channelId );
                        m.Debug( () => $"Unregistered incoming channel {channelId}" );
                        s.WriteAck( Protocol.M_UNPUB_TOPIC );
                        break;
                    case Protocol.M_SUB_TOPIC:
                        channelName = s.ReadString( Protocol.TextEncoding );
                        channelId = session.RegisterOrGetOutgoingChannel( channelName );
                        m.Debug( () => $"Registered outgoing channel {channelName} to {channelId}" );
                        s.WriteAck( Protocol.M_SUB_TOPIC );
                        s.WriteUInt16( channelId );
                        break;
                    case Protocol.M_UNSUB_TOPIC:
                        channelId = s.ReadUInt16();
                        session.UnsubscribeOutgoingChannel( channelId );
                        m.Debug( () => $"Unregistered outgoing channel {channelId}" );
                        s.WriteAck( Protocol.M_UNSUB_TOPIC );
                        break;
                    case Protocol.M_MSG_PUB:
                        channelId = s.ReadUInt16();
                        channelName = session.GetIncomingChannelById( channelId );
                        if( channelName == null )
                        {
                            m.Error( $"Invalid incoming channelId {channelId}" );
                            s.Error( Protocol.E_INVALID_CHANNEL );
                            quit = true;
                            break;
                        }
                        len = s.ReadInt32();
                        if( len < 1 )
                        {
                            m.Error( $"Invalid incoming length {len}" );
                            s.Error( Protocol.E_INVALID_LENGTH );
                            quit = true;
                        }
                        else
                        {
                            payload = s.ReadBuffer( len );
                            s.WriteAck( Protocol.M_MSG_PUB );
                            if( _channelHandlers.TryGetValue( channelName, out handler ) )
                            {
                                handler( m, payload, session );
                            }
                        }
                        break;
                    case Protocol.M_ACK:
                        int type = s.ReadByte();
                        m.Debug( () => $"Received ACK from client for message type {type}" );
                        break;
                    case Protocol.M_PING:
                        m.Debug( () => $"Pong!" );
                        s.WriteByte( Protocol.M_PING );
                        break;
                    case Protocol.M_BYE:
                        m.Debug( () => $"Bye!" );
                        s.WriteByte( Protocol.M_BYE );
                        quit = true;
                        break;
                    default:
                        m.Error( $"Invalid header {header}" );
                        s.Error( Protocol.E_INVALID_MESSAGE );
                        quit = true;
                        break;
                }
            }
            return Task.FromResult( 0 );
        }
    }

}
