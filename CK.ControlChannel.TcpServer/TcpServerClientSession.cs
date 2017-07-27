using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CK.ControlChannel.Abstractions;
using System.Diagnostics;
using CK.Core;
using System.IO;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    internal class TcpServerClientSession : IServerClientSession
    {
        private readonly TcpClient _tcpClient;

        private string _sessionId;
        private string _clientName;
        private readonly IReadOnlyDictionary<string, string> _clientData;
        private readonly Stream _stream;

        internal TcpServerClientSession( TcpClient tcpClient, string sessionId, string clientName,
            IReadOnlyDictionary<string, string> clientData, Stream stream )
        {
            Debug.Assert( tcpClient != null );
            _tcpClient = tcpClient;
            _sessionId = sessionId;
            _clientName = clientName;
            _clientData = clientData;
            _stream = stream;
        }

        public string SessionId => _sessionId;

        public string ClientName => _clientName;

        public bool IsConnected => _tcpClient.Connected;

        public bool IsAuthenticated { get; internal set; }

        public IReadOnlyDictionary<string, string> ClientData => _clientData;

        public void Send( string channel, byte[] data )
        {
            _stream.WriteByte( ControlChannelServer.H_MSG );
            _stream.WriteString( channel, Encoding.UTF8 );
            _stream.WriteInt32( data.Length );
            _stream.WriteBuffer( data );
        }

        internal TcpClient TcpClient => _tcpClient;

        internal Stream Stream => _stream;
    }
}