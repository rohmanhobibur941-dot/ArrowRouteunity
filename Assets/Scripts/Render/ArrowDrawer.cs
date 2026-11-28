using System.Collections.Generic;
using UnityEngine;

namespace ArrowRoute
{
    /// <summary>
    /// Draws one snake-like arrow into a MeshBuilder: body with rounded corners and a rounded arrow head.
    /// Candy is glossy with an outline; Classic and Neon are flat, but just as thick.
    /// Coordinates are in cell units (1 cell = 1 unit), y points up.
    /// </summary>
    public static class ArrowDrawer
    {
        static Color32 C(Color c, float alpha)
        {
            return new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                               (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
        }

        /// <summary>Polyline with rounded (not 90 degree) corners.</summary>
        public static List<Vector2> RoundedPath(List<Vector2> pts, float r)
        {
            List<Vector2> o = new List<Vector2>(pts.Count * 3);
            o.Add(pts[0]);
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[i - 1], p1 = pts[i], p2 = pts[i + 1];
                Vector2 d1 = p1 - p0, d2 = p2 - p1;
                float l1 = d1.magnitude, l2 = d2.magnitude;
                float cross = d1.x * d2.y - d1.y * d2.x;
                if (l1 < 1e-4f || l2 < 1e-4f || Mathf.Abs(cross) < 0.01f * l1 * l2)
                {
                    o.Add(p1);   // straight on (or a zero length piece)
                    continue;
                }
                float rr = Mathf.Min(r, Mathf.Min(l1, l2) / 2f);
                Vector2 a = p1 - d1 / l1 * rr;
                Vector2 b = p1 + d2 / l2 * rr;
                o.Add(a);
                for (int s = 1; s <= 4; s++)
                {
                    float t = s / 5f;
                    float u = 1f - t;
                    o.Add(a * (u * u) + p1 * (2f * u * t) + b * (t * t));
                }
                o.Add(b);
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        public static void Draw(MeshBuilder mb, List<Vector2> pts, Color color, float sw, float alpha, bool candy)
        {
            if (pts.Count < 2) return;
            List<Vector2> path = RoundedPath(pts, sw * 1.5f);
            Color32 body = C(color, alpha);
            Color32 outline = C(Themes.Shade(color, 0.55f), alpha);
            if (candy) mb.Stroke(path, sw * 1.3f, outline, true);
            mb.Stroke(path, sw, body, true);
            if (candy)
            {
                List<Vector2> shifted = new List<Vector2>(path.Count);
                Vector2 off = new Vector2(-0.18f * sw, 0.18f * sw);
                for (int i = 0; i < path.Count; i++) shifted.Add(path[i] + off);
                mb.Stroke(shifted, sw * 0.26f, C(Color.white, 0.38f * alpha), true);
            }

            // arrow head
            Vector2 tip = pts[pts.Count - 1];
            int k = pts.Count - 2;
            while (k > 0 && (pts[k] - tip).sqrMagnitude < 1e-4f) k--;
            Vector2 dir = tip - pts[k];
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            float hl = sw * 1.9f;
            float hw = sw * 1.4f;
            Vector2 apex = tip + dir * hl;
            Vector2 b1 = new Vector2(tip.x - dir.y * hw, tip.y + dir.x * hw);
            Vector2 b2 = new Vector2(tip.x + dir.y * hw, tip.y - dir.x * hw);
            if (candy) mb.RoundedTri(apex, b1, b2, outline, sw * 0.225f);
            mb.RoundedTri(apex, b1, b2, body, sw * 0.1f);
        }
    }
}
