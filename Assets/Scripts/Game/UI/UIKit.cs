using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Weiqi.Infrastructure;

namespace Weiqi.UI
{
    public static class Palette
    {
        public static readonly Color Gold = new Color(0.96f, 0.78f, 0.38f, 1f);
        public static readonly Color Ivory = new Color(0.96f, 0.93f, 0.86f, 1f);
        public static readonly Color Muted = new Color(0.70f, 0.72f, 0.80f, 1f);
        public static readonly Color Vermilion = new Color(0.93f, 0.36f, 0.28f, 1f);
        public static readonly Color GlassTop = new Color(0.19f, 0.21f, 0.30f, 0.90f);
        public static readonly Color GlassBottom = new Color(0.08f, 0.09f, 0.15f, 0.92f);
        public static readonly Color ButtonTop = new Color(0.25f, 0.27f, 0.38f, 0.95f);
        public static readonly Color ButtonBottom = new Color(0.12f, 0.13f, 0.21f, 0.95f);
        public static readonly Color PrimaryTop = new Color(0.98f, 0.76f, 0.34f, 1f);
        public static readonly Color PrimaryBottom = new Color(0.78f, 0.47f, 0.15f, 1f);
    }

    /// <summary>Applies the Weiqi/Panel shader to a RawImage and keeps its pixel size in sync.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class PanelSkin : MonoBehaviour
    {
        static readonly int IdSize = Shader.PropertyToID("_Size");
        static readonly int IdHover = Shader.PropertyToID("_Hover");
        static readonly int IdPress = Shader.PropertyToID("_Press");
        static readonly int IdSelected = Shader.PropertyToID("_Selected");
        static readonly int IdGlow = Shader.PropertyToID("_Glow");
        static readonly int IdDisabled = Shader.PropertyToID("_Disabled");

        public RawImage Image { get; private set; }
        public Material Mat { get; private set; }
        RectTransform rt;

        public void Setup(Color top, Color bottom, Color accent, float radius)
        {
            Image = GetComponent<RawImage>();
            rt = (RectTransform)transform;
            Mat = new Material(UIKit.PanelShader) { hideFlags = HideFlags.HideAndDontSave };
            Mat.SetColor("_ColorTop", top);
            Mat.SetColor("_ColorBottom", bottom);
            Mat.SetColor("_Accent", accent);
            Mat.SetFloat("_Radius", radius);
            Image.material = Mat;
            Image.color = Color.white;
            SyncSize();
        }

        public float Hover { set { Mat.SetFloat(IdHover, value); } }
        public float Press { set { Mat.SetFloat(IdPress, value); } }
        public float Selected { set { Mat.SetFloat(IdSelected, value); } }
        public float Glow { set { Mat.SetFloat(IdGlow, value); } }
        public float Disabled { set { Mat.SetFloat(IdDisabled, value); } }

        void SyncSize()
        {
            if (Mat == null || rt == null) return;
            var r = rt.rect;
            Mat.SetVector(IdSize, new Vector4(r.width, r.height, 0, 0));
        }

        void OnRectTransformDimensionsChange() => SyncSize();
        void Start() => SyncSize();
        void OnDestroy() { if (Mat != null) Destroy(Mat); }
    }

    public sealed class FancyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public PanelSkin Skin;
        public Text Label;
        public Action Clicked;
        public bool Silent;

