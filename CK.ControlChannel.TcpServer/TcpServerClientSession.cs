using System;
using System.Collections.Generic;
using CK.ControlChannel.Abstractions;

namespace CK.ControlChannel.TcpServer
{
    internal class TcpServerClientSession : IServerClientSession
    {
        private string _sessionId;
        private string _clientName;
        private bool _isConnected;
        private bool _isAuthenticated;
        private Dictionary<string, string> _clientData;

        public string SessionId => _sessionId;

        public string ClientName => _clientName;

        public bool IsConnected => _isConnected;

        public bool IsAuthenticated => _isAuthenticated;

        public IReadOnlyDictionary<string, string> ClientData => _clientData;

        public void Send( string channel, byte[] data )
        {
            throw new NotImplementedException();
        }
    }
}