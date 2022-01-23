using Chessington.GameEngine;
using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TidyTable.Tables
{
    public static class Classifier
    {
        private static Dictionary<ColourlessPiece, string> names = new Dictionary<ColourlessPiece, string>()
        {
            {ColourlessPiece.Pawn, "P"},
            {ColourlessPiece.Knight, "N"},
            {ColourlessPiece.Bishop, "B"},
            {ColourlessPiece.Rook, "R"},
            {ColourlessPiece.Queen, "Q"},
            {ColourlessPiece.King, "K"},
        };

        public static string Classify(this Board board)
        {
            List<ColourlessPiece> whitePieces = new();
            List<ColourlessPiece> blackPieces = new();
            for (byte i = 0; i < 64; i++)
            {
                var piece = board.GetPieceIndex(i);
                if (piece != (byte)PieceKind.NoPiece)
                {
                    (piece < 6 ? whitePieces : blackPieces).Add((ColourlessPiece)(piece % 6));
                }
            }

            return ClassifyPieceLists(whitePieces, blackPieces);
        }

        public static string Classify(List<PieceKind> pieces) =>
            ClassifyPieceLists(
                pieces.Where(piece => (byte)piece < 6).Select(piece => (ColourlessPiece)piece).ToList(),
                pieces.Where(piece => (byte)piece >= 6).Select(piece => (ColourlessPiece)((byte)piece - 6)).ToList()
            );
            
        public static string ClassifyPieceLists(List<ColourlessPiece> whitePieces, List<ColourlessPiece> blackPieces)
        {
            var whiteString = StringForPieces(whitePieces);
            var blackString = StringForPieces(blackPieces);
            return $"{whiteString}-{blackString}";
        }

        public static string StringForPieces(List<ColourlessPiece> pieces)
        {
            var strings = pieces.Select(piece => names[piece]).ToList();
            strings.Sort();
            return string.Join(null, strings);
        }

        public static string ReverseClassification(string classification)
        {
            var classificationParts = classification.Split('-');
            return $"{classificationParts[1]}-{classificationParts[0]}";
        }

        // Each table solves both colours of a table, so use a colourless version for describing dependencies between tables
        public static string ClassifyColourless(List<PieceKind> pieces)
        {
            var whiteString = StringForPieces(pieces.Where(piece => (byte)piece < 6).Select(piece => (ColourlessPiece)piece).ToList());
            var blackString = StringForPieces(pieces.Where(piece => (byte)piece >= 6).Select(piece => (ColourlessPiece)((byte)piece - 6)).ToList());
            return string.Compare(whiteString, blackString) <= 0 ? $"{whiteString}-{blackString}" : $"{blackString}-{whiteString}";
        }
    }
}
