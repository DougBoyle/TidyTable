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
        private const bool VARIABLE_CODE = false;
        private const int inputSize = 8; // read in bytes
        // TODO: Variable length codes (will need non-consts representing these values)
        private const int outputSize = 12; // output 12-bit blocks, where 0-255 are the original bits
        private const ushort outputMask = (1 << outputSize) - 1; // outputSize binary 1s, for reading in codewords

        private Stream outputStream;
        private byte[][] dictionary;
        private int nextDictionaryIndex;
        private List<byte> currentSequence = new();
        private short currentMatchIndex = -1;

        // buffer is filled and output from the LSB upwards
        private int buffer = 0;
        private int bufferLength = 0;
        private void Write(short value)
        {
            buffer |= value << bufferLength;
            bufferLength += outputSize;
            while (bufferLength >= 8)
            {
                outputStream.WriteByte((byte)buffer);
                buffer >>= 8;
                bufferLength -= 8;
            }
        }

        // data may not end on a bute-boundary, so the decoder must be careful to check size remaining before reading further
        private void Flush()
        {
            if (bufferLength > 0)
            {
                outputStream.WriteByte((byte)buffer);
            }
        }

        public void Encode(Stream input, int inputLength, Stream output)
        {
            dictionary = new byte[1 << outputSize][];
            for (short i = 0; i < (1 << inputSize); i++)
            {
                dictionary[i] = new byte[] { (byte)i };
            }
            nextDictionaryIndex = 1 << inputSize;
            outputStream = output;

            for (int i = 0; i < inputLength; i++)
            {
                EncodeByte(input);
            }
            // need to explicitly write the last sequence
            if (currentMatchIndex >= 0) Write(currentMatchIndex);
            Flush();
        }

        private void EncodeByte(Stream input)
        {
            int b = input.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            currentSequence.Add((byte)b);
            var possibleMatch = FindMatch();
            if (possibleMatch < 0) // no match found, output code and add new sequence to dictionary
            {
                Write(currentMatchIndex);
                if (nextDictionaryIndex < dictionary.Length) dictionary[nextDictionaryIndex++] = currentSequence.ToArray();
                currentSequence = new List<byte>() { (byte)b };
                currentMatchIndex = (short)b;
            } else // still matching, keep scanning new symbols
            {
                currentMatchIndex = possibleMatch;
            }
        }

        private short FindMatch()
        {
            for (short i = 0; i < dictionary.Length; i++)
            {
                if (dictionary[i] == null) return -1;
                else if (dictionary[i].SequenceEqual(currentSequence)) return i;
            }
            return -1;
        }
    }
}
