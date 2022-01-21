Helpful in various places to swap the colour of a board/move e.g. to
allow solving for KQK and then using that in KKQ positions, or only
solving one side of symmetric tables.

Move.FlipColour defined in Normalisation, which uses SwapPieceKindColour
defined in the same file to swap the colour of pieces in a move.

SubTable.SwappedColour (same for LookupTable) takes a table for KQK and
returns one for KKQ. Uses FlipColour(board) defined in Chessington NormalForm
to flip a position for the other colour, and Move.FlipColour for mapping moves.