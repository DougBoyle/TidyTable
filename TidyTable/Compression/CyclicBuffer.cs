using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    // TODO: Meant for use by chess engine to read compressed file up to some point in the output.
    //      Would be closely tied to both compression/decompression, as we only get a benefit if we avoid
    //      Decompressing the whole file at once. Would require a 'nextByte' method or similar on this object Decompress returns,
    //      so that both processes remain within 1 buffer-length of each other
    public class CyclicBuffer : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush() {}

        private int readPosition;
        private int writePosition;
        private int size;
        private byte[] buf;

        public CyclicBuffer(int maxSize)
        {
            size = maxSize;
            buf = new byte[size];
            readPosition = size - 1;
            writePosition = 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // check position of read/write pointers
            // readPos before writePos, with enough space to advance
            if (readPosition < writePosition && readPosition + count < writePosition)
            {
                Array.Copy(buf, readPosition, buffer, offset, count);
                readPosition += count;
            }
            // readPos after writePos, so may wrap around, in which case check it doesn't pass writePos
            else if (readPosition > writePosition && readPosition + count < writePosition + size)
            {
                // won't overflow
                if (readPosition + count < size)
                {
                    Array.Copy(buf, readPosition, buffer, offset, count);
                    readPosition += count;
                } else
                {
                    int firstCount = size - readPosition;
                    Array.Copy(buf, readPosition, buffer, offset, firstCount);
                    count -= firstCount;
                    Array.Copy(buf, 0, buffer, offset + firstCount, count);
                    readPosition = count;
                }
            }
            else throw new IndexOutOfRangeException("Read exceeded the capacity of the buffer");
            return count; // Current implementation always reads the requested number of bytes
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // check position of read/write pointers
            // writePos before readPos, with enough space to advance
            if (writePosition < readPosition && writePosition + count < readPosition)
            {
                Array.Copy(buffer, offset, buf, writePosition, count);
                writePosition += count;
            }
            // writePos after readPos, so may wrap around, in which case check it doesn't pass readPos
            else if (writePosition > readPosition && writePosition + count < readPosition + size)
            {
                // won't overflow
                if (writePosition + count < size)
                {
                    Array.Copy(buffer, offset, buf, writePosition, count);
                    writePosition += count;
                }
                else
                {
                    int firstCount = size - writePosition;
                    Array.Copy(buffer, offset, buf, writePosition, firstCount);
                    count -= firstCount;
                    Array.Copy(buffer, offset + firstCount, buf, 0, count);
                    writePosition = count;
                }
            }
            else throw new IndexOutOfRangeException("Read exceeded the capacity of the buffer");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }
}
