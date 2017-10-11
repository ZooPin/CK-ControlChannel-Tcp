using CK.ControlChannel.Abstractions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CK.ControlChannel.Tcp.Tests
{
    internal static class TestHelper
    {
        public static readonly string DefaultHost = @"127.0.0.1";
        public static readonly int DefaultPort = 43712;

        private static X509Certificate2 _serverCertificate;
        private static X509Certificate2 _clientCertificate;

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

        internal static X509Certificate2 ClientCertificate
        {
            get
            {
                if( _clientCertificate == null )
                {
                    var file = FindFirstFileInParentDirectories( "CK.ControlChannel.Tcp.Tests.Client.pfx" );
                    _clientCertificate = new X509Certificate2( file );
                }
                return _clientCertificate;
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

        public static IReadOnlyDictionary<string, string> ClientDefaultAuthData => new Dictionary<string, string>()
        {
            [ "Hello" ] = "World",
        };

        internal static readonly RemoteCertificateValidationCallback ServerCertificateValidationCallback =
            ( sender, cert, chain, policyErrors ) =>
        {
            return cert.Subject == ServerCertificate.Subject
                && cert.GetCertHash().SequenceEqual( ServerCertificate.GetCertHash() );
        };

        internal static readonly RemoteCertificateValidationCallback ClientCertificateValidationCallback =
            ( sender, cert, chain, policyErrors ) =>
            {
                return cert.Subject == ClientCertificate.Subject
                    && cert.GetCertHash().SequenceEqual( ClientCertificate.GetCertHash() );
            };

        internal static readonly LocalCertificateSelectionCallback ClientCertificateSelectionCallback =
            ( sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers ) =>
            {
                return ClientCertificate;
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

        internal static ControlChannelServer CreateDefaultServer(
            IAuthorizationHandler authHandler = null,
            RemoteCertificateValidationCallback userCertificateValidationCallback = null
            )
        {
            if( authHandler == null )
                authHandler = DefaultAuthHandler;
            return new ControlChannelServer( DefaultHost, DefaultPort, authHandler, ServerCertificate, userCertificateValidationCallback );
        }

        internal static TcpClient CreateTcpClient()
        {
            return new TcpClient( AddressFamily.InterNetwork );
        }

        internal static ControlChannelClient CreateDefaultClient(
            IReadOnlyDictionary<string, string> authData = null,
            LocalCertificateSelectionCallback certSelector = null,
            RemoteCertificateValidationCallback certValidator = null
            )
        {
            if( certSelector == null )
                certSelector = ClientCertificateSelectionCallback;
            if( certValidator == null )
                certValidator = ServerCertificateValidationCallback;
            if( authData == null )
                authData = ClientDefaultAuthData;

            return new ControlChannelClient(
                DefaultHost,
                DefaultPort,
                authData,
                true,
                certValidator,
                certSelector
                );
        }
    }
}
