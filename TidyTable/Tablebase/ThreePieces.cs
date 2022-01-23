using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Endgames;
using TidyTable.Tables;

using CP = Chessington.GameEngine.Pieces.ColourlessPiece;

namespace TidyTable.Tablebase
{
    public class ThreePieces
    {
        public const string TablePrefix = "tables/";

        public static SubTable KQK()
        {
            var filename = TablePrefix + "KQK.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Queen };
            var blackPieces = new List<CP> { CP.King };
            var indexer = new NoPawnBoardIndexing(new List<PieceKind> { PieceKind.WhiteQueen });

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                new List<SubTable>(),
                indexer,
                Normalisation.NormaliseNoPawnsBoard
            );
        }

        public static SubTable KRK()
        {
            var filename = TablePrefix + "KRK.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Rook };
            var blackPieces = new List<CP> { CP.King };
            var indexer = new NoPawnBoardIndexing(new List<PieceKind> { PieceKind.WhiteRook });

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                new List<SubTable>(),
                indexer,
                Normalisation.NormaliseNoPawnsBoard
            );
        }

        public static SubTable KPK()
        {
            var filename = TablePrefix + "KPK.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Pawn };
            var blackPieces = new List<CP> { CP.King };
            var indexer = new WhitePawnBoardIndexing(new List<PieceKind> { PieceKind.WhitePawn });
            var subTables = new List<SubTable>() { KRK(), KQK() }; // flipping is actually unnecessary in this case

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                indexer,
                Normalisation.NormalisePawnsBoard
            );
        }

        // Must generate the subtables for the reverse colour before calling this (for efficient reuse where possible).
        // Cheap to do, so generally always reverse each table once rather than work out which do/don't need both.
        public static SubTable LoadFromFileElseSolve(
            string filename,
            List<CP> whitePieces,
            List<CP> blackPieces,
            List<SubTable> subTables,
            BoardIndexer indexer,
            BoardNormaliser normalisation
        )
        {
            if (File.Exists(filename))
            {
                Console.WriteLine($"Table {filename} loaded from existing file");
                return new SubTable(
                    filename,
                    Classifier.ClassifyPieceLists(whitePieces, blackPieces),
                    indexer.MaxIndex,
                    indexer.Index,
                    normalisation
                );
            }
            else
            {
                var table = new SolvingTable(
                    whitePieces,
                    blackPieces,
                    subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                    indexer.MaxIndex,
                    indexer.Index,
                    normalisation
                );
                table.SolveForPieces();
                SubTable.WriteToFile(table, filename);
                if (filename.EndsWith(".dtm"))
                {
                    LookupTable.WriteToFile(table, filename.Replace(".dtm", ".mv"));
                    LookupTable.WriteToCompressedFile(table, filename.Replace(".dtm", ".mv.huf"));
                }   
                Console.WriteLine($"Table {filename} written to file");
                return new SubTable(table);
            }
        }
    }
}
