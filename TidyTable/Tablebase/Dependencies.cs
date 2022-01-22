using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Tables;

namespace TidyTable.Tablebase
{
    // Everything in this file assumes kings not included
    internal class Dependencies
    {
        // TODO: Special case warning about pawns on both sides
        public static List<List<PieceKind>> DependsOn(List<PieceKind> pieces)
        {
            List<List<PieceKind>> result = new();
            HashSet<string> covered = new();

            void AddIfNeeded(List<PieceKind> pieces)
            {
                if (IsInsufficientMaterialWithoutKings(pieces)) return;
                
                var classification = Classifier.ClassifyWithoutKings(pieces);
                var reverse = Classifier.ReverseClassification(classification);
                // Due to how cases added, should always be sufficient to check just one
                if (covered.Contains(classification) || covered.Contains(reverse)) return;
                
                covered.Add(classification);
                covered.Add(reverse);
                result.Add(pieces);
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                var copy = new List<PieceKind>(pieces);
                var piece = pieces[i];
                copy.RemoveAt(i);
                if (piece == PieceKind.WhitePawn || piece == PieceKind.BlackPawn)
                {
                    for (var promotion = 1; promotion < 4; promotion++) {
                        var promotionCopy = new List<PieceKind>(copy);
                        var newPiece = (PieceKind)((byte)piece + promotion);
                        promotionCopy.Add(newPiece);
                        AddIfNeeded(promotionCopy);

                    }
                }
                AddIfNeeded(copy);
            }
            return result;
        }

        public static List<List<PieceKind>> AllThreePieces() => new List<List<PieceKind>> {
            new List<PieceKind>{ PieceKind.WhiteRook }, new List<PieceKind>{ PieceKind.WhiteQueen }
        };

        // 4 pieces, not counting when both pieces on one side
        // TODO: Not yet able to handle pawn vs pawn
        public static List<List<PieceKind>> AllTwoVsTwoPieces()
        {
            var result = new List<List<PieceKind>>();
            // Can skip insufficient material cases N-N, N-B, B-N, B-B
            // Due to flipping colours to exploit symmetry, can assume white >= black or that white has a pawn (separate as not insufficient material)
            for (byte whitePiece = (byte)PieceKind.WhiteRook; whitePiece < (byte)PieceKind.WhiteKing; whitePiece++)
            {
                for (byte blackPiece = (byte)PieceKind.BlackKnight; blackPiece <= whitePiece + (byte)PieceKind.BlackPawn; blackPiece++)
                {
                    result.Add(new List<PieceKind>() { (PieceKind)whitePiece, (PieceKind)blackPiece });
                }
            }

            // positions where white has a pawn
            for (byte blackPiece = (byte)PieceKind.BlackPawn; blackPiece < (byte)PieceKind.BlackKing; blackPiece++)
            {
                result.Add(new List<PieceKind>() { PieceKind.WhitePawn, (PieceKind)blackPiece });
            }
            return result;
        }

        // 4 pieces, where both on same side
        public static List<List<PieceKind>> AllThreeVsOnePieces()
        {
            var result = new List<List<PieceKind>>();
            // Avoid duplication by keeping 2nd piece <= first piece
            for (byte firstPiece = (byte)PieceKind.WhitePawn; firstPiece < (byte)PieceKind.WhiteKing; firstPiece++)
            {
                for (byte secondPiece = (byte)PieceKind.WhitePawn; secondPiece <= firstPiece; secondPiece++)
                {
                    result.Add(new List<PieceKind>() { (PieceKind)firstPiece, (PieceKind)secondPiece });
                }
            }
            return result;
        }

        public static List<List<PieceKind>> AllFourPieces() => AllTwoVsTwoPieces().Concat(AllThreeVsOnePieces()).ToList();

        public static bool IsInsufficientMaterialWithoutKings(List<PieceKind> pieces)
        {
            var whitePieces = pieces.Where(piece => (byte)piece < 6);
            var blackPieces = pieces.Where(piece => (byte)piece >= 6);
            return whitePieces.Count() <= 1 && blackPieces.Count() <= 1 && pieces.All(IsMinorPiece);
        }

        public static bool IsMinorPiece(PieceKind piece) =>
            piece == PieceKind.WhiteKnight || piece == PieceKind.BlackKnight
            || piece == PieceKind.WhiteBishop || piece == PieceKind.BlackBishop;
    }
}
