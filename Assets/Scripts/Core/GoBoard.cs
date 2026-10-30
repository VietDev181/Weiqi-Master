using System;
using System.Collections.Generic;

namespace Weiqi.Core
{
    public enum Stone : byte { Empty = 0, Black = 1, White = 2 }

    public enum MoveError { None, OutOfBounds, Occupied, Suicide, Ko, NotYourTurn, GameOver }

    public static class StoneExt
    {
        public static Stone Opp(this Stone s) => s == Stone.Black ? Stone.White : Stone.Black;
    }

    /// <summary>
    /// Pure Go board: stones, liberties, captures, suicide and simple ko. No Unity dependencies,
    /// allocation-free in the hot path so the AI can use it for playouts.
    /// Index = y * Size + x, y = 0 is the bottom row.
    /// </summary>
    public sealed class GoBoard
    {
        public readonly int Size;
        public readonly int Count;
        public readonly Stone[] Cells;
        public readonly int[][] Neighbors;
        public readonly int[][] Diagonals;
        public ulong Hash;
        public int KoPoint = -1;

        readonly ulong[] zobrist;
        readonly int[] stack;
        readonly int[] region;
        readonly int[] visited;
        readonly int[] libMark;
        int stamp;

        public GoBoard(int size)
        {
            Size = size;
            Count = size * size;
            Cells = new Stone[Count];
            stack = new int[Count];
            region = new int[Count];
            visited = new int[Count];
            libMark = new int[Count];

            Neighbors = new int[Count][];
            Diagonals = new int[Count][];
            var tmp = new List<int>(4);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                tmp.Clear();
                if (x > 0) tmp.Add(Idx(x - 1, y));
                if (x < size - 1) tmp.Add(Idx(x + 1, y));
                if (y > 0) tmp.Add(Idx(x, y - 1));
                if (y < size - 1) tmp.Add(Idx(x, y + 1));
                Neighbors[Idx(x, y)] = tmp.ToArray();

                tmp.Clear();
                if (x > 0 && y > 0) tmp.Add(Idx(x - 1, y - 1));
                if (x < size - 1 && y > 0) tmp.Add(Idx(x + 1, y - 1));
                if (x > 0 && y < size - 1) tmp.Add(Idx(x - 1, y + 1));
                if (x < size - 1 && y < size - 1) tmp.Add(Idx(x + 1, y + 1));
                Diagonals[Idx(x, y)] = tmp.ToArray();
            }

            // Same seed for every board of the same size so hashes are comparable between copies.
            zobrist = new ulong[Count * 3];
            ulong s = 0x9E3779B97F4A7C15UL ^ (ulong)size;
            for (int i = 0; i < zobrist.Length; i++)
            {
                s ^= s << 13; s ^= s >> 7; s ^= s << 17;
                zobrist[i] = s;
            }
        }

        public int Idx(int x, int y) => y * Size + x;
        public int X(int idx) => idx % Size;
        public int Y(int idx) => idx / Size;

        public void CopyFrom(GoBoard other)
        {
            Array.Copy(other.Cells, Cells, Count);
            Hash = other.Hash;
            KoPoint = other.KoPoint;
        }

        public GoBoard Clone()
        {
            var b = new GoBoard(Size);
            b.CopyFrom(this);
            return b;
        }

        public void Clear()
        {
            Array.Clear(Cells, 0, Count);
            Hash = 0;
            KoPoint = -1;
        }

        public void Pass() { KoPoint = -1; }

        /// <summary>Counts liberties of the group containing <paramref name="start"/>, optionally listing its stones.</summary>
        public int Liberties(int start, List<int> group = null)
        {
            Stone c = Cells[start];
            stamp++;
            int sp = 0, libs = 0;
            stack[sp++] = start;
            visited[start] = stamp;
            while (sp > 0)
            {
                int i = stack[--sp];
                group?.Add(i);
                var nb = Neighbors[i];
                for (int k = 0; k < nb.Length; k++)
                {
                    int n = nb[k];
                    Stone s = Cells[n];
                    if (s == Stone.Empty)
                    {
                        if (libMark[n] != stamp) { libMark[n] = stamp; libs++; }
                    }
                    else if (s == c && visited[n] != stamp)
                    {
                        visited[n] = stamp;
                        stack[sp++] = n;
                    }
                }
            }
            return libs;
        }

        int RemoveGroup(int start, List<int> removed)
        {
            Stone c = Cells[start];
            int n = 0;
            int sp = 0;
            stack[sp++] = start;
            Cells[start] = Stone.Empty;
            Hash ^= zobrist[start * 3 + (int)c];
            while (sp > 0)
            {
                int i = stack[--sp];
                n++;
                removed?.Add(i);
                var nb = Neighbors[i];
                for (int k = 0; k < nb.Length; k++)
                {
                    int m = nb[k];
                    if (Cells[m] == c)
                    {
                        Cells[m] = Stone.Empty;
                        Hash ^= zobrist[m * 3 + (int)c];
                        stack[sp++] = m;
                    }
                }
            }
            return n;
        }

