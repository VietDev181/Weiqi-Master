using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Weiqi.Board;
using Weiqi.Core;
using Weiqi.Infrastructure;

namespace Weiqi.UI
{
    /// <summary>In-game HUD plus the glue between GoGame, the AI, the board view and audio.</summary>
    public sealed class GameScreen : MonoBehaviour
    {
        sealed class PlayerCard
        {
            public RectTransform Root;
            public PanelSkin Skin;
            public Text Name, Info;
            public float Glow;
        }

        public RectTransform Root;
        public CanvasGroup Group;
        public event Action MenuRequested;

        BoardView board;
        RectTransform boardRt;
        PlayerCard blackCard, whiteCard;
        Text status;
        RectTransform statusRt;
        PanelSkin statusSkin;
        RectTransform playRow, scoreRow;
        FancyButton undoBtn, passBtn, resignBtn, terrBtn;
        GameObject resultOverlay;
        RectTransform resultCard;
        Text resultTitle, resultBody;

        GameConfig cfg;
        GoGame game;
        GoAI ai;
        Stone aiColor;
        Task<int> aiTask;
        float aiStarted;
        int aiVersion;
        string flashMessage, flashKey;
        float flashUntil;
        bool lastPassNotice;

        const float MinThink = 0.55f;

        public static GameScreen Create(Transform parent)
        {
            var rt = UIKit.Rect("GameScreen", parent);
            UIKit.Stretch(rt);
            var gs = rt.gameObject.AddComponent<GameScreen>();
            gs.Root = rt;
            gs.Group = rt.gameObject.AddComponent<CanvasGroup>();
            gs.Build();
            return gs;
        }

        // ================================================================== build

        void Build()
        {
            var bimg = UIKit.Rect("Board", Root);
            boardRt = bimg;
            bimg.gameObject.AddComponent<RawImage>();
            board = bimg.gameObject.AddComponent<BoardView>();
            board.PointClicked += OnPointClicked;

            blackCard = BuildCard("BlackCard", 1f);
            whiteCard = BuildCard("WhiteCard", 2f);

            statusSkin = UIKit.Panel(Root, "Status", new Color(0.16f, 0.18f, 0.27f, 0.88f), new Color(0.07f, 0.08f, 0.14f, 0.9f), 30f);
            statusRt = (RectTransform)statusSkin.transform;
            status = UIKit.Label(statusRt, "", 36, Palette.Ivory);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 20;
            status.resizeTextMaxSize = 36;
            UIKit.Stretch(status.rectTransform, 12);

            playRow = Row("PlayRow");
            undoBtn = Btn(playRow, "undo", OnUndo, false, 30);
            passBtn = Btn(playRow, "pass", OnPass, false, 30);
            resignBtn = Btn(playRow, "resign", OnResign, false, 30);
            terrBtn = Btn(playRow, "terr", () => { board.ShowTerritory = !board.ShowTerritory; terrBtn.Selected = board.ShowTerritory; }, false, 30);
            Btn(playRow, "menu", () => MenuRequested?.Invoke(), false, 30);

            scoreRow = Row("ScoreRow");
            Btn(scoreRow, "resume", () => { game.ResumePlay(); AfterStateChange(); }, false, 30);
            Btn(scoreRow, "confirm", () => game.ConfirmScore(), true, 32);

            BuildResult();
        }

        static FancyButton Btn(Transform parent, string key, Action onClick, bool primary, int size)
        {
            var b = UIKit.Button(parent, key, onClick, primary, size);
            Loc.Bind(b.Label, key);
            return b;
        }

        RectTransform Row(string name)
        {
            var r = UIKit.Rect(name, Root);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 18; h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            return r;
        }

        PlayerCard BuildCard(string name, float mode)
        {
            var c = new PlayerCard();
            c.Skin = UIKit.Panel(Root, name, Palette.GlassTop, Palette.GlassBottom, 30f);
            c.Root = (RectTransform)c.Skin.transform;
            var icon = UIKit.Emblem(c.Root, mode, 0.0f);
            var ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0, 0.5f);
            ir.pivot = new Vector2(0, 0.5f);
            ir.sizeDelta = new Vector2(110, 110);
            ir.anchoredPosition = new Vector2(22, 0);

