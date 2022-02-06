using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Compression
{
    public class LZWHuffman
    {
        public static void Encode(byte[] input, BinaryWriter output)
        {

            var lzwStream = new TwelveBitStream();
            LZW.Compress(new MemoryStream(input), input.Length, lzwStream);
            var stream = lzwStream.stream.stream;

            // Set up for bytes just written to be read out now for Huffman encoding

            // Each 12-bit symbol occupies 2 bytes
            var symbolLength = stream.Length / 2;
            stream.Seek(0, SeekOrigin.Begin);
            Huffman12.Compress(new BinaryReader(stream), output, (int)symbolLength);
        }

        public static Stream Decode(BinaryReader input) => new LZWStream(new Huffman12Reader(input));

        public static void Test()
        {
            var filename = "tables/automated/KR-KN.dtm";
            var outputFile = "tables/automated/KR-KN.compress";
            var stream = new FileStream(filename, FileMode.Open);
            var length = (int)stream.Length;
            var array = new BinaryReader(stream).ReadBytes(length);
            var outputWriter = new BinaryWriter(new FileStream(outputFile, FileMode.Create));
            Encode(array, outputWriter);
            outputWriter.Close();

            var decodedStream = Decode(new BinaryReader(new FileStream(outputFile, FileMode.Open)));
            stream.Seek(0, SeekOrigin.Begin);
            for (long i = 0; i < length; i++)
            {
                var original = stream.ReadByte();
                var decoded = decodedStream.ReadByte();
                if (original < 0) throw new Exception("Something went wrong reading original");
                if (decoded < 0) throw new Exception("Issue decoding stream");
                if (original != decoded) throw new Exception($"Streams don't match at byte {i}");
            }
            Console.WriteLine("Correctly decoded data");
        }

        public static void TestAlternative()
        {
            var filename = "tables/automated/KR-KN.dtm";
            var outputFile = "tables/automated/KR-KN.compare";
            var stream = new FileStream(filename, FileMode.Open);
            var length = (int)stream.Length;
      //      var array = new BinaryReader(stream).ReadBytes(length);
      //      var outputWriter = new BinaryWriter(new FileStream(outputFile, FileMode.Create));

     //       var outuptArray = new byte[length];
            var outputStream = new TwelveBitStream();
            var outputSize = LZW.Compress(stream, length, outputStream);
            Console.WriteLine($"Compressed down to {outputStream.stream.stream.Length} bytes");
      //      outputWriter.Close();
        }
    }
}
