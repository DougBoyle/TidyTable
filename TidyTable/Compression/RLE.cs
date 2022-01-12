namespace TidyTable.Compression
{
    public static class RLE
    {
        /*
         * Run Length Encoding is straightforward in principal, the only concern being escaping characters
         * so that not every singular byte needs to be run length encoded, potentially doubling the length.
         * Given this, the requirement that the length of a run is at most 255 (to fit into 1 byte) is guaranteed.
         * 
         * Only encoding at the byte-level is considered, which is a common approach to take.
         * 
         * Denote the escape byte 255 as \ (for obvious reasons)
         * 1)   '\' -> '\ \'
         * 2)   A * [4 <= n < 255] -> '\ A n'    Nether A not n is 255, so cannot misinterpret as the literal byte 255 '\'
         * 3)   Only runs of 4 or more are encoded, so a 3 or fewer run is copied directly as it would not be compressed
        */

        public const byte ESCAPE_BYTE = byte.MaxValue;

        public static int Compress(byte[] input, byte[] output, int inputLength)
        {
            // Output never larger than input, so require an array of at least the same size and don't check otherwise
            if (output.Length < inputLength) return 0;

            int inputIndex = 0;
            int outputIndex = 0;

            while (inputIndex < inputLength)
            {
                byte currentByte = input[inputIndex];
                if (currentByte == ESCAPE_BYTE)
                {
                    inputIndex++;
                    output[outputIndex++] = ESCAPE_BYTE;
                    output[outputIndex++] = ESCAPE_BYTE;
                }
                else if (inputIndex < inputLength - 3 // space for a run of at least 4
                    && input[inputIndex + 1] == currentByte
                    && input[inputIndex + 2] == currentByte
                    && input[inputIndex + 3] == currentByte
                )
                {
                    int end = inputIndex + 4;
                    while (end < inputLength && input[end] == currentByte && end - inputIndex + 1 < ESCAPE_BYTE)
                    {
                        end++;
                    }
                    output[outputIndex++] = ESCAPE_BYTE;
                    output[outputIndex++] = currentByte;
                    output[outputIndex++] = (byte)(end - inputIndex);
                    inputIndex = end;
                } else
                {
                    output[outputIndex++] = input[inputIndex++];
                }
            }
            return outputIndex;
        }

        // Rather than checking throughout the program, an out of bounds error is caught
        // on either array by the try/catch block, and exceeding inputLength is checked at the end.
        // This means that it is possible for the algorithm to read beyond inputLength for invalid input.
        public static int Decompress(byte[] input, byte[] output, int inputLength)
        {

            int inputIndex = 0;
            int outputIndex = 0;
            try
            {
                while (inputIndex < inputLength)
                {
                    byte currentByte = input[inputIndex++];
                    if (currentByte == ESCAPE_BYTE)
                    {
                        byte originalByte = input[inputIndex++];
                        if (originalByte == ESCAPE_BYTE) // escaped ESCAPE_BYTE from original data
                        {
                            output[outputIndex++] = ESCAPE_BYTE;
                        }
                        else // run, need to get run length
                        {
                            byte length = input[inputIndex++];
                            Array.Fill(output, originalByte, outputIndex, length);
                            outputIndex += length;
                        }

                    }
                    else // literal byte
                    {
                        output[outputIndex++] = currentByte;
                    }
                }
            } catch (IndexOutOfRangeException)
            {
                return 0;
            }
            return inputIndex == inputLength ? outputIndex : 0;
        }
    }
}
