using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    /*
     * LZW is a widely used algorithm, similar to LZF but slightly more complex, and it is described here:
     *   https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Welch
     * Having done this, it is common to compress the data further using an Adaptive Entropy Encoder such as a Huffman code,
     * which compresses the most frequently seen symbols based on their probability distribution.
     * !!! Based on this, a Huffman code working on 12-bit values rather than bytes may be preferred !!!
     * 
     * The scheme works by mapping 8 bits -> 12 bit codes based on an implicit dictionary, initially all 1-byte values.
     * Repeatedly, the longest sequence still in the dictionary is found, and the dictionary value for that is output
     * and a new dictionary entry, consisting of the substring just found + the next character (so not in the dictionary) 
     * is added as a new dictionary entry. The next sequence is found starting from that new additional character.
     * 
     * Decoding then just reads out these values, each time building the next dictionary entry as the previous sequence emitted
     * plus the first character of the following sequence emitted. The only difficult case is where the next code is the one
     * the encoder just added to the dictionary, since the decoder effectively builds the dictionary one step behind the encoder.
     * In this case, due to the construction above, the first character of the next sequence was the last character of the new
     * sequence just added to the dictionary. For it to be encoded as the newly added codeword, it is just the previously output
     * sequence plus it's first character again, so the sequence encoded can be found implicity in the form cXc, where the last
     * sequence output was Z = cX.
     * 
     * This can be packed further by using a variable width code, rather than always 8 bits -> 12 bits. The code starts 1-bit wider
     * than the size of the input symbols (or increases after just the first symbol) and grows as required up to some maximum (e.g. 12).
     * The encoder must output the encoded word and THEN increase the code size upon adding the new symbol. Similarly,the decoder
     * is always 1 code word behind so must increase the size after adding the 2nd last table entry rather than the last.
     * Once the maximum size table is filled, either no new code words are added from that point, or a special 'clear' code is reserved
     * that allows the algorithm to restart after that point, if the input distribution has now changed significantly.
     * 
     * Handling variable-width codes with huffman coding no longer allows considering the distribution of individual symbols, and
     * we return to generating a Huffman code based on the byte-distribution. Which approach is better should be experimented with!
     * 
     * An existing implementation can be found in SharpLZW. Some experimentation is needed to find the best encoding configuration.
     * 
     */


    // Note: Various assumptions in code about input/output size e.g. input is bytes, output < 16 bits
    public class LZW
    {
        public int inputSize = 8; // read in bytes
        // TODO: Variable length codes (will need non-consts representing these values)
        public const int outputSize = 12; // output 12-bit blocks, where 0-255 are the original bits
        private const ushort outputMask = (1 << outputSize) - 1; // outputSize binary 1s, for reading in codewords
        private const int dictionarySize = 1 << outputSize;

        private TwelveBitStream outputStream;
        public byte[][] dictionary;
        public short nextDictionaryIndex = 0;

        private readonly LZWNode Tree = new();
        private LZWNode Current;

        // buffer is filled and output from the LSB upwards
        private int buffer = 0;
        private int bufferLength = 0;
        int totalBytesOutput = 0;

        public LZW(int inputSize)
        {
            this.inputSize = inputSize;
        }

        public static int Compress(Stream input, int inputLength, TwelveBitStream output, int maxBits = 8) =>
            new LZW(maxBits).Encode(input, inputLength, output);

        public static int Decompress(Stream input, int inputLength, Stream output, int maxBits = 8) =>
            new LZW(maxBits).Decode(input, inputLength, output);

        private short Read(Stream input)
        {
            while (bufferLength < outputSize)
            {
                var b = input.ReadByte();
                if (b < 0) throw new EndOfStreamException();
                buffer |= b << bufferLength;
                bufferLength += 8;
            }
            short output = (short)(buffer & outputMask);
            buffer >>= outputSize;
            bufferLength -= outputSize;
            return output;
        }

        // Regardless of the input size, assumes symbols are 1 per byte
        public int Encode(Stream input, int inputLength, TwelveBitStream output)
        {
            Current = Tree;
            for (int i = 0; i < (1 << inputSize); i++)
            {
                FindChildOrAdd((byte)i);
            }
            outputStream = output;

            while (inputLength-- > 0) EncodeByte(input);
            // need to explicitly write the last sequence
            if (Current.Index >= 0) outputStream.Write(Current.Index);
            Console.WriteLine($"LZW Compression created {nextDictionaryIndex} dictionary entries out of max {dictionarySize}");
            return totalBytesOutput;
        }

        

        private void EncodeByte(Stream input)
        {
            int b = input.ReadByte();
            if (b < 0) throw new EndOfStreamException();

            byte value = (byte)b;
            var nextNode = FindChildOrAdd(value);
            if (nextNode != null)  Current = nextNode;
            else
            {
                outputStream.Write(Current.Index);
                Current = Tree;
                Current = FindChildOrAdd(value)!;
            }
        }

        private LZWNode? FindChildOrAdd(byte value)
        {
            if (Current.RootChild == null)
            {
                if (nextDictionaryIndex < dictionarySize) Current.AddChild(nextDictionaryIndex++, value);
                return null;
            }

            var node = Current.RootChild;
            while (node.Value != value)
            {
                if (value > node.Value)
                {
                    if (node.RightSibling != null) node = node.RightSibling;
                    else
                    {
                        if (nextDictionaryIndex < dictionarySize) node.AddRight(nextDictionaryIndex++, value);
                        return null;
                    }
                } else
                {
                    if (node.LeftSibling != null) node = node.LeftSibling;
                    else
                    {
                        if (nextDictionaryIndex < dictionarySize) node.AddLeft(nextDictionaryIndex++, value);
                        return null;
                    }
                }
            }
            return node;
        }

        // Regardless of symbol length, writes them to 1-byte boundaries
        public int Decode(Stream input, int inputLength, Stream output)
        {
            if (inputLength == 0) return 0;
            // map bytes to number of symbols (rounds away any trailing bits)
            inputLength = (inputLength * inputSize) / outputSize;

            InitialiseDictionary();

            byte b = (byte)Read(input);
            inputLength--;
            var lastSequence = new List<byte>() { b };
            output.WriteByte(b);
            int outputBytes = 1;


            while (inputLength-- > 0)
            {
                short value = Read(input);
                var sequence = dictionary[value];
                if (sequence != null)
                {
                    output.Write(sequence, 0, sequence.Length);
                    outputBytes += sequence.Length;
                    lastSequence.Add(sequence[0]);
                    if (nextDictionaryIndex < dictionary.Length) dictionary[nextDictionaryIndex++] = lastSequence.ToArray();
                    lastSequence = sequence.ToList();
                } else
                {
                    lastSequence.Add(lastSequence[0]);
                    sequence = lastSequence.ToArray();
                    output.Write(sequence, 0, sequence.Length);
                    outputBytes += sequence.Length;
                    if (nextDictionaryIndex < dictionary.Length) dictionary[nextDictionaryIndex++] = sequence;
                }
            }

            return outputBytes;
        }

        public void InitialiseDictionary()
        {
            dictionary = new byte[1 << outputSize][];
            for (short i = 0; i < (1 << inputSize); i++)
            {
                dictionary[i] = new byte[] { (byte)i };
            }
            nextDictionaryIndex = (short)(1 << inputSize);
        }
    }

    // As above, but reads from a Huffman12Reader (that provides 12-bit values at a time as shorts), and can decode incrementally
    // Due to decoding incrementally, does not check bounds on reads
    public class LZWStream: ReadOnlyStream
    {
        private readonly LZW lzw;
        private readonly Huffman12Reader huffmanReader;

        private byte[] buffer = Array.Empty<byte>();
        private int bufferPosition;
        private List<byte> lastSequence;

        public LZWStream(Huffman12Reader huffmanReader, int maxBits = 8)
        {
            this.huffmanReader = huffmanReader;
            lzw = new(maxBits);
            lzw.InitialiseDictionary();

            // must read the first byte specially, as no previous sequence
            var firstSymbol = (byte)huffmanReader.ReadSymbol();
            buffer = new byte[1] { firstSymbol };
            lastSequence = buffer.ToList();
        }

        public override int ReadByte()
        {
            if (bufferPosition < buffer.Length) return buffer[bufferPosition++];

            short value = huffmanReader.ReadSymbol();
            var sequence = lzw.dictionary[value];
            if (sequence != null)
            {
                SetBuffer(sequence);
                lastSequence.Add(sequence[0]);
                if (lzw.nextDictionaryIndex < lzw.dictionary.Length) lzw.dictionary[lzw.nextDictionaryIndex++] = lastSequence.ToArray();
                lastSequence = sequence.ToList();
            }
            else
            {
                lastSequence.Add(lastSequence[0]);
                sequence = lastSequence.ToArray();
                SetBuffer(sequence);
                if (lzw.nextDictionaryIndex < lzw.dictionary.Length) lzw.dictionary[lzw.nextDictionaryIndex++] = sequence;
            }
            return ReadByte();
        }

        private void SetBuffer(byte[] sequence)
        {
            buffer = sequence;
            bufferPosition = 0;

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            if (bufferPosition < this.buffer.Length)
            {
                bytesRead = Math.Min(count, this.buffer.Length - bufferPosition);
                Array.Copy(this.buffer, bufferPosition, buffer, offset, bytesRead);
                bufferPosition += bytesRead;
            }

            if (bytesRead < count)
            {
                var value = ReadByte();
                if (value < 0) return value;
                buffer[offset] = (byte)value;
                bytesRead++;
            }

            return bytesRead + Read(buffer, offset + bytesRead, count - bytesRead);
        }
    }
}
