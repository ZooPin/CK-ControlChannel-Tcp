using CK.Core;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                ["hello"] = "world"
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
                ["hello"] = "world"
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
                ["hello"] = "world"
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
                ["hello"] = "world"
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

                    client.RegisterChannelHandler( "test", ( a, b, c ) =>
                     {
                         complete = true;
                         ev.Set();
                     } );
                    await Task.Delay( 200 );

                    var t = Task.Run( () => server.ActiveSessions.Single().Send( "test", new byte[] { 0x00 } ) );

                    ev.WaitOne( 1000 );
                    complete.Should().Be( true );
                }
            }
        }
    }
}
