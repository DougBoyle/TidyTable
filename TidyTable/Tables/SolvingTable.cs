using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.TableFormats;

namespace TidyTable.Tables
{
    // Used for actually solving an endgame
    public class SolvingTable
    {
        public string Classification;

        // includes kings
        public readonly List<ColourlessPiece> WhitePieces;
        public readonly List<ColourlessPiece> BlackPieces;
        readonly Dictionary<string, SubTable> SubTables;

        private readonly bool includeEnPassantCases;

        // Each normalised position lists an outcome/move for both black/white
        public readonly TableEntry?[] WhiteTable;
        public readonly TableEntry?[] BlackTable;
        public readonly uint MaxIndex;

        public readonly IndexGetter GetIndex;
        public readonly BoardNormaliser NormaliseBoard;

        public bool IsInitialised { get; private set; } = false;

        private bool TablesChanging = true;
        private int changes = 0;
        private int iterations = 0;

        public SolvingTable(
            List<ColourlessPiece> whitePieces,
            List<ColourlessPiece> blackPieces,
            List<SubTable> subTables,
            uint maxIndex, // maximum given normalisation
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            Classification = Classifier.ClassifyPieceLists(whitePieces, blackPieces);
            WhitePieces = whitePieces;
            BlackPieces = blackPieces;
            SubTables = subTables.ToDictionary(table => table.Classification, table => table);
            WhiteTable = new TableEntry[maxIndex];
            BlackTable = new TableEntry[maxIndex];
            MaxIndex = maxIndex;
            GetIndex = getIndex;
            NormaliseBoard = normaliseBoard;
            includeEnPassantCases = whitePieces.Contains(ColourlessPiece.Pawn) && blackPieces.Contains(ColourlessPiece.Pawn);
        }

        public void SolveForPieces()
        {
            // initialise a table of all positions
            PopulateTable();
            iterations = 0;

            // until tables stop changing, iterate over all entries to solve by backtracking from checkmates/draws
            while (TablesChanging)
            {
                TablesChanging = false;
                changes = 0;
                UpdateBlackTable();
                UpdateWhiteTable();
                Console.WriteLine($"Iteration {iterations} complete, {changes} changes");
                iterations++;
            }
            FillInDraws(WhiteTable, BlackTable);
            FillInDraws(BlackTable, WhiteTable);
            IsInitialised = true;

            LogQuickTableVerification();
        }

        private void LogQuickTableVerification()
        {
            var blackCanDraw = BlackTable.Any(entry => entry != null && entry.Outcome == Outcome.Draw);
            var longestMate = WhiteTable.MaxBy(entry => entry?.DTM ?? 0)?.DTM;
            Console.WriteLine($"Draw exists for black: {blackCanDraw}");
            Console.WriteLine($"Longest mate for white is {longestMate} ply ({(longestMate + 1) / 2} moves)");
        }

        // Could also pass this in to use normalisation knowledge,
        // but for now just iterate through all boards (will duplicate many positions)
        /* Only assumption is that all normalisation methods put white king on left half of board (limited by games with pawns) */
        // To save time, avoid creating boards where white/black king at adjacent, as these are impossible to reach
        // (Any other positions, will check opponent's king isn't already attacked before adding to that board)
        private void PopulateTable()
        {
            // handle kings explicitly, since always present
            var otherPieces = WhitePieces.Where(piece => piece != ColourlessPiece.King).Select(piece => (PieceKind)piece)
                .Concat(BlackPieces.Where(piece => piece != ColourlessPiece.King).Select(piece => (PieceKind)(piece + 6))).ToList();

            for (int wk = 0; wk < 32; wk++)
            {
                var whiteKingSquare = (wk % 4) + 8 * (wk / 4);
                for (int blackKingSquare = 0; blackKingSquare < 64; blackKingSquare++)
                {
                    if (blackKingSquare / 8 - whiteKingSquare / 8 <= 1 && whiteKingSquare / 8 - blackKingSquare / 8 <= 1
                        && (blackKingSquare % 8) - (whiteKingSquare % 8) <= 1 && (whiteKingSquare % 8) - (blackKingSquare % 8) <= 1) continue;
                    Board board = new();
                    board.AddPiece(whiteKingSquare, (byte)PieceKind.WhiteKing);
                    board.AddPiece(blackKingSquare, (byte)PieceKind.BlackKing);
                    board.Castling = 0;
                    AddPieces(board, otherPieces);
                }
            }
        }

