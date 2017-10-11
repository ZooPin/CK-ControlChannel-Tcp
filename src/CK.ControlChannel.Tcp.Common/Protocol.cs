using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CK.ControlChannel.Tcp
{
    /// <summary>
    /// Simplified messaging application-layer protocol utilities
    /// </summary>
    public static class Protocol
    {
        public const byte PROTOCOL_VERSION = 0x00;

        public const byte M_MSG_PUB = 0x00;
        public const byte M_ACK = 0x01;

        public const byte M_AUTH_FAIL = 0x02;

        public const byte M_PUB_TOPIC = 0x03;
        public const byte M_UNPUB_TOPIC = 0x04;

        public const byte M_SUB_TOPIC = 0x05;
        public const byte M_UNSUB_TOPIC = 0x06;

        public const byte M_PING = 0xFD;
        public const byte M_ERROR = 0xFE;
        public const byte M_BYE = 0xFF;

        public const string E_INVALID_CHANNEL = "INVALID_CHANNEL";
        public const string E_INVALID_LENGTH = "INVALID_LENGTH";
        public const string E_INVALID_MESSAGE = "INVALID_MESSAGE";
        public const string E_INTERNAL_ERROR = "INTERNAL_ERROR";

        public static readonly Encoding TextEncoding = Encoding.UTF8;

        public static void WriteAck( this Stream s, byte messageHeader )
        {
            s.WriteByte( M_ACK );
            s.WriteByte( messageHeader );
        }
        public static void WriteBye( this Stream s )
        {
            s.WriteByte( M_BYE );
        }
        public static void WritePing( this Stream s )
        {
            s.WriteByte( M_PING );
        }
        //public static void Error( this Stream s, CKExceptionData ex )
        //{
        //    s.WriteByte( M_ERROR );
        //    using( CKBinaryWriter sw = new CKBinaryWriter( s, TextEncoding, true ) )
        //    {
        //        ex.Write( sw, true );
        //    }
        //}
        //public static void Error( this Stream s, Exception ex )
        //{
        //    s.Error( CKExceptionData.CreateFrom( ex ) );
        //}
        public static void Error( this Stream s, string e )
        {
            s.WriteByte( M_ERROR );
            s.WriteString( e, TextEncoding );
        }
    }
}
