using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using CK.ControlChannel.Tcp;
using System.Net.Sockets;
using System.Threading.Tasks;

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

        internal static ControlChannelServer CreateDefaultServer()
        {
            return new ControlChannelServer( DefaultHost, DefaultPort, ServerCertificate );
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
    }
}