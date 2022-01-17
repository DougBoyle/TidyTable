using static TidyTable.Compression.Huffman;

namespace TidyTable.Compression
{
    public class Huffman
    {
        /* Own implementation before looking at existing libraries
         * 
         * TODO: Reconstructing tree and decompressing file
         * Note: New instance is used each time, as fields are cleared after operation
         * 
        */

        // TODO: Write directly to output buffer wherever possible, rather than making a copy
        public static int Compress(byte[] input, byte[] output, int inputLength)
        {

            var huffman = new Huffman();
            var tree = huffman.BuildHuffmanTree(input, inputLength);
            var mapping = huffman.TreeToMapping(tree);
            var code = huffman.EncodeBytes(mapping, input, inputLength);

            var totalLength = huffman.TreeEncoding.Length + code.Count; 
            if (totalLength > output.Length) return 0;

            // Copy bytes to output
            int outputLength = huffman.TreeEncoding.Length;
            Array.Copy(huffman.TreeEncoding, output, outputLength);
            foreach (byte b in code)
            {
                output[outputLength++] = b;
            }
            return outputLength;
        }

        // TODO: Must handle possible write exception when calling this with an array-backed output
        public static int Compress(byte[] input, BinaryWriter output, int inputLength)
        {

            var huffman = new Huffman();
            var tree = huffman.BuildHuffmanTree(input, inputLength);
            var mapping = huffman.TreeToMapping(tree);
            // Copy bytes to output
            int outputLength = huffman.TreeEncoding.Length;
            output.Write(huffman.TreeEncoding);

            outputLength += huffman.EncodeBytes(mapping, input, inputLength, output);

            return outputLength;
        }

        private readonly int[] frequencyTable = new int[256];

        private HuffmanNode BuildHuffmanTree(byte[] input, int inputLength)
        {
            for (int i = 0; i < inputLength; i++) frequencyTable[input[i]]++;
            List<HuffmanNode> forest = frequencyTable.Select((count, n) => new HuffmanNode(count, (byte)n, null, null))
                .Where(node => node.Frequency > 0).ToList();
            while (forest.Count > 1)
            {
                var node1 = forest.MinBy(node => node.Frequency)!;
                forest.Remove(node1);
                var node2 = forest.MinBy(node => node.Frequency)!;
                forest.Remove(node2);
                forest.Add(new HuffmanNode(node1.Frequency + node2.Frequency, null, node1, node2));
            }
            return forest.First()!;
        }

        // Output mapping of bytes to bits:
        // Just need to write the structure of the tree, and the byte represented by each leaf node.
        // Can encode the structure by defining a traversal order, then just need to indicate internal/leaf (0/1) node at each step.
        // 256 Leaf nodes => 256 + 255 = 511 bits so 64 bytes for tree, then 256 bytes for what byte each ordered leaf represents.
        // Achieves fixed overal size, as variable size words are encoded in order of 0s and 1s in first 64 bytes.
        // Pre-order traversal i.e. Node, Left, Right - (so encoding always starts with a 0, parse it anyway)
        // Can do this at same time as converting the tree to an array to look up the encoding of each byte

        // Fields reused during decoding too

        private readonly byte[] TreeEncoding = new byte[64 + 256];
        // For writing bits/bytes to output. Insert new bits at most significant end, so 'first' bit is LSB of output
        private byte Buffer = 0;
        private int BufferLength = 0; // index from LSB where next bit will be written
        private int TreeIndex = 0;
        private int LeafCount = 0; // int to allow looping 0 <= LeafCount < 256

        private void WriteLeaf(byte value)
        {
            TreeEncoding[64 + LeafCount++] = value;
            Buffer |= (byte)(1 << BufferLength);
            BufferGrew();   
        }

        private void WriteNode()
        {
            BufferGrew();
        }

        private void FlushBuffer()
        {
            if (BufferLength > 0)
            {
                BufferLength = 7;
                BufferGrew();
            }
        }

        private void BufferGrew()
        {
            if (++BufferLength == 8)
            {
                TreeEncoding[TreeIndex++] = Buffer;
                Buffer = 0;
                BufferLength = 0;
            }
        }

        // Root of tree corresponds to LSB of nodes, to avoid corresponding to a variable bit position on decoding
        private ByteMapping[] TreeToMapping(HuffmanNode tree)
        {
            ByteMapping[] mappings = new ByteMapping[256];

            void MapPath(ByteMapping currentMapping, HuffmanNode branch)
            {
                if (branch.Value != null)
                {
                    byte value = (byte)branch.Value;
                    WriteLeaf(value);
                    mappings[value] = currentMapping;
                } else
                {
                    WriteNode();
                    var mappingCopy = new ByteMapping() {
                        MappedLength = (byte)(currentMapping.MappedLength + 1),
                        MappedValue = currentMapping.MappedValue | (1UL << currentMapping.MappedLength),
                    };
                    currentMapping.MappedLength++;
                    MapPath(currentMapping, branch.Left!);
                    MapPath(mappingCopy, branch.Right!);
                }
            }

            MapPath(new ByteMapping(), tree);

            // Now values that don't occur are left out, have to flush buffer of any remaining bits
            FlushBuffer();

            return mappings;
        }

