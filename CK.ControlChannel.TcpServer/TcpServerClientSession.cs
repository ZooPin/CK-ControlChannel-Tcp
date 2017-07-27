using System;
using System.Collections.Generic;
using System.Net.Sockets;
using CK.ControlChannel.Abstractions;
using System.Diagnostics;

namespace CK.ControlChannel.Tcp
{
    internal class TcpServerClientSession : IServerClientSession
    {
        private readonly TcpClient _tcpClient;

        private string _sessionId;
        private string _clientName;
        private bool _isConnected;
        private bool _isAuthenticated;
        private Dictionary<string, string> _clientData;

        internal TcpServerClientSession( TcpClient tcpClient )
        {
            Debug.Assert( tcpClient != null );
            _tcpClient = tcpClient;
        }

        public string SessionId => _sessionId;

        public string ClientName => _clientName;

        public bool IsConnected => _isConnected;

        public bool IsAuthenticated => _isAuthenticated;

        public IReadOnlyDictionary<string, string> ClientData => _clientData;

        public void Send( string channel, byte[] data )
        {
            throw new NotImplementedException();
        }

        internal TcpClient TcpClient => _tcpClient;
    }
}