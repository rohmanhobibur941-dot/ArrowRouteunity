using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArrowRoute
{
    /// <summary>
    /// The play screen: header (home, level, difficulty, arrows left, hearts, progress), the board in the
    /// middle, and the bottom bar (themes, settings, zoom, hint). The look changes with the theme.
    /// </summary>
    public sealed class GameScreen : ScreenBase
    {
        // the board camera is the only camera of this scene
        protected override bool NeedsCamera { get { return false; } }

        GameSession S;
        BoardView bv;
        RectTransform boardArea, chrome;
        Themes.UiPalette pal;
        float topInset, bottomInset, footerH;
        int builtTheme = -1;

        RectTransform levelNum, arrowsNum, centerCol, pill, hintNum, progFill;
        readonly RawImage[] hearts = new RawImage[3];
        readonly RawImage[] stars = new RawImage[3];
        Slider zoomSlider;

        // what is currently shown (to update only when something changes)
        int shownLevel = -1, shownLeft = -1, shownTotal = -1, shownHearts = -1, shownHints = -999, shownDiff = -1;
        float shownProgress = -1f;
        GameStatus shownStatus = GameStatus.Playing;

        DialogHandle result;
        RectTransform adPill;
        int adPillState;
        float adPillUntil;

        protected override void Build()
        {
            S = Services.Session;
            Insets(out topInset, out bottomInset);

            GameObject bvGo = new GameObject("BoardView");
            bv = bvGo.AddComponent<BoardView>();
            bv.S = S;
            boardArea = Ui.Obj(root, "BoardArea");
            bv.Area = boardArea;
            bv.ZoomChanged += OnBoardZoom;

            BuildChrome();
        }

        // ------------------------------------------------------------ header + bottom bar

        void BuildChrome()
        {
            pal = Themes.Ui(S.Theme);
            builtTheme = S.Theme;
            if (chrome != null) Destroy(chrome.gameObject);
            chrome = Ui.Obj(root, "Chrome");
            Ui.Stretch(chrome);
            chrome.SetAsFirstSibling();

            float headerH = 350f + topInset;
            footerH = 210f + bottomInset;
            boardArea.anchorMin = Vector2.zero;
            boardArea.anchorMax = Vector2.one;
            boardArea.offsetMin = new Vector2(0f, footerH + 8f);
            boardArea.offsetMax = new Vector2(0f, -(headerH + 8f));

            BuildHeader(headerH);
            BuildFooter(footerH);

            shownLevel = shownLeft = shownTotal = shownHearts = shownDiff = -1;
            shownHints = -999;
            shownProgress = -1f;
            pill = null;
        }

        void Background(RectTransform bar, bool top, Color overlay)
        {
            if (pal.background != null)
            {
                RawImage r = Ui.Raw(bar, "background", pal.background);
                Ui.Stretch(r.rectTransform);
                r.uvRect = top ? new Rect(0f, 0.72f, 1f, 0.28f) : new Rect(0f, 0f, 1f, 0.25f);
            }
            Image ov = Ui.Rect(bar, "overlay", overlay);
            Ui.Stretch(ov.rectTransform);
        }

        void BuildHeader(float h)
        {
            RectTransform header = Ui.Obj(chrome, "Header");
            header.anchorMin = new Vector2(0, 1);
            header.anchorMax = new Vector2(1, 1);
            header.pivot = new Vector2(0.5f, 1);
            header.sizeDelta = new Vector2(0, h);
            header.anchoredPosition = Vector2.zero;
            Background(header, true, pal.headTop);
            Image edge = Ui.Rect(header, "edge", pal.edge);
            edge.rectTransform.anchorMin = new Vector2(0, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 0);
            edge.rectTransform.pivot = new Vector2(0.5f, 0);
            edge.rectTransform.sizeDelta = new Vector2(0, 8);
            edge.rectTransform.anchoredPosition = Vector2.zero;

            float y = -(topInset + 24f);
            RectTransform home = Ui.RoundButton(header, "ic_home", Dialogs.Green, 120, ConfirmHome);
            Ui.Pin(home, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y), new Vector2(120, 120));
            RectTransform restart = Ui.RoundButton(header, "ic_restart", Dialogs.Green, 120, ConfirmRestart);
            Ui.Pin(restart, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, y), new Vector2(120, 120));

            // level number + difficulty in the middle
            centerCol = Ui.Obj(header, "Center");
            Ui.Pin(centerCol, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -(topInset + 22f)), new Vector2(520, 150));
            VerticalLayoutGroup v = centerCol.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.spacing = 8;
            HorizontalLayoutGroup lv = Ui.HGroup(centerCol, "LevelRow", 12, TextAnchor.MiddleCenter);
            Ui.Label(lv.transform, "lbl_level", 56);
            levelNum = Ui.Number(lv.transform, S.Level.ToString(), "dw", 60);

            // arrows left / total (chip)
            RectTransform chip = Ui.Obj(header, "ArrowsChip");
            Ui.Pin(chip, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -(topInset + 185f)), new Vector2(10, 10));
            Image cb = chip.gameObject.AddComponent<Image>();
            cb.sprite = Art.Round;
            cb.type = Image.Type.Sliced;
            cb.pixelsPerUnitMultiplier = 24f / 36f;
            cb.color = pal.chip;
            cb.raycastTarget = false;
            HorizontalLayoutGroup ch = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.padding = new RectOffset(28, 28, 10, 10);
            ch.spacing = 14;
            ch.childAlignment = TextAnchor.MiddleCenter;
            ch.childControlWidth = true;
            ch.childControlHeight = true;
            ch.childForceExpandWidth = false;
            ch.childForceExpandHeight = false;
            ContentSizeFitter cf = chip.gameObject.AddComponent<ContentSizeFitter>();
            cf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            cf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Ui.Label(chip, "lbl_arrows", 44);
            arrowsNum = Ui.Number(chip, "0/0", "dw", 46);

            // hearts
            HorizontalLayoutGroup hr = Ui.HGroup(header, "Hearts", 10, TextAnchor.MiddleRight);
            RectTransform hrt = (RectTransform)hr.transform;
            Ui.Pin(hrt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -(topInset + 178f)), new Vector2(10, 10));
            ContentSizeFitter hf = hrt.gameObject.AddComponent<ContentSizeFitter>();
            hf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            hf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (int i = 0; i < 3; i++) hearts[i] = Ui.Icon(hrt, "ic_heart", 72);

            // progress bar with 3 stars
            RectTransform bar = Ui.Obj(header, "Progress");
            Ui.Pin(bar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -(topInset + 272f)), new Vector2(1020, 62));
            Image bbg = Ui.Round(bar, "bg", Ui.Hex(0x2A1A0C, 0.9f), 31);
            Ui.Stretch(bbg.rectTransform);
            Image fill = Ui.Round(bar, "fill", Ui.Hex(0x5FC8F0), 31);
            progFill = fill.rectTransform;
            progFill.anchorMin = new Vector2(0, 0);
            progFill.anchorMax = new Vector2(0.02f, 1);
            progFill.offsetMin = Vector2.zero;
            progFill.offsetMax = Vector2.zero;
            for (int i = 0; i < 3; i++)
            {
                RawImage st = Ui.Raw(bar, "star" + i, "ic_star_dim");
                Ui.Pin(st.rectTransform, new Vector2((i + 1) / 3f, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(50, 50));
                stars[i] = st;
            }
        }

        void BuildFooter(float h)
        {
            RectTransform footer = Ui.Obj(chrome, "Footer");
            footer.anchorMin = new Vector2(0, 0);
            footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(0.5f, 0);
            footer.sizeDelta = new Vector2(0, h);
            footer.anchoredPosition = Vector2.zero;
            Background(footer, false, pal.bar);
            Image edge = Ui.Rect(footer, "edge", pal.edge);
            edge.rectTransform.anchorMin = new Vector2(0, 1);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.pivot = new Vector2(0.5f, 1);
            edge.rectTransform.sizeDelta = new Vector2(0, 8);
            edge.rectTransform.anchoredPosition = Vector2.zero;

            float by = bottomInset + 40f;
            RectTransform theme = Ui.RoundButton(footer, "ic_palette", Dialogs.Blue, 130, () => Dialogs.ThemePicker(root, S));
            Ui.Pin(theme, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, by), new Vector2(130, 130));
            RectTransform gear = Ui.RoundButton(footer, "ic_gear", Dialogs.Orange, 130, () => Dialogs.Settings(root, ConfirmRestart));
            Ui.Pin(gear, new Vector2(0, 0), new Vector2(0, 0), new Vector2(185, by), new Vector2(130, 130));

            // zoom bar
            RectTransform zb = Ui.Obj(footer, "Zoom");
            zb.anchorMin = new Vector2(0, 0);
            zb.anchorMax = new Vector2(1, 0);
            zb.pivot = new Vector2(0.5f, 0);
            zb.offsetMin = new Vector2(350f, bottomInset + 45f);
            zb.offsetMax = new Vector2(-195f, bottomInset + 165f);
            Image zbg = Ui.Round(zb, "bg", pal.zoomBg, 60);
            Ui.Stretch(zbg.rectTransform);
            ZoomSign(zb, false, new Vector2(0f, 0.5f), new Vector2(16f, 0f), () => bv.SetZoom(bv.Zoom - 0.25f));
            ZoomSign(zb, true, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), () => bv.SetZoom(bv.Zoom + 0.25f));
            zoomSlider = Ui.MakeSlider(zb, 1f, 4f, Ui.Hex(0x7A5630), Ui.Hex(0xFFD27A), Ui.Hex(0xFFA03A), v => bv.SetZoom(v));
            Ui.Inset((RectTransform)zoomSlider.transform, 110, 0, 110, 0);
            zoomSlider.SetValueWithoutNotify(bv.Zoom);

            // hint button with the number of hints left
            RectTransform hint = Ui.RoundButton(footer, "ic_bulb", Dialogs.Green, 130, OnHintClick);
            Ui.Pin(hint, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, by), new Vector2(130, 130));
            Image badgeRing = Ui.Disc(hint, "badgeRing", Color.white);
            Ui.Pin(badgeRing.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(74, 74));
            Image badge = Ui.Disc(hint, "badge", Ui.Hex(0xFF7A1A));
            Ui.Pin(badge.rectTransform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6, -6), new Vector2(64, 64));
            hintNum = Ui.Number(badge.transform, "3", "dw", 32);
            Ui.Pin(hintNum, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        }

        void ZoomSign(RectTransform parent, bool plus, Vector2 anchor, Vector2 pos, Action onClick)
        {
            RectTransform rt = Ui.Obj(parent, plus ? "Plus" : "Minus");
            Ui.Pin(rt, anchor, new Vector2(anchor.x, 0.5f), pos, new Vector2(90, 90));
            Image hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Ui.MakeButton(rt.gameObject, hit, onClick);
            Image hz = Ui.Round(rt, "h", Color.white, 4);
            Ui.Pin(hz.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 9));
            if (plus)
            {
                Image vt = Ui.Round(rt, "v", Color.white, 4);
                Ui.Pin(vt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 42));
            }
        }

        void OnBoardZoom(float z)
        {
            if (zoomSlider != null) zoomSlider.SetValueWithoutNotify(z);
        }

        // ------------------------------------------------------------ buttons

        void ConfirmHome() { Dialogs.Confirm(root, "lbl_leave_title", () => SceneManager.LoadScene("Menu")); }

        void ConfirmRestart() { Dialogs.Confirm(root, "lbl_restart_title", () => S.StartLevel()); }

        protected override void OnBack() { ConfirmHome(); }

        void OnHintClick()
        {
            if (S.Hints > 0 || S.HintIsFree()) S.UseHint();
            else WatchAd(() => S.AddHint());
        }

        /// <summary>Shows a rewarded ad (AdMob first, then Unity). If none is loaded yet it is loaded now and shown when ready.</summary>
        void WatchAd(Action onReward)
        {
            Services.Ads.ShowRewarded(onReward, () => ShowAdPill(2), waiting =>
            {
                if (waiting) ShowAdPill(1);
                else if (adPillState == 1) HideAdPill();
            });
        }

        void ShowAdPill(int state)
        {
            HideAdPill();
            adPillState = state;
            adPillUntil = Time.time + 3.2f;
            adPill = Ui.Obj(root, "AdPill");
            Ui.Pin(adPill, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, footerH + 130f), new Vector2(780, 100));
            Image pbg = Ui.Round(adPill, "bg", Ui.Hex(0x1E1004, 0.82f), 50);
            Ui.Stretch(pbg.rectTransform);
            Ui.MakeButton(adPill.gameObject, pbg, () => Debug.Log("Ads: " + Services.Ads.LastError));   // tap = print the reason
            RawImage l = Ui.Label(adPill, state == 1 ? "lbl_loading_ad" : "lbl_ad_not_ready", 46);
            Ui.IgnoreLayout(l.gameObject);
            Ui.Pin(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, l.rectTransform.sizeDelta);
        }

        void HideAdPill()
        {
            if (adPill != null) Destroy(adPill.gameObject);
            adPill = null;
            adPillState = 0;
        }

        // ------------------------------------------------------------ per frame

        protected override void Update()
        {
            base.Update();
            if (S.Theme != builtTheme) BuildChrome();
            Refresh();
            bv.InputBlocked = Dialogs.Count > 0;
            if (adPill != null && adPillState == 2 && Time.time > adPillUntil) HideAdPill();
        }

        void Refresh()
        {
            if (S.Level != shownLevel)
            {
                shownLevel = S.Level;
                Ui.SetNumber(levelNum, S.Level.ToString(), "dw", 60);
            }
            int diff = S.Difficulty;
            if (diff != shownDiff || pill == null)
            {
                shownDiff = diff;
                if (pill != null) Destroy(pill.gameObject);
                pill = Ui.Obj(centerCol, "Pill");
                Image pbg = pill.gameObject.AddComponent<Image>();
                pbg.sprite = Art.Round;
                pbg.type = Image.Type.Sliced;
                pbg.color = Themes.DifficultyColor(diff);
                pbg.raycastTarget = false;
                HorizontalLayoutGroup pg = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
                pg.padding = new RectOffset(36, 36, 8, 8);
                pg.childAlignment = TextAnchor.MiddleCenter;
                pg.childControlWidth = true;
                pg.childControlHeight = true;
                pg.childForceExpandWidth = false;
                pg.childForceExpandHeight = false;
                Ui.Label(pill, Themes.DifficultyLabel(diff), 42);
            }
            int left = S.Arrows.Count;
            if (left != shownLeft || S.TotalArrows != shownTotal)
            {
                shownLeft = left;
                shownTotal = S.TotalArrows;
                Ui.SetNumber(arrowsNum, left + "/" + S.TotalArrows, "dw", 46);
            }
            if (S.Hearts != shownHearts)
            {
                shownHearts = S.Hearts;
                for (int i = 0; i < 3; i++) hearts[i].texture = Art.Tex(i < S.Hearts ? "ic_heart" : "ic_heart_off");
            }
            int hv = S.Hints > 0 ? S.Hints : -1;   // -1 = show "+" (watch an ad to get a hint)
            if (hv != shownHints)
            {
                shownHints = hv;
                Ui.SetNumber(hintNum, hv > 0 ? hv.ToString() : "+", "dw", 32);
            }
            float prog = Mathf.Clamp01((S.TotalArrows - left) / (float)S.TotalArrows);
            if (Mathf.Abs(prog - shownProgress) > 0.0005f)
            {
                shownProgress = prog;
                progFill.anchorMax = new Vector2(Mathf.Max(0.02f, prog), 1f);
                for (int i = 0; i < 3; i++)
                    stars[i].texture = Art.Tex(prog >= (i + 1) / 3f - 0.001f ? "ic_star" : "ic_star_dim");
            }

            if (S.Status != shownStatus)
            {
                shownStatus = S.Status;
                if (result != null) { result.Close(); result = null; }
                if (shownStatus == GameStatus.Won) ShowWin();
                else if (shownStatus == GameStatus.Lost) ShowLose();
            }
        }

        // ------------------------------------------------------------ result dialogs

        void ShowWin()
        {
            DialogHandle d = Dialogs.Open(root, "lbl_victory", Dialogs.Green, false, null);
            result = d;
            HorizontalLayoutGroup row = Ui.HGroup(d.content, "Stars", 16, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++) Ui.Icon(row.transform, i < S.Stars ? "ic_star" : "ic_star_dim", i == 1 ? 150 : 118);
            if (!S.HintClaimed)
            {
                RectTransform hb = Ui.GlossyButton(d.content, "lbl_get_hint", Dialogs.Orange, 96, 170, null);
                Ui.SetClick(hb, () => WatchAd(() =>
                {
                    S.ClaimWinHint();
                    if (hb != null) Destroy(hb.gameObject);
                }));
            }
            Ui.GlossyButton(d.content, "lbl_next_level", Dialogs.Green, 58, 130, () =>
            {
                int finished = S.Level;
                d.Close();
                result = null;
                if (finished % 3 == 0) Services.Ads.ShowInterstitial(() => S.NextLevel());
                else S.NextLevel();
            });
        }

        void ShowLose()
        {
            DialogHandle d = Dialogs.Open(root, "lbl_defeat", Dialogs.Green, false, null);
            result = d;
            HorizontalLayoutGroup row = Ui.HGroup(d.content, "Heart", 0, TextAnchor.MiddleCenter);
            Ui.Icon(row.transform, "ic_heart_off", 130);
            if (!S.ReviveUsed)
            {
                Ui.GlossyButton(d.content, "lbl_extra_life", Dialogs.Orange, 96, 170, () => WatchAd(() =>
                {
                    d.Close();
                    result = null;
                    S.Revive();
                }));
            }
            Ui.GlossyButton(d.content, "lbl_try_again", Dialogs.Green, 58, 130, () =>
            {
                d.Close();
                result = null;
                S.StartLevel();
            });
        }
    }
}
