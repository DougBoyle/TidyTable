There are three mostly indepedent concerns in constructing efficient tablebases:

1. **Number of entries**:  
    All valid positions need to be mapped to as few distinct indicies as possible, by exploiting
    symmetry in positions. 
    * We naively get 64 x 63 x 62 ... indicies, 
      although this may be treated as 64 x 64 x ... to simplify computing indices.
    * For pawnless boards, restricting the white king to the a1-d1-d4 triangle reduces this
      to a factor of 10 rather than 64. Furthermore, for the 4 out of 10 squares on the diagonal,
      the black king can be restricted to the a1-h1-h8 triangle for a further 20% reduction.
    * For boards with pawns, we only have horizontal symmetry so this is reduced to 32 x ... by
      enforcing the white king be on the left half of the board, but each pawn only contributes 48
      rather than 64 as it cannot be on the first or last file of the board.
    * **TODO:** For boards with 1+ pawn on each side, En Passant also needs to be stored. As this is 
      only in specific positions, it is best indexed separately. 
2. **Encoding of entries as bits/bytes**:  
    The information for each entry must be stored as efficiently as possible. The information required
    differs for computing the tables vs using them, as computing them requires knowing Outcome + DTM/DTZ,
    but using them only requires being able to get the best move, and ideally also the outcome.  
    * This is affected by the metric used. DTZ is optimal but sometimes a slower mate, DTM is the fastest
      mate but may be a draw under the 50 move rule. The compromise chosen is minimising DTM but being aware
      of the DTZ, so checkmates that are always too long are ignored while accepting that some apparent checkmates
      will actually be draws based on this table, as mates longer than 50 moves are relatively rare. A full
      approach is to divide each position into 100 positions for each DTM, grouped into intervals based on the
      choice of move (e.g. captures/pawn moves are always a single interval/choice). This is exact but slow.
    * The encoding of information may also be table specific. For example, the longest mate in KQK is 19 ply,
      so DTM never needs storing with more than 5 bits, but this information is only available after computing 
      the solution. More extreme, white in KQK only ever has at most 27 + 8 = 35 possible moves and black has 
      at most 8, so the best move can be stored with just 6 and 3 bits respectively as long as the same move
      generation is used and gives a deterministic order, even when colours are reversed (for KKQ).
3. **Compression of entries into file**:  
    Lastly is how all of these entries are packed into a file.  
    * Any of a range of compression methods can be used, ideally picking one which performs best for the actual
      data to store and can still be read out relatively easily during a game (without decoding the entire table).
    * A related concern is whether all indices are stored, including null entries for impossible positions, which
      save space by indices becoming implicit and null entries being small if run length encoded. The opposite
      extreme is storing only indices for valid positions. A potential compromise is storing blocks, which helps
      with only having to decode part of the table at a time, and storing just the start index of a block to allow
      skipping over null entries where the block would otherwise start.