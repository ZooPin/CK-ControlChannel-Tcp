using CK.ControlChannel.Abstractions;
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
        public const byte H_MSG = 0x01;
        public const byte H_BYE = 0xFF;
        public static readonly Encoding BaseStreamEncoding = Encoding.ASCII;
    }
    
}
