using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArrowRoute
{
    public sealed class DialogHandle
    {
        public GameObject root;
        public RectTransform content;
        public bool canClose;
        public Action onClosed;

        public void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            Dialogs.Forget(this);
            if (onClosed != null) onClosed();
        }
    }

    /// <summary>Pop-up panels (settings, themes, how to play, confirm). Built from code, picture labels only.</summary>
    public static class Dialogs
    {
        static readonly List<DialogHandle> stack = new List<DialogHandle>();

        public static readonly Color Green = Ui.Hex(0x3FBF2C);
        public static readonly Color Orange = Ui.Hex(0xFF9A1F);
        public static readonly Color Blue = Ui.Hex(0x2D8CFF);
        public static readonly Color Red = Ui.Hex(0xE9453A);
        public static readonly Color Cream = Ui.Hex(0xFFF5DC);

        public static int Count
        {
            get { stack.RemoveAll(d => d.root == null); return stack.Count; }
        }

        public static void Reset() { stack.Clear(); }
        internal static void Forget(DialogHandle d) { stack.Remove(d); }

        /// <summary>Back key: closes the top dialog. Returns true if a dialog was open (even if it can not be closed).</summary>
        public static bool CloseTop()
        {
            if (Count == 0) return false;
            DialogHandle d = stack[stack.Count - 1];
            if (d.canClose) d.Close();
            return true;
        }

        public static DialogHandle Open(Transform canvas, string titleTex, Color ribbon, bool canClose, Action onClosed)
        {
            DialogHandle h = new DialogHandle();
            h.canClose = canClose;
            h.onClosed = onClosed;

            RectTransform root = Ui.Obj(canvas, "Dialog");
            Ui.Stretch(root);
            root.SetAsLastSibling();
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.62f);
            dim.raycastTarget = true;
            h.root = root.gameObject;

            RectTransform panel = Ui.Obj(root, "Panel");
            Ui.Pin(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 400));
            VerticalLayoutGroup v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(44, 44, 120, 48);
            v.spacing = 26;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            Image border = Ui.Round(panel, "border", ribbon, 70);
            Ui.IgnoreLayout(border.gameObject);
            Ui.Inset(border.rectTransform, -10, -10, -10, -10);
            border.transform.SetSiblingIndex(0);
            Image bg = Ui.Round(panel, "bg", Cream, 62);
            Ui.IgnoreLayout(bg.gameObject);
            Ui.Stretch(bg.rectTransform);
            bg.transform.SetSiblingIndex(1);

            RectTransform rb = Ui.Obj(panel, "Ribbon");
            Ui.IgnoreLayout(rb.gameObject);
            Ui.Pin(rb, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(660, 140));
            Image rbImg = Ui.Round(rb, "ribbonBg", ribbon, 46);
            Ui.Stretch(rbImg.rectTransform);
            Image rbShade = Ui.Round(rb, "ribbonShade", new Color(0, 0, 0, 0.18f), 46);
            rbShade.rectTransform.anchorMin = new Vector2(0, 0);
            rbShade.rectTransform.anchorMax = new Vector2(1, 0.35f);
            rbShade.rectTransform.offsetMin = Vector2.zero;
            rbShade.rectTransform.offsetMax = Vector2.zero;
            RawImage title = Ui.Label(rb, titleTex, 72);
            Ui.IgnoreLayout(title.gameObject);
            Ui.Pin(title.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, title.rectTransform.sizeDelta);
            if (title.rectTransform.sizeDelta.x > 560f)   // very long titles are scaled down to fit
                title.rectTransform.localScale = Vector3.one * (560f / title.rectTransform.sizeDelta.x);

            if (canClose)
            {
                DialogHandle hh = h;
                RectTransform x = Ui.RoundButton(panel, "ic_close", Red, 100, () => hh.Close());
                Ui.IgnoreLayout(x.gameObject);
                Ui.Pin(x, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-28, -28), new Vector2(100, 100));
            }

            h.content = panel;
            stack.Add(h);
            return h;
        }

        static void SettingRow(RectTransform parent, string label, bool value, Action<bool> set)
        {
            HorizontalLayoutGroup row = Ui.HGroup(parent, "Row", 10, TextAnchor.MiddleLeft);
            LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 104;
            Ui.Label(row.transform, label, 64);
            Ui.Spacer(row.transform, 0, 0, 1);
            Ui.Toggle(row.transform, value, set);
        }

        public static DialogHandle Settings(Transform canvas, Action onRestart)
        {
            DialogHandle d = Open(canvas, "lbl_settings", Blue, true, null);
            SettingRow(d.content, "lbl_sound", Prefs.Sound, v => Prefs.Sound = v);
            SettingRow(d.content, "lbl_vibration", Prefs.Vibrate, v => Prefs.Vibrate = v);
            if (onRestart != null)
            {
                DialogHandle dd = d;
                Ui.GlossyButton(d.content, "lbl_restart", Orange, 52, 130, () => { dd.Close(); onRestart(); });
            }
            return d;
        }

        public static DialogHandle ThemePicker(Transform canvas, GameSession s)
        {
            DialogHandle d = Open(canvas, "lbl_theme", Blue, true, null);
            HorizontalLayoutGroup row = Ui.HGroup(d.content, "Themes", 24, TextAnchor.MiddleCenter);
            string[] names = { "lbl_classic", "lbl_neon", "lbl_candy" };
            int[] ids = { Themes.Classic, Themes.Neon, Themes.Candy };
            Image[] borders = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                int theme = ids[i];
                VerticalLayoutGroup col = Ui.VGroup(row.transform, "Theme" + i, 8, TextAnchor.UpperCenter);
                RectTransform box = Ui.Obj(col.transform, "Preview");
                LayoutElement le = box.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 220;
                le.preferredHeight = 220;
                Image hit = box.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                Image border = Ui.Round(box, "border", Ui.Hex(0xB98B4A), 40);
                Ui.Stretch(border.rectTransform);
                borders[i] = border;
                Image inner = Ui.Round(box, "bg", Themes.BoardBg(theme), 34);
                Ui.Inset(inner.rectTransform, 6, 6, 6, 6);
                Color c0 = Themes.ArrowColor(theme, 0), c1 = Themes.ArrowColor(theme, 1);
                Image a0 = Ui.Round(box, "a0", c0, 8);
                Ui.Pin(a0.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12, 30), new Vector2(120, 18));
                Image h0 = Ui.Disc(box, "h0", c0);
                Ui.Pin(h0.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60, 30), new Vector2(34, 34));
                Image a1 = Ui.Round(box, "a1", c1, 8);
                Ui.Pin(a1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12, -30), new Vector2(120, 18));
                Image h1 = Ui.Disc(box, "h1", c1);
                Ui.Pin(h1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60, -30), new Vector2(34, 34));
                Ui.Label(col.transform, names[i], 34);
                Image[] bs = borders;
                Ui.MakeButton(box.gameObject, hit, () =>
                {
                    s.SetTheme(theme);
                    for (int k = 0; k < 3; k++) bs[k].color = ids[k] == s.Theme ? Green : Ui.Hex(0xB98B4A);
                });
            }
            for (int k = 0; k < 3; k++) borders[k].color = ids[k] == s.Theme ? Green : Ui.Hex(0xB98B4A);
            return d;
        }

        public static DialogHandle HowToPlay(Transform canvas)
        {
            DialogHandle d = Open(canvas, "lbl_howto", Orange, true, null);
            string[] tex = { "lbl_how_1", "lbl_how_2", "lbl_how_3", "lbl_how_4", "lbl_how_5" };
            float[] hs = { 46, 92, 92, 46, 92 };   // one-line pictures 46 high, two-line pictures 92 high
            for (int i = 0; i < tex.Length; i++)
            {
                VerticalLayoutGroup holder = Ui.VGroup(d.content, "Line", 0, TextAnchor.MiddleCenter);
                Ui.Label(holder.transform, tex[i], hs[i]);
            }
            return d;
        }

        /// <summary>"Are you sure?" panel used for going home and for restarting a level.</summary>
        public static DialogHandle Confirm(Transform canvas, string titleTex, Action onYes)
        {
            DialogHandle d = Open(canvas, titleTex, Orange, true, null);
            HorizontalLayoutGroup msg = Ui.HGroup(d.content, "Msg", 0, TextAnchor.MiddleCenter);
            Ui.Label(msg.transform, "lbl_lose_progress", 100);
            HorizontalLayoutGroup row = Ui.HGroup(d.content, "Buttons", 24, TextAnchor.MiddleCenter);
            row.childForceExpandWidth = true;
            DialogHandle dd = d;
            Ui.GlossyButton(row.transform, "lbl_no", Orange, 56, 130, () => dd.Close());
            Ui.GlossyButton(row.transform, "lbl_yes", Green, 56, 130, () => { dd.Close(); onYes(); });
            return d;
        }
    }
}
