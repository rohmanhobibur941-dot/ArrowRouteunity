using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArrowRoute
{
    public sealed class SplashScreen : ScreenBase
    {
        protected override void Build()
        {
            RawImage bg = Ui.Raw(root, "Splash", "splash_bg");
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bg.rectTransform.sizeDelta = Vector2.zero;
            AspectRatioFitter fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Texture2D t = Art.Tex("splash_bg");
            fit.aspectRatio = t != null ? (float)t.width / t.height : 0.45f;
            StartCoroutine(Go());
        }

        IEnumerator Go()
        {
            yield return new WaitForSeconds(2f);
            SceneManager.LoadScene("Menu");
        }
    }
}
