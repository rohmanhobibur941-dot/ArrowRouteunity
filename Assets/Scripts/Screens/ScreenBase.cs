using UnityEngine;

namespace ArrowRoute
{
    /// <summary>Base of Splash, Menu and Game screens. Each scene only holds one of these; it builds all UI in Awake.</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected Canvas canvas;
        protected RectTransform root;

        protected virtual bool NeedsCamera { get { return true; } }

        void Awake()
        {
            Services.Init();
            Dialogs.Reset();
            Ui.EnsureEventSystem();
            if (NeedsCamera && FindFirstObjectByType<Camera>() == null)
            {
                GameObject camGo = new GameObject("UiCamera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
            }
            canvas = Ui.MakeCanvas(transform, "Canvas", 0);
            root = (RectTransform)canvas.transform;
            Build();
        }

        protected abstract void Build();

        protected virtual void Update()
        {
            Services.Session.Tick(Time.deltaTime);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!Dialogs.CloseTop()) OnBack();
            }
        }

        protected virtual void OnBack() { }

        /// <summary>Top and bottom screen insets (notch, gesture bar) in canvas units.</summary>
        protected static void Insets(out float top, out float bottom)
        {
            float sf = Mathf.Sqrt((Screen.width / 1080f) * (Screen.height / 1920f));
            if (sf <= 0.01f) sf = 1f;
            Rect sa = Screen.safeArea;
            top = Mathf.Max(0f, (Screen.height - sa.yMax) / sf);
            bottom = Mathf.Max(0f, sa.y / sf);
        }
    }
}
