using System;
using System.Collections.Generic;

namespace Weiqi.Core
{
    public enum GamePhase { Playing, Scoring, Finished }

    public struct ScoreResult
    {
        public int BlackArea, WhiteArea;
        public float Komi;
        public float BlackTotal => BlackArea;
        public float WhiteTotal => WhiteArea + Komi;
        public Stone Winner => BlackTotal > WhiteTotal ? Stone.Black : Stone.White;
        public float Margin => Math.Abs(BlackTotal - WhiteTotal);
    }

    /// <summary>Rules and flow of one game: turns, passes, positional superko, scoring phase, undo.</summary>
    public sealed class GoGame
    {
        sealed class Snapshot
        {
            public Stone[] Cells; public ulong Hash; public int Ko;
            public Stone ToMove; public int CapB, CapW, LastMove, Passes;
        }

        public const int PassMove = -1;

        public readonly int Size;
        public readonly float Komi;
        public readonly GoBoard Board;
        public Stone ToMove { get; private set; } = Stone.Black;
        public GamePhase Phase { get; private set; } = GamePhase.Playing;
        public int CapturedByBlack { get; private set; }
        public int CapturedByWhite { get; private set; }
        public int LastMove { get; private set; } = -2;   // -2 none, -1 pass, >=0 point
        public int ConsecutivePasses { get; private set; }
        public int MoveNumber => history.Count;
        public bool Resigned { get; private set; }
        public Stone ResignedBy { get; private set; }
        public ScoreResult? Result { get; private set; }
        public readonly bool[] Dead;
        public readonly Stone[] Owner;

        public event Action<int, Stone, List<int>> MovePlayed;   // point, colour, captured points
        public event Action<Stone> Passed;
        public event Action<MoveError> MoveRejected;
        public event Action PhaseChanged;
        public event Action StateReset;                          // undo / resume

        readonly List<Snapshot> history = new List<Snapshot>();
        readonly HashSet<ulong> seen = new HashSet<ulong>();
        readonly GoBoard scratch;
        readonly List<int> capBuf = new List<int>();

        public GoGame(int size, float komi)
        {
            Size = size;
            Komi = komi;
            Board = new GoBoard(size);
            scratch = new GoBoard(size);
            Dead = new bool[Board.Count];
            Owner = new Stone[Board.Count];
            seen.Add(Board.Hash);
        }

        public static float DefaultKomi(int size) => size <= 9 ? 7f : 7.5f;

        Snapshot Capture() => new Snapshot
        {
            Cells = (Stone[])Board.Cells.Clone(), Hash = Board.Hash, Ko = Board.KoPoint,
            ToMove = ToMove, CapB = CapturedByBlack, CapW = CapturedByWhite,
            LastMove = LastMove, Passes = ConsecutivePasses
        };

        public bool CheckLegal(int idx, out MoveError error)
        {
            error = MoveError.None;
            if (Phase != GamePhase.Playing) { error = MoveError.GameOver; return false; }
            if (idx < 0 || idx >= Board.Count) { error = MoveError.OutOfBounds; return false; }
            scratch.CopyFrom(Board);
            error = scratch.TryPlay(idx, ToMove, out _);
            if (error == MoveError.None && seen.Contains(scratch.Hash)) error = MoveError.Ko;
            return error == MoveError.None;
        }

        public MoveError Play(int idx)
        {
            if (!CheckLegal(idx, out var err)) { MoveRejected?.Invoke(err); return err; }

            history.Add(Capture());
            capBuf.Clear();
            Stone color = ToMove;
            Board.TryPlay(idx, color, out int n, capBuf);
            if (color == Stone.Black) CapturedByBlack += n; else CapturedByWhite += n;
            seen.Add(Board.Hash);
            LastMove = idx;
            ConsecutivePasses = 0;
            ToMove = color.Opp();
            MovePlayed?.Invoke(idx, color, new List<int>(capBuf));
            return MoveError.None;
        }

        public void Pass()
        {
            if (Phase != GamePhase.Playing) return;
            history.Add(Capture());
            Stone who = ToMove;
            Board.Pass();
            LastMove = PassMove;
            ConsecutivePasses++;
            ToMove = who.Opp();
            Passed?.Invoke(who);
            if (ConsecutivePasses >= 2) EnterScoring();
        }

        void EnterScoring()
        {
            Array.Clear(Dead, 0, Dead.Length);
            Phase = GamePhase.Scoring;
            RefreshOwner();
            PhaseChanged?.Invoke();
        }

        public void RefreshOwner() => Board.Territory(Dead, Owner, out _, out _);

        public ScoreResult CurrentScore()
        {
            Board.Territory(Dead, Owner, out int b, out int w);
            return new ScoreResult { BlackArea = b, WhiteArea = w, Komi = Komi };
        }

        /// <summary>Scoring phase: toggles the chain at idx between alive and dead.</summary>
        public void ToggleDead(int idx)
        {
            if (Phase != GamePhase.Scoring || idx < 0 || idx >= Board.Count) return;
            if (Board.Cells[idx] == Stone.Empty) return;
            Board.FloodChain(idx, Dead, !Dead[idx]);
            RefreshOwner();
            PhaseChanged?.Invoke();
        }

        public void ConfirmScore()
        {
            if (Phase != GamePhase.Scoring) return;
            Result = CurrentScore();
            Phase = GamePhase.Finished;
            PhaseChanged?.Invoke();
        }

        public void ResumePlay()
        {
            if (Phase != GamePhase.Scoring) return;
            Array.Clear(Dead, 0, Dead.Length);
            ConsecutivePasses = 0;
            Phase = GamePhase.Playing;
            StateReset?.Invoke();
            PhaseChanged?.Invoke();
        }

        public void Resign(Stone who)
        {
            if (Phase == GamePhase.Finished) return;
            Resigned = true;
            ResignedBy = who;
            Phase = GamePhase.Finished;
            PhaseChanged?.Invoke();
        }

        public bool CanUndo => history.Count > 0 && Phase != GamePhase.Finished;

        public bool Undo()
        {
            if (!CanUndo) return false;
            var s = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            if (s.Hash != Board.Hash) seen.Remove(Board.Hash);   // a pass leaves the position unchanged
            Array.Copy(s.Cells, Board.Cells, Board.Count);
            Board.Hash = s.Hash; Board.KoPoint = s.Ko;
            ToMove = s.ToMove; CapturedByBlack = s.CapB; CapturedByWhite = s.CapW;
            LastMove = s.LastMove; ConsecutivePasses = s.Passes;
            Array.Clear(Dead, 0, Dead.Length);
            bool wasScoring = Phase == GamePhase.Scoring;
            Phase = GamePhase.Playing;
            StateReset?.Invoke();
            if (wasScoring) PhaseChanged?.Invoke();
            return true;
        }
    }
}