            c.Name = UIKit.Label(c.Root, "", 34, Palette.Ivory, TextAnchor.UpperLeft);
            var nr = c.Name.rectTransform;
            nr.offsetMin = new Vector2(150, 0); nr.offsetMax = new Vector2(-14, -24);
            c.Name.resizeTextForBestFit = true; c.Name.resizeTextMinSize = 18; c.Name.resizeTextMaxSize = 34;

            c.Info = UIKit.Label(c.Root, "", 26, Palette.Muted, TextAnchor.LowerLeft, FontStyle.Normal, false);
            var ifr = c.Info.rectTransform;
            ifr.offsetMin = new Vector2(150, 22); ifr.offsetMax = new Vector2(-14, 0);
            c.Info.resizeTextForBestFit = true; c.Info.resizeTextMinSize = 16; c.Info.resizeTextMaxSize = 26;
            return c;
        }

        void BuildResult()
        {
            var dim = UIKit.Panel(Root, "Result", new Color(0, 0, 0, 0.62f), new Color(0, 0, 0, 0.62f), 0f, true);
            UIKit.Stretch((RectTransform)dim.transform);
            resultOverlay = dim.gameObject;

            var card = UIKit.Panel(dim.transform, "Card", Palette.GlassTop, Palette.GlassBottom, 40f, true);
            resultCard = (RectTransform)card.transform;
            UIKit.Center(resultCard, new Vector2(960, 560));

            resultTitle = UIKit.Label(card.transform, "", 84, Palette.Gold);
            var tr = resultTitle.rectTransform;
            tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1);
            tr.sizeDelta = new Vector2(0, 130); tr.anchoredPosition = new Vector2(0, -40);
            resultTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            resultBody = UIKit.Label(card.transform, "", 36, Palette.Ivory, TextAnchor.UpperCenter, FontStyle.Normal, false);
            var br = resultBody.rectTransform;
            br.offsetMin = new Vector2(50, 150); br.offsetMax = new Vector2(-50, -190);

