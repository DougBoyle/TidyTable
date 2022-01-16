TODO:
- Reimplement some of the methods currently taken from Chessington
  e.g. Fipping board colour, to no longer involve Square.  
- Move all writing/reading methods to separate files.
- Commonise the handling of LookupTable/SubTable from file -> short[] or compressed.
- Let decompression methods instead return a stream, so no need to pass a byte[]
  as the output when size not even known.