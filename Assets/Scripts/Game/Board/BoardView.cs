using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Weiqi.Core;
using Weiqi.UI;

namespace Weiqi.Board
{
    /// <summary>
    /// Presents a GoGame through the Weiqi/Board shader. The board state lives in a tiny point-filtered texture;
    /// this component animates stone drops / capture fades and translates pointer input to intersections.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class BoardView : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler, IPointerClickHandler, IPointerEnterHandler
    {
        const float Zoom = 1.12f, Pad = 0.9f;

        public event Action<int> PointClicked;
        public event Action<int> PointHovered;
        public bool InputEnabled = true;
        bool showTerritory;
        public bool ShowTerritory { get => showTerritory; set { showTerritory = value; dirty = true; } }

        RawImage img;
        Material mat;
        RectTransform rt;
        Texture2D tex;
        Color32[] px;
        GoGame game;
        int n;

        float[] anim, ghostLife, ghostDelay;
        Stone[] ghostColor;
        int hover = -1;
        bool dirty;
        float reveal;
        float revealTarget = 1f;
        float pulse, pulseTarget;
        Stone hoverColor = Stone.Black;
        bool animating;
        float shakeT;

        static readonly int IdState = Shader.PropertyToID("_State");
        static readonly int IdN = Shader.PropertyToID("_N");
        static readonly int IdHover = Shader.PropertyToID("_Hover");
        static readonly int IdReveal = Shader.PropertyToID("_Reveal");
        static readonly int IdPulse = Shader.PropertyToID("_Pulse");

        void Awake()
        {
            img = GetComponent<RawImage>();
            rt = (RectTransform)transform;
            mat = new Material(UIKit.BoardShader) { hideFlags = HideFlags.HideAndDontSave };
            img.material = mat;
            img.color = Color.white;
            img.raycastTarget = true;
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (tex != null) Destroy(tex);
        }

        public void Bind(GoGame g)
        {
            if (game != null) Unhook();
            game = g;
            n = g.Size;
            if (tex != null) Destroy(tex);
            tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            px = new Color32[n * n];
            anim = new float[n * n];
            ghostLife = new float[n * n];
            ghostDelay = new float[n * n];
            ghostColor = new Stone[n * n];
            for (int i = 0; i < anim.Length; i++) anim[i] = 1f;
            mat.SetTexture(IdState, tex);
            mat.SetFloat(IdN, n);
            hover = -1;
            reveal = 0f; revealTarget = 1f;
            pulse = 0f; pulseTarget = 0f;

            game.MovePlayed += OnMovePlayed;
            game.StateReset += OnStateReset;
            game.PhaseChanged += OnPhaseChanged;
            dirty = true;
        }

        void Unhook()
        {
            game.MovePlayed -= OnMovePlayed;
            game.StateReset -= OnStateReset;
            game.PhaseChanged -= OnPhaseChanged;
        }

        void OnMovePlayed(int idx, Stone color, List<int> captured)
        {
            anim[idx] = 0f;
            ghostLife[idx] = 0f;
            for (int k = 0; k < captured.Count; k++)
            {
                int c = captured[k];
                ghostColor[c] = color.Opp();
                ghostLife[c] = 1f;
                ghostDelay[c] = 0.04f * k;
                anim[c] = 1f;
            }
            dirty = true;
        }

        void OnStateReset()
        {
            Array.Clear(ghostLife, 0, ghostLife.Length);
            for (int i = 0; i < anim.Length; i++) anim[i] = 1f;
            dirty = true;
        }

        void OnPhaseChanged() { dirty = true; }

        public void SetVictoryGlow(bool on) { pulseTarget = on ? 1f : 0f; }
        public void SetHoverColor(Stone c) { hoverColor = c; dirty = true; }

        public void Shake()
        {
            shakeT = 0.28f;
        }

        void Update()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime;

            reveal = Mathf.MoveTowards(reveal, revealTarget, dt * 1.6f);
            pulse = Mathf.MoveTowards(pulse, pulseTarget, dt * 1.2f);
            mat.SetFloat(IdReveal, reveal);
            mat.SetFloat(IdPulse, pulse);

            animating = false;
            for (int i = 0; i < anim.Length; i++)
            {
                if (anim[i] < 1f) { anim[i] = Mathf.Min(1f, anim[i] + dt / 0.28f); animating = true; }
                if (ghostLife[i] > 0f)
                {
                    if (ghostDelay[i] > 0f) ghostDelay[i] -= dt;
                    else ghostLife[i] = Mathf.Max(0f, ghostLife[i] - dt / 0.45f);
                    animating = true;
                }
            }

            if (shakeT > 0f)
            {
                shakeT -= dt;
                float k = Mathf.Clamp01(shakeT / 0.28f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(shakeT * 60f) * 1.4f * k);
            }
            else rt.localRotation = Quaternion.identity;

            if (dirty || animating) Upload();
            mat.SetVector(IdHover, HoverVector());
        }

