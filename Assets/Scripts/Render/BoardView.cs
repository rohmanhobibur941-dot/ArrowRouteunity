using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowRoute
{
    /// <summary>
    /// Draws the board and handles touch input (tap an arrow, drag to move, pinch to zoom).
    /// The board is drawn with its own orthographic camera that only covers the board area of the screen,
    /// so zoom / pan never rebuilds the meshes. 1 world unit = 1 grid cell.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public GameSession S;
        public RectTransform Area;      // the board area of the UI (its screen rectangle is used)
        public bool InputBlocked;       // true while a dialog is open
        public float Zoom = 1f;
        public event Action<float> ZoomChanged;

        const float ArrowWidth = 0.24f;

        Camera cam;
        Mesh bgMesh, staticMesh, dynMesh;
        readonly MeshBuilder bgB = new MeshBuilder();
        readonly MeshBuilder stB = new MeshBuilder();
        readonly MeshBuilder dynB = new MeshBuilder();
        readonly Vector3[] corners = new Vector3[4];

        Rect vp;
        Vector2 camOffset;
        float baseCell = 10f, cellPx = 10f;
        int builtGen = -1, builtVersion = -1, builtTheme = -1;
        bool hadDynamic;

        bool tracking, moved;
        Vector2 startPos, lastMouse;
        float startTime;

        void Awake()
        {
            GameObject camGo = new GameObject("BoardCamera");
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -1;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            Material mat = Resources.Load<Material>("Materials/BoardMat");
            if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
            bgMesh = MakeLayer("BoardBg", 0, mat);
            staticMesh = MakeLayer("BoardArrows", 1, mat);
            dynMesh = MakeLayer("BoardAnimated", 2, mat);
        }

        Mesh MakeLayer(string name, int order, Material mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            MeshFilter f = go.AddComponent<MeshFilter>();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            Mesh m = new Mesh();
            m.name = name;
            m.indexFormat = IndexFormat.UInt32;
            m.MarkDynamic();
            f.sharedMesh = m;
            return m;
        }

        public void SetZoom(float z) { ZoomAt(z, vp.center); }

        // ------------------------------------------------------------ per frame

        void LateUpdate()
        {
            if (S == null || Area == null) return;
            Area.GetWorldCorners(corners);
            vp = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
            if (vp.width < 2f || vp.height < 2f) return;

            cam.rect = new Rect(vp.x / Screen.width, vp.y / Screen.height, vp.width / Screen.width, vp.height / Screen.height);
            cam.ResetAspect();

            if (S.Gen != builtGen || S.Theme != builtTheme)
            {
                if (S.Gen != builtGen)
                {
                    Zoom = 1f;
                    camOffset = Vector2.zero;
                    if (ZoomChanged != null) ZoomChanged(Zoom);
                }
                RebuildAll();
            }
            else if (S.Version != builtVersion)
            {
                RebuildStatic();
                builtVersion = S.Version;
            }

            if (S.Animating || hadDynamic)
            {
                RebuildDynamic();
                hadDynamic = S.Animating;
            }

            UpdateCell();
            HandleInput();
            ApplyCamera();
        }

        Vector2 Center { get { return new Vector2((S.W - 1) / 2f, -(S.H - 1) / 2f); } }

        void UpdateCell()
        {
            baseCell = Mathf.Min(vp.width / (S.W + 0.5f), vp.height / (S.H + 0.5f));
            cellPx = baseCell * Zoom;
        }

        void ApplyCamera()
        {
            UpdateCell();
            float viewW = vp.width / cellPx, viewH = vp.height / cellPx;
            float limX = Mathf.Max(0f, ((S.W - 1) - viewW) / 2f + 1f);
            float limY = Mathf.Max(0f, ((S.H - 1) - viewH) / 2f + 1f);
            camOffset.x = Mathf.Clamp(camOffset.x, -limX, limX);
            camOffset.y = Mathf.Clamp(camOffset.y, -limY, limY);
            cam.orthographicSize = viewH / 2f;
            Vector2 c = Center + camOffset;
            cam.transform.position = new Vector3(c.x, c.y, -10f);
        }

        // ------------------------------------------------------------ building the meshes

        static Vector2 P(Cell c) { return new Vector2(c.x, -c.y); }

        List<Vector2> CellPoints(Arrow a)
        {
            List<Vector2> pts = new List<Vector2>(a.cells.Length);
            for (int i = 0; i < a.cells.Length; i++) pts.Add(P(a.cells[i]));
            return pts;
        }

        void RebuildAll()
        {
            builtGen = S.Gen;
            builtTheme = S.Theme;
            builtVersion = S.Version;
            cam.backgroundColor = Themes.BoardBg(S.Theme);

            // faint pattern (so empty parts never look blank) + the dot of every grid point
            bgB.Clear();
            Color32 pat = Themes.PatternColor(S.Theme);
            const float step = 3.6f;
            int row = 0;
            for (float y = 3f; y > -(S.H + 3f); y -= step * 0.6f)
            {
                for (float x = (row % 2 == 0) ? -3f : -3f + step / 2f; x < S.W + 3f; x += step)
                    bgB.Disc(new Vector2(x, y), step * 0.16f, pat, 8);
                row++;
            }
            Color32 dot = Themes.DotColor(S.Theme);
            for (int y = 0; y < S.H; y++)
                for (int x = 0; x < S.W; x++)
                    if (S.Mask[y * S.W + x]) bgB.Disc(new Vector2(x, -y), 0.07f, dot, 6);
            bgB.Apply(bgMesh);

            RebuildStatic();
            RebuildDynamic();
            hadDynamic = S.Animating;
        }

        void RebuildStatic()
        {
            stB.Clear();
            bool candy = S.Theme == Themes.Candy;
            for (int i = 0; i < S.Arrows.Count; i++)
            {
                Arrow a = S.Arrows[i];
                if (a.id == S.HintId || a.id == S.BumpArrowId) continue;   // drawn animated
                Color col = a.id == S.BlockedId ? Themes.Red : Themes.ArrowColor(S.Theme, a.id);
                ArrowDrawer.Draw(stB, CellPoints(a), col, ArrowWidth, 1f, candy);
            }
            stB.Apply(staticMesh);
        }

        void RebuildDynamic()
        {
            dynB.Clear();
            bool candy = S.Theme == Themes.Candy;
            for (int i = 0; i < S.Arrows.Count; i++)
            {
                Arrow a = S.Arrows[i];
                if (a.id == S.BumpArrowId)
                {
                    ArrowDrawer.Draw(dynB, SlidePoints(a, S.BumpShift), Themes.Red, ArrowWidth, 1f, candy);
                }
                else if (a.id == S.HintId)
                {
                    // flash green and grow a little so the player sees which arrow to tap
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI / 0.76f);
                    Color col = Color.Lerp(Themes.Hint, Themes.HintLight, pulse);
                    ArrowDrawer.Draw(dynB, CellPoints(a), col, ArrowWidth * (1.3f + 0.3f * pulse), 1f, candy);
                }
            }
            for (int i = 0; i < S.Ghosts.Count; i++)
            {
                Ghost g = S.Ghosts[i];
                float p = g.Progress;
                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, (p - 0.6f) / 0.4f));
                ArrowDrawer.Draw(dynB, ExitPoints(g), Themes.ArrowColor(S.Theme, g.arrow.id), ArrowWidth, alpha, candy);
            }
            dynB.Apply(dynMesh);
        }

        /// <summary>Points from `a` to `b` along the polyline (a, b measured in cells along the path).</summary>
        static List<Vector2> Window(List<Vector2> pts, float a, float b)
        {
            List<Vector2> o = new List<Vector2>();
            o.Add(At(pts, a));
            int i = Mathf.FloorToInt(a) + 1;
            while (i < b && i < pts.Count)
            {
                o.Add(pts[i]);
                i++;
            }
            o.Add(At(pts, b));
            return o;
        }

        static Vector2 At(List<Vector2> pts, float f)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, pts.Count - 2);
            float t = f - i;
            return Vector2.LerpUnclamped(pts[i], pts[i + 1], t);
        }

        /// <summary>The arrow moved `shift` cells along its own path and then straight on in the direction it points.</summary>
        List<Vector2> SlidePoints(Arrow a, float shift)
        {
            int len = a.cells.Length;
            Dir d = a.Direction;
            List<Vector2> pts = CellPoints(a);
            int x = a.Head.x, y = a.Head.y;
            int ext = 3 + Mathf.FloorToInt(shift);
            for (int i = 0; i < ext; i++)
            {
                x += DirUtil.Dx(d);
                y += DirUtil.Dy(d);
                pts.Add(new Vector2(x, -y));
            }
            return Window(pts, shift, shift + (len - 1));
        }

        /// <summary>The arrow sliding out of the board (follows its own path, then leaves in the direction it points).</summary>
        List<Vector2> ExitPoints(Ghost g)
        {
            Arrow a = g.arrow;
            int len = a.cells.Length;
            Dir d = a.Direction;
            List<Vector2> pts = CellPoints(a);
            int x = a.Head.x, y = a.Head.y;
            int outside = 0;
            while (outside < len + 1)
            {
                x += DirUtil.Dx(d);
                y += DirUtil.Dy(d);
                pts.Add(new Vector2(x, -y));
                if (x < 0 || x >= S.W || y < 0 || y >= S.H) outside++;
            }
            int rayCount = pts.Count - len;
            float s = g.Progress * rayCount;
            return Window(pts, s, s + (len - 1));
        }

        // ------------------------------------------------------------ input

        bool InArea(Vector2 screen) { return vp.Contains(screen); }

        void PanPixels(Vector2 delta)
        {
            camOffset.x -= delta.x / cellPx;
            camOffset.y -= delta.y / cellPx;
        }

        void ZoomAt(float newZoom, Vector2 pivot)
        {
            newZoom = Mathf.Clamp(newZoom, 1f, 4f);
            if (S == null || vp.width < 2f) { Zoom = newZoom; return; }
            UpdateCell();
            Vector2 c = Center + camOffset;
            Vector2 wp = c + (pivot - vp.center) / cellPx;
            Zoom = newZoom;
            cellPx = baseCell * Zoom;
            Vector2 c2 = wp - (pivot - vp.center) / cellPx;
            camOffset = c2 - Center;
            if (ZoomChanged != null) ZoomChanged(Zoom);
        }

        void HandleInput()
        {
            if (S.Loading || InputBlocked)
            {
                tracking = false;
                return;
            }
            int tc = Input.touchCount;
            if (tc >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                Vector2 pa = a.position - a.deltaPosition, pb = b.position - b.deltaPosition;
                float prevDist = (pa - pb).magnitude, curDist = (a.position - b.position).magnitude;
                Vector2 mid = (a.position + b.position) / 2f;
                if (prevDist > 1f && InArea(mid))
                {
                    ZoomAt(Zoom * (curDist / prevDist), mid);
                    PanPixels((a.deltaPosition + b.deltaPosition) / 2f);
                }
                moved = true;
                tracking = false;
                return;
            }
            if (tc == 1)
            {
                Touch t = Input.GetTouch(0);
                Pointer(t.phase == TouchPhase.Began, t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary,
                        t.phase == TouchPhase.Ended, t.position, t.deltaPosition);
                if (t.phase == TouchPhase.Canceled) tracking = false;
                return;
            }
            if (!Input.touchSupported)
            {
                // mouse (Unity editor / desktop)
                float wheel = Input.mouseScrollDelta.y;
                Vector2 mp = Input.mousePosition;
                if (Mathf.Abs(wheel) > 0.01f && InArea(mp)) ZoomAt(Zoom * (1f + wheel * 0.1f), mp);
                Vector2 delta = mp - lastMouse;
                Pointer(Input.GetMouseButtonDown(0), Input.GetMouseButton(0), Input.GetMouseButtonUp(0), mp, delta);
                lastMouse = mp;
            }
            else if (tracking)
            {
                tracking = false;
            }
        }

        void Pointer(bool began, bool held, bool ended, Vector2 pos, Vector2 delta)
        {
            if (began)
            {
                if (InArea(pos)) { tracking = true; moved = false; startPos = pos; startTime = Time.time; }
                return;
            }
            if (!tracking) return;
            if (ended)
            {
                if (!moved && Time.time - startTime < 0.6f) Tap(pos);
                tracking = false;
                return;
            }
            if (held)
            {
                if ((pos - startPos).magnitude > 14f) moved = true;
                if (moved) PanPixels(delta);
            }
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        void Tap(Vector2 screen)
        {
            Vector2 c = Center + camOffset;
            Vector2 world = c + (screen - vp.center) / cellPx;
            Vector2 g = new Vector2(world.x, -world.y);   // grid coordinates
            float thr = Mathf.Max(0.5f, 18f / cellPx);
            Arrow best = null;
            float bestD = thr;
            for (int i = 0; i < S.Arrows.Count; i++)
            {
                Arrow a = S.Arrows[i];
                for (int k = 0; k + 1 < a.cells.Length; k++)
                {
                    Vector2 p0 = new Vector2(a.cells[k].x, a.cells[k].y);
                    Vector2 p1 = new Vector2(a.cells[k + 1].x, a.cells[k + 1].y);
                    float d = DistToSegment(g, p0, p1);
                    if (d < bestD) { bestD = d; best = a; }
                }
            }
            if (best != null) S.OnTap(best);
        }
    }
}
