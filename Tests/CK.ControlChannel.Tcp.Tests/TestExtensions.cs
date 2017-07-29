using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;

namespace CK.ControlChannel.Tcp.Tests
{
    public static class TestExtensions
    {
        public static Task ConnectAsync( this TcpClient @this, ControlChannelServer s )
        {
            return @this.ConnectAsync( s.Host, s.Port );
        }
        public static async Task<Stream> GetDataStreamAsync(
            this TcpClient @this,
            ControlChannelServer s,
            LocalCertificateSelectionCallback clientCertCallback = null
            )
        {
            Stream writeStream;
            if( s.IsSecure )
            {
                var ssl = new SslStream(
                    @this.GetStream(),
                    false,
                    TestHelper.ServerCertificateValidationCallback,
                    clientCertCallback,
                    EncryptionPolicy.RequireEncryption
                    );
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
}
