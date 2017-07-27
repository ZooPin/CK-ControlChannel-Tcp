using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CK.ControlChannel.Tcp.Tests
{
    internal static class TestHelper
    {
        internal static X509Certificate2 GetServerCertificate()
        {
            var file = FindFirstFileInParentDirectories( "CK.ControlChannel.Tcp.Tests.pfx" );
            return new X509Certificate2( file );
        }

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
    }
}