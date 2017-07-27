using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp
{
    public partial class ControlChannelServer
    {
        public const byte ProtocolVersion = 0x00;

        public async Task<int> ReadProtocolVersionAsync( Stream s )
        {
            byte[] protocolVersionBuffer = new byte[1];
            await s.ReadAsync( protocolVersionBuffer, 0, protocolVersionBuffer.Length );
            return protocolVersionBuffer[0];
        }
    }
}
