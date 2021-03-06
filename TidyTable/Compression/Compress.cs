namespace TidyTable.Compression
{
    // 0 indicates an error occurred during compression/decompression
    static class Compress
    {
        // TODO: Ability to compress in blocks of custom size (with default).
        // Can start each block with its compressed (or even decompressed so that doesn't need to be known) length
        // to allow rapidly scanning through file to required point
        public delegate int CompressionScheme(byte[] input, byte[] output, int inputLength);
        public static readonly CompressionScheme EncodingScheme = Huffman.Compress;
        public static readonly CompressionScheme DecodingScheme = Huffman.Decompress;

        public delegate int StreamCompressionScheme(byte[] input, BinaryWriter output, int inputLength);
        public static readonly StreamCompressionScheme StreamEncodingScheme = Huffman.Compress;
        public static readonly StreamCompressionScheme StreamDecodingScheme = Huffman.Decompress;

        public static void CompressToFile(byte[] input, int inputLength, string filename)
        {
            var output = new BinaryWriter(new FileStream(filename, FileMode.Create));
            CompressBytes(input, inputLength, output);
            output.Flush();
        }

        public static void Decompress(string input, BinaryWriter output)
        {
            byte[] inputBuffer = File.ReadAllBytes(input);
            StreamDecodingScheme(inputBuffer, output, inputBuffer.Length);
            output.Flush();
        }

        public static void CompressFile(string input, string output)
        {
            byte[] inputBuffer = File.ReadAllBytes(input);
            int outputBufferSize = inputBuffer.Length * 2;
            byte[] outputBuffer = new byte[outputBufferSize];
            int outputSize;
            
            while ((outputSize = CompressBytes(inputBuffer, inputBuffer.Length, outputBuffer)) == 0)
            {
                outputBufferSize *= 2;
                outputBuffer = new byte[outputBufferSize];
            }

            using var fs = new FileStream(output, FileMode.Create);
            fs.Write(outputBuffer, 0, outputSize);
        }

        public static int CompressBytes(byte[] input, int size, byte[] output)
        {
            return EncodingScheme(input, output, size);
        }

        public static int CompressBytes(byte[] input, int size, BinaryWriter output)
        {
            return StreamEncodingScheme(input, output, size);
        }

        public static void DeompressFile(string input, string output)
        {
            byte[] inputBuffer = File.ReadAllBytes(input);
            int outputBufferSize = 2 * inputBuffer.Length;
            byte[] outputBuffer = new byte[outputBufferSize];
            int outputSize;

            while ((outputSize = DecompressBytes(inputBuffer, inputBuffer.Length, outputBuffer)) == 0)
            {
                outputBufferSize *= 2;
                outputBuffer = new byte[outputBufferSize];
            }

            using var fs = new FileStream(output, FileMode.Create);
            fs.Write(outputBuffer, 0, outputSize);
        }

        

        public static int DecompressBytes(byte[] input, int size, byte[] output)
        {
            return DecodingScheme(input, output, size);
        }

        public static int DecompressBytes(byte[] input, int size, BinaryWriter output)
        {
            return StreamDecodingScheme(input, output, size);
        }

        public static bool FileEquals(string file1, string file2)
        {
            using FileStream s1 = new(file1, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream s2 = new(file2, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader b1 = new(s1);
            using BinaryReader b2 = new(s2);
            while (true)
            {
                byte[] data1 = b1.ReadBytes(64 * 1024);
                byte[] data2 = b2.ReadBytes(64 * 1024);
                if (data1.Length != data2.Length)
                    return false;
                if (data1.Length == 0)
                    return true;
                if (!data1.SequenceEqual(data2))
                    return false;
            }
        }
    }
}
