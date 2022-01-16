using Chessington.GameEngine;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Endgames;
using TidyTable.TableFormats;
using static Chessington.GameEngine.AI.Endgame.NormalForm;

namespace TidyTable.Tables
{
    // Used when playing a game, not during solving tables,
    // so just needs to give a move (in the configuration of the input board) and outcome
    public class LookupTable
    {
        public readonly string Classification;
        public readonly MoveSearcher GetMove;

        public LookupTable(string classification, MoveSearcher getMove)
        {
            Classification = classification;
            GetMove = getMove;
        }

        // Given one for matching KP-K, returns one for K-KP i.e. positions that were winning for White now win for Black
        // FlipColour swaps Black/White pieces (changes index) and current player, so we want the Outcome of that player, not opposite.
        // Do however need to convert the found move back to the original colour/flip the position of the board.
        public LookupTable SwappedColour()
        {
            MoveSearcher newGetMove = (in Board board) =>
            {
                // flip the colours so that index lookup will work (affects direction pawns move etc.)
                var flippedBoard = FlipColour(board);

                var entry = GetMove(flippedBoard);
                if (entry == null) return null;
                // Board/move shouldn't actually matter here, but use the original one anyway
                return new ProbeTableEntry(entry.Outcome, entry.Move?.FlipColour());
            };
            return new LookupTable(Classifier.ReverseClassification(Classification), newGetMove);
        }

        // First half is for White, second half for Black
        // Identical structure as the method in SubTable, but maps TableEntry -> ProbeTableEntry instead
        public static void WriteToFile(SolvingTable table, string filename)
        {
            using FileStream fs = File.OpenWrite(filename);
            for (var player = (int)Player.White; player <= (int)Player.Black; player++) {
                var colourTable = player == (int)Player.White ? table.WhiteTable : table.BlackTable;
                var colouredKnight = player == (int)Player.White ? (byte)PieceKind.WhiteKnight : (byte)PieceKind.BlackKnight;
                for (int i = 0; i < colourTable.Length; i++)
                {
                    TableEntry? entry = colourTable[i];
                    ushort encoded = entry != null ? new ProbeTableEntry(entry).ToShort(colouredKnight) : (ushort)0;
                    byte[] bytes = BitConverter.GetBytes(encoded);
                    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    fs.Write(bytes, 0, (int)ProbeTableEntry.CompressedSize);
                }
            }
        }

        public LookupTable(
            string filename,
            string classification,
            int maxIndex,
            IndexGetter getIndex,
            BoardNormaliserWithMapping normaliseBoard
        )
        {
            Classification = classification;
            // Unlike SubTable, encoded shorts can't be decoded until Board available, 
            // since positions of pieces etc. needed to determine promotions.
            var WhiteTable = new ushort[maxIndex];
            var BlackTable = new ushort[maxIndex];

            if (!File.Exists(filename)) throw new FileNotFoundException(filename);
            using var stream = File.Open(filename, FileMode.Open);

            foreach (var table in new ushort[][] { WhiteTable, BlackTable })
            {
                for (int i = 0; i < table.Length; i++)
                {
                    var bytes = new byte[ProbeTableEntry.CompressedSize];
                    stream.Read(bytes, 0, (int)ProbeTableEntry.CompressedSize);
                    table[i] = (ushort)((bytes[0] << 8) + bytes[1]);
                }
            }

            GetMove = (in Board board) =>
            {
                var table = board.CurrentPlayer == Player.White ? WhiteTable : BlackTable;
                var boardCopy = new Board(board);
                var mapping = normaliseBoard(boardCopy);
                ushort entry = table[getIndex(boardCopy)];
                var decodedEntry = ProbeTableEntry.FromShort(entry, boardCopy);
                // Map move back into the squares of the original board before normalising
                decodedEntry?.Move?.Map(mapping);
                return decodedEntry;
            };
        }

        public static void WriteToCompressedFile(SolvingTable table, string filename)
        {
            // TODO: Use streams instead.
            // byte[] size is known from size of table.WhiteTable/BlackTable and ProbeTableEntry size
            var numBytes = table.WhiteTable.Length * 2u * ProbeTableEntry.CompressedSize;
            byte[] uncompressed = new byte[numBytes];
            uint byteIndex = 0;

            for (var player = (int)Player.White; player <= (int)Player.Black; player++)
            {
                var colourTable = player == (int)Player.White ? table.WhiteTable : table.BlackTable;
                var colouredKnight = player == (int)Player.White ? (byte)PieceKind.WhiteKnight : (byte)PieceKind.BlackKnight;
                for (int i = 0; i < colourTable.Length; i++)
                {
                    TableEntry? entry = colourTable[i];
                    ushort encoded = entry != null ? new ProbeTableEntry(entry).ToShort(colouredKnight) : (ushort)0;
                    byte[] bytes = BitConverter.GetBytes(encoded);
                    if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    Array.Copy(bytes, 0, uncompressed, byteIndex, ProbeTableEntry.CompressedSize);
                    byteIndex += ProbeTableEntry.CompressedSize;
                }
            }

            Compression.Compress.CompressToFile(uncompressed, (int)byteIndex, filename);
        }

        public static LookupTable FromOnDemandCompressedFile(
            string filename,
            string classification,
            uint maxIndex,
            IndexGetter getIndex,
            BoardNormaliserWithMapping normaliseBoard
        )
        {
            if (!File.Exists(filename)) throw new FileNotFoundException(filename);

            ushort GetEntry(uint index, Player player)
            {
                // Assumption of layout [ White: short * maxIndex, Black: short * maxIndex ]
                if (player == Player.Black) index += maxIndex;
                index *= ProbeTableEntry.CompressedSize;
                
                var stream = new MemoryStream();
                Compression.Compress.Decompress(filename, new BinaryWriter(stream));

                stream.Seek(index, SeekOrigin.Begin);
                var reader = new BinaryReader(stream);
                var result = reader.ReadUInt16();
                reader.Close();
                return result;
            }

            MoveSearcher GetMove = (in Board board) =>
            {
                var boardCopy = new Board(board);
                var mapping = normaliseBoard(boardCopy);
                ushort entry = GetEntry(getIndex(boardCopy), board.CurrentPlayer);
                var decodedEntry = ProbeTableEntry.FromShort(entry, boardCopy);
                // Map move back into the squares of the original board before normalising
                decodedEntry?.Move?.Map(mapping);
                return decodedEntry;
            };

            return new LookupTable(classification, GetMove);
        }
    }
}
