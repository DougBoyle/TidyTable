using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Endgames;
using TidyTable.Tables;

namespace TidyTable.Tablebase
{
    public static class TableLoading
    {
        private static Dictionary<string, int> maxBits = new()
        {
            { "KR-K", 5 },
            { "KQ-K", 5 },
            { "KNN-K", 1 },
            { "BKN-K", 7 },
            { "BBK-K", 6 },
            { "KP-K", 5 },
            { "KR-KN", 6 },
            { "KR-BK", 6 },
            { "KR-KR", 3 },
            { "KQ-KN", 6 },
            { "KQ-BK", 5 },
            { "KQ-KR", 6 },
            { "KQ-KQ", 4 },
            { "KNR-K", 5 },
            { "BKR-K", 5 },
            { "KRR-K", 4 },
            { "KNQ-K", 4 },
            { "BKQ-K", 4 },
            { "KQR-K", 3 },
            { "KQQ-K", 3 },
            { "KP-KN", 4 },
            { "KP-BK", 3 },
            { "KP-KR", 5 },
            { "KP-KQ", 6 },
            { "KNP-K", 5 },
            { "BKP-K", 5 },
            { "KPR-K", 4 },
            { "KPQ-K", 4 },
            { "KP-KP", 5 },
            { "KPP-K", 5 },
        };

        public static int? getMaxBits(string classification)
        {
            if (maxBits.ContainsKey(classification))
            {
                return maxBits[classification];
            }
            else return null;
        }

        public static SubTable LoadFromFileElseSolve(
           string filename,
           List<ColourlessPiece> whitePieces,
           List<ColourlessPiece> blackPieces,
           List<SubTable> subTables,
           BoardIndexer indexer,
           BoardNormaliser normalisation
        )
        {
            var classification = Classifier.ClassifyPieceLists(whitePieces, blackPieces);
            var isSymmetric = Classifier.ReverseClassification(classification) == classification;
            var solvingTable = filename + ".solv";

            var maxBits = getMaxBits(classification);
            Console.WriteLine($"Known number of bits for table {classification} is {maxBits}");

            if (File.Exists(solvingTable) && maxBits != null)
            {
                Console.WriteLine($"Table {filename} loaded from existing file");
                return new SubTable(
                    solvingTable,
                    classification,
                    indexer.MaxIndex,
                    indexer.Index,
                    normalisation,
                    isSymmetric
                );
            }
            else
            {
                if (isSymmetric)
                {
                    var table = new SolvingTableSymmetric(
                        whitePieces,
                        subTables,
                        indexer.MaxIndex,
                        indexer.Index,
                        normalisation
                    );
                    table.SolveForPieces();
                    SubTable.WriteToFile(table, solvingTable);

                    var wldTable = new WLDTable(table);
                    var dtzTable = new DTZTable(table, new Dictionary<string, DTZTable>());

                    wldTable.WriteToFile(filename + ".wld");
                    dtzTable.WriteToFile(filename + ".dtz");

                    Console.WriteLine($"Table {filename} written to file");
                    return new SubTable(table);
                }
                else
                {
                    var table = new SolvingTable(
                        whitePieces,
                        blackPieces,
                        subTables,
                        indexer.MaxIndex,
                        indexer.Index,
                        normalisation
                    );
                    table.SolveForPieces();
                    SubTable.WriteToFile(table, solvingTable);

                    var wldTable = new WLDTable(table);
                    var dtzTable = new DTZTable(table, new Dictionary<string, DTZTable>());

                    wldTable.WriteToFile(filename + ".wld");
                    dtzTable.WriteToFile(filename + ".dtz");

                    Console.WriteLine($"Table {filename} written to file");
                    return new SubTable(table);
                }
            }
        }
    }
}
