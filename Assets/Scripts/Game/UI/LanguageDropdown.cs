using UnityEngine;
using UnityEngine.UI;

namespace Weiqi.UI
{
    /// <summary>Drop-up language picker: a head button showing the current language and a grid of choices.</summary>
    public sealed class LanguageDropdown
    {
        readonly FancyButton head;
        readonly RectTransform list;
        readonly FancyButton[] items;

        public LanguageDropdown(RectTransform parent, Vector2 size, Vector2 pos)
        {
            head = UIKit.Button(parent, "", Toggle, false, 28);
            Anchor((RectTransform)head.transform, size, pos, new Vector2(0.5f, 1f));

            // popup grid opens above the head so it never runs off the bottom of the screen
            var skin = UIKit.Panel(parent, "LanguageList", Palette.GlassTop, Palette.GlassBottom, 26f, true);
            list = (RectTransform)skin.transform;
            int n = Loc.NativeNames.Length;
            int rows = (n + 1) / 2;
            const float cellH = 58f, gap = 10f, pad = 14f;
            float w = size.x + 60f;
            float h = rows * cellH + (rows - 1) * gap + pad * 2;
            Anchor(list, new Vector2(w, h), pos + new Vector2(0, 12f), new Vector2(0.5f, 0f));
            // pivot bottom: anchoredPosition y is the bottom edge, which must sit above the head's top edge
            list.anchoredPosition = new Vector2(pos.x, pos.y + 12f);

            var grid = list.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
            grid.spacing = new Vector2(gap, gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.cellSize = new Vector2((w - pad * 2 - gap) * 0.5f, cellH);

            items = new FancyButton[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                items[i] = UIKit.Button(list, Loc.NativeNames[i], () => { Loc.Set((Language)idx); Close(); }, false, 26);
            }
            list.gameObject.SetActive(false);
            Loc.Changed += Refresh;
            Refresh();
        }

        static void Anchor(RectTransform r, Vector2 size, Vector2 pos, Vector2 pivot)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = pivot;
            r.sizeDelta = size;
            r.anchoredPosition = pos;
        }

        void Toggle()
        {
            bool open = !list.gameObject.activeSelf;
            list.gameObject.SetActive(open);
            if (open) list.SetAsLastSibling();
        }

        void Close() => list.gameObject.SetActive(false);

        void Refresh()
        {
            head.Label.text = Loc.NativeNames[(int)Loc.Current] + "  ▼";
            for (int i = 0; i < items.Length; i++) items[i].Selected = i == (int)Loc.Current;
        }
    }
}
