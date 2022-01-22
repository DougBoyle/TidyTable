// See https://aka.ms/new-console-template for more information
using Chessington.GameEngine;
using Chessington.GameEngine.Pieces;
using TidyTable.Compression;
using TidyTable.Endgames;
using TidyTable.Tablebase;
using TidyTable.TableFormats;
using TidyTable.Tables;

/*
// Testing compression
if (args.Length >= 2) 
{
    Compress.CompressFile(args[0], args[1]);
    Compress.DeompressFile(args[1], $"{args[0]}.check");
    Console.WriteLine($"Are files equal after compressing/decompressing: {Compress.FileEquals(args[0], $"{args[0]}.check")}");
}
*/

var watch = new System.Diagnostics.Stopwatch();
watch.Start();

TidyTable.Tablebase.FourPieces.KQKR();

watch.Stop();
Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
Console.WriteLine("Press enter to exit...");
Console.ReadLine();