        private void AddPieces(Board board, List<PieceKind> pieces)
        {
            // Add to table if not already present - no need to normalise, taking any example is sufficient
            // (but may be slightly faster if all stored positions are normalised?)
            if (pieces.Count == 0)
            {
                var boardCopy = new Board(board);
                // Normalise board before computing index - TODO: Should GetIndex do nomalising too?
                NormaliseBoard(boardCopy);
                var index = GetIndex(boardCopy);

                if (WhiteTable[index] == null && !boardCopy.InCheck(Player.Black))
                {
                    WhiteTable[index] = new TableEntry(index, new Board(boardCopy));
                    if (includeEnPassantCases) AddWhiteEnPassantCases(boardCopy);
                }
                if (BlackTable[index] == null && !boardCopy.InCheck(Player.White))
                {
                    var blackBoard = new Board(boardCopy);
                    blackBoard.CurrentPlayer = Player.Black;
                    BlackTable[index] = new TableEntry(index, blackBoard);
                    if (includeEnPassantCases) AddBlackEnPassantCases(blackBoard);
                }
            }
            else
            {
                // list is shared so a piece removed needs to be added back after, same for modifying the board
                var piece = pieces[0];
                pieces.RemoveAt(0);
                // Pawns can't be placed on the ends of the board
                var isPawn = piece == PieceKind.WhitePawn || piece == PieceKind.BlackPawn;
                byte start = (byte)(isPawn ? 8 : 0);
                byte end = (byte)(isPawn ? 56 : 64);
                for (byte square = start; square < end; square++)
                {
                    if (board.GetPieceIndex(square) == (byte)PieceKind.NoPiece)
                    {
                        board.AddPiece(square, (byte)piece);
                        AddPieces(board, pieces);
                        board.RemovePiece((byte)piece, square, 1UL << square);
                    }
                }
                pieces.Insert(0, piece);
            }
        }

        // Only needs to set the en-passant square/create a table entry when an en-passant capture could be possible
        private void AddWhiteEnPassantCases(Board board)
        {
            for (int col = 0; col < 8; col++)
            {
                byte epSqure = (byte)(40 + col);
                // avoid computing indices when not actually an en-passant position
                if (board.PieceBoard[epSqure - 8] == (byte)PieceKind.BlackPawn
                    && board.PieceBoard[epSqure] == (byte)PieceKind.NoPiece
                    && board.PieceBoard[epSqure + 8] == (byte)PieceKind.NoPiece
                    && ((col > 0 && board.PieceBoard[epSqure - 9] == (byte)PieceKind.WhitePawn)
                        || (col < 7 && board.PieceBoard[epSqure - 7] == (byte)PieceKind.WhitePawn))
                )
                {
                    var copy = new Board(board)
                    {
                        EnPassantIndex = epSqure
                    };
                    var index = GetIndex(copy);
                    WhiteTable[index] = new TableEntry(index, copy);
                }
            }
        }

        private void AddBlackEnPassantCases(Board board)
        {
            for (int col = 0; col < 8; col++)
            {
                byte epSqure = (byte)(16 + col);
                // avoid computing indices when not actually an en-passant position
                if (board.PieceBoard[epSqure + 8] == (byte)PieceKind.WhitePawn
                    && board.PieceBoard[epSqure] == (byte)PieceKind.NoPiece
                    && board.PieceBoard[epSqure - 8] == (byte)PieceKind.NoPiece
                    && ((col > 0 && board.PieceBoard[epSqure + 7] == (byte)PieceKind.BlackPawn)
                        || (col < 7 && board.PieceBoard[epSqure + 9] == (byte)PieceKind.BlackPawn))
                )
                {
                    var copy = new Board(board)
                    {
                        EnPassantIndex = epSqure
                    };
                    var index = GetIndex(copy);
                    BlackTable[index] = new TableEntry(index, copy);
                }
            }
        }

        private void UpdateBlackTable()
        {
            // TODO: Parallel for, ensuring shared writes are atomic (e.g. num changes/TablesChanging)
            Parallel.For(0, BlackTable.Length, tableIndex =>
            //     for (int tableIndex = 0; tableIndex < BlackTable.Length; tableIndex++)
            {
                TableEntry? entry = BlackTable[tableIndex];
                // When sub-tables present, max DTM in table increases unevenly due to different DTM at positions in sub tables.
                // Hence can't assume a win/loss is the minimum/maximum value until DTM < number of iterations already performed.
                // (Can't check entry.DTM < iterations on it's own, as this is initialsed as -1)
                if (entry == null || (entry.Outcome != Outcome.Unknown && entry.DTM < iterations)) return; // continue;

                UpdateEntry(tableIndex, entry, BlackTable, WhiteTable);
            });
        }

        private void UpdateWhiteTable()
        {
            // TODO: Parallel for, ensuring shared writes are atomic (e.g. num changes/TablesChanging)
            Parallel.For(0, WhiteTable.Length, tableIndex =>
            //   for (int tableIndex = 0; tableIndex < WhiteTable.Length; tableIndex++)
            {
                TableEntry? entry = WhiteTable[tableIndex];
                // When sub-tables present, max DTM in table increases unevenly due to different DTM at positions in sub tables.
                // Hence can't assume a win/loss is the minimum/maximum value until DTM < number of iterations already performed.
                // (Can't check entry.DTM < iterations on it's own, as this is initialsed as -1)
                if (entry == null || (entry.Outcome != Outcome.Unknown && entry.DTM < iterations)) return; // continue;

                UpdateEntry(tableIndex, entry, WhiteTable, BlackTable);
            });
        }

