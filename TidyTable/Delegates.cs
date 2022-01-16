using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using TidyTable.TableFormats;

namespace TidyTable
{
    // Looks up a position in an already solved table.
    // Specifying board as an in parameter doesn't actually prevent modifying it's content,
    // but serves as a reminder to copy the board before modifying it.
    public delegate SubTableEntry? OutcomeSearcher(in Board board);
    // As above but used during playing to get the Outcome and move, in terms of the board passed as input.
    // This includes mapping a normalised move back to the original board,
    // and any handling of flipped colour vs underlying table.
    public delegate ProbeTableEntry? MoveSearcher(in Board board);

    // The above method usually requires Normalising then Indexing a position, described here,
    // and these methods are used on the current table being solved:
    // Normalised position => position after move => Normalised again => New index into table

    // Normalises the board passed in, but won't construct a function to reverse mapping of squares/moves.
    public delegate void BoardNormaliser(Board board);
    // Gets the index of a position, assuming an already normalised board.
    public delegate uint IndexGetter(Board board);


    public delegate byte SquareMapper(byte index);
    // Normalises the board passed in, and returns a method to map normalised squares back to normal ones.
    public delegate SquareMapper BoardNormaliserWithMapping(Board board);

    public static class Delegates
    {
        public static void Map(this Move move, SquareMapper mapping)
        {
            move.FromIdx = mapping(move.FromIdx);
            move.ToIdx = mapping(move.ToIdx);
        }
    }
}
