using CK.Core;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CK.ControlChannel.Tcp.Tests
{
    [Collection( "Main collection" )]
    public class ClientTests
    {
        [Fact]
        public void client_can_be_created_with_valid_properties()
        {
            string host = "localhost";
            int port = 43712;
            Dictionary<string, string> authenticationData = new Dictionary<string, string>()
            {
                [ "hello" ] = "world"
            };
            using( ControlChannelClient client = new ControlChannelClient( host, port, authenticationData, false ) )
            {
                client.Host.Should().Be( host );
                client.Port.Should().Be( port );
                client.IsSecure.Should().BeFalse();
                client.IsOpen.Should().BeFalse();
            }
        }

        [Fact]
        public async Task client_can_be_opened()
        {
            var m = new ActivityMonitor();
            Dictionary<string, string> authenticationData = new Dictionary<string, string>()
            {
                [ "hello" ] = "world"
            };
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                using( var client = new ControlChannelClient(
                    server.Host,
                    server.Port,
                    authenticationData,
                    true,
                    TestHelper.ServerCertificateValidationCallback,
                    TestHelper.ClientCertificateSelectionCallback
                    ) )
                {
                    await client.OpenAsync( m );
                    client.IsOpen.Should().BeTrue();
                    client.IsSecure.Should().BeTrue();
                }
            }
        }

        [Fact]
        public async Task client_can_send_messages()
        {
            var m = new ActivityMonitor();
            var ev = new ManualResetEvent( false );
            bool complete = false;
            Dictionary<string, string> authenticationData = new Dictionary<string, string>()
            {
                [ "hello" ] = "world"
            };
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                server.RegisterChannelHandler( "test", ( mo, d, c ) =>
                {
                    ev.Set();
                    complete = true;
                } );
                using( var client = new ControlChannelClient(
                    server.Host,
                    server.Port,
                    authenticationData,
                    true,
                    TestHelper.ServerCertificateValidationCallback,
                    TestHelper.ClientCertificateSelectionCallback
                    ) )
                {
                    await client.OpenAsync( m );
                    await client.SendAsync( "test", new byte[] { 0x00 } );

                    ev.WaitOne( 1000 );
                    complete.Should().Be( true );
                }
            }
        }

        [Fact]
        public async Task client_can_receive_messages()
        {
            var m = new ActivityMonitor();
            var ev = new ManualResetEvent( false );
            bool complete = false;
            Dictionary<string, string> authenticationData = new Dictionary<string, string>()
            {
                [ "hello" ] = "world"
            };
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                using( var client = new ControlChannelClient(
                    server.Host,
                    server.Port,
                    authenticationData,
                    true,
                    TestHelper.ServerCertificateValidationCallback,
                    TestHelper.ClientCertificateSelectionCallback
                    ) )
                {
                    await client.OpenAsync( m );

                    client.OnChannelRegistered += ( sender, chanArgs ) =>
                    {
                        sender.Should().Be( client );
                        chanArgs.ChannelName.Should().Be( "test" );
                        ev.Set();
                    };

                    // Register incoming channel
                    client.RegisterChannelHandler( "test", ( mon, data ) =>
                     {
                         data.Should().BeEquivalentTo( new byte[] { 0x42 } );
                         complete = true;
                         ev.Set();
                     } );

                    // Wait for registration
                    ev.WaitOne();
                    ev.Reset();

                    // Send Server-to-Client message
                    await Task.Run( () => server.ActiveSessions.Single().Send( "test", new byte[] { 0x42 } ) );

                    ev.WaitOne();
                    complete.Should().Be( true );
                }
            }
        }

        [Fact]
        public async Task client_can_queue_message_offline()
        {
            var m = new ActivityMonitor();
            Dictionary<string, string> authenticationData = new Dictionary<string, string>()
            {
                [ "hello" ] = "world"
            };
            using( var client = new ControlChannelClient(
                TestHelper.DefaultHost,
                TestHelper.DefaultPort,
                authenticationData,
                true,
                TestHelper.ServerCertificateValidationCallback,
                TestHelper.ClientCertificateSelectionCallback
                ) )
            {
                await client.OpenAsync( m );

                await client.SendAsync( "test", new byte[] { 0x00 } );
            }
        }

        [Fact]
        public async Task client_can_send_messages_without_connecting_manually()
        {
            var m = new ActivityMonitor();
            var ev = new ManualResetEvent( false );
            bool complete = false;
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                server.RegisterChannelHandler( "test", ( mo, d, c ) =>
                {
                    ev.Set();
                    complete = true;
                } );
                using( var client = TestHelper.CreateDefaultClient() )
                {
                    await client.SendAsync( "test", new byte[] { 0x00 } );

                    ev.WaitOne( 1000 );
                    complete.Should().Be( true );
                }
            }
        }

        [Fact]
        public async Task client_can_register_incoming_channel_before_connecting()
        {
            var m = new ActivityMonitor();
            var ev = new ManualResetEvent( false );
            bool complete = false;
            using( var server = TestHelper.CreateDefaultServer() )
            {
                server.Open();
                server.RegisterChannelHandler( "test", ( mo, d, c ) =>
                {
                    ev.Set();
                    complete = true;
                } );
                using( var client = TestHelper.CreateDefaultClient() )
                {

                    client.OnChannelRegistered += ( sender, chanArgs ) =>
                    {
                        // On channel registered
                        sender.Should().Be( client );
                        chanArgs.ChannelName.Should().Be( "test" );
                        ev.Set();
                    };

                    // Register incoming channel
                    client.RegisterChannelHandler( "test", ( mon, data ) =>
                    {
                        // On message received from server
                        data.Should().BeEquivalentTo( new byte[] { 0x42 } );
                        complete = true;
                        ev.Set();
                    } );

                    // Connect manually
                    await client.OpenAsync( m );

                    // Wait for server registration (OnChannelRegistered above)
                    ev.WaitOne();
                    ev.Reset();

                    // Send Server-to-Client message
                    await Task.Run( () => server.ActiveSessions.Single().Send( "test", new byte[] { 0x42 } ) );

                    // Wait for Server-to-Client message (RegisterChannelHandler above)
                    ev.WaitOne( 1000 );
                    complete.Should().Be( true );
                }
            }
        }
    }
}
