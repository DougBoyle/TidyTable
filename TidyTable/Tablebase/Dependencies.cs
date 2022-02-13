using Chessington.GameEngine.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TidyTable.Endgames;
using TidyTable.Tables;

namespace TidyTable.Tablebase
{
    // Everything in this file assumes kings not included
    internal class Dependencies
    {
        const string TablePrefix = "tables/v2/";

        // returns a list of colour-independent tables that need solving before this table
        public static HashSet<string> DependsOn(List<PieceKind> pieces)
        {
            HashSet<string> result = new();

            void AddIfNeeded(List<PieceKind> pieces)
            {
                if (IsInsufficientMaterialWithoutKings(pieces)) return;
                
                var classification = Classifier.ClassifyColourless(pieces);
                if (result.Contains(classification)) return;

                result.Add(classification);
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
            new List<PieceKind>{ PieceKind.WhiteRook },
            new List<PieceKind>{ PieceKind.WhiteQueen },
            new List<PieceKind>{ PieceKind.WhitePawn },
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

        public static List<List<PieceKind>> OrderTables()
        {
            List<List<PieceKind>> allTables = AllThreePieces().Concat(AllFourPieces()).ToList();
            List<List<PieceKind>> orderedList = new();
            HashSet<string> covered = new();
            // to catch algorithm getting stuck
            var changing = true;
            while (changing)
            {
                changing = false;
                var tablesReadyToSolve = allTables.Where(table => DependsOn(table).IsSubsetOf(covered)).ToList();
                allTables.RemoveAll(table => tablesReadyToSolve.Contains(table));
                orderedList.AddRange(tablesReadyToSolve);
                foreach (var table in tablesReadyToSolve)
                {
                    changing = true;
                    covered.Add(Classifier.ClassifyColourless(table));
                }
            }
            if (allTables.Count > 0) throw new ArgumentException("Missing tables required to solve others");
            return orderedList;
        }

        public static void SolveAllTables()
        {
            var pieceLists = OrderTables();
            var solvedTables = new List<SubTable>();
            foreach (var table in pieceLists)
            {

                var hasPawns = table.Any(piece => piece == PieceKind.WhitePawn || piece == PieceKind.BlackPawn);
                var hasBlackPawns = table.Any(piece => piece == PieceKind.BlackPawn);
                
                BoardIndexer indexer = !hasPawns ? new NoPawnBoardIndexing(table)
                    : !hasBlackPawns ? new WhitePawnBoardIndexing(table)
                    : throw new NotSupportedException("Indexing/normalisation for pawns on black/both sides not yet implemented");
                BoardNormaliser normaliser = hasPawns ? Normalisation.NormalisePawnsBoard : Normalisation.NormaliseNoPawnsBoard;


                // include the kings in the filename, as they are expected for the actual table classifications
                var tableWithKings = new List<PieceKind>(table)
                {
                    PieceKind.WhiteKing,
                    PieceKind.BlackKing
                };
                var name = Classifier.Classify(tableWithKings);
                Console.WriteLine($"Solving for table {name}");

                var filename = TablePrefix + name; // needed to write extra tables in LoadFromFileElseSolve

                var whitePieces = tableWithKings.Where(piece => (byte)piece < 6).Select(piece => (ColourlessPiece)piece).ToList();
                var blackPieces = tableWithKings.Where(piece => (byte)piece >= 6).Select(piece => (ColourlessPiece)((byte)piece - 6)).ToList();

                var solved = TableLoading.LoadFromFileElseSolve(
                    filename,
                    whitePieces,
                    blackPieces,
                    solvedTables,
                    indexer,
                    normaliser
                );
                solvedTables.Add(solved);
                if (Classifier.ClassifyPieceLists(whitePieces, blackPieces) != Classifier.ClassifyPieceLists(blackPieces, whitePieces))
                {
                    solvedTables.Add(solved.SwappedColour());
                }

                Console.WriteLine($"Solved table {name}");
            }
        }
    }
}
