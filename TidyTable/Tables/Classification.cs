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

        public static string ClassifyPieceLists(List<ColourlessPiece> whitePieces, List<ColourlessPiece> blackPieces)
        {
            var whiteStrings = whitePieces.Select(piece => names[piece]).ToList();
            var blackStrings = blackPieces.Select(piece => names[piece]).ToList();
            whiteStrings.Sort();
            blackStrings.Sort();
            var whiteString = string.Join(null, whiteStrings);
            var blackString = string.Join(null, blackStrings);
            return $"{whiteString}-{blackString}";
        }

        public static string ReverseClassification(string classification)
        {
            var classificationParts = classification.Split('-');
            return $"{classificationParts[1]}-{classificationParts[0]}";
        }
    }
}
