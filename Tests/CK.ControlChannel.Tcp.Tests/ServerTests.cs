using CK.ControlChannel.Abstractions;
using CK.ControlChannel.Tcp;
using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
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
            using( ControlChannelServer s = new ControlChannelServer( host, port, TestHelper.DefaultAuthHandler ) )
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
            using( ControlChannelServer s = new ControlChannelServer( TestHelper.DefaultHost, TestHelper.DefaultPort, TestHelper.DefaultAuthHandler, serverCertificate ) )
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
            ManualResetEvent evt = new ManualResetEvent( false );
            var authHandler = new TestAuthHandler( ( session ) =>
            {
                evt.Set();
                return true;
            } );
            using( ControlChannelServer s = TestHelper.CreateDefaultServer( authHandler ) )
            {
                s.Open();
                s.RegisterChannelHandler( "test", ( m, data, session ) =>
                 {
                     data.ShouldAllBeEquivalentTo( new byte[] { 0x00, 0x01, 0x02, 0x03 } );
                     session.Send( "test-backchannel", new byte[] { 0x04, 0x05, 0x06, 0x07 } );
                     evt.Set();
                 } );
                using( var c = TestHelper.CreateTcpClient() )
                {
                    await c.ConnectAsync( s );
                    using( Stream st = await c.GetDataStreamAsync( s ) )
                    {
                        st.WriteProtocolVersion();
                        st.WriteAuthentication();

                        evt.WaitOne();
                        evt.Reset();

                        var dict = st.ReadControl();
                        dict.Keys.Should().Contain( ControlMessage.TypeKey );
                        dict[ControlMessage.TypeKey].Should().Be( "AuthOk" );

                        byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                        st.WriteByte( ControlChannelServer.H_MSG );
                        st.WriteString( "test", Encoding.UTF8 );
                        st.WriteInt32( data.Length );
                        st.WriteBuffer( data );

                        st.Flush();
                        evt.WaitOne();
                        evt.Reset();

                        byte header = (byte)st.ReadByte();
                        header.Should().Be( ControlChannelServer.H_MSG );
                        string channel = st.ReadString( Encoding.UTF8 );
                        channel.Should().Be( "test-backchannel" );
                        int len = st.ReadInt32();
                        data = st.ReadBuffer( len );

                        data.ShouldAllBeEquivalentTo( new byte[] { 0x04, 0x05, 0x06, 0x07 } );

                        st.WriteByte( ControlChannelServer.H_BYE );
                        st.ReadByte().Should().Be( ControlChannelServer.H_BYE );
                    }
                }
            }
        }
    }

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
