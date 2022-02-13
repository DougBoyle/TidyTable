using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.TableFormats;
using TidyTable.Endgames;
using static Chessington.GameEngine.AI.Endgame.NormalForm;
using TidyTable.Compression;

namespace TidyTable.Tables
{
    public class DTZTable
    {
        public readonly string Classification;

        private readonly BoardNormaliser normalise;
        private readonly IndexGetter getIndex;
        private readonly WLDTable WLDTable;
        private readonly Dictionary<string, DTZTable> allTables;
        private readonly byte[] Data;

        private readonly int maxBits = 0;
        private readonly bool symmetric = false;

        public DTZTable(SolvingTable table, Dictionary<string, DTZTable> allTables)
        {
            this.allTables = allTables;

            Classification = table.Classification;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;

            WLDTable = new WLDTable(table);
            Data = new byte[table.MaxIndex]; // only stores values for white
            for (int i = 0; i < table.WhiteTable.Length; i++)
            {
                var dtz = table.WhiteTable[i]?.DTZ ?? 0;
                maxBits = Math.Max(dtz, maxBits);
                Data[i] = dtz;
            }
            maxBits = (int)Math.Floor(Math.Log2(maxBits)) + 1;

            AddSelfToAllTables();
        }

        public DTZTable(SolvingTableSymmetric table, Dictionary<string, DTZTable> allTables)
        {
            symmetric = true;
            this.allTables = allTables;

            Classification = table.Classification;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;

            WLDTable = new WLDTable(table);
            Data = new byte[table.MaxIndex]; // only stores values for white, but lookup faster when symmetric
            for (int i = 0; i < table.Table.Length; i++)
            {
                var dtz = table.Table[i]?.DTZ ?? 0;
                maxBits = Math.Max(dtz, maxBits);
                Data[i] = dtz;
            }
            maxBits = (int)Math.Floor(Math.Log2(maxBits)) + 1;

            AddSelfToAllTables();
        }

        public Outcome GetOutcome(in Board board) => WLDTable.GetOutcome(board);

        public virtual int DTZ(in Board board)
        {
            var copy = new Board(board);
            if (symmetric)
            {
                if (board.CurrentPlayer == Player.Black) copy = FlipColour(copy);
                normalise(copy);
                return Data[getIndex(copy)];
            }

            if (board.CurrentPlayer == Player.White)
            {
                normalise(copy);
                return Data[getIndex(copy)];
            } else
            {
                // DTZ values not stored for black
                // For a draw, return 0
                // For a win, return 0 if no moves else minimum of DTZ for white after next move + 1
                // For a loss, return 0 if no moves else maximum of DTZ for white after next move + 1
                var outcome = WLDTable.GetOutcome(board);
                if (outcome == Outcome.Draw) return 0;

                var availableMoves = board.GetAllAvailableMoves();
                if (availableMoves.Count == 0) return 0;

                var gameInfo = new GameExtraInfo(board);
                if (outcome == Outcome.Win)
                {
                    return availableMoves.Select(move => DTZForMove(copy, gameInfo, move, requiredOutcome: Outcome.Lose)).Min();
                } else
                {
                    return availableMoves.Select(move => DTZForMove(copy, gameInfo, move, requiredOutcome: null)).Max();
                }
            }
        }

        // DTZ potentially requires a 1-ply search, and this therefore requires a 1 to 2-ply search
        // Applies the same trick as above of min/maximising the DTZ, doing a self-call for the lookup
        // TODO: Can optimise by flipping to black (and flipping move back) if symmetric
        public virtual Move? GetMove(in Board board)
        {
            // Not actually necessary
            var copy = new Board(board);
            // Will rely on MakeMoveWithoutRecording/UndoMove move not modifying board
            var moves = copy.GetAllAvailableMoves();
            if (moves.Count == 0) return null;

            var gameInfo = new GameExtraInfo(copy);
            // also relies on GetOutcome not modifying board
            switch (WLDTable.GetOutcome(copy))
            {
                case Outcome.Win:
                    return moves.MinBy(move => DTZForMove(copy, gameInfo, move, requiredOutcome: Outcome.Lose));
                case Outcome.Lose:
                    return moves.MaxBy(move => DTZForMove(copy, gameInfo, move, requiredOutcome: null));
                default:
                    // DTZForMove always adds 1, and draws have DTZ = 0, so look for first with DTZForMove 1
                    // Note: Drawn position, so no move leads to an immediate checkmate (other case where DTZ = 0),
                    //      and cannot checkmate self on own turn, so any loses would take at least 2 ply => DTZ > 1
                    return moves.First(move => DTZForMove(copy, gameInfo, move, requiredOutcome: null) == 1);
            }
        }

