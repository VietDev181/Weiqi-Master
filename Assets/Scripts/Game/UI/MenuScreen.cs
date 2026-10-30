using System;
using UnityEngine;
using UnityEngine.UI;
using Weiqi.Core;
using Weiqi.Infrastructure;

namespace Weiqi.UI
{
    public enum GameMode { VsAI, VsHuman }

    public sealed class GameConfig
    {
        public GameMode Mode = GameMode.VsAI;
        public int Size = 9;
        public AiLevel Level = AiLevel.Normal;
        public Stone HumanColor = Stone.Black;
    }

    /// <summary>Start screen: emblem, title, game setup card, rules and sound toggles.</summary>
    public sealed class MenuScreen
    {
        public readonly RectTransform Root;
        public readonly CanvasGroup Group;
        public event Action<GameConfig> StartRequested;

        readonly GameConfig cfg = new GameConfig();
        readonly RectTransform left, card;
        readonly GameObject levelRow, sideRow;
        readonly GameObject rulesOverlay;
        readonly LanguageDropdown langDropdown;
        readonly FancyButton musicBtn, sfxBtn;
        Vector2 leftSize = new Vector2(780, 760), cardSize = new Vector2(840, 800);

        public MenuScreen(Transform parent)
        {
            Root = UIKit.Rect("MenuScreen", parent);
            UIKit.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();

            // ---------------- left: identity
            left = UIKit.Rect("Identity", Root);
            UIKit.Center(left, leftSize);

            var emblem = UIKit.Emblem(left, 0f, 0.9f);
            var er = emblem.rectTransform;
            er.anchorMin = er.anchorMax = new Vector2(0.5f, 1f);
            er.pivot = new Vector2(0.5f, 1f);
            er.sizeDelta = new Vector2(340, 340);
            er.anchoredPosition = new Vector2(0, -10);

            var title = UIKit.Label(left, "WEIQI MASTER", 100, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            var tr = title.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(leftSize.x + 200, 130);
            tr.anchoredPosition = new Vector2(0, -370);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.18f, 0.02f, 0.8f);
            outline.effectDistance = new Vector2(2, -2);

            var sub = UIKit.Label(left, "", 34, Palette.Ivory, TextAnchor.MiddleCenter, FontStyle.Italic);
            var sr = sub.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(leftSize.x, 60);
            sr.anchoredPosition = new Vector2(0, -505);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            Loc.Bind(sub, "sub");

            langDropdown = new LanguageDropdown(left, new Vector2(440, 64), new Vector2(0, -590));

            var footer = UIKit.Rect("Footer", left);
            footer.anchorMin = footer.anchorMax = new Vector2(0.5f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(leftSize.x, 76);
            footer.anchoredPosition = new Vector2(0, 20);
            var fl = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            fl.spacing = 16; fl.childForceExpandWidth = true; fl.childForceExpandHeight = true;
            fl.childControlWidth = true; fl.childControlHeight = true;

            rulesOverlay = BuildRules();
            UIKit.Button(footer, "howto", () => rulesOverlay.SetActive(true), false, 26);
            musicBtn = UIKit.Button(footer, "", ToggleMusic, false, 26);
            sfxBtn = UIKit.Button(footer, "", ToggleSfx, false, 26);
            Loc.Bind(footer.GetChild(0).GetComponentInChildren<Text>(), "howto");
            RefreshSoundLabels();
            Loc.Changed += RefreshSoundLabels;

            // ---------------- right: setup card
            card = UIKit.Rect("Card", Root);
            UIKit.Center(card, cardSize);
            var bg = UIKit.Panel(card, "Glass", Palette.GlassTop, Palette.GlassBottom, 36f);
            UIKit.Stretch((RectTransform)bg.transform);
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 44, 44);
            v.spacing = 10;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var mode = Row("mode.cap", new[] { "mode.ai", "mode.human" }, 0, null);
            mode.Changed += i => { cfg.Mode = i == 0 ? GameMode.VsAI : GameMode.VsHuman; RefreshRows(); };

            var size = Row("size.cap", new[] { "9 × 9", "13 × 13", "19 × 19" }, 0, null);
            size.Changed += i => cfg.Size = i == 0 ? 9 : i == 1 ? 13 : 19;

            var level = Row("level.cap", new[] { "level.easy", "level.normal", "level.hard" }, 1, out levelRow);
            level.Changed += i => cfg.Level = (AiLevel)i;

            var side = Row("side.cap", new[] { "side.black", "side.white" }, 0, out sideRow);
            side.Changed += i => cfg.HumanColor = i == 0 ? Stone.Black : Stone.White;

            var spacer = UIKit.Rect("Spacer", card);
            UIKit.Size(spacer.gameObject, 0, 14);

            var start = UIKit.Button(card, "start", () => StartRequested?.Invoke(new GameConfig
            {
                Mode = cfg.Mode, Size = cfg.Size, Level = cfg.Level, HumanColor = cfg.HumanColor
            }), true, 38);
            Loc.Bind(start.Label, "start");
            UIKit.Size(start.gameObject, 0, 100);
            rulesOverlay.transform.SetAsLastSibling();
        }

