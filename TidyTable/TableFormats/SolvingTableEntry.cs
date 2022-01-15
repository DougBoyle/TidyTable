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
    public class SolvingTableEntry
    {
        public readonly long Index;
        public readonly sbyte DTZ;
        public readonly sbyte DTM;
        public readonly Outcome Outcome;
        
        public SolvingTableEntry(sbyte DTZ, sbyte DTM, Outcome outcome)
        {
            this.DTZ = DTZ;
            this.DTM = DTM;
            Outcome = outcome;
        }

        public SolvingTableEntry(long index, sbyte DTZ, sbyte DTM, Outcome outcome)
        {
            Index = index;
            this.DTZ = DTZ;
            this.DTM = DTM;
            Outcome = outcome;
        }
    }
}
