using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Endgames
{
    public static class Normalisation
    {
        public static readonly SquareMapper identity = square => square;

        // Flips along a1-h8 diagonal, to place king in lower triangle of board.
        // Returns a function for reversing this transformation the resulting squares.
        private static SquareMapper TransposeBoard(Board b, SquareMapper previous)
        {
            for (int i = 1; i < 8; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    byte square1 = (byte)((i << 3) + j);
                    byte square2 = (byte)((j << 3) + i);
                    Swap(b, square1, square2);
                }
            }
            return square => previous((byte)(((square & 7) << 3) | ((square >> 3) & 7)));
        }

        private static SquareMapper ReverseAlongRows(Board b, SquareMapper previous)
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    byte square1 = (byte)((row << 3) + col);
                    byte square2 = (byte)(square1 ^ 7);
                    Swap(b, square1, square2);
                }
            }
            return square => previous((byte)(square ^ 7));
        }
        private static SquareMapper ReverseAlongColumns(Board b, SquareMapper previous)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    byte square1 = (byte)((row << 3) + col);
                    byte square2 = (byte)(square1 ^ 56);
                    Swap(b, square1, square2);
                }
            }
            return square => previous((byte)(square ^ 56));
        }

        private static void Swap(Board board, byte square1, byte square2)
        {
            byte tmp = board.GetPieceIndex(square1);
            board.AddPiece(square1, board.GetPieceIndex(square2));
            board.AddPiece(square2, tmp);
        }

        // Swaps both the colour of pieces, and the row to account for pawn direction
        public static Move FlipColour(this Move move)
        {
            return new(
                move.FromIdx ^= 56,
                move.ToIdx ^= 56,
                SwapPieceKindColour(move.MovingPiece),
                SwapPieceKindColour(move.CapturedPiece),
                SwapPieceKindColour(move.PromotionPiece)
            );
        }

        private static byte SwapPieceKindColour(byte piece)
        {
            if (piece == (byte)PieceKind.NoPiece) return piece;
            else return (byte)(piece < 6 ? piece + 6 : piece - 6);
        }
    }
}
