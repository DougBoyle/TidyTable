namespace TidyTable.Compression
{
    // Same as th regular Huffman coding program, but for 12-bit values represented as shorts (i.e. 2-byte boundaries)

    /*
     * Format:
     *   <bits in last byte> | <tree structure length - 2 bytes> | tree structure | tree values | encoding
     * 
     * 
     */
    public class Huffman12
    {
        private BinaryWriter output;

        private Huffman12(BinaryWriter output)
        {
            this.output = output;
        }

        public static long Compress(BinaryReader input, BinaryWriter output, int inputLength)
        {
            var huffman = new Huffman12(output);
            var tree = huffman.BuildHuffmanTree(input, inputLength);
            var mapping = huffman.TreeToMapping(tree);

            huffman.EncodeBytes(mapping, input, inputLength);


            output.Seek(0, SeekOrigin.End);

            return output.BaseStream.Length;
        }

        private readonly int[] frequencyTable = new int[4096];

        private Huffman12Node BuildHuffmanTree(BinaryReader input, int inputLength)
        {
            for (int i = 0; i < inputLength; i++) frequencyTable[input.ReadInt16()]++;
            List<Huffman12Node> forest = frequencyTable.Select((count, n) => new Huffman12Node(count, (short)n, null, null))
                .Where(node => node.Frequency > 0).ToList();
            while (forest.Count > 1)
            {
                var node1 = forest.MinBy(node => node.Frequency)!;
                forest.Remove(node1);
                var node2 = forest.MinBy(node => node.Frequency)!;
                forest.Remove(node2);
                forest.Add(new Huffman12Node(node1.Frequency + node2.Frequency, null, node1, node2));
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

        private readonly List<bool> TreeTraversal = new(); // false = inner node, true = leaf
        private readonly List<short> TreeValues = new();

        private void WriteTree() // maximum is 4096*2/8 + 4096*1.5 = 7168 bytes (vs 320 for byte input)
        {
            foreach (var b in TreeTraversal) WriteBit(b);
            FlushBitBuffer();

            foreach (var value in TreeValues) WriteSymbol(value);
            FlushBuffer();

            output.Seek(1, SeekOrigin.Begin);
            output.Write((short)((TreeTraversal.Count + 7) / 8 + (TreeValues.Count * 3 + 1) / 2));
            output.Seek(0, SeekOrigin.End);
        }

        
        private int TreeIndex = 0;
        private int LeafCount = 0; // int to allow looping 0 <= LeafCount < 256


        // ---------------------------------------- Writing Bits ------------------------------------------------------------

        // For writing bits as bytes to output. Insert new bits at most significant end, so 'first' bit is LSB of output
        private byte BitBuffer = 0;
        private int BitBufferLength = 0; // index from LSB where next bit will be written

        private void WriteLeaf(short value)
        {
            TreeTraversal.Add(true);
            TreeValues.Add(value);
        }

        private void WriteNode()
        {
            TreeTraversal.Add(false);
        }

        private void WriteBit(bool b)
        {
            BitBuffer |= (byte)((b ? 1 : 0) << BitBufferLength);
            BitBufferGrew();
        }

        private void FlushBitBuffer()
        {
            if (BitBufferLength > 0)
            {
                BitBufferLength = 7;
                BitBufferGrew();
            }
        }

        private void BitBufferGrew()
        {
            if (++BitBufferLength == 8)
            {
                output.Write((byte)BitBuffer);
                BitBuffer = 0;
                BitBufferLength = 0;
            }
        }

        // ---------------------------------------- Writing 12-bit values/codewords --------------------------------------

        private ulong Buffer = 0;
        private int BufferLength = 0;
        private void WriteSymbol(short symbol)
        {
            Buffer |= (ulong)symbol << BufferLength;
            BufferLength += 12;

            while (BufferLength >= 8)
            {
                // Buffer appends onto MSB, so oldest bits are at LSB, already in correct order
                output.Write((byte)Buffer);
                Buffer >>= 8;
                BufferLength -= 8;
            }
        }

        private void WriteCodeword(Mapping value)
        {
            Buffer |= value.MappedValue << BufferLength;
            BufferLength += value.MappedLength;

            if (BufferLength > 64)
                throw new ArgumentException("Encoding chose a codeword with length exceeding buffer capacity");

            while (BufferLength >= 8)
            {
                // Buffer appends onto MSB, so oldest bits are at LSB, already in correct order
                output.Write((byte)Buffer);
                Buffer >>= 8;
                BufferLength -= 8;
            }
        }

        // returns number of bits in last byte
        private byte FlushBuffer()
        {
            if (BufferLength > 0)
            {
                output.Write((byte)Buffer);
                var bits = (byte)BufferLength;
                Buffer = 0;
                BufferLength = 0;
                return bits;
            } else return 8;
        }

        // ------------------------------------------------------------------------------------------------------------------

        // Root of tree corresponds to LSB of nodes, to avoid corresponding to a variable bit position on decoding
        private Mapping[] TreeToMapping(Huffman12Node tree)
        {
            Mapping[] mappings = new Mapping[4096];

            void MapPath(Mapping currentMapping, Huffman12Node branch)
            {
                if (branch.Value != null)
                {
                    byte value = (byte)branch.Value;
                    WriteLeaf(value);
                    mappings[value] = currentMapping;
                } else
                {
                    WriteNode();
                    var mappingCopy = new Mapping() {
                        MappedLength = (byte)(currentMapping.MappedLength + 1),
                        MappedValue = currentMapping.MappedValue | (1UL << currentMapping.MappedLength),
                    };
                    currentMapping.MappedLength++;
                    MapPath(currentMapping, branch.Left!);
                    MapPath(mappingCopy, branch.Right!);
                }
            }

            MapPath(new Mapping(), tree);
            WriteTree();

            return mappings;
        }

        // TODO: Encode actual file, using a buffer to handle variable bits
        // TODO! ALL WRONG, NEED TO BE APPENDING TO TOP NOT BOTTOM!!! LSB is root of tree so read from LSB to MSB,
        //      which means Earlier writes are at LSB and later writes at MSB
        // Bits are appended from LSB towards MSB of buffer. Read output in byte order but from LSB to MSB of each byte.
        // As Root of tree is also LSB of encoding, makes appending to buffer easy too
        // TODO: Indicate end of mapped file, most likely by putting number of bytes at start of file as int

        private void EncodeBytes(Mapping[] mapping, BinaryReader input, int inputLength)
        {
            for (int i = 0; i < inputLength; i++) WriteCodeword(mapping[input.ReadInt16()]);
            var bitsInLastByte = FlushBuffer();
            output.Seek(0, SeekOrigin.Begin);
            output.Write(bitsInLastByte);
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
            return new Huffman12().Decode(input, output, size);
        }

        // TODO: Must handle possible write exception when calling this with an array-backed output
        public static int Decompress(byte[] input, BinaryWriter output, int size)
        {

            return new Huffman12().Decode(input, output, size);
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
        public class Mapping
        {
            // Byte value left implicit as array index
            public ulong MappedValue;
            public byte MappedLength;
        }

        public class Huffman12Node
        {
            public readonly int Frequency;
            public short? Value;
            public Huffman12Node? Left; // 0
            public Huffman12Node? Right; // 1

            public Huffman12Node(int frrequency, short? value, Huffman12Node? left, Huffman12Node? right) { 
                Frequency = frrequency;
                Value = value;
                Left = left;
                Right = right;
            }
        }
    }

    public class Huffman12Reader
    {
        private Stream Stream;

        private readonly byte[] IndexMapping = new byte[256];
        private readonly HuffmanNode Root;
        // For writing bits/bytes to output. Insert new bits at most significant end, so 'first' bit is LSB of output
        private byte Buffer = 0;
        private int BufferLength = 0; // index from LSB where next bit will be written
        private int LeafCount = 0;

        private int bitsInLastByte;

        public Huffman12Reader(Stream stream)
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

        private void BytesToTree(Huffman12Node currentNode)
        {
            bool isLeaf = ReadBit();
            if (isLeaf)
            {
                currentNode.Value = IndexMapping[LeafCount++];
            }
            else
            {
                Huffman12Node left = new(0, null, null, null);
                BytesToTree(left);
                Huffman12Node right = new(0, null, null, null);
                BytesToTree(right);
                currentNode.Left = left;
                currentNode.Right = right;
            }
        }
        private byte ReadSymbol(Huffman12Node node)
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

        public static short ReadInt12(BinaryReader reader)
        {
            try
            {
                return reader.ReadInt16();
            } catch (Exception)
            {
                return -1; // End of stream, and Int12 is never -1 when represented as shorts
            }
        }
    }
}
