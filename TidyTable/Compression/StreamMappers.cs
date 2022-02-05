using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    public interface TwelveBitStream
    {
        void Write(short value);
    }

    public abstract class ReadOnlyStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() => throw new NotImplementedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }

    // TODO: Also the reverse
    public class TwelveBitsToByteStream : ReadOnlyStream, TwelveBitStream
    {
        private readonly Stream stream = new MemoryStream();

        private int buffer = 0;
        private int bufferLength = 0;

        public void Write(short value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        public override int ReadByte()
        {
            while (bufferLength < 8)
            {
                // underlying stream holds 12-bit values over 2 bytes each
                var b = stream.ReadByte();
                var b2 = stream.ReadByte();
                if (b < 0 || b2 < 0) return -1;
                buffer |= b << bufferLength;
                bufferLength += 8;
                buffer |= b2 << bufferLength;
                bufferLength += 4;
            }
            var result = buffer & 0xff;
            buffer >>= 8;
            bufferLength -= 8;
            return result;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var position = offset;
            int value;
            while (count-- > 0 && (value = ReadByte()) != -1)
            {
                buffer[position++] = (byte)value;
            }
            return position - offset;
        }
    }

    // TODO: Handle any padding at end?
    // Takes a stream and a symbol size <= 8.
    // Assuming 1-symbol per byte of underlying stream, packs them together to now ignore byte boundaries
    public class PackByteStream : ReadOnlyStream
    {
        private readonly Stream stream;
        private readonly int symbolSize;

        public PackByteStream(int symbolSize, Stream stream)
        {
            this.stream = stream;
            this.symbolSize = symbolSize;
        }

        private int buffer = 0;
        private int bufferLength = 0;

        public override int ReadByte()
        {
            while (bufferLength < 8)
            {
                var b = stream.ReadByte();
                if (b < 0) return -1;
                buffer |= b << bufferLength;
                bufferLength += symbolSize;
            }
            var result = buffer & 0xff;
            buffer >>= 8;
            bufferLength -= 8;
            return result;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var position = offset;
            int value;
            while (count-- > 0 && (value = ReadByte()) != -1)
            {
                buffer[position++] = (byte)value;
            }
            return position - offset;
        }
    }

    // Reverse of the above, returns 1 byte per symbol
    public class UnpackByteStream: ReadOnlyStream
    {
        private readonly Stream stream;
        private readonly int symbolSize;
        private readonly int symbolMask;

        public UnpackByteStream(int symbolSize, Stream stream)
        {
            this.stream = stream;
            this.symbolSize = symbolSize;
            symbolMask = (1 << symbolSize) - 1;
        }

        private int buffer = 0;
        private int bufferLength = 0;

        public override int ReadByte()
        {
            if (bufferLength < symbolSize)
            {
                var b = stream.ReadByte();
                if (b < 0) return -1;
                buffer |= b << bufferLength;
                bufferLength += symbolSize;
            }
            var result = buffer & symbolMask;
            buffer >>= symbolSize;
            bufferLength -= symbolSize;
            return result;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var position = offset;
            int value;
            while (count-- > 0 && (value = ReadByte()) != -1)
            {
                buffer[position++] = (byte)value;
            }
            return position - offset;
        }
    }
}
