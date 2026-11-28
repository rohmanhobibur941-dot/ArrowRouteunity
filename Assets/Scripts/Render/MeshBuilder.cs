using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowRoute
{
    /// <summary>Collects coloured triangles (strokes, discs, quads) and uploads them to one Mesh.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color32> cols = new List<Color32>();
        readonly List<int> tris = new List<int>();

        public void Clear() { verts.Clear(); cols.Clear(); tris.Clear(); }

        int V(Vector2 p, Color32 c)
        {
            verts.Add(new Vector3(p.x, p.y, 0f));
            cols.Add(c);
            return verts.Count - 1;
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int i = V(a, col);
            V(b, col);
            V(c, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = V(a, col);
            V(b, col);
            V(c, col);
            V(d, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public void Disc(Vector2 center, float r, Color32 col, int seg)
        {
            int c0 = V(center, col);
            for (int s = 0; s <= seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg;
                V(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
            }
            for (int s = 0; s < seg; s++)
            {
                tris.Add(c0); tris.Add(c0 + 1 + s); tris.Add(c0 + 2 + s);
            }
        }

        public void Segment(Vector2 a, Vector2 b, float width, Color32 col)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);
            Quad(a + n, a - n, b - n, b + n, col);
        }

        /// <summary>A thick line along the points (mitered corners, optional round caps).</summary>
        public void Stroke(List<Vector2> raw, float width, Color32 col, bool caps)
        {
            List<Vector2> p = new List<Vector2>(raw.Count);
            for (int i = 0; i < raw.Count; i++)
            {
                if (p.Count == 0 || (raw[i] - p[p.Count - 1]).sqrMagnitude > 1e-6f) p.Add(raw[i]);
            }
            int n = p.Count;
            float hw = width * 0.5f;
            if (n < 2)
            {
                if (n == 1 && caps) Disc(p[0], hw, col, 8);
                return;
            }
            int baseI = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 dPrev = i > 0 ? (p[i] - p[i - 1]).normalized : (p[1] - p[0]).normalized;
                Vector2 dNext = i < n - 1 ? (p[i + 1] - p[i]).normalized : dPrev;
                Vector2 n1 = new Vector2(-dPrev.y, dPrev.x);
                Vector2 n2 = new Vector2(-dNext.y, dNext.x);
                Vector2 m = n1 + n2;
                float ml = m.magnitude;
                m = ml < 1e-4f ? n1 : m / ml;
                float k = Mathf.Max(0.45f, Vector2.Dot(m, n1));
                Vector2 off = m * (hw / k);
                V(p[i] + off, col);
                V(p[i] - off, col);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = baseI + 2 * i;
                tris.Add(a); tris.Add(a + 2); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 3);
            }
            if (caps)
            {
                Cap(p[0], (p[0] - p[1]).normalized, hw, col);
                Cap(p[n - 1], (p[n - 1] - p[n - 2]).normalized, hw, col);
            }
        }

        void Cap(Vector2 c, Vector2 outDir, float hw, Color32 col)
        {
            Vector2 nrm = new Vector2(-outDir.y, outDir.x);
            int c0 = V(c, col);
            const int seg = 6;
            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.PI * s / seg;
                V(c + nrm * (hw * Mathf.Cos(a)) + outDir * (hw * Mathf.Sin(a)), col);
            }
            for (int s = 0; s < seg; s++)
            {
                tris.Add(c0); tris.Add(c0 + 1 + s); tris.Add(c0 + 2 + s);
            }
        }

        /// <summary>A filled triangle with rounded corners (rad = how much it is rounded / grown).</summary>
        public void RoundedTri(Vector2 a, Vector2 b, Vector2 c, Color32 col, float rad)
        {
            Tri(a, b, c, col);
            Segment(a, b, rad * 2f, col);
            Segment(b, c, rad * 2f, col);
            Segment(c, a, rad * 2f, col);
            Disc(a, rad, col, 8);
            Disc(b, rad, col, 8);
            Disc(c, rad, col, 8);
        }

        public void Apply(Mesh m)
        {
            m.Clear();
            if (verts.Count == 0) return;
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
        }
    }
}
