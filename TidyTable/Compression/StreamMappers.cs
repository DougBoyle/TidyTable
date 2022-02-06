using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    // TODO: !!! Can just use BinaryWriter/Reader for reading/writing shorts (and treating as 12-bit values)
    //           But need to somehow indicate the length of the stream
    // TODO: For all methods, using the same underlying stream requires seeking to read/write position!
    public interface ITwelveBitStream
    {
        void Write(short value);
    }

    // Read/Write carry on from the same positions and don't interfere, but cannot seek as a result
    public class DualStream: Stream
    {
        private long ReadPosition = 0;
        private long WritePosition = 0;
        public readonly Stream stream;

        public DualStream()
        {
            stream = new MemoryStream();
        }

        public DualStream(Stream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => WritePosition;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Write(short value)
        {
            stream.Seek(WritePosition, SeekOrigin.Begin);
            stream.WriteByte((byte)value);
            WritePosition += 2;
        }

        public override void Flush() {
            stream.Seek(WritePosition, SeekOrigin.Begin);
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            stream.Seek(ReadPosition, SeekOrigin.Begin);
            var bytesAvailable = (int)Math.Min(count, WritePosition - ReadPosition);
            var bytesRead = stream.Read(buffer, offset, bytesAvailable);
            ReadPosition += bytesRead;
            return bytesRead;
        }

        public override int ReadByte()
        {
            stream.Seek(ReadPosition, SeekOrigin.Begin);
            if (ReadPosition >= WritePosition) return -1;
            
            var value = stream.ReadByte();
            if (value >= 0) ReadPosition++;
            return value;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Seek(WritePosition, SeekOrigin.Begin);
            stream.Write(buffer, offset, count);
            WritePosition += count;
        }

        public override void WriteByte(byte value)
        {
            stream.Seek(WritePosition++, SeekOrigin.Begin);
            stream.WriteByte(value);
        }

        public byte[] ToArray()
        {
            if (stream is MemoryStream memoryStream)
            {
                return memoryStream.ToArray();
            } else
            {
                throw new NotImplementedException();
            }
        }
    }

    public abstract class ReadOnlyStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }

    // TODO: Also the reverse
    public class TwelveBitsToByteStream : ReadOnlyStream, ITwelveBitStream
    {
        private readonly DualStream stream = new();

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

    // Wraps twelve-bit values as 2 bytes each (assuming top bits of each value are 0 anyway, effectively just a short stream)
    public class TwelveBitStream: ITwelveBitStream
    {
        public readonly DualStream stream = new();

        public void Write(short value)
        {
            stream.WriteByte((byte)value);
            stream.WriteByte((byte)(value >> 8));
        }

        public short Read()
        {
            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();
            if (b1 < 0 || b2 < 0) return -1;
            return (short)((b2 << 8) | b1);
        }

        public short[] ToArray()
        {
            var byteArray = stream.ToArray();
            var result = new short[byteArray.Length/2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (short)(byteArray[2 * i] | (byteArray[2 * i + 1] << 8));
            }
            return result;
        }
    }
}
