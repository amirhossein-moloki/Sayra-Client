using System;
using System.IO;

namespace Sayra.Client.Shared.Models
{
    public class SecureMessageFrame
    {
        public uint Header { get; set; } = 0x53415952; // "SAYR"
        public ushort MessageCode { get; set; } = 0x0501;
        public uint PayloadLength => (uint)(EncryptedPayload?.Length ?? 0);
        public byte[] EncryptedPayload { get; set; } = Array.Empty<byte>();
        public byte[] Hmac { get; set; } = Array.Empty<byte>();

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(Header);
            writer.Write(MessageCode);
            writer.Write(PayloadLength);
            writer.Write(EncryptedPayload);
            writer.Write(Hmac);
            return ms.ToArray();
        }

        public static SecureMessageFrame FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 10 + 32)
            {
                throw new ArgumentException("Invalid message frame: too short", nameof(bytes));
            }
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);
            var header = reader.ReadUInt32();
            var messageCode = reader.ReadUInt16();
            var payloadLength = reader.ReadUInt32();

            if (ms.Length < 10 + payloadLength + 32)
            {
                throw new ArgumentException("Invalid message frame: incomplete payload or hmac");
            }

            var encryptedPayload = reader.ReadBytes((int)payloadLength);
            var hmac = reader.ReadBytes(32);

            return new SecureMessageFrame
            {
                Header = header,
                MessageCode = messageCode,
                EncryptedPayload = encryptedPayload,
                Hmac = hmac
            };
        }
    }
}
