LZMA (and LZMA2 it's successor) are algorithms used by the 7-zip library.

It can be considered a scaled-up version of LZF, in all of performance, memory
requirements, and complexity of hashing/matching.

Wikipedia describes the algorithm here: https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm#Compression_algorithm_details  
A C# implementation of both can be found at: https://github.com/weltkante/managed-lzma/tree/master/shared/Compression/Implementation  
Along with a C implementation at: https://github.com/weltkante/managed-lzma/tree/master/native/lzma  

At a minimum, for just the single threaded code (Not 'Mt'), the following files are needed:  
    * `LzFind` to search for matches (1000 lines)  
    * `LzHash` to compute hashes (20 lines)  
    * `CRC` is presumably a cyclic redundancy check (30 lines)  
    * `LzmaEnc` (2600 lines) and `LzmaDec` (1400 lines)  
    * `LzmaLib` as a wrapper and `Types` definitions (300 lines)  

This gives a total of about 5000 lines of code to implement, and the LZMA2 version
of encode/decode add an additional 1000 lines.

For now will stick to simpler methods, and may import this library as-is in the future
as this is not a Inforamtion Theory project to understand 5k lines of compression logic.