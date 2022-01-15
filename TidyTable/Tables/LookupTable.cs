using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Tables
{
    // Used when playing a game, not during solving tables,
    // so just needs to give a move (in the configuration of the input board) and outcome
    public class LookupTable
    {
        public readonly string Classification;
        public readonly MoveSearcher GetMove;

        public LookupTable(string classification, MoveSearcher getMove)
        {
            Classification = classification;
            GetMove = getMove;
        }
    }
}
