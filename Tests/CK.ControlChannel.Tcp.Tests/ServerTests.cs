using CK.ControlChannel.Abstractions;
using CK.ControlChannel.Tcp;
using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public async Task server_auth_can_fail()
        {
            var authHandler = new TestAuthHandler( ( session ) => false );
            using( ControlChannelServer server = TestHelper.CreateDefaultServer( authHandler ) )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( Stream s = await client.GetDataStreamAsync( server ) )
                    {
                        // Authentication
                        s.WriteByte( Protocol.PROTOCOL_VERSION );
                        s.WriteControl( new Dictionary<string, string>()
                        {
                            ["hello"] = "world",
                        } );

                        s.ReadByte().Should().Be( Protocol.M_AUTH_FAIL );
                        s.ReadByte().Should().Be( -1 ); // EOS
                        server.ActiveSessions.Should().HaveCount( 0 );
                    }
                }
            }
        }

        [Fact]
        public async Task server_auth_can_succeed()
        {
            var authHandler = new TestAuthHandler( ( session ) => true );
            using( ControlChannelServer server = TestHelper.CreateDefaultServer( authHandler ) )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( Stream s = await client.GetDataStreamAsync( server ) )
                    {
                        // Authentication
                        s.WriteByte( Protocol.PROTOCOL_VERSION );
                        s.WriteControl( new Dictionary<string, string>()
                        {
                            ["hello"] = "world",
                        } );
                        s.ReadAck().Should().Be( Protocol.PROTOCOL_VERSION );
                        server.ActiveSessions.Should().HaveCount( 1 );
                        var session = server.ActiveSessions.First();
                        session.IsConnected.Should().Be( true );
                        session.IsAuthenticated.Should().Be( true );
                    }
                }
            }
        }

        [Fact]
        public async Task server_ping()
        {
            using( ControlChannelServer server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( Stream s = await client.GetDataStreamAsync( server ) )
                    {
                        s.WriteDummyAuth();

                        // Ping
                        s.WritePing();
                        byte r = (byte)s.ReadByte();
                        r.Should().Be( Protocol.M_PING );

                        server.ActiveSessions.Should().HaveCount( 1 );
                        var session = server.ActiveSessions.First();
                        session.IsConnected.Should().Be( true );
                        session.IsAuthenticated.Should().Be( true );
                    }
                }
            }
        }

        [Fact]
        public async Task server_quit()
        {
            using( ControlChannelServer server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( Stream s = await client.GetDataStreamAsync( server ) )
                    {
                        s.WriteDummyAuth();

                        // Bye
                        s.WriteBye();
                        s.ReadByte().Should().Be( Protocol.M_BYE );

                        // Verify disconnect
                        s.EnsureDisconnected();
                        server.ActiveSessions.Should().HaveCount( 0 );
                    }
                }
            }
        }

        [Fact]
        public async Task server_incoming_channel_error()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;
                // Send data
                byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                s.WriteByte( Protocol.M_MSG_PUB );
                s.WriteUInt16( 0xFFFF );
                //s.WriteInt32( data.Length );
                //s.WriteBuffer( data );
                s.ReadByte().Should().Be( Protocol.M_ERROR );
                s.ReadString( Protocol.TextEncoding ).Should().Be( Protocol.E_INVALID_CHANNEL );

                // Verify disconnect
                s.EnsureDisconnected();
                c.Server.ActiveSessions.Should().HaveCount( 0 );
            }
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
                        // Authentication
                        st.WriteByte( Protocol.PROTOCOL_VERSION );
                        st.WriteControl( new Dictionary<string, string>()
                        {
                            ["hello"] = "world",
                            ["username"] = "whatever",
                            ["password"] = "whatever",
                        } );
                        st.ReadAck().Should().Be( Protocol.PROTOCOL_VERSION );

                        // Ping
                        st.WritePing();
                        byte r = (byte)st.ReadByte();
                        r.Should().Be( Protocol.M_PING );

                        // Register client -> server channel
                        st.WriteByte( Protocol.M_PUB_TOPIC );
                        st.WriteString( "test", Protocol.TextEncoding );
                        ushort testChannelId = st.ReadUInt16();

                        // Register server -> client channel
                        st.WriteByte( Protocol.M_SUB_TOPIC );
                        st.WriteString( "test-backchannel", Protocol.TextEncoding );
                        ushort testBackchannelChannelId = st.ReadUInt16();

                        // Send data
                        byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                        st.WriteByte( Protocol.M_MSG_PUB );
                        st.WriteUInt16( testChannelId );
                        st.WriteInt32( data.Length );
                        st.WriteBuffer( data );
                        st.ReadAck().Should().Be( Protocol.M_MSG_PUB );

                        // Peceive data
                        st.ReadByte().Should().Be( Protocol.M_MSG_PUB );
                        st.ReadUInt16().Should().Be( testBackchannelChannelId );
                        int len = st.ReadInt32();
                        data = await st.ReadBufferAsync( len );
                        data.ShouldAllBeEquivalentTo( new byte[] { 0x04, 0x05, 0x06, 0x07 } );
                        st.WriteAck( Protocol.M_MSG_PUB );

                        // Bye
                        st.WriteBye();
                        r = (byte)st.ReadByte();
                        r.Should().Be( Protocol.M_BYE );

                        st.EnsureDisconnected();
                    }
                }
            }
        }

        [Fact]
        public async Task high_speed_ping_pong_battle()
        {
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                List<Task> clientTasks = new List<Task>();
                for( int i = 0; i < Environment.ProcessorCount; i++ )
                {
                    clientTasks.Add( Task.Factory.StartNew( async () =>
                    {

                        var m = new ActivityMonitor();
                        using( var client = TestHelper.CreateTcpClient() )
                        {
                            await client.ConnectAsync( server );
                            var s = await client.GetDataStreamAsync( server );
                            s.WriteDummyAuth();
                            for( int j = 0; j < 1000; j++ )
                            {
                                m.Debug( "Ping!" );
                                s.WritePing();
                                s.ReadByte().Should().Be( Protocol.M_PING );
                            }
                        }

                    }, TaskCreationOptions.LongRunning ).Unwrap() );
                }
                Task.WaitAll( clientTasks.ToArray() );
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
