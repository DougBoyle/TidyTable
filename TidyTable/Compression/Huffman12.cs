using static TidyTable.Compression.Huffman12;

namespace TidyTable.Compression
{
    // Same as th regular Huffman coding program, but for 12-bit values represented as shorts (i.e. 2-byte boundaries)

    /*
     * Format:
     *  <num codewords (4 bytes)> | tree structure | tree values | encoding
     * 
     *  Size of tree not stored, simply read the whole structure, then populate the values
     *    (Means traversing the tree twice, but should have negligible impact for ~8k nodes vs IO cost)
     *  Number of codewords is stored, to avoid needing an end marker (or number of bits in last byte)
     */
    public class Huffman12
    {
        private const int symbolLength = 12;
        private const int numSymbols = 1 << symbolLength;
        private const ulong symbolMask = (1 << symbolLength) - 1;


        private BinaryReader input;
        private BinaryWriter output;

        public Huffman12(BinaryReader input, BinaryWriter output)
        {
            this.input = input;
            this.output = output;
        }

        // inputLength is the number of 12-bit values, or half the length of the input stream
        public static long Compress(BinaryReader input, BinaryWriter output, int inputLength)
        {
            output.Write(inputLength);
            var huffman = new Huffman12(input, output);
            var tree = huffman.BuildHuffmanTree(input, inputLength);
            var mapping = huffman.TreeToMapping(tree);

            huffman.EncodeBytes(mapping, input, inputLength);

            return output.BaseStream.Length;
        }

        private readonly int[] frequencyTable = new int[numSymbols];

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
                output.Write(BitBuffer);
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
            BufferLength += symbolLength;

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
            Mapping[] mappings = new Mapping[numSymbols];

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
            FlushBuffer();
        }

        /* --------------------------------- Decoding ---------------------------------------- */
        // First build tree with all leaves empty, then populate leaves
        // Only used for decoding initial tree structure
        private bool ReadBit()
        {
            if (BitBufferLength == 0)
            {
                BitBufferLength = 8;
                BitBuffer = input.ReadByte();
            }
            BitBufferLength--;
            bool isLeaf = (BitBuffer & 1) == 1;
            BitBuffer >>= 1;
            return isLeaf;
        }

        private short ReadSymbol()
        {
            while (BufferLength < symbolLength)
            {
                Buffer |= (ulong)(input.ReadByte()) << BufferLength;
                BufferLength += 8;
            }
            BufferLength -= symbolLength;
            short value = (short)(Buffer & symbolMask);
            Buffer >>= symbolLength;
            return value;
        }

        private void ResetBitBuffer()
        {
            BitBuffer = 0;
            BitBufferLength = 0;
        }

        private void BytesToTree(Huffman12Node currentNode)
        {
            bool isLeaf = ReadBit();
            if (isLeaf)
            {
                currentNode.Value = 0; // To be filled in later
            } else
            {
                Huffman12Node left = new(0, null, null, null);
                BytesToTree(left);
                Huffman12Node right = new(0, null, null, null);
                BytesToTree(right);
                currentNode.Left = left;
                currentNode.Right = right;
            }
        }

        private void PopulateTree(Huffman12Node currentNode)
        {
            if (currentNode.Value != null)
            {
                currentNode.Value = ReadSymbol();
            } else
            {
                if (currentNode.Left != null) PopulateTree(currentNode.Left);
                if (currentNode.Right != null) PopulateTree(currentNode.Right);
            }
        }

        public static int Decompress(BinaryReader input, BinaryWriter output)
        {
            return new Huffman12(input, output).Decode();
        }

        private int Decode()
        {
            int numCodewords = input.ReadInt32();

            // Read tree encoding
            Huffman12Node root = new(0, null, null, null);
            SetUpDecoding(root);

            // Extra test ensures that the correct number of bits are read from the last byte
            for (int i = 0; i < numCodewords; i++) output.Write(ReadSymbol(root));
            return numCodewords;
        }

        public void SetUpDecoding(Huffman12Node root)
        {
            BytesToTree(root);
            PopulateTree(root);
            ResetBitBuffer(); // Could pack together, but wastes at most 1 byte here for nice layout
        }

        public short ReadSymbol(Huffman12Node node)
        {
            while (node.Value == null) node = ReadBit() ? node.Right! : node.Left!;
            return (short)node.Value;
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

    // For only decoding up to the point needed, then discarding
    public class Huffman12Reader
    {
        // does all the actual work
        private readonly Huffman12 processor;
        private readonly BinaryReader reader;
        
        // Allows re-reading multiple times without decoding Huffman tree again
        private readonly long startOfData;
        private readonly long dataLength;
        private long symbolsRead = 0;

        private readonly Huffman12Node Root;

        public Huffman12Reader(BinaryReader reader)
        {
            // output stream won't actually be used
            this.reader = reader;
            processor = new Huffman12(reader, new BinaryWriter(new MemoryStream()));
            dataLength = reader.ReadInt32();

            Root = new(0, null, null, null);
            processor.SetUpDecoding(Root);

            startOfData = reader.BaseStream.Position;
        }

        public short ReadSymbol()
        {
            if (symbolsRead++ < dataLength) throw new EndOfStreamException();
            return processor.ReadSymbol(Root);
        }

        public void Seek(uint offset)
        {
            while (offset-- > 0)
            {
                ReadSymbol();
            }
        }

        public void Reset()
        {
            reader.BaseStream.Seek(startOfData, SeekOrigin.Begin);
            symbolsRead = 0;
        }
    }
}
