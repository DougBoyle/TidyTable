using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Endgames
{
    public static class BoardIndexing
    {
        // No pawns vs Pawns on one side vs 1+ Pawns each side (en-passant) cases to each encode separately
        // Can use triangle symmetry in king case
        // - Can continue symmetry until first piece placed off of main diagonal
        // - Even if hard to track possibilities in determining index,
        //     can make some indicies impossble (always mirrored) hence more 0 bytes in output.
        // - If easy to index/reduces max - can subtract bitcount of previous pieces in total occupancy bitboard
        //     so that number of possibilities goes 64 * 63 * 62 * ...
        // - Can reduce pawn indices to 48 not 64, as pawns can't go on either end ranks unless considering en-passant
    }
}