        private void FillInDraws(TableEntry?[] myTable, TableEntry?[] theirTable)
        {
            // TODO: Parallel for, ensuring shared writes are atomic
            Parallel.For(0, myTable.Length, index =>
            //   for (int index = 0; index < myTable.Length; index++)
            {
                TableEntry? entry = myTable[index];
                if (entry == null || entry.Outcome != Outcome.Unknown) return; // continue;

                var board = entry.Board;

                var allAvailableMoves = board.GetAllAvailableMoves();


                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, board, theirTable)));
                var choice = ChooseRemainingDraws(allEntries);

                entry.Update(choice.Item1, choice.Item2);
                entry.Outcome = Outcome.Draw;
            });
        }

        private void UpdateEntry(int tableIndex, TableEntry entry, TableEntry?[] myTable, TableEntry?[] theirTable)
        {
            var board = entry.Board;
            var allAvailableMoves = board.GetAllAvailableMoves();

            if (allAvailableMoves.Count == 0) // either checkmate or stalemate
            {
                TablesChanging = true;
                Interlocked.Increment(ref changes);
            //    changes++;
                entry.DTM = 0;
                entry.DTZ = 0;
                entry.Outcome = board.InCheck(Player.Black) ? Outcome.Lose : Outcome.Draw;
            }
            else
            {
                // Recursively find best outcome based on WhiteTable

                // entries represent possible replacements for current entry, depending on the 'move' played
                // (based on each entry for opponent in the position reached, but same board and flipped outcome)
                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, board, theirTable)));
                (Move, SubTableEntry)? choice = ChooseEntry(allEntries);
                // Results sometimes need overwriting with a lower DTM, but when an entry is already known
                // and the entry found doesn't lower the DTM, don't wrongly signal TablesChanging
                if (choice != null && (entry.Outcome == Outcome.Unknown || choice?.Item2.DTM < entry.DTM))
                {
                    (Move, SubTableEntry) chosen = ((Move, SubTableEntry))choice;
                    TablesChanging = true;
                    // changes++;
                    Interlocked.Increment(ref changes);
                    entry.Update(chosen.Item1, chosen.Item2);
                }
            }
        }

        private SubTableEntry GetEntryForMove(Move move, in Board board, TableEntry?[] otherTable)
        {
            // TODO: Board copied in each case, so GetEntry/GetIndex are allowed to manipulate the board they receive
            var boardCopy = new Board(board);
            boardCopy.MakeMoveWithoutRecording(move);

            // Simplifies to other table
            // (TODO: Consider En-passant if 1+ pawn either side)
            if (move.CapturedPiece != (byte)PieceKind.NoPiece || move.PromotionPiece != (byte)PieceKind.NoPiece)
            {
                // Check for insufficient material -> immediate draw
                if (boardCopy.IsInsufficientMaterial())
                {
                    return new SubTableEntry(1, 1, Outcome.Draw);
                }
                else
                {
                    var classification = Classifier.Classify(boardCopy);
                    var opponentEntry = SubTables[classification].GetOutcome(boardCopy);
                    if (opponentEntry == null)
                        throw new Exception($"Reached position that should not be possible, subBoard {classification}");
                    return opponentEntry.BeforeMove(move);
                }
            }
            else
            {
                // table was initialised with all valid positions, so should not be null
                NormaliseBoard(boardCopy); // TODO: Should GetIndex assume board is normalised or not?
                var opponentEntry = otherTable[GetIndex(boardCopy)];
                if (opponentEntry == null) throw new Exception("Reached position that should not be possible");
                return opponentEntry.SolvingTableEntry().BeforeMove(move);
            }
        }

        public static (Move, SubTableEntry)? ChooseEntry(IEnumerable<(Move, SubTableEntry)> entries)
        {
            // 1. Any win (without 50 move counter hit) => win, pick smallest DTM
            if (entries.Any(entry => entry.Item2.Outcome == Outcome.Win))
            {
                return entries
                    .Where(entry => entry.Item2.Outcome == Outcome.Win)
                    .MinBy(entry => entry.Item2.DTM)!;
            }
            // 2. Any unknown (and no win) => unknown, must wait, no change
            else if (entries.Any(entry => entry.Item2.Outcome == Outcome.Unknown)) return null;
            // 3. Any draw (and no win) => drawing, pick draw with smallest DTM
            else if (entries.Any(entry => entry.Item2.Outcome == Outcome.Draw))
            {
                return entries
                    .Where(entry => entry.Item2.Outcome == Outcome.Draw)
                    .MinBy(entry => entry.Item2.DTM)!;
            }
            // 4. All losing => losing, pick largest DTM.
            // *** technically, should try to set up stalemate traps, but can't detect with this method
            else return entries.MaxBy(entry => entry.Item2.DTM);
        }

        public static (Move, SubTableEntry) ChooseRemainingDraws(IEnumerable<(Move, SubTableEntry)> entries)
        {
            return entries
                .Where(entry => entry.Item2.Outcome == Outcome.Draw || entry.Item2.Outcome == Outcome.Unknown)
                .First();
        }
    }
}
