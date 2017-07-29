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
        public async Task server_data_length_error()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;

                // Register client -> server channel
                s.WriteByte( Protocol.M_PUB_TOPIC );
                s.WriteString( "test", Protocol.TextEncoding );
                ushort testChannelId = s.ReadUInt16();

                // Send data
                byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                s.WriteByte( Protocol.M_MSG_PUB );
                s.WriteUInt16( testChannelId );
                s.WriteInt32( -1 );
                //s.WriteBuffer( data );
                s.ReadByte().Should().Be( Protocol.M_ERROR );
                s.ReadString( Protocol.TextEncoding ).Should().Be( Protocol.E_INVALID_LENGTH );

                // Verify disconnect
                s.EnsureDisconnected();
                c.Server.ActiveSessions.Should().HaveCount( 0 );
            }
        }

        [Fact]
        public async Task server_incoming_channel_error_after_unregistering()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;

                // Register client -> server channel
                s.WriteByte( Protocol.M_PUB_TOPIC );
                s.WriteString( "test", Protocol.TextEncoding );
                ushort testChannelId = s.ReadUInt16();

                // Send data
                byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                s.WriteByte( Protocol.M_MSG_PUB );
                s.WriteUInt16( testChannelId );
                s.WriteInt32( data.Length );
                s.WriteBuffer( data );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_MSG_PUB );

                // Unregister client -> server channel
                s.WriteByte( Protocol.M_UNPUB_TOPIC );
                s.WriteUInt16( testChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNPUB_TOPIC );

                // Send data
                data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                s.WriteByte( Protocol.M_MSG_PUB );
                s.WriteUInt16( testChannelId );
                s.ReadByte().Should().Be( Protocol.M_ERROR );
                s.ReadString( Protocol.TextEncoding ).Should().Be( Protocol.E_INVALID_CHANNEL );

                // Verify disconnect
                s.EnsureDisconnected();
                c.Server.ActiveSessions.Should().HaveCount( 0 );
            }
        }

        [Fact]
        public async Task server_outgoing_test()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;
                var session = c.Server.ActiveSessions.Single();

                // Register server -> client channel
                s.WriteByte( Protocol.M_SUB_TOPIC );
                s.WriteString( "test", Protocol.TextEncoding );
                ushort testChannelId = s.ReadUInt16();

                // Other side needs to be on other thread
                var t = Task.Run( () => session.Send( "test", new byte[] { 0x00 } ) );

                // Receive data
                s.ReadByte().Should().Be( Protocol.M_MSG_PUB );
                s.ReadUInt16().Should().Be( testChannelId );
                int len = s.ReadInt32();
                var data = await s.ReadBufferAsync( len );
                data.ShouldAllBeEquivalentTo( new byte[] { 0x00 } );
                s.WriteAck( Protocol.M_MSG_PUB );

            }
        }

        [Fact]
        public async Task server_incoming_channel_registration_reuses_ids()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;

                // Register client -> server channel
                s.WriteByte( Protocol.M_PUB_TOPIC );
                s.WriteString( "test", Protocol.TextEncoding );
                ushort testChannelId = s.ReadUInt16();

                testChannelId.Should().Be( 0 );

                // Register client -> server channel
                s.WriteByte( Protocol.M_PUB_TOPIC );
                s.WriteString( "test2", Protocol.TextEncoding );
                ushort test2ChannelId = s.ReadUInt16();

                test2ChannelId.Should().Be( 1 );

                // Unregister client -> server channel
                s.WriteByte( Protocol.M_UNPUB_TOPIC );
                s.WriteUInt16( testChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNPUB_TOPIC );

                // Register client -> server channel
                s.WriteByte( Protocol.M_PUB_TOPIC );
                s.WriteString( "test3", Protocol.TextEncoding );
                ushort test3ChannelId = s.ReadUInt16();

                test3ChannelId.Should().Be( 0 );

                // Unregister client -> server channel
                s.WriteByte( Protocol.M_UNPUB_TOPIC );
                s.WriteUInt16( test2ChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNPUB_TOPIC );

                // Unregister client -> server channel
                s.WriteByte( Protocol.M_UNPUB_TOPIC );
                s.WriteUInt16( test3ChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNPUB_TOPIC );

            }
        }

        [Fact]
        public async Task server_outgoing_channel_registration_reuses_ids()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;

                // Register server -> client channel
                s.WriteByte( Protocol.M_SUB_TOPIC );
                s.WriteString( "test", Protocol.TextEncoding );
                ushort testChannelId = s.ReadUInt16();

                testChannelId.Should().Be( 0 );

                // Register server -> client channel
                s.WriteByte( Protocol.M_SUB_TOPIC );
                s.WriteString( "test2", Protocol.TextEncoding );
                ushort test2ChannelId = s.ReadUInt16();

                test2ChannelId.Should().Be( 1 );

                // Unregister server -> client channel
                s.WriteByte( Protocol.M_UNSUB_TOPIC );
                s.WriteUInt16( testChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNSUB_TOPIC );

                // Register server -> client channel
                s.WriteByte( Protocol.M_SUB_TOPIC );
                s.WriteString( "test3", Protocol.TextEncoding );
                ushort test3ChannelId = s.ReadUInt16();

                test3ChannelId.Should().Be( 0 );

                // Unregister server -> client channel
                s.WriteByte( Protocol.M_UNSUB_TOPIC );
                s.WriteUInt16( test2ChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNSUB_TOPIC );

                // Unregister server -> client channel
                s.WriteByte( Protocol.M_UNSUB_TOPIC );
                s.WriteUInt16( test3ChannelId );
                s.ReadByte().Should().Be( Protocol.M_ACK );
                s.ReadByte().Should().Be( Protocol.M_UNSUB_TOPIC );

            }
        }

        [Fact]
        public async Task server_invalid_message_error()
        {
            using( var c = await TestServerWithTcpClient.Create() )
            {
                var s = c.Stream;
                s.WriteByte( 0x72 );
                s.ReadByte().Should().Be( Protocol.M_ERROR );
                s.ReadString( Protocol.TextEncoding ).Should().Be( Protocol.E_INVALID_MESSAGE );

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
            using( ControlChannelServer server = TestHelper.CreateDefaultServer( authHandler ) )
            {
                server.Open();
                server.RegisterChannelHandler( "test", ( m, data, session ) =>
                 {
                     data.ShouldAllBeEquivalentTo( new byte[] { 0x00, 0x01, 0x02, 0x03 } );
                     session.Send( "test-backchannel", new byte[] { 0x04, 0x05, 0x06, 0x07 } );
                     evt.Set();
                 } );
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
                            ["username"] = "whatever",
                            ["password"] = "whatever",
                        } );
                        s.ReadAck().Should().Be( Protocol.PROTOCOL_VERSION );

                        // Ping
                        s.WritePing();
                        byte r = (byte)s.ReadByte();
                        r.Should().Be( Protocol.M_PING );

                        // Register client -> server channel
                        s.WriteByte( Protocol.M_PUB_TOPIC );
                        s.WriteString( "test", Protocol.TextEncoding );
                        ushort testChannelId = s.ReadUInt16();

                        // Register server -> client channel
                        s.WriteByte( Protocol.M_SUB_TOPIC );
                        s.WriteString( "test-backchannel", Protocol.TextEncoding );
                        ushort testBackchannelChannelId = s.ReadUInt16();

                        // Send data
                        byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03 };
                        s.WriteByte( Protocol.M_MSG_PUB );
                        s.WriteUInt16( testChannelId );
                        s.WriteInt32( data.Length );
                        s.WriteBuffer( data );
                        s.ReadAck().Should().Be( Protocol.M_MSG_PUB );

                        // Receive data
                        s.ReadByte().Should().Be( Protocol.M_MSG_PUB );
                        s.ReadUInt16().Should().Be( testBackchannelChannelId );
                        int len = s.ReadInt32();
                        data = await s.ReadBufferAsync( len );
                        data.ShouldAllBeEquivalentTo( new byte[] { 0x04, 0x05, 0x06, 0x07 } );
                        s.WriteAck( Protocol.M_MSG_PUB );

                        // Bye
                        s.WriteBye();
                        r = (byte)s.ReadByte();
                        r.Should().Be( Protocol.M_BYE );

                        s.EnsureDisconnected();
                    }
                }
            }
        }

        [Fact]
        public void high_speed_ping_pong_battle()
        {
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.RegisterChannelHandler( "ping", ( m, data, clientSession ) =>
                {
                    m.Debug( "Pong!!" );
                    clientSession.Send( "pong", new byte[] { 0x02 } );
                } );
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

                                s.WriteByte( Protocol.M_PUB_TOPIC );
                                s.WriteString( "ping", Protocol.TextEncoding );
                                ushort pingChanId = s.ReadUInt16();

                                s.WriteByte( Protocol.M_SUB_TOPIC );
                                s.WriteString( "pong", Protocol.TextEncoding );
                                ushort pongChanId = s.ReadUInt16();

                                m.Debug( "Ping!!" );
                                s.WriteByte( Protocol.M_MSG_PUB );
                                s.WriteUInt16( pingChanId );
                                var data = new byte[] { 0x02 };
                                s.WriteInt32( data.Length );
                                s.WriteBuffer( data );
                                s.ReadAck().Should().Be( Protocol.M_MSG_PUB );

                                // Receive data
                                s.ReadByte().Should().Be( Protocol.M_MSG_PUB );
                                s.ReadUInt16().Should().Be( pongChanId );
                                int len = s.ReadInt32();
                                data = await s.ReadBufferAsync( len );
                                s.WriteAck( Protocol.M_MSG_PUB );

                                s.WriteByte( Protocol.M_UNPUB_TOPIC );
                                s.WriteUInt16( pingChanId );
                                s.ReadAck().Should().Be( Protocol.M_UNPUB_TOPIC );

                                s.WriteByte( Protocol.M_UNSUB_TOPIC );
                                s.WriteUInt16( pongChanId );
                                s.ReadAck().Should().Be( Protocol.M_UNSUB_TOPIC );
                            }
                        }

                    }, TaskCreationOptions.LongRunning ).Unwrap() );
                }
                Task.WaitAll( clientTasks.ToArray() );
            }
        }

        [Fact]
        public async Task server_accepts_client_ssl_certificate()
        {
            using( var server = TestHelper.CreateDefaultServer( null, TestHelper.ClientCertificateValidationCallback ) )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( var s = await client.GetDataStreamAsync( server, TestHelper.ClientCertificateSelectionCallback ) )
                    {
                        s.WriteDummyAuth();
                        s.WriteBye();
                        s.ReadByte();
                    }
                }
            }
        }

        [Fact]
        public async Task server_refuses_client_ssl_certificate()
        {
            using( var server = TestHelper.CreateDefaultServer( null, ( sender, cert, chain, policyErrors ) => false ) )
            {
                server.Open();
                using( var client = TestHelper.CreateTcpClient() )
                {
                    await client.ConnectAsync( server );
                    using( var s = await client.GetDataStreamAsync( server, TestHelper.ClientCertificateSelectionCallback ) )
                    {
                        Action act = () =>
                        {
                            s.WriteDummyAuth();
                            s.WriteBye();
                            s.ReadByte();
                        };
                        act.ShouldThrow<IOException>().WithInnerException<SocketException>();
                    }
                }
            }
        }
    }
}
