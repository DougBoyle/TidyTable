using Chessington.GameEngine;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Endgames
{
    // Takes a normalised board and outputs its index into the tablebase
    public static class BoardIndexing
    {
        // No pawns vs Pawns on one side vs 1+ Pawns each side (en-passant) cases to each encode separately
        // Can use triangle symmetry in king case
        // - Can continue symmetry until first piece placed off of main diagonal
        // - Even if hard to track possibilities in determining index,
        //     can make some indicies impossble (always mirrored) hence more 0 bytes in output.
        // - If easy to index/reduces max - can subtract bitcount of previous pieces in total occupancy bitboard
        //     so that number of possibilities goes 64 * 63 * 62 * ...
        // - Can reduce pawn indices to 48 not 64, as pawns can't go on either end ranks unless considering en-passant

        public static int IndexNoPawnBoard(in Board board, List<PieceKind> nonKingPieces)
        {
            // Index is an int (max 2^31 - 1), and each piece takes 6 bits (2^6 = 64) except white king which takes 4 (2^4 = 16 > 10),
            // so at most 5 pieces including king allowed (requiring 28 bits)
            if (nonKingPieces.Count > 3) throw new ArgumentException("int index can only accomodate 5 pieces including kings");
            int index = 0;

            int whiteKingIndex = board.FindKing(Player.White);
            int kingCol = whiteKingIndex % 8;
            switch (whiteKingIndex / 8)
            {
                case 0: index = kingCol; break;
                case 1: index = 3 + kingCol; break;
                case 2: index = 5 + kingCol; break;
                case 3: index = 9; break;
            }

            index <<= 6;
            index |= board.FindKing(Player.Black);

            ulong[] bitboards = new ulong[12];
            Array.Copy(board.Bitboards, bitboards, 12);
            foreach (PieceKind piece in nonKingPieces)
            {
                index <<= 6;
                ulong bit = BitUtils.PopLSB(ref bitboards[(byte)piece]);
                if (bit == 0) throw new ArgumentException($"Board was missing a {piece}");
                index |= BitUtils.BitToIndex(bit);
            }
            return index;
        }

        // TODO: May want to make this stateful! i.e. record value of each king and each subsequent piece
        // TODO: Carefully test this!
        /* 
         * How values are calculated: (int is actually too small for 6 pieces, but that is independent of this analysis)
         * WK      BK     np1       np2     p1  p2
         * ...     ...    61*48*48  48*48   48  1
         * 
         * Pawn index is 'square - 8' to account for never being on first row.
         * Piece index is 'square - count(lower bits set for earlier pieces)' hence max is 64 - number of previous pieces
         * 
         * Kings are slightly complicated, let x = (max squares of next piece) * (value of following piece), can work in units of that
         * For WK off diagonal:
         *      Black king is as usual above, but index -= 3 if after white king, as can guarantee not adjacent and WK not in corner
         *      WK is then just 0-5 (inclusive) based on position in triangle, times 61*x
         * WK on diagonal:
         *      Normalisation will ensure the black king is not in the upper triangle, so only 8th triangular = 36 squares
         *      Black king index taken out of those 36 squares, -2 if after white king
         *      For white king, first add a base value of 6*61*x to exceed all cases above
         *      Then WK is 0-3 (inclusive) based on which point along diagonal, times 34*x
         *
         */
        public static int IndexNoPawnBoard2(in Board board, List<PieceKind> nonKingPieces)
        {
            // Index is an int (max 2^31 - 1), and each piece takes 6 bits (2^6 = 64) except white king which takes 4 (2^4 = 16 > 10),
            // so at most 5 pieces including king allowed (requiring 28 bits)
            if (nonKingPieces.Count > 3) throw new ArgumentException("int index can only accomodate 5 pieces including kings");
            int index = 0;

            // Initialise values associated with each piece - this is the part that could be moved to state
            // First sort to put any pawns at the start:
            //      48 < 64 - pieceIndex, so only use the 64-n trick for non-pawns, and want higher n in those cases
            nonKingPieces.Sort((p1, p2) =>
            {
                var p1IsPawn = IsPawn(p1);
                var p2IsPawn = IsPawn(p2);
                return Convert.ToInt32(p2IsPawn) - Convert.ToInt32(p1IsPawn); // -ve if p1 pawn, hence p1 smaller
            });
            int wkOffDiagonalValue;
            int wkOnDiagonalValue;
            int bkValue;
            int[] pieceValues = new int[nonKingPieces.Count];
            
            int currentValue = 1;
            for (int i = pieceValues.Length - 1; i >= 0; i--)
            {
                pieceValues[i] = currentValue;
                int numberOfValidSquares = IsPawn(nonKingPieces[i]) ? 48 : 64 - 2 - i; // - 2 kings - already placed pieces
                currentValue *= numberOfValidSquares;
            }
            bkValue = currentValue;
            wkOffDiagonalValue = bkValue * 61; // wk not in a corner, and bk cannot be adjacent, so only 61 squares
            wkOnDiagonalValue = bkValue * 34; // normalisation puts bk on a triangle of 36 squares, -2 adjacent/on white king

            /*-----------------------------------------------------------------------*/

            int whiteKingIndex = board.FindKing(Player.White);
            int kingCol = whiteKingIndex & 7;
            int kingRow = (whiteKingIndex >> 3) & 7;

            int blackKingIndex = board.FindKing(Player.Black);

            // complex case, normalisation applies an additional symmetry when WK on diagonal and BK above it
            if (kingCol == kingRow)
            {
                index += wkOffDiagonalValue * 6;
                index += wkOnDiagonalValue * BoardIndexToSmallTriangle[whiteKingIndex];
                var blackKingIndexInTriangle = BoardIndexToLargeTriangle[blackKingIndex];
                index += bkValue * (blackKingIndex > whiteKingIndex ? blackKingIndexInTriangle - 2 : blackKingIndexInTriangle);
            } else // simple case
            {
                index += wkOffDiagonalValue * BoardIndexToSmallTriangle[whiteKingIndex];
                index += bkValue * (blackKingIndex > whiteKingIndex ? blackKingIndex - 3 : blackKingIndex);
            }

            // handle non-king pieces
            ulong occupancy = board.Bitboards[(byte)PieceKind.WhiteKing] | board.Bitboards[(byte)PieceKind.BlackKing];
            ulong[] bitboards = new ulong[12];
            Array.Copy(board.Bitboards, bitboards, 12);
            for (int i = 0; i < nonKingPieces.Count; i++) {
                var piece = nonKingPieces[i];
                var value = pieceValues[i];
                ulong bit = BitUtils.PopLSB(ref bitboards[(byte)piece]);
                if (bit == 0) throw new ArgumentException($"Board was missing a {piece}");
                if (IsPawn(piece))
                {
                    // no adjustment for number of occupied squares, as first rank already removed
                    index += value * (BitUtils.BitToIndex(bit) - 8); 
                } else
                {
                    var numPreviousPieces = BitUtils.Count1s((bit - 1) & occupancy);
                    index += value * (BitUtils.BitToIndex(bit) - numPreviousPieces);
                }
                occupancy |= bit;
            }
            return index;
        }

        private static bool IsPawn(PieceKind piece) => piece == PieceKind.WhitePawn || piece == PieceKind.BlackPawn;
        
        // Maps the white king's position to the appropriate index, 0-5 off diagonal, 0-3 on diagonal
        private static readonly Dictionary<int, int> BoardIndexToSmallTriangle = new Dictionary<int, int>()
        {
            {0, 0}, {9, 1}, {18, 2}, {27, 3}, // on diagonal
            {1, 0}, {2, 1}, {3, 2}, // rank 1 off diagonal
            {10, 3}, {11, 4}, // rank 2 off diagonal
            {19, 5}, // rank 3 off diagonal
        };

        // Maps the black king's position to the appropriate index 0-35 in larger triangle
        // A bit lazy doing things this way (could just subtract based on row), but very straightforward
        private static readonly Dictionary<int, int> BoardIndexToLargeTriangle = new Dictionary<int, int>()
        {
            {0, 0}, {1, 1}, {2, 2},   {3, 3},   {4, 4},   {5, 5},   {6, 6},   {7, 7},   // rank 1
                    {9, 8}, {10, 9},  {11, 10}, {12, 11}, {13, 12}, {14, 13}, {15, 14}, // rank 2
                            {18, 15}, {19, 16}, {20, 17}, {21, 18}, {22, 19}, {23, 20}, // rank 3
                                      {27, 21}, {28, 22}, {29, 23}, {30, 24}, {31, 25}, // rank 4
                                                {36, 26}, {37, 27}, {38, 28}, {39, 29}, // rank 5
                                                          {45, 30}, {46, 31}, {47, 32}, // rank 6
                                                                    {54, 33}, {55, 34}, // rank 7
                                                                              {63, 35}, // rank 8
        };
    }
}
