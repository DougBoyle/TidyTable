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
            var whiteTable = table.WhiteTable.Select(entry => entry == null ? null : new SubTableEntry(entry)).ToArray()!;
            var blackTable = table.BlackTable.Select(entry => entry == null ? null : new SubTableEntry(entry)).ToArray()!;
            var getIndex = table.GetIndex;
            var normaliseBoard = table.NormaliseBoard;


            GetOutcome = (in Board board) =>
            {
                var boardCopy = new Board(board);
                normaliseBoard(boardCopy);
                var index = getIndex(boardCopy);
                return (board.CurrentPlayer == Player.White ? whiteTable : blackTable)[index];
            };
        }

        public SubTable(SolvingTableSymmetric table)
        {
            Classification = table.Classification;
            var compactTable = table.Table.Select(entry => entry == null ? null : new SubTableEntry(entry)).ToArray()!;
            var getIndex = table.GetIndex;
            var normaliseBoard = table.NormaliseBoard;

            GetOutcome = (in Board board) =>
            {
                var boardCopy = new Board(board);
                if (board.CurrentPlayer == Player.Black) boardCopy = FlipColour(boardCopy);
                normaliseBoard(boardCopy);
                var index = getIndex(boardCopy);
                return compactTable[index];
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
                return new SubTableEntry(entry.DTZ, entry.Outcome);
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
                    byte encoded = entry != null ? new SubTableEntry(entry).ToByte() : (byte)0;
                    fs.WriteByte(encoded);
                }
            }
        }

        public static void WriteToFile(SolvingTableSymmetric table, string filename)
        {
            using FileStream fs = File.OpenWrite(filename);
            for (int i = 0; i < table.Table.Length; i++)
            {
                TableEntry? entry = table.Table[i];
                byte encoded = entry != null ? new SubTableEntry(entry).ToByte() : (byte)0;
                fs.WriteByte(encoded);
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
                    var value = stream.ReadByte();
                    if (value < 0) throw new EndOfStreamException("Received -1 when reading byte from stream");
                    table[i] = SubTableEntry.FromByte((byte)value);
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
                var value = stream.ReadByte();
                if (value < 0) throw new EndOfStreamException("Received -1 when reading byte from stream");
                Table[i] = SubTableEntry.FromByte((byte)value);
            }

            return (in Board board) =>
            {
                var boardCopy = new Board(board);
                if (board.CurrentPlayer == Player.Black) boardCopy = FlipColour(boardCopy);
                normaliseBoard(boardCopy);
                return Table[getIndex(boardCopy)];
            };
        }
    }
}
