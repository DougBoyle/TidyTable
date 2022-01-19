// See https://aka.ms/new-console-template for more information
using TidyTable.Compression;

/*
// Testing compression
if (args.Length >= 2) 
{
    Compress.CompressFile(args[0], args[1]);
    Compress.DeompressFile(args[1], $"{args[0]}.check");
    Console.WriteLine($"Are files equal after compressing/decompressing: {Compress.FileEquals(args[0], $"{args[0]}.check")}");
}
*/

// TODO: KQKR incorrect - program says 37 moves, online TB says 35 with example: 8/8/8/8/2r5/8/2k5/K6Q w - - 0 1
TidyTable.Tablebase.FourPieces.KQKR();
Console.WriteLine("Press enter to exit...");
Console.ReadLine();