        Segmented Row(string caption, string[] options, int initial, GameObject _unused)
        {
            return Row(caption, options, initial, out _);
        }

        Segmented Row(string caption, string[] options, int initial, out GameObject rowObj)
        {
            var row = UIKit.Rect("Row", card);
            var vl = row.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 8; vl.childControlWidth = true; vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.padding = new RectOffset(0, 0, 8, 8);

            var cap = UIKit.Label(row, "", 24, Palette.Muted, TextAnchor.MiddleLeft, FontStyle.Bold, false);
            Loc.Bind(cap, caption);
            UIKit.Size(cap.gameObject, 0, 34);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            var seg = new Segmented(row, options, initial, 74f, 28);
            rowObj = row.gameObject;
            return seg;
        }

        void RefreshRows()
        {
            bool ai = cfg.Mode == GameMode.VsAI;
            levelRow.SetActive(ai);
            sideRow.SetActive(ai);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        void ToggleMusic() { AudioService.Instance.SetMusic(!AudioService.Instance.MusicOn); RefreshSoundLabels(); }
        void ToggleSfx() { AudioService.Instance.SetSfx(!AudioService.Instance.SfxOn); RefreshSoundLabels(); }

        void RefreshSoundLabels()
        {
            var a = AudioService.Instance;
            if (a == null) return;
            musicBtn.Label.text = Loc.T(a.MusicOn ? "music.on" : "music.off");
            sfxBtn.Label.text = Loc.T(a.SfxOn ? "sfx.on" : "sfx.off");
            musicBtn.Selected = a.MusicOn;
            sfxBtn.Selected = a.SfxOn;
        }

        GameObject BuildRules()
        {
            var overlay = UIKit.Panel(Root, "Rules", new Color(0, 0, 0, 0.68f), new Color(0, 0, 0, 0.68f), 0f, true);
            UIKit.Stretch((RectTransform)overlay.transform);
            var panel = UIKit.Panel(overlay.transform, "Panel", Palette.GlassTop, Palette.GlassBottom, 36f, true);
            UIKit.Center((RectTransform)panel.transform, new Vector2(1180, 820));

            var head = UIKit.Label(panel.transform, "", 52, Palette.Gold);
            Loc.Bind(head, "rules.title");
            var hr = head.rectTransform;
            hr.anchorMin = new Vector2(0, 1); hr.anchorMax = new Vector2(1, 1);
            hr.pivot = new Vector2(0.5f, 1); hr.sizeDelta = new Vector2(0, 90); hr.anchoredPosition = new Vector2(0, -26);

            var t = UIKit.Label(panel.transform, "", 30, Palette.Ivory, TextAnchor.UpperLeft, FontStyle.Normal, false);
            Loc.Bind(t, "rules.body");
            t.lineSpacing = 1.1f;
            var tr = t.rectTransform;
            tr.offsetMin = new Vector2(60, 130); tr.offsetMax = new Vector2(-60, -130);

            var close = UIKit.Button(panel.transform, "rules.ok", () => overlay.gameObject.SetActive(false), true, 34);
            Loc.Bind(close.Label, "rules.ok");
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(320, 84);
            cr.anchoredPosition = new Vector2(0, 28);

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        /// <summary>Arrange identity + card side by side (landscape) or stacked (portrait), scaled to fit.</summary>
        public void Layout(Vector2 canvas)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            cardSize = card.rect.size;
            bool wide = canvas.x >= canvas.y * 1.15f;
            if (wide)
            {
                float s = Mathf.Min(1f, canvas.y * 0.94f / Mathf.Max(leftSize.y, cardSize.y),
                    canvas.x * 0.5f * 0.96f / Mathf.Max(leftSize.x, cardSize.x));
                left.localScale = card.localScale = Vector3.one * s;
                left.anchoredPosition = new Vector2(-canvas.x * 0.25f, 0);
                card.anchoredPosition = new Vector2(canvas.x * 0.25f, 0);
            }
            else
            {
                float total = leftSize.y * 0.78f + cardSize.y;
                float s = Mathf.Min(1f, canvas.y * 0.96f / total, canvas.x * 0.96f / Mathf.Max(leftSize.x, cardSize.x));
                left.localScale = card.localScale = Vector3.one * s;
                float top = canvas.y * 0.5f - 10f;
                left.anchoredPosition = new Vector2(0, top - leftSize.y * 0.5f * s);
                card.anchoredPosition = new Vector2(0, -canvas.y * 0.5f + cardSize.y * 0.5f * s + 20f);
            }
        }
    }
}
