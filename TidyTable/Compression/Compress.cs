namespace TidyTable.Compression
{
    // 0 indicates an error occurred during compression/decompression
    static class Compress
    {
        // TODO: Ability to compress in blocks of custom size (with default).
        // Can start each block with its compressed (or even decompressed so that doesn't need to be known) length
        // to allow rapidly scanning through file to required point
        public static void CompressFile(string input, string output)
        {
            byte[] inputBuffer = File.ReadAllBytes(input);
            int outputBufferSize = inputBuffer.Length;
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
            return RLE.Compress(input, output, size);
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
            return RLE.Decompress(input, output, size);
        }
    }
}
