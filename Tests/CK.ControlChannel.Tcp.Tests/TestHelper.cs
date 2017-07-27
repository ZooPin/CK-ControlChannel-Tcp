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
                    return new X509Certificate2( file );
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
        public static void WriteProtocolVersion( this Stream @this )
        {
            @this.WriteByte( ControlChannelServer.ProtocolVersion );
        }
        public static void WriteAuthentication( this Stream @this )
        {
            Dictionary<string, string> data = new Dictionary<string, string>()
            {
                [ControlMessage.TypeKey] = "Auth",
                ["Test"] = "Test",
            };
            WriteControl( @this, data );
        }

        public static IReadOnlyDictionary<string, string> ReadControl( this Stream s )
        {
            int len = s.ReadInt32();
            byte[] buffer = s.ReadBuffer( len );
            return ControlMessage.DeserializeControlMessage( buffer );
        }

        public static void WriteControl( this Stream s, IReadOnlyDictionary<string, string> data )
        {
            byte[] buffer = ControlMessage.SerializeControlMessage( data );
            s.WriteInt32( buffer.Length );
            s.WriteBuffer( buffer );
        }
    }
}