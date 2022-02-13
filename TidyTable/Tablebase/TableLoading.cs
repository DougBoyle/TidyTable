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
            if (File.Exists(solvingTable))
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
