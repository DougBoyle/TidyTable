using Chessington.GameEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Endgames
{
    public static class NormalisationWithMapping
    {
        public static readonly SquareMapper identity = square => square;

        // TODO: TEST THIS
        public static SquareMapper NormaliseNoPawnsBoardWithMapping(Board board)
        {
            var mapper = identity;
            byte king = board.FindKing(Player.White);

            // flip into lower half of board
            if (king > 32)
            {
                mapper = ReverseAlongColumns(board, mapper);
                king = (byte)(king ^ 56);
            }

            // flip into left half of board
            if ((king & 7) >= 4) // Really just testing (king & 4) != 0
            {
                mapper = ReverseAlongRows(board, mapper);
                king = (byte)(king ^ 7);
            }

            // flips along diagonal if row > column
            if (((king >> 3) & 7) > (king & 7))
            {
                mapper = TransposeBoard(board, mapper);
            }
            else if (((king >> 3) & 7) == (king & 7)) // Checks for white king on diagonal
            {
                // TODO: Could keep repeating till any piece not on diagonal, not just kings
                var blackKing = board.FindKing(Player.Black);
                if (((blackKing >> 3) & 7) > (blackKing & 7)) // and black king above it
                {
                    mapper = TransposeBoard(board, mapper);
                }
            }
            return mapper;
        }

        public static SquareMapper NormalisePawnsBoardWithMapping(Board board)
        {
            byte king = board.FindKing(Player.White);
            // flip into left half of board
            return (king & 7) >= 4 ? ReverseAlongRows(board, identity) : identity;
        }
        

        // Flips along a1-h8 diagonal, to place king in lower triangle of board.
        // Returns a function for reversing this transformation the resulting squares.
        private static SquareMapper TransposeBoard(Board b, SquareMapper previous)
        {
            Normalisation.TransposeBoard(b);
            return square => previous((byte)(((square & 7) << 3) | ((square >> 3) & 7)));
        }

        private static SquareMapper ReverseAlongRows(Board b, SquareMapper previous)
        {
            Normalisation.ReverseAlongRows(b);
            return square => previous((byte)(square ^ 7));
        }
        private static SquareMapper ReverseAlongColumns(Board b, SquareMapper previous)
        {
            Normalisation.ReverseAlongColumns(b);
            return square => previous((byte)(square ^ 56));
        }
    }
}
