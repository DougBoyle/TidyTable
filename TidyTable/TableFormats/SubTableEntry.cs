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
        public readonly long Index;
        public readonly sbyte DTZ;
        public readonly sbyte DTM;
        public readonly Outcome Outcome;
        
        public SubTableEntry(sbyte DTZ, sbyte DTM, Outcome outcome)
        {
            this.DTZ = DTZ;
            this.DTM = DTM;
            Outcome = outcome;
        }

        public SubTableEntry(long index, sbyte DTZ, sbyte DTM, Outcome outcome)
        {
            Index = index;
            this.DTZ = DTZ;
            this.DTM = DTM;
            Outcome = outcome;
        }

        public SubTableEntry(TableEntry entry)
        {
            Index = entry.Index;
            DTZ = entry.DTZ;
            DTM = entry.DTM;
            Outcome = entry.Outcome;
        }

        public SubTableEntry BeforeMove(Move move)
        {
            var dtm = (sbyte)(DTM + 1);
            var dtz = (sbyte)(TableEntryExtensions.ResetsFiftyMoveCounter(move) ? 0 : DTZ + 1);
            // check for 100 moves happens before possibly resetting (very rare edge case)
            var outcome = DTZ >= 100 ? Outcome.Draw : TableEntryExtensions.Opposite(Outcome);
            return new SubTableEntry(dtz, dtm, outcome);
        }

        public ushort ToShort()
        {
            if (DTZ < 0 || DTM < 0) throw new ArgumentOutOfRangeException("Invalid DTZ/DTM, overflow past 127 detected");
            ushort result = (ushort)Outcome;
            result <<= 7;
            result |= (byte)DTZ;
            result <<= 7;
            result |= (byte)DTM;
            return result;
        }

        public static SubTableEntry FromShort(ushort value)
        {
            sbyte DTM = (sbyte)(value & 0x7F);
            value >>= 7;
            sbyte DTZ = (sbyte)(value & 0x7F);
            value >>= 7;
            Outcome outcome = (Outcome)value;
            return new SubTableEntry(DTZ, DTM, outcome);
        }
    }
}
