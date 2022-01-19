using Chessington.GameEngine;
using Chessington.GameEngine.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.TableFormats;

namespace TidyTable.Tables
{
    public class DTMTable // TODO: Create this, only storing DTM for white and using sub-DTM tables/small search to lookup
    {
        public string Classification;

        private BoardNormaliser normalise;
        private IndexGetter getIndex;
        private WLDTable WLDTable;
        private sbyte[] Data;

        private int maxBits = 0;

        public DTMTable(
            WLDTable wldTable, // TODO: Having both of these is redundant
            SolvingTable table 
        )
        {
            Classification = table.Classification;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;

            WLDTable = wldTable;
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
        }

        public int DTM(in Board board)
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
                    return availableMoves.Select(move => DTMForMove(copy, gameInfo, move)).Min();
                } else
                {
                    return availableMoves.Select(move => DTMForMove(copy, gameInfo, move)).Max();
                }
            }
        }

        // DTM potentially requires a 1-ply search, and this therefore requires a 1 to 2-ply search
        // Applies the same trick as above of min/maximising the DTM, doing a self-call for the lookup
        public Move? GetMove(in Board board)
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
                    return moves.MinBy(move => DTMForMove(copy, gameInfo, move));
                case Outcome.Lose:
                    return moves.MaxBy(move => DTMForMove(copy, gameInfo, move));
                default:
                    // DTMForMove always adds 1, and draws have DTM = 0, so look for first with DTMForMove 1
                    // Note: Drawn position, so no move leads to an immediate checkmate (other case where DTM = 0),
                    //      and cannot checkmate self on own turn, so any loses would take at least 2 ply => DTM > 1
                    return moves.First(move => DTMForMove(copy, gameInfo, move) == 1);
            }
        }

        private int DTMForMove(Board board, GameExtraInfo gameInfo, Move move)
        {
            // Relies on this method not modifying (normalising) the object passed to it
            board.MakeMoveWithoutRecording(move);
            var dtm = DTM(board);
            board.UndoMove(move, gameInfo);
            return dtm + 1;
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
            int maxBits // number of bits needed to store maximum DTM present in the table
        )
        {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);

            Classification = classification;
            normalise = normaliseBoard;
            this.getIndex = getIndex;
            this.maxBits = maxBits;
            sbyte bitMask = (sbyte)((1 << maxBits) - 1); // for extracting data from bottom of buffer
            Data = new sbyte[maxIndex];

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
                } else // read in another byte of data
                {
                    buffer |= stream.ReadByte() << bufferLength;
                    bufferLength += 8;
                }
            }
        }
    }
}