        // requiredOutcome: For a win, must only consider subsequent positions losing for opponent
        private int DTZForMove(Board board, GameExtraInfo gameInfo, Move move, Outcome? requiredOutcome)
        {
            // Relies on this method not modifying (normalising) the object passed to it
            board.MakeMoveWithoutRecording(move);

            var table = SelectTable(move, board);
            int dtz;
            if (requiredOutcome != null && ((table?.GetOutcome(board) ?? Outcome.Draw) != requiredOutcome)) dtz = 10000;
            else dtz = table?.DTZ(board) ?? 0;

            board.UndoMove(move, gameInfo);
            return dtz + 1;
        }

        private DTZTable? SelectTable(Move move, Board board)
        {
            if (move.CapturedPiece != (byte)PieceKind.NoPiece || move.PromotionPiece != (byte)PieceKind.NoPiece)
            {
                if (board.IsInsufficientMaterial()) return null;

                var classification = Classifier.Classify(board);
                return allTables[classification];

            }
            else return this;
        }

        public void WriteToFile(string filename)
        {
            var writer = new BinaryWriter(new FileStream(filename, FileMode.Create));
            LZWHuffman.Encode(Data, writer, maxBits);
            Console.WriteLine($"DTZ table {filename} has max bits {maxBits}");
            writer.Close();
        }

        public DTZTable(
            string filename,
            string classification,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard,
            WLDTable wldTable,
            int maxBits, // number of bits needed to store maximum DTZ present in the table
             Dictionary<string, DTZTable> allTables
        )
        {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);

            this.allTables = allTables;
            Classification = classification;
            normalise = normaliseBoard;
            this.getIndex = getIndex;
            this.maxBits = maxBits;
            Data = new byte[maxIndex];
            WLDTable = wldTable;

            var reader = new BinaryReader(new FileStream(filename, FileMode.Open));
            LZWHuffman.Decode(reader, maxBits).Read(Data, 0, Data.Length);

            AddSelfToAllTables();
        }

        private void AddSelfToAllTables()
        {
            allTables[Classification] = this;
            // additional table for flipped position if not symmetric
            var reversed = Classifier.ReverseClassification(Classification);
            if (reversed != Classification)
            {
                allTables[reversed] = new DTZTableReversed(this);
            }
        }

        // for use by DTZTableReversed, flips the classification but just reverses all other fields
        public DTZTable(DTZTable oppositeColour)
        {
            Classification = Classifier.ReverseClassification(oppositeColour.Classification);
            normalise = oppositeColour.normalise;
            getIndex = oppositeColour.getIndex;
            WLDTable = oppositeColour.WLDTable;
            allTables = oppositeColour.allTables;
            Data = oppositeColour.Data;
            maxBits = oppositeColour.maxBits;
        }
    }

    public class DTZTableReversed: DTZTable
    {
        public DTZTableReversed(DTZTable oppositeColour): base(oppositeColour) { }

        public override Move? GetMove(in Board board)
        {
            // makes an extra copy of the board, inefficient but not a massive overhead
            var flippedBoard = FlipColour(new Board(board));
            var oppositeMove = base.GetMove(flippedBoard);
            return oppositeMove?.FlipColour();
        }

        public override int DTZ(in Board board)
        {
            // makes an extra copy of the board, inefficient but not a massive overhead
            var flippedBoard = FlipColour(new Board(board));
            return base.DTZ(flippedBoard);
        }
    }
}
