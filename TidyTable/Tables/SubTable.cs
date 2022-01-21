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

        // Uses the original SolvingTable, rather than mapping its tables to tables of SubTableEntries and only keeping those
        public SubTable(SolvingTable table)
        {
            Classification = table.Classification;
            GetOutcome = (in Board board) =>
            {
                var boardCopy = new Board(board);
                table.NormaliseBoard(boardCopy);
                var index = table.GetIndex(boardCopy);
                var tableEntry = (board.CurrentPlayer == Player.White ? table.WhiteTable : table.BlackTable)[index];
                if (tableEntry == null) return null;
                return new SubTableEntry(tableEntry);
            };
        }

        public SubTable(SolvingTableSymmetric table)
        {
            Classification = table.Classification;
            GetOutcome = (in Board board) =>
            {
                var boardCopy = new Board(board);
                if (board.CurrentPlayer == Player.Black) FlipColour(boardCopy);
                table.NormaliseBoard(boardCopy);
                var index = table.GetIndex(boardCopy);
                var tableEntry = table.Table[index];
                if (tableEntry == null) return null;
                return new SubTableEntry(tableEntry);
            };
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

        public static void WriteToFile(SolvingTableSymmetric table, string filename)
        {
            using FileStream fs = File.OpenWrite(filename);
            for (int i = 0; i < table.Table.Length; i++)
            {
                TableEntry? entry = table.Table[i];
                ushort encoded = entry != null ? new SubTableEntry(entry).ToShort() : (ushort)0;
                byte[] bytes = BitConverter.GetBytes(encoded);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                fs.Write(bytes, 0, 2);
            }     
        }

        public SubTable(
            string filename,
            string classification,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard,
            bool symmetric = false
        )
        {
            Classification = classification;
            GetOutcome = symmetric
                ? FromSymmetricFile(
                    filename,
                    maxIndex,
                    getIndex,
                    normaliseBoard
                ) : FromFile(
                    filename,
                    maxIndex,
                    getIndex,
                    normaliseBoard
                );
        }

        private static OutcomeSearcher FromFile(
            string filename,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            SubTableEntry?[] WhiteTable = new SubTableEntry?[maxIndex];
            SubTableEntry?[] BlackTable = new SubTableEntry?[maxIndex];

            // TODO: Allow compression in between
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

            return (in Board board) =>
            {
                var table = board.CurrentPlayer == Player.White ? WhiteTable : BlackTable;
                var boardCopy = new Board(board);
                normaliseBoard(boardCopy);
                return table[getIndex(boardCopy)];
            };
        }

        private static OutcomeSearcher FromSymmetricFile(
            string filename,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliser normaliseBoard
        )
        {
            SubTableEntry?[] Table = new SubTableEntry?[maxIndex];

            // TODO: Allow compression in between
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            using var stream = File.Open(filename, FileMode.Open);

            for (int i = 0; i < Table.Length; i++)
            {
                var bytes = new byte[2];
                stream.Read(bytes, 0, 2);
                ushort value = (ushort)((bytes[0] << 8) + bytes[1]);
                Table[i] = SubTableEntry.FromShort(value);
            }

            return (in Board board) =>
            {
                var boardCopy = new Board(board);
                if (board.CurrentPlayer == Player.Black) FlipColour(boardCopy);
                normaliseBoard(boardCopy);
                return Table[getIndex(boardCopy)];
            };
        }
    }
}
