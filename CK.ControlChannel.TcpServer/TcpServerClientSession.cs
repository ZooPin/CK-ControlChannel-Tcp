using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CK.ControlChannel.Abstractions;
using System.Diagnostics;
using CK.Core;
using System.IO;
using System.Text;
using System.Linq;

namespace CK.ControlChannel.Tcp
{
    internal class TcpServerClientSession : IServerClientSession
    {
        private string _sessionId;
        private string _clientName;
        private readonly IReadOnlyDictionary<string, string> _clientData;
        private readonly List<string> _incomingChannels = new List<string>();
        private readonly Dictionary<string, ushort> _incomingChannelsByName = new Dictionary<string, ushort>();
        private readonly List<string> _outgoingChannels = new List<string>();
        private readonly Dictionary<string, ushort> _outgoingChannelsByName = new Dictionary<string, ushort>();
        private readonly Stream _stream;

        internal TcpServerClientSession( string sessionId, string clientName,
            IReadOnlyDictionary<string, string> clientData, Stream stream )
        {
            _sessionId = sessionId;
            _clientName = clientName;
            _clientData = clientData;
            _stream = stream;
            IsConnected = true;
        }

        public string SessionId => _sessionId;

        public string ClientName => _clientName;

        public bool IsConnected { get; internal set; }

        public bool IsAuthenticated { get; internal set; }

        public IReadOnlyDictionary<string, string> ClientData => _clientData;

        internal Stream Stream => _stream;

        public void Send( string channel, byte[] data )
        {
            if( channel == null ) { throw new ArgumentNullException( nameof( channel ) ); }
            if( data == null ) { throw new ArgumentNullException( nameof( data ) ); }
            ushort channelId = 0;
            if( _outgoingChannelsByName.TryGetValue( channel, out channelId ) )
            {
                _stream.WriteByte( Protocol.M_MSG_PUB );
                _stream.WriteUInt16( channelId );
                _stream.WriteInt32( data.Length );
                _stream.WriteBuffer( data );
                _stream.EnsureAck( Protocol.M_MSG_PUB );
            }
        }

        internal ushort RegisterOrGetIncomingChannel( string channel )
        {
            ushort channelId = 0;
            if( !_incomingChannelsByName.TryGetValue( channel, out channelId ) )
            {
                int idx = _incomingChannels.IndexOf( null );
                if( idx < 0 && _incomingChannels.Count < UInt16.MaxValue )
                {
                    _incomingChannels.Add( channel );
                    idx = _incomingChannels.Count - 1;
                }
                else
                {
                    _incomingChannels[idx] = channel;
                }
                channelId = (ushort)idx;
                _incomingChannelsByName[channel] = channelId;
            }
            return channelId;
        }
        internal ushort RegisterOrGetOutgoingChannel( string channel )
        {
            ushort channelId = 0;
            if( !_outgoingChannelsByName.TryGetValue( channel, out channelId ) )
            {
                int idx = _outgoingChannels.IndexOf( null );
                if( idx < 0 && _outgoingChannels.Count < UInt16.MaxValue )
                {
                    _outgoingChannels.Add( channel );
                    idx = _outgoingChannels.Count - 1;
                }
                else
                {
                    _outgoingChannels[idx] = channel;
                }
                channelId = (ushort)idx;
                _outgoingChannelsByName[channel] = channelId;
            }
            return channelId;
        }

        internal void UnsubscribeIncomingChannel( ushort channelId )
        {
            string s = _incomingChannels[channelId];
            _incomingChannels[channelId] = null;
            _incomingChannelsByName.Remove( s );
        }

        internal void UnsubscribeOutgoingChannel( ushort channelId )
        {
            string s = _outgoingChannels[channelId];
            _outgoingChannels[channelId] = null;
            _outgoingChannelsByName.Remove( s );
        }

        internal string GetIncomingChannelById( ushort channelId )
        {
            if( channelId < _incomingChannels.Count )
            {
                return _incomingChannels[channelId];
            }
            return null;
        }
        internal string GetOutgoingChannelById( ushort channelId )
        {
            if( channelId < _outgoingChannels.Count )
            {
                return _outgoingChannels[channelId];
            }
            return null;
        }
    }
}
