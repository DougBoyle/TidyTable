using Chessington.GameEngine;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TidyTable.Endgames.BoardIndexing;

namespace TidyTable.Endgames
{
    // Takes a normalised board and outputs its index into the tablebase

    // No pawns vs Pawns on one side vs 1+ Pawns each side (en-passant) cases to each encode separately
    // Can use triangle symmetry in king case
    // - Can continue symmetry until first piece placed off of main diagonal
    // - Even if hard to track possibilities in determining index,
    //     can make some indicies impossble (always mirrored) hence more 0 bytes in output.
    // - If easy to index/reduces max - can subtract bitcount of previous pieces in total occupancy bitboard
    //     so that number of possibilities goes 64 * 63 * 62 * ...
    // - Can reduce pawn indices to 48 not 64, as pawns can't go on either end ranks (unless considering en-passant)

    /* General approach for how values are calculated:
     * - int is actually too small for 6 pieces, but that is independent of this analysis
     * - pawns alway ordered to the start, leaves fewer squares for other pieces
     * WK      BK        p1        p2      np1     np2
     * ...     ...       47*60*59  60*59   59      1
     *                                     ^ The following piece np2 is after 5 already placed, so max 59 positions
     * 
     * Pawn index is 'square - 8 - count(lower bit position pawns)' to account for never being on first row
     * Piece index is 'square - count(lower bit position pieces)'
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
    // TODO: Carefully test this!

    public class NoPawnBoardIndexing: BoardIndexer
    {
        public uint MaxIndex { get; }

        private readonly List<PieceKind> nonKingPieces;
        private readonly int wkOffDiagonalValue;
        private readonly int wkOnDiagonalValue;
        private readonly int bkValue;
        private readonly int[] pieceValues;

        public NoPawnBoardIndexing(List<PieceKind> nonKingPieces)
        {
            // Index is an int (max 2^31 - 1), and each piece takes ~6 bits (2^6 = 64) bits,
            // so at most 5 pieces including king allowed (requiring 28 bits)
            if (nonKingPieces.Count > 3) throw new ArgumentException("int index can only accomodate 5 pieces including kings");
            this.nonKingPieces = nonKingPieces;
            pieceValues = new int[nonKingPieces.Count];

            int currentValue = 1;
            for (int i = pieceValues.Length - 1; i >= 0; i--)
            {
                pieceValues[i] = currentValue;
                int numberOfValidSquares = 64 - 2 - i; // - 2 kings - already placed pieces
                currentValue *= numberOfValidSquares;
            }
            bkValue = currentValue;
            wkOffDiagonalValue = bkValue * 61; // wk not in a corner, and bk cannot be adjacent, so only 61 squares
            wkOnDiagonalValue = bkValue * 34; // normalisation puts bk on a triangle of 36 squares, -2 adjacent/on white king

            MaxIndex = (uint)(6 * wkOffDiagonalValue + 4 * wkOnDiagonalValue);
        }

        // never uses the full 32 bits, so can safely treat as int then return uint
        public uint Index(Board board)
        {
            int index = 0;
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
            }
            else // simple case
            {
                index += wkOffDiagonalValue * BoardIndexToSmallTriangle[whiteKingIndex];
                index += bkValue * (blackKingIndex > whiteKingIndex ? blackKingIndex - 3 : blackKingIndex);
            }

            // handle non-king pieces
            ulong occupancy = board.Bitboards[(byte)PieceKind.WhiteKing] | board.Bitboards[(byte)PieceKind.BlackKing];
            ulong[] bitboards = new ulong[12];
            Array.Copy(board.Bitboards, bitboards, 12);
            for (int i = 0; i < nonKingPieces.Count; i++)
            {
                var piece = nonKingPieces[i];
                var value = pieceValues[i];
                ulong bit = BitUtils.PopLSB(ref bitboards[(byte)piece]);
                if (bit == 0) throw new ArgumentException($"Board was missing a {piece}");
                var numPreviousPieces = BitUtils.Count1s((bit - 1) & occupancy);
                index += value * (BitUtils.BitToIndex(bit) - numPreviousPieces);

                occupancy |= bit;
            }
            return (uint)index;
        }
    }

    // Unlike for no pawns above, due to fixed pawn direction, cannot flip any direction except horizontally,
    // so only 2x symmetry by enforcing the white king be on the left half of the board
    public class WhitePawnBoardIndexing: BoardIndexer
    {
        public uint MaxIndex { get; }

        private readonly List<PieceKind> nonKingPieces;
        private readonly int wkValue;
        private readonly int bkValue;
        private readonly int[] pieceValues;

        public WhitePawnBoardIndexing(List<PieceKind> nonKingPieces)
        {
            // Index is an int (max 2^31 - 1), and each piece takes ~6 bits (2^6 = 64) bits,
            // so at most 5 pieces including king allowed (requiring 28 bits)
            if (nonKingPieces.Count > 3) throw new ArgumentException("int index can only accomodate 5 pieces including kings");
            this.nonKingPieces = nonKingPieces;
            nonKingPieces.Sort((p1, p2) => (byte)p1 - (byte)p2); // put white pawns at start of list

            pieceValues = new int[nonKingPieces.Count];

            int currentValue = 1;
            for (int i = pieceValues.Length - 1; i >= 0; i--)
            {
                pieceValues[i] = currentValue;
                // Use the fact that pawns are first, so number of pawn squares is 48 - i
                int numberOfValidSquares = nonKingPieces[i] == PieceKind.WhitePawn ? 48 - i : 64 - 2 - i;
                currentValue *= numberOfValidSquares;
            }
            bkValue = currentValue;
            // eliminate 2 squares as bk can never be on same square or 1 of the square just after/before wk
            wkValue = bkValue * 62;

            MaxIndex = (uint)(wkValue * 32);
        }

        public uint Index(Board board)
        {
            int whiteKingIndex = board.FindKing(Player.White);
            // can discard bit 3 = which half of row, and shift bits for which row down one
            int whiteKingHalfBoardIndex = ((whiteKingIndex & 56) >> 1) + (whiteKingIndex & 0x3);
            int index = whiteKingHalfBoardIndex * wkValue;

            int blackKingIndex = board.FindKing(Player.Black);
            index += (blackKingIndex > whiteKingIndex ? blackKingIndex - 2 : blackKingIndex) * bkValue;

            // handle non-king pieces
            ulong occupancy = board.Bitboards[(byte)PieceKind.WhiteKing] | board.Bitboards[(byte)PieceKind.BlackKing];
            // count the number of pawns separately, restricted to indices 8-55
            ulong pawnOccupancy = 0UL;
            ulong[] bitboards = new ulong[12];
            Array.Copy(board.Bitboards, bitboards, 12);
            for (int i = 0; i < nonKingPieces.Count; i++)
            {
                var piece = nonKingPieces[i];
                var value = pieceValues[i];
                ulong bit = BitUtils.PopLSB(ref bitboards[(byte)piece]);
                if (bit == 0) throw new ArgumentException($"Board was missing a {piece}");
                if (IsPawn(piece))
                {
                    var numPreviousPawns = BitUtils.Count1s((bit - 1) & pawnOccupancy);
                    index += value * (BitUtils.BitToIndex(bit) - 8 - numPreviousPawns);
                    pawnOccupancy |= bit;
                }
                else
                {
                    var numPreviousPieces = BitUtils.Count1s((bit - 1) & occupancy);
                    index += value * (BitUtils.BitToIndex(bit) - numPreviousPieces);
                }
                occupancy |= bit;
            }
            return (uint)index;
        }
    }

    // TODO: Pawns on both sides, need to specially encode en-passant positions.
    //       Do white/black to move (and capture) separately.
    //       Encode pawn to be captured as 0-7, leftmost pawn to capture it as 0/1 (leftmost in case pawn each side)
    //       Can ignore whenever En-Passant available but capture not possible

    /*  // First sort to put any pawns at the start, only needed for when both sides have pawns
        nonKingPieces.Sort((p1, p2) =>
            {
                var p1IsPawn = IsPawn(p1);
                var p2IsPawn = IsPawn(p2);
                return Convert.ToInt32(p2IsPawn) - Convert.ToInt32(p1IsPawn); // -ve if p1 pawn, hence p1 smaller
            }); 
    */

    public static class BoardIndexing
    {
        public static bool IsPawn(PieceKind piece) => piece == PieceKind.WhitePawn || piece == PieceKind.BlackPawn;
        
        // Maps the white king's position to the appropriate index, 0-5 off diagonal, 0-3 on diagonal
        public static readonly Dictionary<int, int> BoardIndexToSmallTriangle = new Dictionary<int, int>()
        {
            {0, 0}, {9, 1}, {18, 2}, {27, 3}, // on diagonal
            {1, 0}, {2, 1}, {3, 2}, // rank 1 off diagonal
            {10, 3}, {11, 4}, // rank 2 off diagonal
            {19, 5}, // rank 3 off diagonal
        };

        // Maps the black king's position to the appropriate index 0-35 in larger triangle
        // A bit lazy doing things this way (could just subtract based on row), but very straightforward
        public static readonly Dictionary<int, int> BoardIndexToLargeTriangle = new Dictionary<int, int>()
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

    public interface BoardIndexer
    {
        uint MaxIndex { get; }
        uint Index(Board board);
    }
}