            var row = UIKit.Rect("Buttons", card.transform);
            row.anchorMin = new Vector2(0, 0); row.anchorMax = new Vector2(1, 0);
            row.pivot = new Vector2(0.5f, 0); row.sizeDelta = new Vector2(-100, 100); row.anchoredPosition = new Vector2(0, 40);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24; h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            Btn(row, "viewboard", () => resultOverlay.SetActive(false), false, 30);
            Btn(row, "tomenu", () => MenuRequested?.Invoke(), false, 30);
            Btn(row, "newgame", () => StartGame(cfg), true, 32);
            resultOverlay.SetActive(false);
        }

        // ================================================================== layout

        public void Layout(Vector2 c)
        {
            bool wide = c.x >= c.y * 1.1f;
            float S;
            if (wide)
            {
                S = Mathf.Min(c.y - 250f, c.x * 0.52f);
                float cy = -10f;
                Place(boardRt, new Vector2(S, S), new Vector2(0, cy));
                float cardW = Mathf.Clamp((c.x - S) * 0.5f - 50f, 200f, 380f);
                float cardH = 190f;
                float off = S * 0.5f + 28f + cardW * 0.5f;
                Place(blackCard.Root, new Vector2(cardW, cardH), new Vector2(-off, cy + S * 0.5f - cardH * 0.5f - 40f));
                Place(whiteCard.Root, new Vector2(cardW, cardH), new Vector2(off, cy + S * 0.5f - cardH * 0.5f - 40f));
                Place(statusRt, new Vector2(Mathf.Min(1100f, c.x - 100f), 84f), new Vector2(0, c.y * 0.5f - 72f));
                PlaceRow(playRow, new Vector2(Mathf.Min(1000f, c.x - 60f), 88f), new Vector2(0, -c.y * 0.5f + 66f));
                PlaceRow(scoreRow, new Vector2(Mathf.Min(820f, c.x - 60f), 88f), new Vector2(0, -c.y * 0.5f + 66f));
            }
            else
            {
                S = Mathf.Min(c.x - 40f, c.y - 640f);
                float cardW = (c.x - 70f) * 0.5f;
                float cardH = 160f;
                Place(statusRt, new Vector2(c.x - 60f, 90f), new Vector2(0, c.y * 0.5f - 70f));
                Place(blackCard.Root, new Vector2(cardW, cardH), new Vector2(-cardW * 0.5f - 15f, c.y * 0.5f - 230f));
                Place(whiteCard.Root, new Vector2(cardW, cardH), new Vector2(cardW * 0.5f + 15f, c.y * 0.5f - 230f));
                Place(boardRt, new Vector2(S, S), new Vector2(0, -20f));
                PlaceRow(playRow, new Vector2(c.x - 40f, 96f), new Vector2(0, -c.y * 0.5f + 70f));
                PlaceRow(scoreRow, new Vector2(c.x - 40f, 96f), new Vector2(0, -c.y * 0.5f + 70f));
            }
            float rs = Mathf.Min(1f, (c.x - 40f) / 960f);
            resultCard.localScale = Vector3.one * rs;
        }

        static void Place(RectTransform r, Vector2 size, Vector2 pos)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }

        static void PlaceRow(RectTransform r, Vector2 size, Vector2 pos) => Place(r, size, pos);

        // ================================================================== game flow

        public void StartGame(GameConfig config)
        {
            cfg = config;
            aiVersion++;
            aiTask = null;
            flashMessage = null;
            lastPassNotice = false;
            resultOverlay.SetActive(false);

            if (game != null)
            {
                game.MovePlayed -= OnMovePlayed;
                game.Passed -= OnPassed;
                game.PhaseChanged -= OnPhaseChanged;
            }

            game = new GoGame(cfg.Size, GoGame.DefaultKomi(cfg.Size));
            game.MovePlayed += OnMovePlayed;
            game.Passed += OnPassed;
            game.PhaseChanged += OnPhaseChanged;

            if (cfg.Mode == GameMode.VsAI)
            {
                ai = new GoAI(cfg.Level);
                aiColor = cfg.HumanColor.Opp();
            }
            else ai = null;

            board.Bind(game);
            board.InputEnabled = true;
            board.SetVictoryGlow(false);
            board.ShowTerritory = false;
            terrBtn.Selected = false;
            board.SetHoverColor(Stone.Black);
            blackCard.Glow = whiteCard.Glow = 0f;

            SetNames();

            AudioService.Instance.PlayMusic("colorful-candy-321734");
            AudioService.Instance.Play(Sfx.Start);
            AfterStateChange();
        }

        void SetNames()
        {
            if (cfg == null) return;
            if (cfg.Mode == GameMode.VsAI)
            {
                string lv = Loc.T(cfg.Level == AiLevel.Easy ? "level.easy" : cfg.Level == AiLevel.Normal ? "level.normal" : "level.hard");
                string cpu = Loc.T("cpu", lv), you = Loc.T("you");
                blackCard.Name.text = cfg.HumanColor == Stone.Black ? you : cpu;
                whiteCard.Name.text = cfg.HumanColor == Stone.White ? you : cpu;
            }
            else
            {
                blackCard.Name.text = Loc.T("p1");
                whiteCard.Name.text = Loc.T("p2");
            }
        }

        void OnLanguageChanged()
        {
            SetNames();
            RefreshHud();
            if (resultOverlay.activeSelf && game != null && game.Phase == GamePhase.Finished) FillResult();
        }

        void OnEnable() { Loc.Changed += OnLanguageChanged; }
        void OnDisable() { Loc.Changed -= OnLanguageChanged; }

        public void Shutdown()
        {
            aiVersion++;
            aiTask = null;
        }

        bool IsAiTurn => ai != null && game.Phase == GamePhase.Playing && game.ToMove == aiColor;

        void OnMovePlayed(int idx, Stone color, System.Collections.Generic.List<int> captured)
        {
            lastPassNotice = false;
            AudioService.Instance.Play(color == Stone.Black ? Sfx.StoneBlack : Sfx.StoneWhite);
            if (captured.Count > 0)
                AudioService.Instance.Play(Sfx.Capture, Mathf.Clamp(0.6f + captured.Count * 0.1f, 0.6f, 1f));
            AfterStateChange();
        }

        void OnPassed(Stone who)
        {
            lastPassNotice = game.Phase == GamePhase.Playing;
            AudioService.Instance.Play(Sfx.Pass);
            AfterStateChange();
        }

        void OnPhaseChanged() { AfterStateChange(); }

        void AfterStateChange()
        {
            if (game == null) return;
            board.SetHoverColor(game.ToMove);
            bool playing = game.Phase == GamePhase.Playing;
            bool scoring = game.Phase == GamePhase.Scoring;
            playRow.gameObject.SetActive(game.Phase != GamePhase.Finished && playing);
            scoreRow.gameObject.SetActive(scoring);
            board.InputEnabled = game.Phase != GamePhase.Finished && !IsAiTurn;

            undoBtn.Interactable = game.CanUndo;
            passBtn.Interactable = playing && !IsAiTurn;
            resignBtn.Interactable = playing;

            if (game.Phase == GamePhase.Finished) ShowResult();
            else TryStartAi();
            RefreshHud();
        }

        void TryStartAi()
        {
            if (!IsAiTurn || aiTask != null) return;
            var snapshot = game.Board.Clone();
            bool passed = game.LastMove == GoGame.PassMove;
            float komi = game.Komi;
            Stone color = aiColor;
            int moveNo = game.MoveNumber;
            var brain = ai;
            int ver = ++aiVersion;
            aiStarted = Time.unscaledTime;
            board.InputEnabled = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            aiTask = Task.FromResult(brain.ChooseMove(snapshot, passed, komi, color, moveNo));
#else
            aiTask = Task.Run(() => brain.ChooseMove(snapshot, passed, komi, color, moveNo));
#endif
            aiTaskVersion = ver;
        }

        int aiTaskVersion;

        void Update()
        {
            AnimateHud();
            if (aiTask != null && aiTask.IsCompleted && Time.unscaledTime - aiStarted >= MinThink)
            {
                var t = aiTask;
                aiTask = null;
                if (aiTaskVersion == aiVersion && IsAiTurn && !t.IsFaulted)
                {
                    int mv = t.Result;
                    if (mv < 0) game.Pass();
                    else if (game.Play(mv) != MoveError.None) game.Pass();
                }
                else if (t.IsFaulted && aiTaskVersion == aiVersion && IsAiTurn) { Debug.LogException(t.Exception); game.Pass(); }
            }
            if (flashMessage != null && Time.unscaledTime > flashUntil) { flashMessage = null; RefreshHud(); }
            if (aiTask != null) RefreshHud();   // animated "thinking" dots
        }

        // ================================================================== input

        void OnPointClicked(int idx)
        {
            if (game == null) return;
            if (game.Phase == GamePhase.Playing)
            {
                if (IsAiTurn) return;
                var err = game.Play(idx);
                if (err != MoveError.None)
                {
                    AudioService.Instance.Play(Sfx.Illegal);
                    board.Shake();
                    flashKey = err == MoveError.Occupied ? "err.occupied"
                        : err == MoveError.Suicide ? "err.suicide"
                        : err == MoveError.Ko ? "err.ko" : "err.generic";
                    Flash();
                }
            }
            else if (game.Phase == GamePhase.Scoring)
            {
                if (game.Board.Cells[idx] != Stone.Empty)
                {
                    AudioService.Instance.Play(Sfx.Click, 0.8f);
                    game.ToggleDead(idx);
                }
            }
        }

        void OnPass()
        {
            if (game.Phase != GamePhase.Playing || IsAiTurn) return;
            game.Pass();
        }

        void OnUndo()
        {
            if (!game.CanUndo) return;
            aiVersion++; aiTask = null;
            if (ai == null) game.Undo();
            else
            {
                do { if (!game.Undo()) break; } while (game.ToMove != cfg.HumanColor);
            }
            flashMessage = null;
            lastPassNotice = false;
            AfterStateChange();
        }

        void OnResign()
        {
            if (game.Phase != GamePhase.Playing) return;
            aiVersion++; aiTask = null;
            game.Resign(ai != null ? cfg.HumanColor : game.ToMove);
        }

        void Flash()
        {
            flashMessage = flashKey;
            flashUntil = Time.unscaledTime + 2.2f;
            RefreshHud();
        }

        // ================================================================== HUD

        static string Name(Stone s) => Loc.T(s == Stone.Black ? "black" : "white");

        void RefreshHud()
        {
            if (game == null) return;
            var est = game.CurrentScore();
            blackCard.Info.text = Loc.T("captured", game.CapturedByBlack) + "\n" + Loc.T("est", est.BlackTotal.ToString("0.#"));
            whiteCard.Info.text = Loc.T("captured", game.CapturedByWhite) + "  ·  " + Loc.T("komi", game.Komi.ToString("0.#")) + "\n" + Loc.T("est", est.WhiteTotal.ToString("0.#"));

            string s;
            if (flashMessage != null) { s = "<color=#F08A74>" + Loc.T(flashMessage) + "</color>"; }
            else if (game.Phase == GamePhase.Scoring)
            {
                var sc = game.CurrentScore();
                s = Loc.T("st.scoring", sc.BlackTotal.ToString("0.#"), sc.WhiteTotal.ToString("0.#"));
            }
            else if (game.Phase == GamePhase.Finished) s = Loc.T("st.finished");
            else if (aiTask != null)
            {
                int dots = 1 + (int)(Time.unscaledTime * 3f) % 3;
                s = Loc.T("st.thinking") + new string('.', dots);
            }
            else if (ai != null) s = Loc.T("st.yourturn", Name(game.ToMove));
            else s = Loc.T("st.turn", Name(game.ToMove));
            if (flashMessage == null && lastPassNotice) s += Loc.T("st.passed");
            status.text = s;
        }

        void AnimateHud()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime * 8f;
            bool playing = game.Phase == GamePhase.Playing;
            AnimateCard(blackCard, playing && game.ToMove == Stone.Black, dt);
            AnimateCard(whiteCard, playing && game.ToMove == Stone.White, dt);
        }

        static void AnimateCard(PlayerCard c, bool active, float dt)
        {
            c.Glow = Mathf.MoveTowards(c.Glow, active ? 1f : 0f, dt);
            c.Skin.Glow = c.Glow * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f));
            float s = 1f + 0.03f * c.Glow;
            c.Root.localScale = new Vector3(s, s, 1f);
        }

        void ShowResult()
        {
            board.InputEnabled = false;
            FillResult();
            resultOverlay.SetActive(true);
            resultOverlay.transform.SetAsLastSibling();
            bool humanWon = HumanWon();
            board.SetVictoryGlow(humanWon);
            AudioService.Instance.Play(humanWon ? Sfx.Win : Sfx.Lose);
            AudioService.Instance.PlayMusic(humanWon ? "spring-day-318971" : "game-over-284367", 1.5f);
        }

        Stone Winner() => game.Resigned ? game.ResignedBy.Opp() : game.Result.Value.Winner;
        bool HumanWon() => ai == null || Winner() == cfg.HumanColor;

        void FillResult()
        {
            bool vsAi = ai != null;
            Stone winner = Winner();
            string body;
            if (game.Resigned) body = Loc.T("res.resigned", Name(game.ResignedBy));
            else
            {
                var r = game.Result.Value;
                body = Loc.T("res.score", r.BlackArea, r.WhiteArea, r.Komi.ToString("0.#"), r.WhiteTotal.ToString("0.#")) +
                       "\n\n" + Loc.T("res.margin", Name(winner), r.Margin.ToString("0.#"));
            }
            bool humanWon = HumanWon();
            resultTitle.text = vsAi ? Loc.T(humanWon ? "res.win" : "res.lose") : Loc.T("res.colorwins", Name(winner).ToUpper());
            resultTitle.color = humanWon ? Palette.Gold : Palette.Vermilion;
            resultBody.text = body;
        }
    }
}
