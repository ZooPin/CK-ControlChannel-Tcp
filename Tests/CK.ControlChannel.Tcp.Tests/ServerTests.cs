﻿using CK.ControlChannel.TcpServer;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace CK.ControlChannel.Tcp.Tests
{
    [Collection( "Main collection" )]
    public class ServerTests
    {
        [Fact]
        public void server_can_be_created_with_valid_properties()
        {
            string host = "localhost";
            int port = 43712;
            using( ControlChannelServer s = new ControlChannelServer( host, port ) )
            {
                s.Host.Should().Be( host );
                s.Port.Should().Be( port );
                s.IsSecure.Should().BeFalse();
                s.IsOpen.Should().BeFalse();
                s.ActiveSessions.Should().NotBeNull().And.BeEmpty();
            }
        }
    }
}
