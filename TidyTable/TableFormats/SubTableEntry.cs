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
        public readonly byte DTZ;
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

        public ushort ToShort() // Represents as just 9 bits
        {
            if (DTZ > 99) throw new ArgumentOutOfRangeException("Invalid DTZ, should be in range 0-99");
            ushort result = (ushort)Outcome;
            result <<= 7;
            result |= (byte)DTZ;
            return result;
        }

        public static SubTableEntry FromShort(ushort value)
        {
            byte DTZ = (byte)(value & 0x7F);
            value >>= 7;
            Outcome outcome = (Outcome)value;
            return new SubTableEntry(DTZ, outcome);
        }
    }
}
