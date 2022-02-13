using Chessington.GameEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Compression;
using TidyTable.TableFormats;
using static Chessington.GameEngine.AI.Endgame.NormalForm;

namespace TidyTable.Tables
{
    public class WLDTable
    {
        public string Classification;

        // Based on TableEntry.Outcome, so 0 = draw, 1 = win, 2 = loss
        // Stores all values for white, followed by all values for black
        // As usual, fill each byte from the LSB upwards
        public byte[] Outcomes;
        public uint MaxIndex;

        private byte buffer = 0;
        private int bufferLength = 0;
        private int index = 0;

        private readonly BoardNormaliser normalise;
        private readonly IndexGetter getIndex;

        private readonly bool symmetric = false;

        // Need SolvingTable, as only it exposes underlying white/black tables and max index, normalisation, indexing
        public WLDTable(SolvingTable table)
        {
            Classification = table.Classification;
            MaxIndex = table.MaxIndex;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;

            // bytes = entries * 2 sides * 2 bits per entry / 8 bits per byte = entries / 2 (rounded up)
            uint numBytes = (MaxIndex + 1) / 2;
            Outcomes = new byte[numBytes];

            for (var player = (int)Player.White; player <= (int)Player.Black; player++)
            {
                var colourTable = player == (int)Player.White ? table.WhiteTable : table.BlackTable;
                Array.ForEach(colourTable, entry => WriteOutcome(entry?.Outcome ?? Outcome.Draw));
            }

            if (bufferLength > 0) Outcomes[index] = buffer;
        }

        public WLDTable(SolvingTableSymmetric table)
        {
            Classification = table.Classification;
            MaxIndex = table.MaxIndex;
            normalise = table.NormaliseBoard;
            getIndex = table.GetIndex;
            symmetric = true;

            // bytes = entries * 2 bits per entry / 8 bits per byte = entries / 4 (rounded up)
            uint numBytes = (MaxIndex + 3) / 4;
            Outcomes = new byte[numBytes];

            Array.ForEach(table.Table, entry => WriteOutcome(entry?.Outcome ?? Outcome.Draw));

            if (bufferLength > 0) Outcomes[index] = buffer;
        }

        public Outcome GetOutcome(in Board board)
        {
            uint index = 0;
            Board copy = new(board);
            if (board.CurrentPlayer == Player.Black)
            {
                if (symmetric)
                {
                    copy = FlipColour(copy);
                } else
                {
                    index = MaxIndex;
                }
            }
            normalise(copy);
            index += getIndex(copy);

            int offset = (int)((index & 3) << 1); // 4 entries per byte = last 2 bits, 2 bit size = left shift
            var b = Outcomes[index >> 2]; // 4 entries per byte = divide by 4
            return (Outcome)((b >> offset) & 3);
        }

        private void WriteOutcome(Outcome outcome)
        {
            buffer |= (byte)((byte)outcome << bufferLength);
            bufferLength += 2;
            if (bufferLength == 8)
            {
                Outcomes[index++] = buffer;
                buffer = 0;
                bufferLength = 0;
            }
        }

        public void WriteToFile(string filename)
        {
            var writer = new BinaryWriter(new FileStream(filename, FileMode.Create));
            LZWHuffman.Encode(Outcomes, writer);
            writer.Close();
        }

        public WLDTable(
            string filename,
            string classification,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            Classification = classification;
            MaxIndex = maxIndex;
            normalise = normaliseBoard;
            this.getIndex = getIndex;

            uint numBytes;
            if (Classifier.ReverseClassification(classification) == classification)
            {
                symmetric = true;
                numBytes = (MaxIndex + 3) / 4;
            } else
            {
                // bytes = entries * 2 sides * 2 bits per entry / 8 bits per byte = entries / 2 (rounded up)
                numBytes = (MaxIndex + 1) / 2;
            }

            Outcomes = new byte[numBytes];
            var reader = new BinaryReader(new FileStream(filename, FileMode.Open));
            LZWHuffman.Decode(reader).Read(Outcomes, 0, (int)numBytes);
            reader.Close();
        }
    }
}
