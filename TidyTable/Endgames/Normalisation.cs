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
        // TODO: TEST THIS
        public static void NormaliseNoPawnsBoard(Board board)
        {
            byte king = board.FindKing(Player.White);

            // flip into lower half of board
            if (king >= 32)
            {
                ReverseAlongColumns(board);
                king = (byte)(king ^ 56);
            }

            // flip into left half of board
            if ((king & 7) >= 4) // Really just testing (king & 4) != 0
            {
                ReverseAlongRows(board);
                king = (byte)(king ^ 7);
            }

            // flips along diagonal if row > column
            if (((king >> 3) & 7) > (king & 7))
            {
                TransposeBoard(board);
            }
            else if (((king >> 3) & 7) == (king & 7)) // Checks for white king on diagonal
            {
                // TODO: Could keep repeating till any piece not on diagonal, not just kings
                var blackKing = board.FindKing(Player.Black);
                if (((blackKing >> 3) & 7) > (blackKing & 7)) // and black king above it
                {
                    TransposeBoard(board);
                }
            }
        }

        public static void NormalisePawnsBoard(Board board)
        {
            byte king = board.FindKing(Player.White);
            // flip into left half of board
            if ((king & 7) >= 4)
            {
                ReverseAlongRows(board);
                // For pawn boards, potentially also need to chaange the en-passant index
                if (board.EnPassantIndex != Board.NO_SQUARE) board.EnPassantIndex ^= 7;
            }
        }


        // Flips along a1-h8 diagonal, to place king in lower triangle of board.
        // Returns a function for reversing this transformation the resulting squares.
        public static void TransposeBoard(Board b)
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
        }

        public static void ReverseAlongRows(Board b)
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
        }
        public static void ReverseAlongColumns(Board b)
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
        }

        // Board only expects AddPiece when piece is not NoPiece and nothing there already
        private static void Swap(Board board, byte square1, byte square2)
        {
            byte piece1 = board.GetPieceIndex(square1);
            byte piece2 = board.GetPieceIndex(square2);
            if (piece2 != (byte)PieceKind.NoPiece) board.RemovePiece(piece2, square2, 1UL << square2);
            if (piece1 != (byte)PieceKind.NoPiece)
            {
                board.RemovePiece(piece1, square1, 1UL << square1);
                board.AddPiece(square2, piece1);
            }
            if (piece2 != (byte)PieceKind.NoPiece)
            {
                board.AddPiece(square1, piece2);
            }
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

        public static byte SwapPieceKindColour(byte piece)
        {
            if (piece == (byte)PieceKind.NoPiece) return piece;
            else return (byte)(piece < 6 ? piece + 6 : piece - 6);
        }
    }
}