        /// <summary>Plays a stone. Fails (board unchanged) when occupied, suicide or simple ko.</summary>
        public MoveError TryPlay(int idx, Stone color, out int captured, List<int> capturedList = null)
        {
            captured = 0;
            if (idx < 0 || idx >= Count) return MoveError.OutOfBounds;
            if (Cells[idx] != Stone.Empty) return MoveError.Occupied;
            if (idx == KoPoint) return MoveError.Ko;

            Stone opp = color.Opp();
            Cells[idx] = color;
            int lastCap = -1;
            var nb = Neighbors[idx];
            for (int k = 0; k < nb.Length; k++)
            {
                int n = nb[k];
                if (Cells[n] == opp && Liberties(n) == 0)
                {
                    lastCap = n;
                    captured += RemoveGroup(n, capturedList);
                }
            }

            int ownLibs = -1;
            if (captured == 0)
            {
                ownLibs = Liberties(idx);
                if (ownLibs == 0)
                {
                    Cells[idx] = Stone.Empty;
                    return MoveError.Suicide;
                }
            }

            Hash ^= zobrist[idx * 3 + (int)color];
            KoPoint = -1;
            if (captured == 1)
            {
                bool lone = true;
                for (int k = 0; k < nb.Length; k++) if (Cells[nb[k]] == color) { lone = false; break; }
                if (lone && Liberties(idx) == 1) KoPoint = lastCap;
            }
            return MoveError.None;
        }

        public bool IsLegal(int idx, Stone color)
        {
            if (idx < 0 || idx >= Count || Cells[idx] != Stone.Empty || idx == KoPoint) return false;
            Stone opp = color.Opp();
            var nb = Neighbors[idx];
            for (int k = 0; k < nb.Length; k++)
                if (Cells[nb[k]] == Stone.Empty) return true;
            // temporarily place to evaluate
            Cells[idx] = color;
            bool ok = false;
            for (int k = 0; k < nb.Length && !ok; k++)
            {
                int n = nb[k];
                if (Cells[n] == opp) { if (Liberties(n) == 0) ok = true; }
            }
            if (!ok) ok = Liberties(idx) > 0;
            Cells[idx] = Stone.Empty;
            return ok;
        }

        /// <summary>True if filling this point would destroy one of our own eyes (used by AI playouts).</summary>
        public bool IsOwnEye(int idx, Stone color)
        {
            if (Cells[idx] != Stone.Empty) return false;
            var nb = Neighbors[idx];
            for (int k = 0; k < nb.Length; k++) if (Cells[nb[k]] != color) return false;
            var dg = Diagonals[idx];
            int bad = nb.Length < 4 ? 1 : 0;
            Stone opp = color.Opp();
            for (int k = 0; k < dg.Length; k++) if (Cells[dg[k]] == opp) bad++;
            return bad < 2;
        }

        /// <summary>
        /// Area ownership: living stones plus empty/dead regions bordered by only one colour.
        /// <paramref name="owner"/> receives the owner of every point (Empty = neutral).
        /// </summary>
        public void Territory(bool[] dead, Stone[] owner, out int black, out int white)
        {
            black = 0; white = 0;
            stamp++;
            for (int i = 0; i < Count; i++)
            {
                owner[i] = Stone.Empty;
                if (Cells[i] != Stone.Empty && (dead == null || !dead[i]))
                {
                    owner[i] = Cells[i];
                    if (Cells[i] == Stone.Black) black++; else white++;
                }
            }

            for (int i = 0; i < Count; i++)
            {
                bool alive = Cells[i] != Stone.Empty && (dead == null || !dead[i]);
                if (alive || visited[i] == stamp) continue;

                int sp = 0, rc = 0;
                bool tb = false, tw = false;
                stack[sp++] = i;
                visited[i] = stamp;
                while (sp > 0)
                {
                    int p = stack[--sp];
                    region[rc++] = p;
                    var nb = Neighbors[p];
                    for (int k = 0; k < nb.Length; k++)
                    {
                        int n = nb[k];
                        bool nAlive = Cells[n] != Stone.Empty && (dead == null || !dead[n]);
                        if (nAlive)
                        {
                            if (Cells[n] == Stone.Black) tb = true; else tw = true;
                        }
                        else if (visited[n] != stamp)
                        {
                            visited[n] = stamp;
                            stack[sp++] = n;
                        }
                    }
                }

                Stone o = Stone.Empty;
                if (tb && !tw) { o = Stone.Black; black += rc; }
                else if (tw && !tb) { o = Stone.White; white += rc; }
                if (o != Stone.Empty) for (int k = 0; k < rc; k++) owner[region[k]] = o;
            }
        }

        /// <summary>Marks the whole chain containing <paramref name="idx"/> in <paramref name="mask"/>.</summary>
        public void FloodChain(int idx, bool[] mask, bool value)
        {
            var group = new List<int>();
            Liberties(idx, group);
            foreach (int i in group) mask[i] = value;
        }
    }
}