        // TODO: Encode actual file, using a buffer to handle variable bits
        // TODO! ALL WRONG, NEED TO BE APPENDING TO TOP NOT BOTTOM!!! LSB is root of tree so read from LSB to MSB,
        //      which means Earlier writes are at LSB and later writes at MSB
        // Bits are appended from LSB towards MSB of buffer. Read output in byte order but from LSB to MSB of each byte.
        // As Root of tree is also LSB of encoding, makes appending to buffer easy too
        // TODO: Indicate end of mapped file, most likely by putting number of bytes at start of file as int

        private List<byte> EncodeBytes(ByteMapping[] mapping, byte[] input, int inputLength)
        {
            var output = new List<byte>();
            // Assumption that maximum codeword length <= 58
            ulong Buffer = 0;
            int BufferLength = 0;

            for (int i = 0; i < inputLength; i++)
            {
                var codeWord = mapping[input[i]];
                Buffer |= codeWord.MappedValue << BufferLength; 
                BufferLength += codeWord.MappedLength;

                if (BufferLength > 64)
                    throw new ArgumentException("Encoding chose a codeword with length exceeding buffer capacity");
                while (BufferLength >= 8)
                {
                    // Buffer appends onto MSB, so oldest bits are at LSB, already in correct order
                    byte byteToWrite = (byte)Buffer;
                    output.Add(byteToWrite);
                    Buffer >>= 8;
                    BufferLength -= 8;
                }
            }

            // at end, add possible remaining byte
            if (BufferLength != 0)
            {
                output.Add((byte)Buffer); // at most 1 byte

                // At the start of data (after fixed length tree encoding), specify how many bits of last byte to interpret
                output.Insert(0, (byte)BufferLength);
            } else
            {
                output.Insert(0, 8); // fits exact, so whole last byte is data
            }

            return output;
        }

        private int EncodeBytes(ByteMapping[] mapping, byte[] input, int inputLength, BinaryWriter output)
        {
            output.Write(0); // Placeholder for number of bits in last byte
            int codeSize = 1;
            // Assumption that maximum codeword length <= 58
            ulong Buffer = 0;
            int BufferLength = 0;

            for (int i = 0; i < inputLength; i++)
            {
                var codeWord = mapping[input[i]];
                Buffer |= codeWord.MappedValue << BufferLength;
                BufferLength += codeWord.MappedLength;

                if (BufferLength > 64)
                    throw new ArgumentException("Encoding chose a codeword with length exceeding buffer capacity");
                while (BufferLength >= 8)
                {
                    // Buffer appends onto MSB, so oldest bits are at LSB, already in correct order
                    byte byteToWrite = (byte)Buffer;
                    output.Write(byteToWrite);
                    codeSize++;
                    Buffer >>= 8;
                    BufferLength -= 8;
                }
            }

            // at end, add possible remaining byte
            if (BufferLength != 0)
            {
                output.Write((byte)Buffer); // at most 1 byte

                // At the start of data (after fixed length tree encoding), specify how many bits of last byte to interpret
                output.Seek(TreeEncoding.Length, SeekOrigin.Begin);
                output.Write((byte)BufferLength);
            }
            else
            {
                output.Seek(TreeEncoding.Length, SeekOrigin.Begin);
                output.Write(8); // fits exact, so whole last byte is data
            }
            output.Seek(0, SeekOrigin.End);

            return codeSize;
        }

        /* --------------------------------- Decoding ---------------------------------------- */
        // Only used for decoding initial tree structure
        private bool ReadBit(byte[] input, ref int index)
        {
            if (BufferLength == 0)
            {
                BufferLength = 8;
                Buffer = input[index++];
            }
            BufferLength--;
            bool isLeaf = (Buffer & 1) == 1;
            Buffer >>= 1;
            return isLeaf;
        }

        private void BytesToTree(HuffmanNode currentNode)
        {
            bool isLeaf = ReadBit(TreeEncoding, ref TreeIndex);
            if (isLeaf)
            {
                currentNode.Value = TreeEncoding[64 + LeafCount++];
            } else
            {
                HuffmanNode left = new(0, null, null, null);
                BytesToTree(left);
                HuffmanNode right = new(0, null, null, null);
                BytesToTree(right);
                currentNode.Left = left;
                currentNode.Right = right;
            }
        }

        // TODO: This could reuse the same local variables that writing used
        private byte[] Input = Array.Empty<byte>();
        private int InputIndex = 0;

        public static int Decompress(byte[] input, byte[] output, int size)
        {
            return new Huffman().Decode(input, output, size);
        }

