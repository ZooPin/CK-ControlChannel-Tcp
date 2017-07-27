using CK.ControlChannel.Tcp;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
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

        [Fact]
        public void server_can_be_created_with_ssl_certificate()
        {
            X509Certificate2 serverCertificate = TestHelper.ServerCertificate;
            using( ControlChannelServer s = new ControlChannelServer( TestHelper.DefaultHost, TestHelper.DefaultPort, serverCertificate ) )
            {
                s.IsSecure.Should().BeTrue();
            }
        }

        [Fact]
        public void server_can_be_opened()
        {
            using( ControlChannelServer s = TestHelper.CreateDefaultServer() )
            {
                s.Open();
                s.IsOpen.Should().BeTrue();
            }
        }

        [Fact]
        public void server_cannot_be_opened_when_opened()
        {
            using( ControlChannelServer s = TestHelper.CreateDefaultServer() )
            {
                s.Open();

                Action act = () => s.Open();

                act.ShouldThrow<InvalidOperationException>();
            }
        }

        [Fact]
        public void closing_server_updates_properties()
        {
            using( ControlChannelServer s = TestHelper.CreateDefaultServer() )
            {
                s.Open();

                s.Close();

                s.IsOpen.Should().BeFalse();
                s.IsDisposed.Should().BeFalse();
            }
        }

        [Fact]
        public void server_can_be_closed_and_reopened()
        {
            using( ControlChannelServer s = TestHelper.CreateDefaultServer() )
            {
                s.Open();
                s.Close();

                s.Open();

                s.IsOpen.Should().BeTrue();
            }
        }

        [Fact]
        public void disposing_closes_server()
        {
            ControlChannelServer s = TestHelper.CreateDefaultServer();
            s.Open();

            s.Dispose();

            s.IsDisposed.Should().BeTrue();
            s.IsOpen.Should().BeFalse();

        }

        [Fact]
        public async Task server_tcp_test()
        {
            using( ControlChannelServer s = TestHelper.CreateDefaultServer() )
            {
                s.Open();
                using( var c = TestHelper.CreateTcpClient() )
                {
                    await c.ConnectAsync( s );
                    using( Stream st = await c.GetDataStreamAsync( s ) )
                    {
                        await st.WriteProtocolVersion();
                    }
                }
            }
        }
    }
}
