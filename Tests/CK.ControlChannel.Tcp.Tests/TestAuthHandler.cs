using CK.ControlChannel.Abstractions;
using System;

namespace CK.ControlChannel.Tcp.Tests
{
    public class TestAuthHandler : IAuthorizationHandler
    {
        private readonly Func<IServerClientSession, bool> _handler;

        public TestAuthHandler( Func<IServerClientSession, bool> handler )
        {
            _handler = handler;
        }
        public bool OnAuthorizeSession( IServerClientSession s )
        {
            return _handler( s );
        }
    }
}
