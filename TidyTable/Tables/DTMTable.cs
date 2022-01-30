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

namespace TidyTable.Tables
{
    // TODO: Do this for DTZ instead?
    public class DTMTable
    {
        public readonly string Classification;

        private readonly BoardNormaliser normalise;
        private readonly IndexGetter getIndex;
        private readonly WLDTable WLDTable;
        private readonly Dictionary<string, DTMTable> allTables;
        private readonly sbyte[] Data;

        private readonly int maxBits = 0;

        public DTMTable(SolvingTable table, Dictionary<string, DTMTable> allTables)
        {
            this.allTables = allTables;

            Classification = table.Classification;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;

            WLDTable = new WLDTable(table);
            Data = new sbyte[table.MaxIndex]; // only stores values for white
            for (int i = 0; i < table.WhiteTable.Length; i++)
            {
                var dtm = table.WhiteTable[i]?.DTM ?? 0;
                if (table.WhiteTable[i]?.Outcome == Outcome.Draw)
                {
                    dtm = 0;
                }
                maxBits = Math.Max(dtm, maxBits);
                Data[i] = dtm;
            }
            maxBits = (int)Math.Floor(Math.Log2(maxBits)) + 1;

            AddSelfToAllTables();
        }

        public Outcome GetOutcome(in Board board) => WLDTable.GetOutcome(board);

        public virtual int DTM(in Board board)
        {
            // Probably unnecessary
            var copy = new Board(board);
            if (board.CurrentPlayer == Player.White)
            {
                normalise(copy);
                return Data[getIndex(copy)];
            } else
            {
                // DTM values not stored for black
                // For a draw, return 0
                // For a win, return 0 if no moves else minimum of DTM for white after next move + 1
                // For a loss, return 0 if no moves else maximum of DTM for white after next move + 1
                var outcome = WLDTable.GetOutcome(board);
                if (outcome == Outcome.Draw) return 0;

                var availableMoves = board.GetAllAvailableMoves();
                if (availableMoves.Count == 0) return 0;

                var gameInfo = new GameExtraInfo(board);
                if (outcome == Outcome.Win)
                {
                    return availableMoves.Select(move => DTMForMove(copy, gameInfo, move, requiredOutcome: Outcome.Lose)).Min();
                } else
                {
                    return availableMoves.Select(move => DTMForMove(copy, gameInfo, move, requiredOutcome: null)).Max();
                }
            }
        }

        // DTM potentially requires a 1-ply search, and this therefore requires a 1 to 2-ply search
        // Applies the same trick as above of min/maximising the DTM, doing a self-call for the lookup
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
                    return moves.MinBy(move => DTMForMove(copy, gameInfo, move, requiredOutcome: Outcome.Lose));
                case Outcome.Lose:
                    return moves.MaxBy(move => DTMForMove(copy, gameInfo, move, requiredOutcome: null));
                default:
                    // DTMForMove always adds 1, and draws have DTM = 0, so look for first with DTMForMove 1
                    // Note: Drawn position, so no move leads to an immediate checkmate (other case where DTM = 0),
                    //      and cannot checkmate self on own turn, so any loses would take at least 2 ply => DTM > 1
                    return moves.First(move => DTMForMove(copy, gameInfo, move, requiredOutcome: null) == 1);
            }
        }

        // requiredOutcome: For a win, must only consider subsequent positions losing for opponent
        private int DTMForMove(Board board, GameExtraInfo gameInfo, Move move, Outcome? requiredOutcome)
        {
            // Relies on this method not modifying (normalising) the object passed to it
            board.MakeMoveWithoutRecording(move);

            var table = SelectTable(move, board);
            int dtm;
            if (requiredOutcome != null && ((table?.GetOutcome(board) ?? Outcome.Draw) != requiredOutcome)) dtm = 10000;
            else dtm = table?.DTM(board) ?? 0;

            board.UndoMove(move, gameInfo);
            return dtm + 1;
        }

        private DTMTable? SelectTable(Move move, Board board)
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
             // As usual, write starting from LSB
            int buffer = 0;
            int bufferLength = 0;

            using FileStream fs = File.OpenWrite(filename);
            foreach (var value in Data)
            {
                buffer |= value << bufferLength;
                bufferLength += maxBits;

                // relies on maxBits <= 8, so only ever need to clear 1 byte of space
                if (bufferLength >= 8)
                {
                    fs.WriteByte((byte)buffer);
                    buffer >>= 8;
                    bufferLength -= 8;
                }
            }
            if (bufferLength > 0) fs.WriteByte((byte)buffer);
        }

        public DTMTable(
            string filename,
            string classification,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard,
            WLDTable wldTable,
            int maxBits, // number of bits needed to store maximum DTM present in the table
             Dictionary<string, DTMTable> allTables
        )
        {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);

            this.allTables = allTables;
            Classification = classification;
            normalise = normaliseBoard;
            this.getIndex = getIndex;
            this.maxBits = maxBits;
            sbyte bitMask = (sbyte)((1 << maxBits) - 1); // for extracting data from bottom of buffer
            Data = new sbyte[maxIndex];
            WLDTable = wldTable;

            // As usual, write starting from LSB
            int dataIndex = 0;
            int buffer = 0;
            int bufferLength = 0;

            using var stream = File.Open(filename, FileMode.Open);
            while (dataIndex < maxIndex)
            {
                // enough bits to read out an entry
                if (bufferLength >= maxBits)
                {
                    Data[dataIndex++] = (sbyte)(buffer & bitMask);
                    buffer >>= maxBits;
                    bufferLength -= maxBits;
                }
                else // read in another byte of data
                {
                    buffer |= stream.ReadByte() << bufferLength;
                    bufferLength += 8;
                }
            }

            AddSelfToAllTables();
        }

        private void AddSelfToAllTables()
        {
            allTables[Classification] = this;
            // additional table for flipped position if not symmetric
            var reversed = Classifier.ReverseClassification(Classification);
            if (reversed != Classification)
            {
                allTables[reversed] = new DTMTableReversed(this);
            }
        }

        // for use by DTMTableReversed, flips the classification but just reverses all other fields
        public DTMTable(DTMTable oppositeColour)
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

    public class DTMTableReversed: DTMTable
    {
        public DTMTableReversed(DTMTable oppositeColour): base(oppositeColour) { }

        public override Move? GetMove(in Board board)
        {
            // makes an extra copy of the board, inefficient but not a massive overhead
            var flippedBoard = FlipColour(new Board(board));
            var oppositeMove = base.GetMove(flippedBoard);
            return oppositeMove?.FlipColour();
        }

        public override int DTM(in Board board)
        {
            // makes an extra copy of the board, inefficient but not a massive overhead
            var flippedBoard = FlipColour(new Board(board));
            return base.DTM(flippedBoard);
        }
    }
}
