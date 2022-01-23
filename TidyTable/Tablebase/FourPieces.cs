using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Endgames;
using TidyTable.Tables;

using CP = Chessington.GameEngine.Pieces.ColourlessPiece;
using static TidyTable.Tablebase.ThreePieces;

namespace TidyTable.Tablebase
{
    public class FourPieces
    {
        // TODO: Mark as symmetric and solve one-sided
        public static SubTable KQKQ()
        {
            var filename = TablePrefix + "KQKQ.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Queen };
            var blackPieces = new List<CP> { CP.King, CP.Queen };
            var indexer = new NoPawnBoardIndexing(new List<PieceKind> { PieceKind.WhiteQueen, PieceKind.BlackQueen });
            var subTables = new List<SubTable>() { KQK() };


            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                indexer,
                Normalisation.NormaliseNoPawnsBoard
            );
        }

        public static SubTable KQKR()
        {
            var filename = TablePrefix + "KQKR.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Queen };
            var blackPieces = new List<CP> { CP.King, CP.Rook };
            var indexer = new NoPawnBoardIndexing(new List<PieceKind> { PieceKind.WhiteQueen, PieceKind.BlackRook });
            var subTables = new List<SubTable>() { KQK(), KRK() };

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                indexer,
                Normalisation.NormaliseNoPawnsBoard
            );
        }

        // TODO: Mark as symmetric and solve one-sided
        public static SubTable KRKR()
        {
            var filename = TablePrefix + "KRKR.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Rook };
            var blackPieces = new List<CP> { CP.King, CP.Rook };
            var indexer = new NoPawnBoardIndexing(new List<PieceKind> { PieceKind.WhiteRook, PieceKind.BlackRook });
            var subTables = new List<SubTable>() { KRK() };

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                indexer,
                Normalisation.NormaliseNoPawnsBoard
            );
        }

        public static SubTable KPKN()
        {
            var filename = TablePrefix + "KPKN.dtm";
            var whitePieces = new List<CP> { CP.King, CP.Pawn };
            var blackPieces = new List<CP> { CP.King, CP.Knight };
            var indexer = new WhitePawnBoardIndexing(new List<PieceKind> { PieceKind.WhitePawn, PieceKind.BlackKnight });
            var subTables = new List<SubTable>() { KPK() };

            return LoadFromFileElseSolve(
                filename,
                whitePieces,
                blackPieces,
                subTables.Concat(subTables.Select(table => table.SwappedColour())).ToList(),
                indexer,
                Normalisation.NormalisePawnsBoard
            );
        }
    }
}
