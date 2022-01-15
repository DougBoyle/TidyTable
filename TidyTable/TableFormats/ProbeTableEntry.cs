using Chessington.GameEngine.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.TableFormats
{
    // For lookup, we just want the outcome and the move to play.
    // Assumption is that normalising/computing an index will also handle mapping back to a move on the original board.
    internal class ProbeTableEntry
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
    }
}
