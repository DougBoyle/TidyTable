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
        // includes kings
        public readonly List<ColourlessPiece> WhitePieces;
        public readonly List<ColourlessPiece> BlackPieces;
        readonly Dictionary<string, SubTable> SubTables;

        // Each normalised position lists an outcome/move for both black/white
        public readonly TableEntry?[] WhiteTable;
        public readonly TableEntry?[] BlackTable;

        public readonly IndexGetter GetIndex;
        public readonly BoardNormaliser NormaliseBoard;

        public bool IsInitialised { get; private set; } = false;

        private bool TablesChanging = true;
        private int changes = 0;

        public SolvingTable(
            List<ColourlessPiece> whitePieces,
            List<ColourlessPiece> blackPieces,
            List<SubTable> subTables,
            int maxIndex, // maximum given normalisation
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            WhitePieces = whitePieces;
            BlackPieces = blackPieces;
            SubTables = subTables.Concat(subTables.Select(table => table.SwappedColour()))
                .ToDictionary(table => table.Classification, table => table);
            WhiteTable = new TableEntry[maxIndex];
            BlackTable = new TableEntry[maxIndex];
            GetIndex = getIndex;
            NormaliseBoard = normaliseBoard;
        }

        public void SolveForPieces()
        {
            // initialise a table of all positions
            PopulateTable();

            // until tables stop changing, iterate over all entries to solve by backtracking from checkmates/draws
            while (TablesChanging)
            {
                TablesChanging = false;
                changes = 0;
                UpdateBlackTable();
                UpdateWhiteTable();
                Console.WriteLine($"Iteration complete, {changes} changes");
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
            // TODO: When 1+ pawn on each side, will also need to handle en-passant

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
                }
                if (BlackTable[index] == null && !boardCopy.InCheck(Player.White))
                {
                    var blackBoard = new Board(boardCopy);
                    blackBoard.CurrentPlayer = Player.Black;
                    BlackTable[index] = new TableEntry(index, blackBoard);
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

        private void UpdateBlackTable()
        {
            for (int tableIndex = 0; tableIndex < BlackTable.Length; tableIndex++)
            {
                TableEntry? entry = BlackTable[tableIndex];
                if (entry == null || entry.Outcome != Outcome.Unknown) continue;

                UpdateEntry(tableIndex, entry, BlackTable, WhiteTable);
            }
        }

        private void UpdateWhiteTable()
        {
            for (int tableIndex = 0; tableIndex < WhiteTable.Length; tableIndex++)
            {
                TableEntry? entry = WhiteTable[tableIndex];
                if (entry == null || entry.Outcome != Outcome.Unknown) continue;

                UpdateEntry(tableIndex, entry, WhiteTable, BlackTable);
            }
        }

        private void FillInDraws(TableEntry?[] myTable, TableEntry?[] theirTable)
        {
            for (int index = 0; index < myTable.Length; index++)
            {
                TableEntry? entry = myTable[index];
                if (entry == null || entry.Outcome != Outcome.Unknown) continue;

                var board = entry.Board;

                var allAvailableMoves = board.GetAllAvailableMoves();


                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, entry, board, theirTable)));
                var choice = ChooseRemainingDraws(allEntries);

                entry.Update(choice.Item1, choice.Item2);
                entry.Outcome = Outcome.Draw;
            }
        }

        private void UpdateEntry(int tableIndex, TableEntry entry, TableEntry?[] myTable, TableEntry?[] theirTable)
        {
            var board = entry.Board;
            var allAvailableMoves = board.GetAllAvailableMoves();

            if (allAvailableMoves.Count == 0) // either checkmate or stalemate
            {
                TablesChanging = true;
                changes++;
                entry.DTM = 0;
                entry.DTZ = 0;
                entry.Outcome = board.InCheck(Player.Black) ? Outcome.Lose : Outcome.Draw;
            }
            else
            {
                // Recursively find best outcome based on WhiteTable

                // entries represent possible replacements for current entry, depending on the 'move' played
                // (based on each entry for opponent in the position reached, but same board and flipped outcome)
                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, entry, board, theirTable)));
                (Move, SubTableEntry)? choice = ChooseEntry(allEntries);
                if (choice != null)
                {
                    (Move, SubTableEntry) chosen = ((Move, SubTableEntry))choice;
                    TablesChanging = true;
                    changes++;
                    entry.Update(chosen.Item1, chosen.Item2);
                }
            }
        }

        private SubTableEntry GetEntryForMove(Move move, TableEntry currentEntry, in Board board, TableEntry?[] otherTable)
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

        private static (Move, SubTableEntry)? ChooseEntry(IEnumerable<(Move, SubTableEntry)> entries)
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

        private static (Move, SubTableEntry) ChooseRemainingDraws(IEnumerable<(Move, SubTableEntry)> entries)
        {
            return entries
                .Where(entry => entry.Item2.Outcome == Outcome.Draw || entry.Item2.Outcome == Outcome.Unknown)
                .First();
        }
    }
}