        // TODO: Must handle possible write exception when calling this with an array-backed output
        public static int Decompress(byte[] input, BinaryWriter output, int size)
        {

            return new Huffman().Decode(input, output, size);
        }
        private int Decode(byte[] input, byte[] output, int inputLength)
        {
            if (inputLength <= TreeEncoding.Length)
                throw new ArgumentOutOfRangeException("Input length too short to hold more than just compression dictionary");
            // Read tree encoding
            HuffmanNode root = new(0, null, null, null);
            Array.Copy(input, TreeEncoding, TreeEncoding.Length);
            BytesToTree(root);

            // Read number of bits of last byte
            Input = input;
            InputIndex = TreeEncoding.Length;
            byte bitsInLastByte = Input[InputIndex++];

            // Reset buffer
            Buffer = 0;
            BufferLength = 0;
            int outputIndex = 0;
            // Extra test ensures that the correct number of bits are read from the last byte
            while (InputIndex < inputLength || 8-BufferLength < bitsInLastByte)
            {
                byte b = ReadSymbol(root);
                if (outputIndex >= output.Length) return 0;
                output[outputIndex++] = b;
            }
            return outputIndex;
        }

        private int Decode(byte[] input, BinaryWriter output, int inputLength)
        {

            if (inputLength <= TreeEncoding.Length)
                throw new ArgumentOutOfRangeException("Input length too short to hold more than just compression dictionary");
            // Read tree encoding
            HuffmanNode root = new(0, null, null, null);
            Array.Copy(input, TreeEncoding, TreeEncoding.Length);
            BytesToTree(root);

            // Read number of bits of last byte
            Input = input;
            InputIndex = TreeEncoding.Length;
            byte bitsInLastByte = Input[InputIndex++];

            // Reset buffer
            Buffer = 0;
            BufferLength = 0;
            int outputLength = 0;
            // Extra test ensures that the correct number of bits are read from the last byte
            while (InputIndex < inputLength || 8 - BufferLength < bitsInLastByte)
            {
                byte b = ReadSymbol(root);
                output.Write(b);
                outputLength++;
            }
            return outputLength;
        }

        private byte ReadSymbol(HuffmanNode node)
        {
            while (node.Value == null) node = ReadBit(Input, ref InputIndex) ? node.Right! : node.Left!;
            return (byte)node.Value;
        }


        /* --------------------------------- Data Structures ---------------------------------------- */
        public class ByteMapping
        {
            // Byte value left implicit as array index
            // public readonly byte Value;
            public ulong MappedValue;
            public byte MappedLength;
        }

        public class HuffmanNode
        {
            public readonly int Frequency;
            public byte? Value;
            public HuffmanNode? Left; // 0
            public HuffmanNode? Right; // 1

            public HuffmanNode(int frrequency, byte? value, HuffmanNode? left, HuffmanNode? right) { 
                Frequency = frrequency;
                Value = value;
                Left = left;
                Right = right;
            }
        }
    }

    public class HuffmanReader
    {
        private Stream Stream;

        private readonly byte[] IndexMapping = new byte[256];
        private readonly HuffmanNode Root;
        // For writing bits/bytes to output. Insert new bits at most significant end, so 'first' bit is LSB of output
        private byte Buffer = 0;
        private int BufferLength = 0; // index from LSB where next bit will be written
        private int LeafCount = 0;

        private int bitsInLastByte;

        public HuffmanReader(Stream stream)
        {
            Stream = stream;
            // TODO: Tidy up order of fields
            stream.Seek(64, SeekOrigin.Begin);
            stream.Read(IndexMapping, 0, 256);
            stream.Seek(0, SeekOrigin.Begin);

            // Read tree encoding
            Root = new(0, null, null, null);
            BytesToTree(Root);
            stream.Seek(64 + 256, SeekOrigin.Begin); // seek to end of header containing encoding

            // Read number of bits of last byte
            // Could be used to detect when end of stream is, but currently just rely on correct use by caller
            // (since this is only used to lookup a specific index, not process the whole table)
            bitsInLastByte = stream.ReadByte();

            // Reset buffer
            Buffer = 0;
            BufferLength = 0;
        }

        public byte ReadByte()
        {
            return ReadSymbol(Root);
        }

        public ushort ReadUShort()
        {
            ushort result = ReadSymbol(Root);
            result <<= 8;
            result |= ReadSymbol(Root);
            return result;
        }

        public void Seek(uint offset)
        {
            while (offset-- > 0)
            {
                ReadByte();
            }
        }

        private void BytesToTree(HuffmanNode currentNode)
        {
            bool isLeaf = ReadBit();
            if (isLeaf)
            {
                currentNode.Value = IndexMapping[LeafCount++];
            }
            else
            {
                HuffmanNode left = new(0, null, null, null);
                BytesToTree(left);
                HuffmanNode right = new(0, null, null, null);
                BytesToTree(right);
                currentNode.Left = left;
                currentNode.Right = right;
            }
        }
        private byte ReadSymbol(HuffmanNode node)
        {
            while (node.Value == null) node = ReadBit() ? node.Right! : node.Left!;
            return (byte)node.Value;
        }

        private bool ReadBit()
        {
            if (BufferLength == 0)
            {
                BufferLength = 8;
                Buffer = (byte)Stream.ReadByte();
            }
            BufferLength--;
            bool isLeaf = (Buffer & 1) == 1;
            Buffer >>= 1;
            return isLeaf;
        }
    }
}
