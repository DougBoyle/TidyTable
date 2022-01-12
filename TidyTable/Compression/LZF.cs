namespace TidyTable.Compression
{
    public static class LZF
    {
        /*
        Based off of implementation at https://github.com/Chaser324/LZF/blob/master/CLZF2.cs
        That uses a static lock object to be thread safe, for now only considering a non-concurrent implementation
        (C# has a lock (object) { ... } statement like Java synchronized)
        
        Description of algorithm:
            Initially estimate output as 2*input size for both directions, double if insufficient.
        
            Maintain a hash table based on the 'next two' bytes at a position, storing the index before those two bytes.
            Update this each step along the input, keeping track of how many input bytes walked over.
            If the maximum backlog of input bytes is reached (32), write these to the output.
            
            When a hash collision occurs, check for a 3 byte match.
            If found, write any accumulated bytes to the output first, then record this duplicate string by just
            its length - 2 (as it is at least 3 and at most 2^8 + 8) and the offset backwards to find the original.
        
            When there is no longer enough space in the input for another match, record remaining accumulated bytes to the output.

            As length of match is recorded starting with either 111XXXXX or (len - 2) << 5 ..., always greater than 32.
            As maximum literal run is 32, and length of run is written before the run, can decode as literals or back reference by > or < 32.

            The hash table is just a quick lookup for finding previous matches. When decoding, just check literal/reference and handle as such.
         */



        // Table only used for compression, not decompressing
        // 13 recommended for low memory, little difference between 14/15, can use up to 22
        const int LogHashTableSize = 14;
        private const int HashTableSize = 1 << LogHashTableSize;
        private static readonly long[] HashTable = new long[HashTableSize];

        private const uint MAX_LITERAL_RUN = 1 << 5; // 32
        // max offset between matches must fit in 13 bits to write into 2/3 bytes along with match length
        private const uint MAX_OFFSET = 1 << 13;
        // max is 100001000 and shortest match is 3 - will encode into either 3 bits (if len - 2 < 7)
        //  or 11 bits (len - 2 <= 256 + 8 - 2 = 262, so can store as 111 + (len - 2 - 7) as len - 9 <= 255)
        private const uint MAX_MATCH_LENGTH = (1 << 8) + (1 << 3);

        public static int Compress(byte[] input, byte[] output, int inputLength)
        {
            int outputLength = output.Length;

            long hashTableIndex;
            uint inputIndex = 0;
            uint outputIndex = 0;
            long matchIndex;

            uint nextBytes = (uint)(((input[inputIndex]) << 8) | input[inputIndex + 1]);
            long offset;
            int literalBytesSkipped = 0;

            Array.Clear(HashTable, 0, HashTableSize);

            while (inputIndex != inputLength)
            {
                if (inputIndex < inputLength - 2) // at least 3 bytes left, can check for a match
                {
                    nextBytes = (nextBytes << 8) | input[inputIndex + 2]; // keeps next 2 bytes in a short
                    // hash function of next 2 values, AND'd to size of table by bit mask
                    hashTableIndex = Hash(nextBytes);
                    matchIndex = HashTable[hashTableIndex]; // previous index in table
                    HashTable[hashTableIndex] = inputIndex; // store current index into table

                    if ((offset = inputIndex - matchIndex - 1) < MAX_OFFSET // distance between indices with same hash within limit
                        && inputIndex + 4 < inputLength // At least 5 bytes left
                        && matchIndex > 0 // value initialised, not default 0
                        && input[matchIndex + 0] == input[inputIndex + 0] // data has same 3 bytes as current/last index with this hash
                        && input[matchIndex + 1] == input[inputIndex + 1]
                        && input[matchIndex + 2] == input[inputIndex + 2]
                        )
                    {
                        /* match found at *matchIndex++ */
                        uint len = 2;
                        uint maxlen = (uint)inputLength - inputIndex - len; // bytes after those already matched
                        maxlen = maxlen > MAX_MATCH_LENGTH ? MAX_MATCH_LENGTH : maxlen; // cap max length

                        // Not enough space for previous unrecorded bytes plus minimum match length
                        if (outputIndex + literalBytesSkipped + 1 + 3 >= outputLength) 
                            return 0;

                        do // find length of matching bytes
                            len++;
                        while (len < maxlen && input[matchIndex + len] == input[inputIndex + len]);

                        // There are bytes not recorded as part of a match (or first occurence of a later match),
                        // must write them out before advancing the input beyond this match  and recording this match in output
                        if (literalBytesSkipped != 0)
                        {
                            output[outputIndex++] = (byte)(literalBytesSkipped - 1);
                            literalBytesSkipped = -literalBytesSkipped;
                            do
                                output[outputIndex++] = input[inputIndex + literalBytesSkipped];
                            while ((++literalBytesSkipped) != 0);
                        }

                        len -= 2; // len always >= 3, so decrement by 2 to use all values
                        inputIndex++;

                        // off = distance between occurance fits in 13 bits
                        // In either case, as match >= 3 and either 111 or len - 2 written in top 3 bits, first byte is
                        // always >= 32 and a literal run always starts with its length < 32, so can decode appropriately
                        if (len < 7) // fits in 3 bits
                        {
                            // write [ matchLen - 2, offset top 5 bits ] : len < 7 so never 111
                            output[outputIndex++] = (byte)((offset >> 8) + (len << 5));
                        }
                        else // maxLen = 2^8 + 2^3 = 264, so len - 2 - 7 <= 255 so fits in 1 bytes
                        {
                            // write [ 111, offset top 5 bits ]
                            output[outputIndex++] = (byte)((offset >> 8) + (7 << 5));
                            // write len - 2 - 7, can reconstruct by adding this to 111 from previous field + 2 above
                            output[outputIndex++] = (byte)(len - 7);
                        }

                        // write remaining 8 bits of offset into later cell
                        output[outputIndex++] = (byte)offset;

                        // a new match can't overlap the one just found (would duplicate in output),
                        // but the characters at the end of this match can be matched by a later string, so store last 2 indices to hash table
                        inputIndex += len - 1; // skip to end of match and recompute hash of current/next input
                        nextBytes = (uint)(((input[inputIndex]) << 8) | input[inputIndex + 1]);

                        // store new current index into hash location of next 2 values
                        nextBytes = (nextBytes << 8) | input[inputIndex + 2];
                        HashTable[Hash(nextBytes)] = inputIndex;
                        inputIndex++;

                        // repeat, store following index into hash location of it's next 2 cells
                        nextBytes = (nextBytes << 8) | input[inputIndex + 2];
                        HashTable[Hash(nextBytes)] = inputIndex;
                        inputIndex++;
                        continue;
                    }
                }

                // one more literal byte we must copy - inputIndex now points at byte in too short match or just after a match
                literalBytesSkipped++;
                inputIndex++;

                if (literalBytesSkipped == MAX_LITERAL_RUN) // maximum bytes collected up, must write out
                {
                    if (outputIndex + 1 + MAX_LITERAL_RUN >= outputLength) // no space to copy input bytes
                        return 0;

                    output[outputIndex++] = (byte)(MAX_LITERAL_RUN - 1); // number of bytes to be written
                    literalBytesSkipped = -literalBytesSkipped;
                    do
                        output[outputIndex++] = input[inputIndex + literalBytesSkipped]; // write previous lit bytes behind input pointer
                    while ((++literalBytesSkipped) != 0);
                }
            } // while

            // if space, write number of bytes remaining to be copied, followed by those bytes
            if (literalBytesSkipped != 0)
            {
                if (outputIndex + literalBytesSkipped + 1 >= outputLength)
                    return 0;

                output[outputIndex++] = (byte)(literalBytesSkipped - 1);
                literalBytesSkipped = -literalBytesSkipped;
                do
                    output[outputIndex++] = input[inputIndex + literalBytesSkipped];
                while ((++literalBytesSkipped) != 0);
            }

            return (int)outputIndex;
        }

        private static long Hash(uint nextBytes) =>
            ((nextBytes ^ (nextBytes << 5)) >> (int)(((3 * 8 - LogHashTableSize)) - nextBytes * 5) & (HashTableSize - 1));

        public static int Decompress(byte[] input, byte[] output, int inputLength)
        {
            int outputLength = output.Length;

            uint inputIndex = 0;
            uint outputIndex = 0;

            do
            {
                uint literalRunLength = input[inputIndex++];

                if (literalRunLength < (1 << 5)) /* literal run */
                {
                    literalRunLength++;

                    if (outputIndex + literalRunLength > outputLength)
                    {
                        return 0;
                    }

                    do
                        output[outputIndex++] = input[inputIndex++];
                    while ((--literalRunLength) != 0);
                }
                else /* back reference */
                {
                    // top 3 bits encode all or part of match length
                    uint len = literalRunLength >> 5;

                    // bottom 5 bits of literalRunLegth encode top 5 bits of match offset
                    int matchIndex = (int)(outputIndex - ((literalRunLength & 0x1f) << 8) - 1);

                    if (len == 7) // next byte encodes rest of match length
                        len += input[inputIndex++];

                    // next bytes encodes remaining 8 bits of match offset
                    matchIndex -= input[inputIndex++];

                    if (outputIndex + len + 2 > outputLength || matchIndex < 0)
                    {
                        return 0;
                    }

                    // match is at least length 3, so was subtracted by 2 and can copy first 2 bytes immediately
                    output[outputIndex++] = output[matchIndex++];
                    output[outputIndex++] = output[matchIndex++];

                    do
                        output[outputIndex++] = output[matchIndex++];
                    while ((--len) != 0);
                }
            }
            while (inputIndex < inputLength);

            return (int)outputIndex;
        }
    }
}
