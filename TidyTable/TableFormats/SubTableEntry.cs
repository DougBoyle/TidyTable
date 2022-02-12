using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.TableFormats
{
    // Once the endgame table has been completed for a given piece combination,
    // the board/move when used as a subtable are irrelevant.
    // (board only used to complete later moves, of which the best move is one)
    // Only used for reading out of, so all fields readonly
    public class SubTableEntry
    {
        public readonly byte DTZ; // is 0 when the best move is a capture or pawn push
        public readonly Outcome Outcome;
        
        public SubTableEntry(byte DTZ, Outcome outcome)
        {
            this.DTZ = DTZ;
            Outcome = outcome;
        }

        public SubTableEntry(TableEntry entry)
        {
            DTZ = entry.DTZ;
            Outcome = entry.Outcome;
        }

        public SubTableEntry BeforeMove(Move move)
        {
            var dtz = (byte)(TableEntryExtensions.ResetsFiftyMoveCounter(move) ? 0 : DTZ + 1);
            var outcome = dtz >= 100 ? Outcome.Draw : TableEntryExtensions.Opposite(Outcome);
            if (outcome == Outcome.Draw) dtz = 0;
            return new SubTableEntry(dtz, outcome);
        }

        // Since 0 <= DTZ <= 99, can represent as just 1 byte.
        // Draw = 0. (0 outcome, 0 DTZ)
        // DTZ = 0 and lose (in checkmate) => byte value 255, otherwise unused
        // DTZ = 0 and win (this is a zeroing move) => (1 outcome, 0 DTZ)
        // DTZ != 0 => 0 outcome is lose, 1 outcome is win
        public byte ToByte()
        {
            if (DTZ > 99) throw new ArgumentOutOfRangeException("Invalid DTZ, should be in range 0-99");
            if (Outcome == Outcome.Draw) return 0;
            if (DTZ == 0 && Outcome == Outcome.Lose) return 255;
            return (byte)((Outcome == Outcome.Win ? 0x80 : 0) + DTZ);
        }

        public static SubTableEntry FromByte(byte value)
        {
            switch (value)
            {
                case 0: return new SubTableEntry(0, Outcome.Draw);
                case 255: return new SubTableEntry(0, Outcome.Lose);
                default:
                    var isWin = (value & 0x80) != 0 ? Outcome.Win : Outcome.Lose;
                    return new SubTableEntry((byte)(value & 0x7F), isWin);
            }
        }
    }
}
