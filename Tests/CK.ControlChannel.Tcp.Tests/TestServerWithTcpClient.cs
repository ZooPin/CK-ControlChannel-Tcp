using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp.Tests
{
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
