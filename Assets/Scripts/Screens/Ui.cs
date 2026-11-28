using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowRoute
{
    /// <summary>Pictures from Resources/UI and two generated rounded sprites (no art files needed for those).</summary>
    public static class Art
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static Sprite round, circle;

        public static Texture2D Tex(string name)
        {
            Texture2D t;
            if (cache.TryGetValue(name, out t) && t != null) return t;
            t = Resources.Load<Texture2D>("UI/" + name);
            if (t == null) Debug.LogWarning("Missing texture Resources/UI/" + name);
            cache[name] = t;
            return t;
        }

        /// <summary>White rounded square with a 24 px border, for 9-sliced backgrounds.</summary>
        public static Sprite Round
        {
            get
            {
                if (round != null) return round;
                const int S = 64;
                const float R = 24f;
                Texture2D tex = NewTex(S);
                Color32[] px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float dx = Mathf.Max(Mathf.Abs(x + 0.5f - S / 2f) - (S / 2f - R), 0f);
                        float dy = Mathf.Max(Mathf.Abs(y + 0.5f - S / 2f) - (S / 2f - R), 0f);
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) - R;
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                round = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(R, R, R, R));
                return round;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int S = 128;
                Texture2D tex = NewTex(S);
                Color32[] px = new Color32[S * S];
                for (int y = 0; y < S; y++)
                {
                    for (int x = 0; x < S; x++)
                    {
                        float dx = x + 0.5f - S / 2f, dy = y + 0.5f - S / 2f;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) - (S / 2f - 1f);
                        px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        static Texture2D NewTex(int s)
        {
            Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }
    }

    /// <summary>Small "pressed" animation for buttons.</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public void OnPointerDown(PointerEventData e) { transform.localScale = Vector3.one * 0.94f; }
        public void OnPointerUp(PointerEventData e) { transform.localScale = Vector3.one; }
        public void OnPointerExit(PointerEventData e) { transform.localScale = Vector3.one; }
    }

    /// <summary>Helpers that build uGUI objects from code.</summary>
    public static class Ui
    {
        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }

        // ------------------------------------------------------------ canvas / basic objects

        public static Canvas MakeCanvas(Transform parent, string name, int order)
        {
            GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            Canvas cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = order;
            CanvasScaler sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1080f, 1920f);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;
            return cv;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Obj(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Stretch to the parent with insets (left, bottom, right, top).</summary>
        public static void Inset(RectTransform rt, float l, float b, float r, float t)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>Place with anchor + pivot + position + size (all in canvas units).</summary>
        public static void Pin(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void IgnoreLayout(GameObject go)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        public static Image Rect(Transform parent, string name, Color c)
        {
            RectTransform rt = Obj(parent, name);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Image Round(Transform parent, string name, Color c, float radius)
        {
            Image img = Rect(parent, name, c);
            img.sprite = Art.Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, 24f / Mathf.Max(1f, radius));
            return img;
        }

        public static Image Disc(Transform parent, string name, Color c)
        {
            Image img = Rect(parent, name, c);
            img.sprite = Art.Circle;
            img.type = Image.Type.Simple;
            return img;
        }

        public static RawImage Raw(Transform parent, string name, string tex)
        {
            RectTransform rt = Obj(parent, name);
            RawImage raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = Art.Tex(tex);
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>A square picture from Resources/UI with a fixed size (works inside layout groups).</summary>
        public static RawImage Icon(Transform parent, string tex, float size)
        {
            RawImage raw = Raw(parent, tex, tex);
            raw.rectTransform.sizeDelta = new Vector2(size, size);
            LayoutElement le = raw.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            return raw;
        }

        // ------------------------------------------------------------ picture text (no fonts)

        /// <summary>A text label that is a PNG. Sized by height; works inside layout groups.</summary>
        public static RawImage Label(Transform parent, string tex, float height)
        {
            Texture2D t = Art.Tex(tex);
            RawImage raw = Raw(parent, tex, tex);
            float aspect = t != null ? (float)t.width / t.height : 1f;
            raw.rectTransform.sizeDelta = new Vector2(height * aspect, height);
            LayoutElement le = raw.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.preferredWidth = height * aspect;
            return raw;
        }

        /// <summary>A number drawn with the digit pictures. style: "dw" (white) or "dp" (white, green outline).</summary>
        public static RectTransform Number(Transform parent, string text, string style, float height)
        {
            RectTransform rt = Obj(parent, "Number");
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 0f;
            SetNumber(rt, text, style, height);
            return rt;
        }

        public static void SetNumber(RectTransform rt, string text, string style, float height)
        {
            for (int i = rt.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(rt.GetChild(i).gameObject);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                string name = null;
                if (ch >= '0' && ch <= '9') name = style + "_" + ch;
                else if (ch == '+') name = style + "_plus";
                else if (ch == '/') name = style + "_slash";
                if (name != null) Label(rt, name, height);
            }
        }

        // ------------------------------------------------------------ layout groups

        public static VerticalLayoutGroup VGroup(Transform parent, string name, float spacing, TextAnchor align)
        {
            RectTransform rt = Obj(parent, name);
            VerticalLayoutGroup v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HGroup(Transform parent, string name, float spacing, TextAnchor align)
        {
            RectTransform rt = Obj(parent, name);
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static void Spacer(Transform parent, float width, float height, float flexibleWidth)
        {
            RectTransform rt = Obj(parent, "Spacer");
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.flexibleWidth = flexibleWidth;
        }

        // ------------------------------------------------------------ buttons

        public static Button MakeButton(GameObject go, Graphic target, Action onClick)
        {
            Button b = go.AddComponent<Button>();
            b.targetGraphic = target;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { if (onClick != null) onClick(); });
            target.raycastTarget = true;
            go.AddComponent<PressFeedback>();
            return b;
        }

        /// <summary>Replace the click action of a button made with MakeButton / GlossyButton.</summary>
        public static void SetClick(RectTransform btn, Action onClick)
        {
            Button b = btn.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { if (onClick != null) onClick(); });
        }

        /// <summary>Glossy pill button with a picture label. Fills the width of its layout parent.</summary>
        public static RectTransform GlossyButton(Transform parent, string labelTex, Color baseColor, float labelHeight,
                                                 float height, Action onClick)
        {
            RectTransform root = Obj(parent, "Button");
            LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            MakeButton(root.gameObject, hit, onClick);

            float r = height * 0.5f;
            Image shadow = Round(root, "shadow", Themes.Shade(baseColor, 0.55f), r);
            Inset(shadow.rectTransform, 0, 0, 0, 10);
            Image border = Round(root, "border", new Color(1, 1, 1, 0.9f), r);
            Inset(border.rectTransform, 0, 10, 0, 0);
            Image body = Round(root, "body", baseColor, r);
            Inset(body.rectTransform, 5, 15, 5, 5);
            Image gloss = Round(root, "gloss", new Color(1, 1, 1, 0.32f), r * 0.3f);
            gloss.rectTransform.anchorMin = new Vector2(0.1f, 0.6f);
            gloss.rectTransform.anchorMax = new Vector2(0.9f, 0.82f);
            gloss.rectTransform.offsetMin = Vector2.zero;
            gloss.rectTransform.offsetMax = Vector2.zero;

            RawImage label = Label(root, labelTex, labelHeight);
            IgnoreLayout(label.gameObject);
            Pin(label.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero,
                label.rectTransform.sizeDelta);
            return root;
        }

        /// <summary>Round button with an icon from Resources/UI.</summary>
        public static RectTransform RoundButton(Transform parent, string iconTex, Color baseColor, float size, Action onClick)
        {
            RectTransform root = Obj(parent, "RoundButton");
            root.sizeDelta = new Vector2(size, size);
            LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            Image hit = root.gameObject.AddComponent<Image>();
            hit.sprite = Art.Circle;
            hit.color = new Color(0, 0, 0, 0);
            MakeButton(root.gameObject, hit, onClick);

            Image shadow = Disc(root, "shadow", Themes.Shade(baseColor, 0.55f));
            Inset(shadow.rectTransform, 0, 0, 0, 7);
            Image border = Disc(root, "border", new Color(1, 1, 1, 0.9f));
            Inset(border.rectTransform, 0, 7, 0, 0);
            Image body = Disc(root, "body", baseColor);
            Inset(body.rectTransform, 5, 12, 5, 5);
            RawImage icon = Raw(root, "icon", iconTex);
            Pin(icon.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size * 0.55f, size * 0.55f));
            return root;
        }

        /// <summary>ON / OFF switch drawn with picture labels.</summary>
        public static RectTransform Toggle(Transform parent, bool value, Action<bool> onChange)
        {
            RectTransform root = Obj(parent, "Toggle");
            LayoutElement le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 250;
            le.preferredHeight = 100;
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            bool state = value;
            MakeButton(root.gameObject, hit, () =>
            {
                state = !state;
                DrawToggle(root, state);
                onChange(state);
            });
            DrawToggle(root, state);
            return root;
        }

        static void DrawToggle(RectTransform root, bool value)
        {
            for (int i = root.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
            Image bg = Round(root, "bg", Hex(0xE7D6AB), 50);
            Inset(bg.rectTransform, 0, 0, 0, 0);
            Image off = Round(root, "off", value ? new Color(0, 0, 0, 0) : Hex(0xE9453A), 44);
            off.rectTransform.anchorMin = new Vector2(0f, 0f);
            off.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            off.rectTransform.offsetMin = new Vector2(7, 7);
            off.rectTransform.offsetMax = new Vector2(-2, -7);
            Image on = Round(root, "on", value ? Hex(0x3FBF2C) : new Color(0, 0, 0, 0), 44);
            on.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            on.rectTransform.anchorMax = new Vector2(1f, 1f);
            on.rectTransform.offsetMin = new Vector2(2, 7);
            on.rectTransform.offsetMax = new Vector2(-7, -7);
            RawImage offL = Label(root, value ? "lbl_off_m" : "lbl_off_w", 34);
            IgnoreLayout(offL.gameObject);
            Pin(offL.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, offL.rectTransform.sizeDelta);
            RawImage onL = Label(root, value ? "lbl_on_w" : "lbl_on_m", 34);
            IgnoreLayout(onL.gameObject);
            Pin(onL.rectTransform, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, onL.rectTransform.sizeDelta);
        }

        // ------------------------------------------------------------ slider

        public static Slider MakeSlider(Transform parent, float min, float max, Color track, Color fill, Color handle, Action<float> onChange)
        {
            RectTransform root = Obj(parent, "Slider");
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Slider s = root.gameObject.AddComponent<Slider>();

            Image bg = Round(root, "Background", track, 12);
            bg.rectTransform.anchorMin = new Vector2(0, 0.38f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.62f);
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            RectTransform fillArea = Obj(root, "Fill Area");
            fillArea.anchorMin = new Vector2(0, 0.38f);
            fillArea.anchorMax = new Vector2(1, 0.62f);
            fillArea.offsetMin = new Vector2(10, 0);
            fillArea.offsetMax = new Vector2(-10, 0);
            Image fillImg = Round(fillArea, "Fill", fill, 12);
            fillImg.rectTransform.anchorMin = Vector2.zero;
            fillImg.rectTransform.anchorMax = new Vector2(0, 1);
            fillImg.rectTransform.offsetMin = Vector2.zero;
            fillImg.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = Obj(root, "Handle Slide Area");
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(30, 0);
            handleArea.offsetMax = new Vector2(-30, 0);
            Image handleImg = Disc(handleArea, "Handle", handle);
            handleImg.rectTransform.sizeDelta = new Vector2(60, 60);

            s.fillRect = fillImg.rectTransform;
            s.handleRect = handleImg.rectTransform;
            s.targetGraphic = handleImg;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.value = min;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }
    }
}
