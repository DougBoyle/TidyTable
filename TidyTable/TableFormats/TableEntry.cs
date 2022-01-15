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
        public long Index;
        public Board? Board;
        // Always >= 0, so signed byte actually has range 0 - 127 here, limited to this to allow fitting in 7 bits
        // depth to zero of 50-move counter (actually 100 to allow both players moving)
        // DTZ = 0 on a capture/pawn move, game is a draw if >= 100
        public sbyte DTZ = -1; 
        public sbyte DTM = -1; // depth to mate or draw, in ply (DTM in moves is ((ply + 1) / 2))
        public Outcome Outcome = Outcome.Unknown; // for Board.CurrentPlayer (assuming it is not null)
        public Move? Move = null;
    }

    public enum Outcome { Draw = 0, Win = 1, Lose = 2, Unknown = 3 }

    public static class TableEntryExtensions
    {
        public static Outcome Opposite(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Win: return Outcome.Lose;
                case Outcome.Lose: return Outcome.Win;
                default: return outcome;
            }
        }

        public static bool ResetsFiftyMoveCounter(Move move)
        {
            return move.CapturedPiece != (byte)PieceKind.NoPiece 
                || move.MovingPiece == (byte)PieceKind.WhitePawn
                || move.MovingPiece == (byte)PieceKind.BlackPawn;
        }
    }
}
