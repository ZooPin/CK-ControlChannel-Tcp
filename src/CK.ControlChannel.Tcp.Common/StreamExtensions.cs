using CK.ControlChannel.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CK.ControlChannel.Tcp
{
    public static class StreamExtensions
    {
        public static byte ReadAck( this Stream @this )
        {
            byte ack = (byte)@this.ReadByte();
            if( ack != Protocol.M_ACK ) { throw new ControlChannelException( $"Expected {Protocol.M_ACK} but got {ack} instead" ); }
            return (byte)@this.ReadByte();
        }

        public static void EnsureAck( this Stream @this, byte expected )
        {
            byte messageHeader = (byte)@this.ReadAck();
            if( messageHeader != expected ) { throw new ControlChannelException( $"Expected {expected} but got {messageHeader} instead" ); }
        }

        public static int ReadInt32( this Stream @this )
        {
            return BitConverter.ToInt32( @this.ReadBuffer( 4 ), 0 );
        }
        public static async Task<int> ReadInt32Async( this Stream @this )
        {
            return BitConverter.ToInt32( await @this.ReadBufferAsync( 4 ), 0 );
        }
        public static void WriteInt32( this Stream @this, int i )
        {
            @this.WriteBuffer( BitConverter.GetBytes( i ) );
        }
        public static async Task WriteInt32Async( this Stream @this, int i )
        {
            await @this.WriteBufferAsync( BitConverter.GetBytes( i ) );
        }


        public static string ReadString( this Stream @this, Encoding encoding )
        {
            int len = @this.ReadInt32();
            byte[] buffer = @this.ReadBuffer( len );
            return encoding.GetString( buffer );
        }
        public static async Task<string> ReadStringAsync( this Stream @this, Encoding encoding )
        {
            int len = await @this.ReadInt32Async();
            byte[] buffer = await @this.ReadBufferAsync( len );
            return encoding.GetString( buffer );
        }
        public static void WriteString( this Stream @this, string s, Encoding encoding )
        {
            byte[] buffer = encoding.GetBytes( s );
            @this.WriteInt32( buffer.Length );
            @this.WriteBuffer( buffer );
        }
        public static async Task WriteStringAsync( this Stream @this, string s, Encoding encoding )
        {
            byte[] buffer = encoding.GetBytes( s );
            await @this.WriteInt32Async( buffer.Length );
            await @this.WriteBufferAsync( buffer );
        }

        public static async Task<byte> ReadByteAsync( this Stream @this )
        {
            var buffer = await @this.ReadBufferAsync( 1 );
            return buffer[0];
        }

        public static byte[] ReadBuffer( this Stream @this, int len )
        {
            byte[] buffer = new byte[len];
            int offset = 0;
            while( offset < len )
            {
                offset += @this.Read( buffer, offset, (len - offset) );
            }

            return buffer;
        }
        public static async Task<byte[]> ReadBufferAsync( this Stream @this, int len )
        {
            byte[] buffer = new byte[len];
            int offset = 0;
            while( offset < len )
            {
                offset += await @this.ReadAsync( buffer, offset, (len - offset) );
            }

            return buffer;
        }

        public static void WriteBuffer( this Stream @this, byte[] buffer )
        {
            @this.Write( buffer, 0, buffer.Length );
        }
        public static async Task WriteBufferAsync( this Stream @this, byte[] buffer )
        {
            await @this.WriteAsync( buffer, 0, buffer.Length );
        }

        public static IReadOnlyDictionary<string, string> ReadControl( this Stream @this )
        {
            int len = @this.ReadInt32();
            byte[] buffer = @this.ReadBuffer( len );
            return ControlMessage.DeserializeControlMessage( buffer );
        }
        public static void WriteControl( this Stream @this, IReadOnlyDictionary<string, string> data )
        {
            byte[] buffer = ControlMessage.SerializeControlMessage( data );
            @this.WriteInt32( buffer.Length );
            @this.WriteBuffer( buffer );
        }

        public static ushort ReadUInt16( this Stream @this )
        {
            byte[] buffer = @this.ReadBuffer( 2 );
            return BitConverter.ToUInt16( buffer, 0 );
        }
        public static void WriteUInt16( this Stream @this, ushort i )
        {
            @this.WriteBuffer( BitConverter.GetBytes( i ) );
        }
        public static string ToHexString( this byte[] ba )
        {
            StringBuilder hex = new StringBuilder( ba.Length * 2 );
            foreach( byte b in ba ) hex.AppendFormat( "{0:X2}", b );
            return hex.ToString();
        }
        public static string Describe( this X509Certificate c )
        {
            StringBuilder sb = new StringBuilder();
            sb.Append( c.GetCertHash().ToHexString() );
            sb.Append( ": " );
            sb.Append( c.Subject );
            return sb.ToString();
        }
    }
}
