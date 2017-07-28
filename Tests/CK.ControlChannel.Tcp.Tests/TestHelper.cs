using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using CK.ControlChannel.Tcp;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using CK.ControlChannel.Abstractions;
using CK.Core;
using System.Text;
using FluentAssertions;

namespace CK.ControlChannel.Tcp.Tests
{
    internal static class TestHelper
    {
        public static readonly string DefaultHost = @"127.0.0.1";
        public static readonly int DefaultPort = 43712;

        private static X509Certificate2 _serverCertificate;

        internal static X509Certificate2 ServerCertificate
        {
            get
            {
                if( _serverCertificate == null )
                {
                    var file = FindFirstFileInParentDirectories( "CK.ControlChannel.Tcp.Tests.pfx" );
                    _serverCertificate = new X509Certificate2( file );
                }
                return _serverCertificate;
            }
        }

        private static IAuthorizationHandler _defaultAuthHandler;
        internal static IAuthorizationHandler DefaultAuthHandler
        {
            get
            {
                if( _defaultAuthHandler == null )
                {
                    _defaultAuthHandler = new TestAuthHandler( ( s ) => true );
                }
                return _defaultAuthHandler;
            }
        }

        internal static readonly RemoteCertificateValidationCallback ServerCertificateValidationCallback =
            ( sender, cert, chain, policyErrors ) =>
        {
            return cert.Subject == ServerCertificate.Subject
                && cert.GetCertHash().SequenceEqual( ServerCertificate.GetCertHash() );
        };

        internal static string FindFirstFileInParentDirectories( string filename )
        {
            var dllPath = typeof( TestHelper ).GetTypeInfo().Assembly.Location;
            var dllDir = Path.GetDirectoryName( dllPath );
            string path = dllDir;

            while( path != null )
            {
                string filePath = Path.Combine( path, filename );
                if( File.Exists( filePath ) )
                {
                    path = filePath;
                    break;
                }
                path = Path.GetDirectoryName( path );
            }

            return path;
        }

        internal static ControlChannelServer CreateDefaultServer( IAuthorizationHandler authHandler = null )
        {
            if( authHandler == null ) { authHandler = DefaultAuthHandler; }
            return new ControlChannelServer( DefaultHost, DefaultPort, authHandler, ServerCertificate );
        }

        internal static TcpClient CreateTcpClient()
        {
            return new TcpClient( AddressFamily.InterNetwork );
        }
    }

    public static class TestExtensions
    {
        public static Task ConnectAsync( this TcpClient @this, ControlChannelServer s )
        {
            return @this.ConnectAsync( s.Host, s.Port );
        }
        public static async Task<Stream> GetDataStreamAsync( this TcpClient @this, ControlChannelServer s )
        {
            Stream writeStream;
            if( s.IsSecure )
            {
                var ssl = new SslStream( @this.GetStream(), false, TestHelper.ServerCertificateValidationCallback );
                await ssl.AuthenticateAsClientAsync( s.Host );
                writeStream = ssl;
            }
            else
            {
                writeStream = @this.GetStream();
            }
            return writeStream;
        }
        public static void WriteDummyAuth( this Stream s )
        {
            s.WriteByte( Protocol.PROTOCOL_VERSION );
            s.WriteControl( new Dictionary<string, string>()
            {
                ["hello"] = "world",
            } );
            s.ReadAck().Should().Be( Protocol.PROTOCOL_VERSION );
        }

        public static void EnsureDisconnected( this Stream s )
        {
            try
            {
                s.ReadByte().Should().Be( -1 );
            }
            catch( IOException soe )
            {
                soe.InnerException.Should().BeOfType<SocketException>();
            }
        }
    }

    internal class TestServerWithTcpClient : IDisposable
    {
        private readonly ControlChannelServer _server;
        private readonly TcpClient _client;
        private Stream _stream;

        internal ControlChannelServer Server => _server;
        internal TcpClient TcpClient => _client;
        internal Stream Stream => _stream;

        private TestServerWithTcpClient()
        {
            _server = TestHelper.CreateDefaultServer();
            _client = TestHelper.CreateTcpClient();
            _server.Open();
        }

        private async Task OpenAsync()
        {
            await _client.ConnectAsync( _server );
            _stream = await _client.GetDataStreamAsync( _server );
            _stream.WriteDummyAuth();
        }

        internal static async Task<TestServerWithTcpClient> Create()
        {
            var c = new TestServerWithTcpClient();
            await c.OpenAsync();
            return c;
        }

        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
            _server.Dispose();
        }
    }
}