        Vector4 HoverVector()
        {
            if (hover < 0 || !InputEnabled || game.Phase != GamePhase.Playing || game.Board.Cells[hover] != Stone.Empty)
                return new Vector4(-1, -1, 0, 0);
            bool legal = game.CheckLegal(hover, out _);
            return new Vector4(hover % n, hover / n, (float)hoverColor, legal ? 1f : 0.5f);
        }

        void Upload()
        {
            dirty = false;
            var cells = game.Board.Cells;
            var scoring = game.Phase != GamePhase.Playing;
            bool terr = !scoring && showTerritory;
            if (terr) game.RefreshOwner();
            for (int i = 0; i < cells.Length; i++)
            {
                byte r = 0, g = 0, b = 255, a = 255;
                Stone s = cells[i];
                if (s == Stone.Empty && ghostLife[i] > 0f)
                {
                    s = ghostColor[i];
                    a = (byte)Mathf.RoundToInt(Mathf.Clamp01(ghostLife[i]) * 255f);
                }
                if (s != Stone.Empty)
                {
                    r = (byte)(s == Stone.Black ? 100 : 200);
                    b = (byte)Mathf.RoundToInt(anim[i] * 255f);
                }
                if (scoring)
                {
                    if (cells[i] != Stone.Empty) { if (game.Dead[i]) g = 200; }
                    else if (game.Owner[i] == Stone.Black) g = 100;
                    else if (game.Owner[i] == Stone.White) g = 150;
                }
                else
                {
                    if (i == game.LastMove) g = 50;
                    else if (terr && cells[i] == Stone.Empty)
                    {
                        if (game.Owner[i] == Stone.Black) g = 100;
                        else if (game.Owner[i] == Stone.White) g = 150;
                    }
                }

                px[i] = new Color32(r, g, b, a);
            }

            tex.SetPixels32(px);
            tex.Apply(false);
        }

        // ------------------------------------------------------------ input

        bool TryGetPoint(PointerEventData e, out int idx)
        {
            idx = -1;
            if (game == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local))
                return false;
            var rect = rt.rect;
            float u = (local.x - rect.xMin) / rect.width;
            float v = (local.y - rect.yMin) / rect.height;
            float bu = (u - 0.5f) * Zoom + 0.5f;
            float bv = (v - 0.5f) * Zoom + 0.5f;
            float span = n - 1 + 2 * Pad;
            float px = bu * span - Pad, py = bv * span - Pad;
            int cx = Mathf.RoundToInt(px), cy = Mathf.RoundToInt(py);
            if (cx < 0 || cy < 0 || cx >= n || cy >= n) return false;
            if (Mathf.Abs(px - cx) > 0.5f || Mathf.Abs(py - cy) > 0.5f) return false;
            idx = cy * n + cx;
            return true;
        }

        public void OnPointerMove(PointerEventData e)
        {
            int idx;
            int newHover = TryGetPoint(e, out idx) ? idx : -1;
            if (newHover != hover)
            {
                hover = newHover;
                PointHovered?.Invoke(hover);
            }
        }

        public void OnPointerEnter(PointerEventData e) => OnPointerMove(e);

        public void OnPointerExit(PointerEventData e)
        {
            hover = -1;
            PointHovered?.Invoke(-1);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!InputEnabled) return;
            if (TryGetPoint(e, out int idx)) PointClicked?.Invoke(idx);
        }
    }
}
