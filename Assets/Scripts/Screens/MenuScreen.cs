using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArrowRoute
{
    /// <summary>Home page: logo, big play button, theme, how to play and settings (layout of the reference design).</summary>
    public sealed class MenuScreen : ScreenBase
    {
        RectTransform playRt;
        RectTransform toast;
        float toastUntil;
        float lastBack = -10f;

        protected override void Build()
        {
            RawImage bg = Ui.Raw(root, "Background", "bg_home");
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bg.rectTransform.sizeDelta = Vector2.zero;
            AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Texture2D t = Art.Tex("bg_home");
            fit.aspectRatio = t != null ? (float)t.width / t.height : 0.56f;

            // positions come from the reference design (1449 x 2576); k shrinks things on shorter screens
            float k = Mathf.Min(1f, ((float)Screen.height / Screen.width) / (2576f / 1449f));
            const float W = 1080f;

            PlaceImage("home_logo", W * 0.62f * k, 0.138f);
            playRt = PlaceImage("home_play", W * 0.34f * k, 0.378f);
            Ui.MakeButton(playRt.gameObject, playRt.GetComponent<RawImage>(), () => SceneManager.LoadScene("Game"));

            // current level, small, under the play button
            RectTransform lvl = Ui.Obj(root, "Level");
            Ui.Pin(lvl, new Vector2(0.5f, 1f - 0.566f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400, 50));
            HorizontalLayoutGroup row = lvl.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.spacing = 10;
            Ui.Label(lvl, "lbl_level", 30);
            Ui.Number(lvl, Services.Session.Level.ToString(), "dw", 34);

            RectTransform theme = PlaceImage("home_theme", W * 0.53f * k, 0.592f);
            Ui.MakeButton(theme.gameObject, theme.GetComponent<RawImage>(), () => Dialogs.ThemePicker(root, Services.Session));
            RectTransform how = PlaceImage("home_how", W * 0.61f * k, 0.706f);
            Ui.MakeButton(how.gameObject, how.GetComponent<RawImage>(), () => Dialogs.HowToPlay(root));

            // settings gear, bottom right
            Texture2D gt = Art.Tex("home_gear");
            float gw = W * 0.125f;
            RawImage gear = Ui.Raw(root, "home_gear", "home_gear");
            Ui.Pin(gear.rectTransform, new Vector2(1f, 1f - 0.90f), new Vector2(1f, 1f), new Vector2(-W * 0.083f, 0f),
                   new Vector2(gw, gt != null ? gw * gt.height / gt.width : gw));
            Ui.MakeButton(gear.gameObject, gear, () => Dialogs.Settings(root, null));
        }

        /// <summary>Picture placed by its width (canvas units) and its top edge as a fraction of the screen height.</summary>
        RectTransform PlaceImage(string tex, float width, float topFrac)
        {
            Texture2D t = Art.Tex(tex);
            float aspect = t != null ? (float)t.width / t.height : 1f;
            RawImage raw = Ui.Raw(root, tex, tex);
            Ui.Pin(raw.rectTransform, new Vector2(0.5f, 1f - topFrac), new Vector2(0.5f, 1f), Vector2.zero,
                   new Vector2(width, width / aspect));
            return raw.rectTransform;
        }

        protected override void Update()
        {
            base.Update();
            if (playRt != null)
                playRt.localScale = Vector3.one * (1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / 1.8f)));
            if (toast != null && Time.time > toastUntil)
            {
                Destroy(toast.gameObject);
                toast = null;
            }
        }

        /// <summary>Back: the first press shows a hint, the second press within 2 seconds closes the game.</summary>
        protected override void OnBack()
        {
            if (Time.time - lastBack < 2f)
            {
                Application.Quit();
                return;
            }
            lastBack = Time.time;
            toastUntil = Time.time + 2f;
            if (toast == null)
            {
                toast = Ui.Obj(root, "Toast");
                Ui.Pin(toast, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(700, 90));
                Image tb = Ui.Round(toast, "bg", Ui.Hex(0x1E1004, 0.8f), 45);
                Ui.Stretch(tb.rectTransform);
                RawImage l = Ui.Label(toast, "lbl_press_back", 40);
                Ui.IgnoreLayout(l.gameObject);
                Ui.Pin(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, l.rectTransform.sizeDelta);
            }
        }
    }
}
