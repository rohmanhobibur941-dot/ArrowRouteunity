using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArrowRoute
{
    /// <summary>
    /// Turns a board silhouette into a grid of cells. No fonts and no bitmaps are used:
    /// numbers and letters are drawn as thick strokes, animals from ellipses and triangles.
    /// Thread safe (pure maths), so it can run in the background level generator.
    /// </summary>
    public static class ShapeMasks
    {
        public static bool[] Make(ShapeType shape, int w, int h, int samples)
        {
            bool[] m = new bool[w * h];
            List<Vector2[]> strokes = null;
            float half = 0f;
            if (IsGlyph(shape))
            {
                strokes = GlyphStrokes(shape, w, h);
                half = 0.115f * w;
            }
            float rh = Math.Min(h, 1.5f * w);
            float top = (h - rh) / 2f;
            int total = samples * samples;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples;
                            float py = y + (sy + 0.5f) / samples;
                            if (Inside(shape, px, py, w, h, rh, top, strokes, half)) hit++;
                        }
                    }
                    m[y * w + x] = hit * 2 >= total;
                }
            }
            return DropSpecks(m, w, h);
        }

        static bool IsGlyph(ShapeType s)
        {
            return s == ShapeType.One || s == ShapeType.Two || s == ShapeType.Three ||
                   s == ShapeType.LetterA || s == ShapeType.LetterB || s == ShapeType.LetterC;
        }

        static bool Inside(ShapeType s, float px, float py, int w, int h, float rh, float top, List<Vector2[]> strokes, float half)
        {
            float u = px / w;
            float v = (py - top) / rh;   // for animals: 0..1 inside their box
            switch (s)
            {
                case ShapeType.Square:
                {
                    float r = 0.05f * w;
                    float dx = Math.Min(px, w - px);
                    float dy = Math.Min(py, h - py);
                    if (dx < r && dy < r)
                    {
                        float ex = r - dx, ey = r - dy;
                        return ex * ex + ey * ey <= r * r;
                    }
                    return true;
                }
                case ShapeType.Circle:
                {
                    float a = (px / w - 0.5f) / 0.5f;
                    float b = (py / h - 0.5f) / 0.5f;
                    return a * a + b * b <= 1f;
                }
                case ShapeType.Cat:
                    return Ell(u, v, 0.5f, 0.6f, 0.42f, 0.34f)
                        || Tri(u, v, 0.08f, 0.52f, 0.12f, 0.04f, 0.46f, 0.28f)
                        || Tri(u, v, 0.92f, 0.52f, 0.88f, 0.04f, 0.54f, 0.28f);
                case ShapeType.Fish:
                    return Ell(u, v, 0.42f, 0.5f, 0.41f, 0.31f)
                        || Tri(u, v, 0.66f, 0.5f, 0.99f, 0.16f, 0.99f, 0.84f)
                        || Tri(u, v, 0.30f, 0.24f, 0.48f, 0.04f, 0.62f, 0.26f);
                case ShapeType.Rabbit:
                    return Ell(u, v, 0.36f, 0.19f, 0.10f, 0.19f)
                        || Ell(u, v, 0.64f, 0.19f, 0.10f, 0.19f)
                        || Ell(u, v, 0.5f, 0.46f, 0.26f, 0.19f)
                        || Ell(u, v, 0.5f, 0.78f, 0.34f, 0.21f);
                case ShapeType.Bear:
                    return Ell(u, v, 0.5f, 0.56f, 0.42f, 0.38f)
                        || Ell(u, v, 0.17f, 0.2f, 0.15f, 0.12f)
                        || Ell(u, v, 0.83f, 0.2f, 0.15f, 0.12f);
                default:
                {
                    // glyph: close to any stroke segment
                    Vector2 p = new Vector2(px, py);
                    for (int i = 0; i < strokes.Count; i++)
                    {
                        Vector2[] st = strokes[i];
                        for (int k = 0; k + 1 < st.Length; k++)
                        {
                            if (DistToSegment(p, st[k], st[k + 1]) <= half) return true;
                        }
                    }
                    return false;
                }
            }
        }

        static bool Ell(float u, float v, float cx, float cy, float rx, float ry)
        {
            float a = (u - cx) / rx, b = (v - cy) / ry;
            return a * a + b * b <= 1f;
        }

        static bool Tri(float u, float v, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float d1 = Side(u, v, x0, y0, x1, y1);
            float d2 = Side(u, v, x1, y1, x2, y2);
            float d3 = Side(u, v, x2, y2, x0, y0);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Side(float px, float py, float ax, float ay, float bx, float by)
        {
            return (px - bx) * (ay - by) - (ax - bx) * (py - by);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return (p - (a + ab * t)).magnitude;
        }

        // ------------------------------------------------------------ glyph strokes (normalised 0..1)

        static void Arc(List<Vector2> pts, float cx, float cy, float rx, float ry, float a0, float a1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = (a0 + (a1 - a0) * i / n) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + rx * Mathf.Cos(a), cy - ry * Mathf.Sin(a)));
            }
        }

        static List<Vector2> L(params float[] c)
        {
            List<Vector2> l = new List<Vector2>();
            for (int i = 0; i + 1 < c.Length; i += 2) l.Add(new Vector2(c[i], c[i + 1]));
            return l;
        }

        static List<Vector2[]> GlyphStrokes(ShapeType s, int w, int h)
        {
            List<List<Vector2>> raw = new List<List<Vector2>>();
            switch (s)
            {
                case ShapeType.One:
                    raw.Add(L(0.28f, 0.27f, 0.58f, 0.08f, 0.58f, 0.92f));
                    break;
                case ShapeType.Two:
                {
                    List<Vector2> p = new List<Vector2>();
                    Arc(p, 0.5f, 0.29f, 0.31f, 0.21f, 180f, 0f, 10);
                    p.Add(new Vector2(0.15f, 0.92f));
                    p.Add(new Vector2(0.86f, 0.92f));
                    raw.Add(p);
                    break;
                }
                case ShapeType.Three:
                {
                    List<Vector2> p = new List<Vector2>();
                    Arc(p, 0.5f, 0.285f, 0.30f, 0.205f, 160f, -90f, 12);
                    Arc(p, 0.5f, 0.70f, 0.34f, 0.22f, 90f, -160f, 12);
                    raw.Add(p);
                    break;
                }
                case ShapeType.LetterA:
                    raw.Add(L(0.10f, 0.92f, 0.5f, 0.08f, 0.90f, 0.92f));
                    raw.Add(L(0.27f, 0.64f, 0.73f, 0.64f));
                    break;
                case ShapeType.LetterB:
                {
                    raw.Add(L(0.20f, 0.08f, 0.20f, 0.92f));
                    List<Vector2> up = new List<Vector2>();
                    up.Add(new Vector2(0.20f, 0.08f));
                    up.Add(new Vector2(0.55f, 0.08f));
                    Arc(up, 0.55f, 0.285f, 0.27f, 0.205f, 90f, -90f, 10);
                    up.Add(new Vector2(0.20f, 0.49f));
                    raw.Add(up);
                    List<Vector2> lo = new List<Vector2>();
                    lo.Add(new Vector2(0.20f, 0.49f));
                    lo.Add(new Vector2(0.58f, 0.49f));
                    Arc(lo, 0.58f, 0.705f, 0.30f, 0.215f, 90f, -90f, 10);
                    lo.Add(new Vector2(0.20f, 0.92f));
                    raw.Add(lo);
                    break;
                }
                default: // C
                {
                    List<Vector2> p = new List<Vector2>();
                    Arc(p, 0.55f, 0.5f, 0.40f, 0.42f, 40f, 320f, 20);
                    raw.Add(p);
                    break;
                }
            }
            List<Vector2[]> result = new List<Vector2[]>();
            for (int j = 0; j < raw.Count; j++)
            {
                List<Vector2> r = raw[j];
                Vector2[] a = new Vector2[r.Count];
                for (int i = 0; i < r.Count; i++) a[i] = new Vector2(r[i].x * w, r[i].y * h);
                result.Add(a);
            }
            return result;
        }

        /// <summary>Removes tiny islands (fewer than 12 cells) that no arrow could ever use.</summary>
        static bool[] DropSpecks(bool[] m, int w, int h)
        {
            bool[] seen = new bool[w * h];
            int[] stack = new int[w * h];
            List<int> comp = new List<int>();
            for (int start = 0; start < w * h; start++)
            {
                if (!m[start] || seen[start]) continue;
                int sp = 0;
                stack[sp++] = start;
                seen[start] = true;
                comp.Clear();
                while (sp > 0)
                {
                    int k = stack[--sp];
                    comp.Add(k);
                    int x = k % w, y = k / w;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + DirUtil.DX[d], ny = y + DirUtil.DY[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int nk = ny * w + nx;
                        if (m[nk] && !seen[nk]) { seen[nk] = true; stack[sp++] = nk; }
                    }
                }
                if (comp.Count < 12) for (int i = 0; i < comp.Count; i++) m[comp[i]] = false;
            }
            return m;
        }
    }
}
