using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.TableFormats
{
    // For lookup, we just want the outcome and the move to play.
    // Assumption is that normalising/computing an index will also handle mapping back to a move on the original board.
    public class ProbeTableEntry
    {
        public readonly long Index;
        public readonly Outcome Outcome;
        public readonly Move? Move;

        public ProbeTableEntry(Outcome outcome, Move? move)
        {
            Outcome = outcome;
            Move = move;
        }

        public ProbeTableEntry(long index, Outcome outcome, Move? move)
        {
            Index = index;
            Outcome = outcome;
            Move = move;
        }


        // TODO: For certain games, it may be more efficient ot encode the move just by
        //       the index of the move in GetAllAvailableMoves
        // Includes DTM/DTZ for use when generating other tables
        // [ 2 bit promotion | 6 bit from | 6 bit to | 2 bit outcome ]
        // When a move is a promotion (must determine from the board): 0 = N, 1 = B, 2 = R, 3 = Q
        private ushort ToShort(byte colouredKnight)
        {
            ushort result = 0;
            if (Move != null)
            {
                if (Move.PromotionPiece != (byte)PieceKind.NoPiece)
                {
                    result = (ushort)(Move.PromotionPiece - colouredKnight);
                    result <<= 6;
                }
                result |= Move.FromIdx;
                result <<= 6;
                result |= Move.ToIdx;
                result <<= 2;
            }
            result |= (ushort)Outcome;
            return result;
        }

        // The normalised board passed in is necessary to determine the piece and captures/promotions.
        // It must be normalised (+ colour), as the values in the short are assumed to be for the index of that normalised position.
        public static ProbeTableEntry? FromShort(ushort value, Board normalisedBoard)
        {
            if (value == 0) return null;
            var outcome = (Outcome)(value & 3);
            value >>= 2;

            if (value == 0) return new ProbeTableEntry(outcome, null);
            byte to = (byte)(value & 0x3f);
            value >>= 6;
            byte from = (byte)(value & 0x3f);
            value >>= 6;
            byte promotion = (byte)value;

            byte movingPiece = normalisedBoard.GetPieceIndex(from);
            if (movingPiece == (byte)PieceKind.WhitePawn && to >= 56)
            {
                promotion += (byte)PieceKind.WhiteKnight;
            }
            else if (movingPiece == (byte)PieceKind.BlackPawn && to < 8)
            {
                promotion += (byte)PieceKind.BlackKnight;
            }
            else
            {
                promotion = (byte)PieceKind.NoPiece;
            }

            return new ProbeTableEntry(outcome, new Move(normalisedBoard, from, to, promotion));
        }
    }
}
