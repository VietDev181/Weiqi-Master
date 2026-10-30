using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Weiqi.Core
{
    public enum AiLevel { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>
    /// Computer opponent. Easy = noisy heuristics; Normal/Hard = Monte-Carlo tree search biased by the same heuristics.
    /// Thread-safe: works on its own copies only, uses System.Random.
    /// </summary>
    public sealed class GoAI
    {
        public const int Pass = -1;

        sealed class Node
        {
            public int Move;
            public Stone Mover;
            public int Visits;
            public float Wins;
            public float Prior;
            public List<Node> Children;
        }

        readonly Random rng = new Random();
        readonly AiLevel level;

        public GoAI(AiLevel level) { this.level = level; }

        /// <summary>Returns a point index or <see cref="Pass"/>. <paramref name="board"/> is copied, so it may be mutated afterwards.</summary>
        public int ChooseMove(GoBoard board, bool opponentPassed, float komi, Stone color, int moveNumber)
        {
            board = board.Clone();

            if (opponentPassed && IsWinningNow(board, color, komi)) return Pass;

            var heur = new float[board.Count];
            float best = ScoreMoves(board, color, moveNumber, heur);
            if (best < -50f) return Pass;   // nothing sensible left to play

            if (level == AiLevel.Easy) return PickNoisy(board, heur, color, 1.2f);

            int budgetMs = level == AiLevel.Normal ? 700 : 2600;
            if (board.Size >= 19) budgetMs = (int)(budgetMs * 1.4f);
            return Mcts(board, color, komi, heur, budgetMs, moveNumber);
        }

        bool IsWinningNow(GoBoard board, Stone color, float komi)
        {
            var owner = new Stone[board.Count];
            board.Territory(null, owner, out int b, out int w);
            float diff = b - (w + komi);
            return color == Stone.Black ? diff > 0 : diff < 0;
        }

        // ---------- heuristics ----------

        float ScoreMoves(GoBoard board, Stone color, int moveNumber, float[] outScores)
        {
            float best = -1000f;
            int size = board.Size;
            var scratch = board.Clone();
            var group = new List<int>();
            Stone opp = color.Opp();
            var owner = new Stone[board.Count];
            board.Territory(null, owner, out _, out _);

            for (int i = 0; i < board.Count; i++)
            {
                outScores[i] = -1000f;
                if (board.Cells[i] != Stone.Empty || !board.IsLegal(i, color)) continue;
                if (board.IsOwnEye(i, color) && moveNumber > 8) { outScores[i] = -200f; continue; }

                float s = 0f;
                int x = board.X(i), y = board.Y(i);

                // distance to nearest edge (0 = first line)
                int edge = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
                float opening = Math.Max(0f, 1f - moveNumber / (size * 1.6f));
                if (edge == 0) s -= 2.5f + 3f * opening;
                else if (edge == 1) s -= 0.3f + 1.2f * opening;
                else if (edge == 2 || edge == 3) s += 1.4f * opening + 0.3f;
                // pull towards the star points early on
                float cx = (size - 1) / 2f;
                float dc = Math.Abs(x - cx) + Math.Abs(y - cx);
                s += opening * (0.8f - dc * 0.05f);

                // tactics
                scratch.CopyFrom(board);
                scratch.TryPlay(i, color, out int caps);
                s += caps * 6f;

                var nb = board.Neighbors[i];
                int friendly = 0, enemy = 0;
                for (int k = 0; k < nb.Length; k++)
                {
                    int n = nb[k];
                    Stone c = board.Cells[n];
                    if (c == color)
                    {
                        friendly++;
                        int libsBefore = board.Liberties(n);
                        if (libsBefore == 1) s += 7f;   // rescue a group in atari
                        else if (libsBefore == 2) s += 1.5f;
                    }
                    else if (c == opp)
                    {
                        enemy++;
                        int l = board.Liberties(n);
                        if (l == 2) s += 3.2f;      // put in atari
                        else if (l == 1) s += 6f;   // capture (already counted, small bonus)
                        else if (l == 3) s += 0.8f;
                    }
                }

                // liberties of the new group; self-atari is bad
                int myLibs = scratch.Cells[i] == color ? scratch.Liberties(i) : 0;
                if (caps == 0 && myLibs == 1) s -= 9f;
                else if (myLibs == 2) s -= 0.8f;
                else s += Math.Min(myLibs, 5) * 0.25f;

                // contact moves are generally better than far-away ones once the board has stones
                if (moveNumber > 4)
                {
                    if (friendly + enemy > 0) s += 1.0f;
                    else if (!HasStoneNear(board, x, y, 2)) s -= 1.5f;
                }

                // do not throw stones into the opponent's settled area: surrounded by their stones, none of ours nearby
                if (caps == 0 && owner[i] == opp && CountNear(board, x, y, 2, opp) >= 3 && CountNear(board, x, y, 2, color) == 0)
                    s -= 9f;

                outScores[i] = s;
                if (s > best) best = s;
            }
            return best;
        }

        static int CountNear(GoBoard b, int x, int y, int r, Stone who)
        {
            int n = 0;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= b.Size || ny >= b.Size) continue;
                if (b.Cells[ny * b.Size + nx] == who) n++;
            }
            return n;
        }

        static bool HasStoneNear(GoBoard b, int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= b.Size || ny >= b.Size) continue;
                if (b.Cells[ny * b.Size + nx] != Stone.Empty) return true;
            }
            return false;
        }

        int PickNoisy(GoBoard board, float[] heur, Stone color, float noise)
        {
            int bestIdx = Pass;
            float best = -1000f;
            for (int i = 0; i < heur.Length; i++)
            {
                if (heur[i] < -100f) continue;
                float v = heur[i] + (float)rng.NextDouble() * noise * 4f;
                if (v > best) { best = v; bestIdx = i; }
            }
            return bestIdx;
        }

        // ---------- Monte-Carlo tree search ----------

        int Mcts(GoBoard rootBoard, Stone toMove, float komi, float[] heur, int budgetMs, int moveNumber)
        {
            var root = new Node { Mover = toMove.Opp(), Move = -2 };
            var b = new GoBoard(rootBoard.Size);
            var owner = new Stone[rootBoard.Count];
            var path = new List<Node>(128);
            var sw = Stopwatch.StartNew();
            int iter = 0;

            Expand(root, rootBoard, toMove, heur);

            while (true)
            {
                if ((iter & 15) == 0 && sw.ElapsedMilliseconds >= budgetMs) break;
                iter++;

                b.CopyFrom(rootBoard);
                Stone color = toMove;
                int passes = 0;
                Node node = root;
                path.Clear();
                path.Add(root);

                while (node.Children != null && node.Children.Count > 0 && passes < 2)
                {
                    node = Select(node);
                    path.Add(node);
                    if (node.Move == Pass) { passes++; b.Pass(); }
                    else { passes = 0; b.TryPlay(node.Move, color, out _); }
                    color = color.Opp();
                }

                if (passes < 2 && node.Visits > 0 && node.Children == null)
                {
                    Expand(node, b, color, null);
                    if (node.Children.Count > 0)
                    {
                        node = node.Children[rng.Next(node.Children.Count)];
                        path.Add(node);
                        if (node.Move == Pass) { passes++; b.Pass(); }
                        else { passes = 0; b.TryPlay(node.Move, color, out _); }
                        color = color.Opp();
                    }
                }

                Stone winner = Playout(b, color, passes, komi, owner);
                for (int k = 0; k < path.Count; k++)
                {
                    var n = path[k];
                    n.Visits++;
                    if (n.Mover == winner) n.Wins += 1f;
                }
            }

            Node bestChild = null;
            foreach (var c in root.Children)
                if (bestChild == null || c.Visits > bestChild.Visits) bestChild = c;
            return bestChild == null ? Pass : bestChild.Move;
        }

        Node Select(Node parent)
        {
            float logN = (float)Math.Log(parent.Visits + 1);
            Node best = null;
            float bestV = float.MinValue;
            var ch = parent.Children;
            for (int i = 0; i < ch.Count; i++)
            {
                var c = ch[i];
                float v;
                if (c.Visits == 0) v = 10f + c.Prior + (float)rng.NextDouble() * 0.01f;
                else
                {
                    float win = c.Wins / c.Visits;
                    v = win + 0.55f * (float)Math.Sqrt(logN / c.Visits) + c.Prior / (c.Visits + 1);
                }
                if (v > bestV) { bestV = v; best = c; }
            }
            return best;
        }

        void Expand(Node node, GoBoard b, Stone color, float[] heur)
        {
            node.Children = new List<Node>(b.Count / 2);
            for (int i = 0; i < b.Count; i++)
            {
                if (b.Cells[i] != Stone.Empty) continue;
                if (heur != null && heur[i] < -100f) continue;
                if (heur == null && b.IsOwnEye(i, color)) continue;
                if (!b.IsLegal(i, color)) continue;
                node.Children.Add(new Node
                {
                    Move = i, Mover = color,
                    Prior = heur != null ? Math.Max(0f, heur[i]) * 0.04f : 0f
                });
            }
            node.Children.Add(new Node { Move = Pass, Mover = color, Prior = -0.3f });
        }

        Stone Playout(GoBoard b, Stone color, int passes, float komi, Stone[] owner)
        {
            int limit = b.Count * 3;
            int count = b.Count;
            for (int step = 0; step < limit && passes < 2; step++)
            {
                int start = rng.Next(count);
                int chosen = -1;
                for (int k = 0; k < count; k++)
                {
                    int i = start + k; if (i >= count) i -= count;
                    if (b.Cells[i] != Stone.Empty) continue;
                    if (b.IsOwnEye(i, color)) continue;
                    if (b.TryPlay(i, color, out _) == MoveError.None) { chosen = i; break; }
                }
                if (chosen < 0) { passes++; b.Pass(); }
                else passes = 0;
                color = color.Opp();
            }
            b.Territory(null, owner, out int bl, out int wh);
            return bl - (wh + komi) > 0 ? Stone.Black : Stone.White;
        }
    }
}