        bool interactable = true;
        bool selected;
        float hover, press, targetHover, targetPress, selectedV;
        Vector3 baseScale = Vector3.one;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (!value) { targetHover = 0; targetPress = 0; }
                Skin.Disabled = value ? 0f : 1f;
                if (Label != null) Label.color = value ? labelColor : labelColor * new Color(1, 1, 1, 0.5f);
            }
        }

        Color labelColor = Palette.Ivory;
        public void SetLabelColor(Color c) { labelColor = c; if (Label != null) Label.color = c; }

        public bool Selected
        {
            get => selected;
            set { selected = value; }
        }

        void Awake() { baseScale = transform.localScale; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime * 14f;
            hover = Mathf.MoveTowards(hover, targetHover, dt);
            press = Mathf.MoveTowards(press, targetPress, dt * 1.4f);
            selectedV = Mathf.MoveTowards(selectedV, selected ? 1f : 0f, dt);
            Skin.Hover = hover;
            Skin.Press = press;
            Skin.Selected = selectedV;
            float s = 1f + 0.035f * hover - 0.05f * press;
            transform.localScale = baseScale * s;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!interactable) return;
            targetHover = 1;
            if (!Silent) AudioService.Instance?.Play(Sfx.Hover, 0.5f);
        }
        public void OnPointerExit(PointerEventData e) { targetHover = 0; targetPress = 0; }
        public void OnPointerDown(PointerEventData e) { if (interactable) targetPress = 1; }
        public void OnPointerUp(PointerEventData e) { targetPress = 0; }
        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable) return;
            if (!Silent) AudioService.Instance?.Play(Sfx.Click);
            Clicked?.Invoke();
        }
    }

    /// <summary>Row of mutually exclusive FancyButtons (segmented control).</summary>
    public sealed class Segmented
    {
        public readonly RectTransform Root;
        readonly FancyButton[] buttons;
        public int Value { get; private set; }
        public event Action<int> Changed;

        public Segmented(Transform parent, string[] options, int initial, float height = 70f, int fontSize = 28)
        {
            Root = UIKit.Rect("Segmented", parent);
            var h = Root.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            h.childControlWidth = true; h.childControlHeight = true;
            var le = Root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height; le.minHeight = height;

            buttons = new FancyButton[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var b = UIKit.Button(Root, options[i], () => Set(idx, true), false, fontSize);
                Loc.Bind(b.Label, options[i]);
                buttons[i] = b;
            }
            Set(initial, false);
        }

        public void Set(int v, bool notify)
        {
            Value = v;
            for (int i = 0; i < buttons.Length; i++) buttons[i].Selected = i == v;
            if (notify) Changed?.Invoke(v);
        }
    }

    public static class UIKit
    {
        static Shader panelShader, boardShader, bgShader, emblemShader;
        static Font font;

        public static Shader PanelShader => panelShader ? panelShader : panelShader = Resources.Load<Shader>("Shaders/WeiqiPanel");
        public static Shader BoardShader => boardShader ? boardShader : boardShader = Resources.Load<Shader>("Shaders/WeiqiBoard");
        public static Shader BackgroundShader => bgShader ? bgShader : bgShader = Resources.Load<Shader>("Shaders/WeiqiBackground");
        public static Shader EmblemShader => emblemShader ? emblemShader : emblemShader = Resources.Load<Shader>("Shaders/WeiqiEmblem");

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Palatino", "Georgia", "Times New Roman", "PingFang SC", "Hiragino Sans", "Apple SD Gothic Neo", "Microsoft YaHei", "Yu Gothic", "Malgun Gothic", "Noto Sans CJK SC", "Arial" }, 40); }
                catch (Exception) { font = null; }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform r, float pad = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(pad, pad); r.offsetMax = new Vector2(-pad, -pad);
        }

        public static void Center(RectTransform r, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            r.anchoredPosition = Vector2.zero;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold, bool shadow = true)
        {
            var rt = Rect("Text", parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.55f);
                s.effectDistance = new Vector2(0, -2.5f);
            }
            return t;
        }

        public static PanelSkin Panel(Transform parent, string name, Color top, Color bottom, float radius = 28f,
            bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = raycast;
            var skin = rt.gameObject.AddComponent<PanelSkin>();
            skin.Setup(top, bottom, Palette.Gold, radius);
            return skin;
        }

        public static FancyButton Button(Transform parent, string label, Action onClick, bool primary = false, int fontSize = 30)
        {
            var skin = Panel(parent, "Button", primary ? Palette.PrimaryTop : Palette.ButtonTop,
                primary ? Palette.PrimaryBottom : Palette.ButtonBottom, 22f, true);
            var b = skin.gameObject.AddComponent<FancyButton>();
            b.Skin = skin;
            b.Clicked = onClick;
            var t = Label((RectTransform)skin.transform, label, fontSize,
                primary ? new Color(0.16f, 0.09f, 0.03f) : Palette.Ivory, TextAnchor.MiddleCenter, FontStyle.Bold, !primary);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Label = t;
            b.SetLabelColor(t.color);
            return b;
        }

        public static RawImage Emblem(Transform parent, float mode, float glow)
        {
            var rt = Rect("Emblem", parent);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            var m = new Material(EmblemShader) { hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Mode", mode);
            m.SetFloat("_Glow", glow);
            m.SetColor("_Accent", Palette.Gold);
            img.material = m;
            return img;
        }

        public static LayoutElement Size(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w > 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h > 0) { le.preferredHeight = h; le.minHeight = h; }
            return le;
        }
    }
}
