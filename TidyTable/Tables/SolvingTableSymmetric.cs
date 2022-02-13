using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.TableFormats;
using static Chessington.GameEngine.AI.Endgame.NormalForm;

namespace TidyTable.Tables
{
    // When white and black pieces are equal (e.g. KQKQ), only necessary to calculate for one colour,
    // and swap the colour of the board to get results for the other side
    public class SolvingTableSymmetric
    {
        public string Classification;

        // includes kings
        public readonly List<ColourlessPiece> Pieces;
        readonly Dictionary<string, SubTable> SubTables;

        private readonly bool includeEnPassantCases;

        // Each normalised position lists an outcome/move for just one colour, since table symmetric
        public readonly TableEntry?[] Table;
        public readonly uint MaxIndex;

        public readonly IndexGetter GetIndex;
        public readonly BoardNormaliser NormaliseBoard;

        public bool IsInitialised { get; private set; } = false;

        private bool TablesChanging = true;
        private int changes = 0;
        private int iterations = 0;

        public SolvingTableSymmetric(
            List<ColourlessPiece> pieces,
            List<SubTable> subTables,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            Classification = Classifier.ClassifyPieceLists(pieces, pieces);
            Pieces = pieces;
            SubTables = subTables.ToDictionary(table => table.Classification, table => table);
            Table = new TableEntry[maxIndex];
            MaxIndex = maxIndex;
            GetIndex = getIndex;
            NormaliseBoard = normaliseBoard;
            includeEnPassantCases = pieces.Contains(ColourlessPiece.Pawn);
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
                UpdateTable();
                Console.WriteLine($"Iteration {iterations} complete, {changes} changes");
                iterations++;
            }
            FillInDraws();
            IsInitialised = true;

            LogQuickTableVerification();
        }

        private void LogQuickTableVerification()
        {
            var longestNonDraw = Table.MaxBy(entry => entry?.DTZ ?? 0)?.DTZ;
            Console.WriteLine($"Longest mate is {longestNonDraw} ply ({(longestNonDraw + 1) / 2} moves)");
        }

        // Could also pass this in to use normalisation knowledge,
        // but for now just iterate through all boards (will duplicate many positions)
        /* Only assumption is that all normalisation methods put white king on left half of board (limited by games with pawns) */
        // To save time, avoid creating boards where white/black king at adjacent, as these are impossible to reach
        // (Any other positions, will check opponent's king isn't already attacked before adding to that board)
        private void PopulateTable()
        {
            // TODO: Commonise with non-symmetric case

            // handle kings explicitly, since always present. All other pieces => 1 piece of each colour
            var otherPieces = Pieces.Where(piece => piece != ColourlessPiece.King)
                .SelectMany(piece => new List<PieceKind>() { (PieceKind)piece, (PieceKind)(piece + 6) }).ToList();

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

        // TODO: Can commonise with non-symmetric case
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

                if (Table[index] == null && !boardCopy.InCheck(Player.Black))
                {
                    // TODO: Don't need to duplicate boardCopy?
                    Table[index] = new TableEntry(index, boardCopy);
                    if (includeEnPassantCases) AddEnPassantCases(boardCopy);
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
        // Following the same symmetry used elsewhere, only consider when white can capture en-passant
        private void AddEnPassantCases(Board board)
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
                    Table[index] = new TableEntry(index, copy);
                }
            }
        }
        private void UpdateTable()
        {
            Parallel.For(0, Table.Length, tableIndex =>
            {
                TableEntry? entry = Table[tableIndex];
                // When sub-tables present, max DTM in table increases unevenly due to different DTM at positions in sub tables.
                // Hence can't assume a win/loss is the minimum/maximum value until DTM < number of iterations already performed.
                // (Can't check entry.DTM < iterations on it's own, as this is initialsed as -1)
                if (entry == null || entry.Outcome != Outcome.Unknown) return; // continue;

                UpdateEntry(entry);
            });
        }

        private void FillInDraws()
        {
            Parallel.For(0, Table.Length, index =>
            {
                TableEntry? entry = Table[index];
                if (entry == null || entry.Outcome != Outcome.Unknown) return;

                var board = entry.Board;

                var allAvailableMoves = board.GetAllAvailableMoves();


                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, board)));
                var choice = SolvingTable.ChooseRemainingDraws(allEntries);

                entry.Update(choice.Item1, choice.Item2);
                entry.Outcome = Outcome.Draw;
            });
        }

        private void UpdateEntry(TableEntry entry)
        {
            var board = entry.Board;
            var allAvailableMoves = board.GetAllAvailableMoves();

            if (allAvailableMoves.Count == 0) // either checkmate or stalemate
            {
                TablesChanging = true;
                Interlocked.Increment(ref changes);
                entry.DTZ = 0;
                entry.Outcome = board.InCheck(Player.Black) ? Outcome.Lose : Outcome.Draw;
            }
            else
            {
                // Recursively find best outcome based on WhiteTable

                // entries represent possible replacements for current entry, depending on the 'move' played
                // (based on each entry for opponent in the position reached, but same board and flipped outcome)
                var allEntries = allAvailableMoves.Select(move => (move, GetEntryForMove(move, board)));
                (Move, SubTableEntry)? choice = SolvingTable.ChooseEntry(allEntries);
                if (choice != null)
                {
                    (Move, SubTableEntry) chosen = ((Move, SubTableEntry))choice;
                    TablesChanging = true;
                    Interlocked.Increment(ref changes);
                    entry.Update(chosen.Item1, chosen.Item2);
                }
            }
        }

        private SubTableEntry GetEntryForMove(Move move, in Board board)
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
                    return new SubTableEntry(0, Outcome.Draw);
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
                // first flip the colour, to get opponent's outcome from their perspective
                boardCopy = FlipColour(boardCopy);
                NormaliseBoard(boardCopy);
                var opponentEntry = Table[GetIndex(boardCopy)];
                if (opponentEntry == null) throw new Exception("Reached position that should not be possible");
                return opponentEntry.SolvingTableEntry().BeforeMove(move);
            }
        }
    }
}
