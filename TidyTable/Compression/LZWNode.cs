using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    public struct LZWNode
    {
        // The naive implementation of LZW uing an array is slow, as it is scanned for every single byte encoded.
        // Slightly better is to use a Hash table or Tree to store the different sequences, to allow for fast lookup
        //     but still consuming more memory than necessary.
        // The best option is to exploit the structure to achieve O(1) operations when encoding
        // (decoding is already fast, but could also achieve lower memory usage at the cost of slower decoding)

        /*
         * Simple approach: Represent the dictionary as a tree data structure.
         *      To avoid 256 pointers per node, store a dictionary at each node instead (still wastes some space)
         * Clever approach: Represent each dictionary entry by { prefix index, last byte, <first>, <next> }
         *      where <first> is the index of the first entry to use this string as its prefix, and <next> is the next
         *      entry with the same prefix as this one. This list is maintained by inserting new entries at the start of the list.
         *      Even better, store <left> and <right> and search now finds the point in the tree to insert a new entry, as a binary tree
         * 
         */


        /*
         * LZW performance: On .dtm files (16 bit entries of DTM, DTZ, Outcome used to solve other tables),
         *  reduces size from 7.4KB to 5.7KB and uses up all 4096 table entries. 
         *  Only 23% less, but has mapped every 8 bits to 12 bits, likely easy to compress further by following with Huffman coding.
         *  Should also look into using variable length codes, but dictionary is filled so will not a huge impact.
         * 
         */

        public readonly short Index;
       
        // public readonly LZWNode?[] children = new LZWNode?[256];
        public readonly Dictionary<byte, LZWNode> Children = new();

        public LZWNode(short index)
        {
            Index = index;
        }
    }
}
