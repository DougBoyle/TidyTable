using Chessington.GameEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.TableFormats;
using static Chessington.GameEngine.AI.Endgame.NormalForm;

namespace TidyTable.Tables
{
    // Only used for lookup in solving a larger table.
    // Has just a string for the type of position it solves,
    // And a GetEntry method that maps a board to a SolvingTableEntry
    public class SubTable
    {
        public readonly string Classification;
        public readonly OutcomeSearcher GetOutcome;

        public SubTable(string classification, OutcomeSearcher getOutcome)
        {
            Classification = classification;
            GetOutcome = getOutcome;
        }

        // Given one for matching KP-K, returns one for K-KP i.e. positions that were winning for White now win for Black
        // FlipColour swaps Black/White pieces (changes index) and current player, so we want the Outcome of that player, not opposite
        public SubTable SwappedColour()
        {
            OutcomeSearcher newGetOutcome = (in Board board) =>
            {
                // flip the colours so that index lookup will work (affects direction pawns move etc.)
                var flippedBoard = FlipColour(board);

                var entry = GetOutcome(flippedBoard);
                if (entry == null) return null;
                // Board/move shouldn't actually matter here, but use the original one anyway
                return new SubTableEntry(entry.DTZ, entry.DTM, entry.Outcome);
            };
            return new SubTable(Classifier.ReverseClassification(Classification), newGetOutcome);
        }


        // Solving Table Entries can be encoded as shorts, take each value from table, encode it, and write to file.
        // This file then holds enough information to be read as a SubTable.
        // First half is for White, second half for Black
        public static void WriteToFile(SolvingTable table, string filename)
        {
            using FileStream fs = File.OpenWrite(filename);
            foreach (var colourTable in new TableEntry?[][] { table.WhiteTable, table.BlackTable })
            {
                for (int i = 0; i < colourTable.Length; i++)
                {
                    TableEntry? entry = colourTable[i];
                    ushort encoded = entry != null ? new SubTableEntry(entry).ToShort() : (ushort)0;
                    byte[] bytes = BitConverter.GetBytes(encoded);
                    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    fs.Write(bytes, 0, 2);
                }
            }
        }

        public SubTable(
            string filename,
            string classification,
            int maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            Classification = classification;
            SubTableEntry?[] WhiteTable = new SubTableEntry?[maxIndex];
            SubTableEntry?[] BlackTable = new SubTableEntry?[maxIndex];

            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            using var stream = File.Open(filename, FileMode.Open);

            foreach (var table in new SubTableEntry?[][] { WhiteTable, BlackTable })
            {
                for (int i = 0; i < table.Length; i++)
                {
                    var bytes = new byte[2];
                    stream.Read(bytes, 0, 2);
                    ushort value = (ushort)((bytes[0] << 8) + bytes[1]);
                    table[i] = SubTableEntry.FromShort(value);
                }
            }

            GetOutcome = (in Board board) =>
            {
                var table = board.CurrentPlayer == Player.White ? WhiteTable : BlackTable;
                var boardCopy = new Board(board);
                normaliseBoard(boardCopy);
                return table[getIndex(boardCopy)];
            };
        }
    }
}
