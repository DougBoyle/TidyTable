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
    // This class stores all the fields we could be interested in, 
    // so that other classes and schemes for mapping them to bytes can be based on this.
    // Entry includes it's index, to leave it up to other stages to keep all entries or just populated ones.
    public class TableEntry
    {
        // Index is assumed to be 32 bits always, will be stored as such
        public readonly uint Index;
        public readonly Board Board;
        // 0 if draw (including 50 move counter), so valid range is only 0-99
        // DTZ = 0 on a capture/pawn move
        public byte DTZ = 255;
        public Outcome Outcome = Outcome.Unknown; // for Board.CurrentPlayer (assuming it is not null)
        public Move? Move = null;

        public TableEntry(uint index, Board board)
        {
            Index = index;
            Board = board;
        }


        public void Update(Move move, SubTableEntry entry)
        {
            Move = move;
            DTZ = entry.DTZ;
            Outcome = entry.Outcome;
        }

        public SubTableEntry SolvingTableEntry()
        {
            return new(DTZ, Outcome);
        }
    }

    public enum Outcome { Draw = 0, Win = 1, Lose = 2, Unknown = 3 }

    public static class TableEntryExtensions
    {
        public static Outcome Opposite(Outcome outcome)
        {
            return outcome switch
            {
                Outcome.Win => Outcome.Lose,
                Outcome.Lose => Outcome.Win,
                _ => outcome,
            };
        }

        public static bool ResetsFiftyMoveCounter(Move move)
        {
            return move.CapturedPiece != (byte)PieceKind.NoPiece 
                || move.MovingPiece == (byte)PieceKind.WhitePawn
                || move.MovingPiece == (byte)PieceKind.BlackPawn;
        }
    }
}
