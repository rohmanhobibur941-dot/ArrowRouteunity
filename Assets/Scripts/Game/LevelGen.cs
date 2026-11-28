using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ArrowRoute
{
    /// <summary>
    /// Level generator (port of the Kotlin LevelGen). Every level is solvable: arrows are built so that
    /// they can be removed one after another. Pure C#, no UnityEngine objects, so it is safe to run
    /// on a background thread.
    /// </summary>
    public static class LevelGen
    {
        const int MinLen = 4;
        static readonly ShapeType[] Shapes = (ShapeType[])Enum.GetValues(typeof(ShapeType));

        // ------------------------------------------------------------ difficulty / counts

        /// <summary>0 easy, 1 medium, 2 hard, 3 extreme. Every block of 10 levels mixes the kinds. Level 1 is always easy.</summary>
        public static int DifficultyOf(int level)
        {
            GameConfig cfg = GameConfig.Current;
            int idx = Math.Max(0, level - 1);
            int block = idx / 10;
            int pos = idx % 10;
            int[] counts = cfg.mix[Math.Min(block, cfg.mix.Length - 1)].counts;
            List<int> list = new List<int>();
            for (int d = 0; d < 4; d++)
                for (int n = 0; n < counts[d]; n++) list.Add(d);
            while (list.Count < 10) list.Add(1);
            Random r = new Random(block * 7919 + 17);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                int t = list[i]; list[i] = list[j]; list[j] = t;
            }
            if (block == 0)
            {
                int e = list.IndexOf(0);
                if (e > 0) { list[e] = list[0]; list[0] = 0; }
            }
            return list[pos % list.Count];
        }

        /// <summary>Level 1 = 60 arrows, then +12 per level, fewer on easy and more on hard levels.</summary>
        public static int ArrowTarget(int level)
        {
            GameConfig cfg = GameConfig.Current;
            double b = cfg.arrowBase + cfg.arrowStep * (level - 1);
            double wobble = level <= 3 ? 0.0 : ((level * 7919) % 17 - 8) / 100.0;
            double factor = cfg.difficultyFactor[DifficultyOf(level)];
            int v = (int)(b * (1.0 + wobble) * factor);
            return Math.Max(cfg.arrowMin, Math.Min(cfg.arrowMax, v));
        }

        public static ShapeType ShapeFor(int level)
        {
            return Shapes[(Math.Max(1, level) - 1) % Shapes.Length];
        }

        // ------------------------------------------------------------ helpers used while playing

        public static HashSet<int> Occupancy(List<Arrow> arrows, int w)
        {
            HashSet<int> set = new HashSet<int>();
            for (int i = 0; i < arrows.Count; i++)
                foreach (Cell c in arrows[i].cells) set.Add(c.y * w + c.x);
            return set;
        }

        /// <summary>Steps from the head until the ray leaves the grid (at least 1).</summary>
        public static int ExitDistance(Arrow a, int w, int h)
        {
            Dir d = a.Direction;
            int x = a.Head.x, y = a.Head.y, n = 0;
            while (x >= 0 && x < w && y >= 0 && y < h)
            {
                x += DirUtil.Dx(d); y += DirUtil.Dy(d); n++;
            }
            return n;
        }

        /// <summary>Cells from the head to the first arrow in front of it (1 = right next to it).</summary>
        public static int BlockerDistance(Arrow a, int w, int h, HashSet<int> occ)
        {
            Dir d = a.Direction;
            int x = a.Head.x + DirUtil.Dx(d), y = a.Head.y + DirUtil.Dy(d), n = 1;
            while (x >= 0 && x < w && y >= 0 && y < h)
            {
                if (occ.Contains(y * w + x)) return n;
                x += DirUtil.Dx(d); y += DirUtil.Dy(d); n++;
            }
            return n;
        }

        /// <summary>True if every grid point in front of the head (to the edge) is empty.</summary>
        public static bool CanExit(Arrow a, int w, int h, HashSet<int> occ)
        {
            Dir d = a.Direction;
            int x = a.Head.x + DirUtil.Dx(d), y = a.Head.y + DirUtil.Dy(d);
            while (x >= 0 && x < w && y >= 0 && y < h)
            {
                if (occ.Contains(y * w + x)) return false;
                x += DirUtil.Dx(d); y += DirUtil.Dy(d);
            }
            return true;
        }

        // ------------------------------------------------------------ generation

        static void SizeFor(ShapeType shape, float aspect, int needed, out int gw, out int gh)
        {
            int w = 8;
            while (w < 90)
            {
                int h = Math.Max(8, (int)Math.Round(w / aspect));
                bool[] m = ShapeMasks.Make(shape, w, h, 2);
                int n = 0;
                for (int i = 0; i < m.Length; i++) if (m[i]) n++;
                if (n >= needed) { gw = w; gh = h; return; }
                w += n < needed * 0.8 ? 3 : 1;
            }
            gw = 90;
            gh = Math.Max(8, (int)Math.Round(90 / aspect));
        }

        /// <summary>aspect = board width / board height of the play area. Can take ~1-3 s on big levels.</summary>
        public static LevelData Generate(int level, float aspect, bool optimize)
        {
            ShapeType shape = ShapeFor(level);
            int diff = DifficultyOf(level);
            TierCfg tier = GameConfig.Current.tiers[diff];
            int target = ArrowTarget(level);
            Random rnd = new Random(Environment.TickCount ^ (level * 7919));
            Stopwatch sw = Stopwatch.StartNew();
            const long deadlineMs = 2500;

            float avg = (tier.longMin + tier.longMax) / 2f * 0.85f;
            float scale = 1f;
            LevelData best = null;
            int bestDiff = int.MaxValue;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int w, h;
                SizeFor(shape, aspect, (int)(target * avg * 1.05f), out w, out h);
                bool[] mask = ShapeMasks.Make(shape, w, h, 4);
                Gen g = new Gen(w, h, rnd, tier.straight);
                g.Build(mask, tier.longMin, tier.longMax, tier.keepFree, scale);
                if (optimize) g.Optimize(tier.keepFree, sw, deadlineMs);

                List<Arrow> list = new List<Arrow>();
                int cells = 0;
                for (int i = 0; i < g.arrows.Count; i++)
                {
                    List<int> p = g.arrows[i];
                    Cell[] cs = new Cell[p.Count];
                    for (int k = 0; k < p.Count; k++) cs[k] = new Cell(p[k] % w, p[k] / w);
                    list.Add(new Arrow(i, cs));
                    cells += p.Count;
                }
                int diffCount = Math.Abs(list.Count - target);
                if (diffCount < bestDiff)
                {
                    bestDiff = diffCount;
                    best = new LevelData { w = w, h = h, mask = mask, shape = shape, arrows = list };
                }
                if (diffCount <= target * 0.08) break;
                if (sw.ElapsedMilliseconds > deadlineMs) break;
                if (list.Count > 0) avg = (float)cells / list.Count;
                scale = Math.Max(0.5f, Math.Min(2f, scale * list.Count / target));
            }
            return best;
        }

        /// <summary>Never throws: falls back to a simpler generator and finally to a tiny level.</summary>
        public static LevelData GenerateSafe(int level, float aspect, Action<Exception> onError)
        {
            try { return Generate(level, aspect, true); }
            catch (Exception e) { if (onError != null) onError(e); }
            try { return Generate(level, aspect, false); }
            catch (Exception e) { if (onError != null) onError(e); }

            LevelData lv = new LevelData { w = 4, h = 4, shape = ShapeType.Square };
            lv.mask = new bool[16];
            for (int i = 0; i < 16; i++) lv.mask[i] = true;
            lv.arrows = new List<Arrow>
            {
                new Arrow(0, new[] { new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0) }),
                new Arrow(1, new[] { new Cell(0, 2), new Cell(1, 2), new Cell(2, 2), new Cell(3, 2) })
            };
            return lv;
        }

        // ================================================================== builder

        /// <summary>
        /// Two steps. 1) Build(): arrows are placed one by one (placement order = reverse removal order, so the
        /// level is solvable); while more than `keep` arrows are unblocked, the next arrow is forced to cover a
        /// cell in front of an unblocked arrow. 2) Optimize(): flips arrows and grows tails into empty cells so
        /// even more arrows are blocked, and fills holes. Every change is checked with a dependency graph.
        /// </summary>
        sealed class Gen
        {
            readonly int w, h;
            readonly Random rnd;
            readonly float straight;
            public readonly List<List<int>> arrows = new List<List<int>>();
            bool[] free = new bool[0];
            int[] owner = new int[0];

            public Gen(int w, int h, Random rnd, float straight)
            {
                this.w = w; this.h = h; this.rnd = rnd; this.straight = straight;
            }

            int PX(int k) { return k % w; }
            int PY(int k) { return k / w; }

            /// <summary>Direction index (0 up, 1 down, 2 left, 3 right) of a path, from its last two cells.</summary>
            int DirOf(List<int> p)
            {
                int a = p[p.Count - 2], b = p[p.Count - 1];
                int dx = PX(b) - PX(a), dy = PY(b) - PY(a);
                if (dx > 0) return 3;
                if (dx < 0) return 2;
                if (dy > 0) return 1;
                return 0;
            }

            List<int> RayCells(List<int> p)
            {
                int d = DirOf(p);
                int head = p[p.Count - 1];
                int x = PX(head) + DirUtil.DX[d], y = PY(head) + DirUtil.DY[d];
                List<int> o = new List<int>();
                while (x >= 0 && x < w && y >= 0 && y < h)
                {
                    o.Add(y * w + x);
                    x += DirUtil.DX[d]; y += DirUtil.DY[d];
                }
                return o;
            }

            bool RayFree(List<int> p, bool[] occ)
            {
                List<int> r = RayCells(p);
                for (int i = 0; i < r.Count; i++)
                    if (occ[r[i]] || p.Contains(r[i])) return false;
                return true;
            }

            bool SelfBlock(List<int> p)
            {
                List<int> r = RayCells(p);
                for (int i = 0; i < r.Count; i++) if (p.Contains(r[i])) return true;
                return false;
            }

            int FreeAhead(List<int> p, bool[] fr)
            {
                int n = 0;
                List<int> r = RayCells(p);
                for (int i = 0; i < r.Count; i++) if (fr[r[i]]) n++;
                return n;
            }

            bool OnRay(List<int> a, int c)
            {
                int d = DirOf(a);
                int head = a[a.Count - 1];
                int hx = PX(head), hy = PY(head), cx = PX(c), cy = PY(c);
                if (DirUtil.DY[d] == 0) return cy == hy && (cx - hx) * DirUtil.DX[d] > 0;
                return cx == hx && (cy - hy) * DirUtil.DY[d] > 0;
            }

            int FreeDegree(int x, int y, List<int> cells)
            {
                int n = 0;
                for (int d = 0; d < 4; d++)
                {
                    int ax = x + DirUtil.DX[d], ay = y + DirUtil.DY[d];
                    if (ax >= 0 && ax < w && ay >= 0 && ay < h)
                    {
                        int k = ay * w + ax;
                        if (free[k] && !cells.Contains(k)) n++;
                    }
                }
                return n;
            }

            List<int> GrowFrom(List<int> cells, int target)
            {
                int prevDir = -1;
                List<int> opts = new List<int>();
                List<int> cand = new List<int>();
                while (cells.Count < target)
                {
                    int cur = cells[cells.Count - 1];
                    int cx = PX(cur), cy = PY(cur);
                    opts.Clear();
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + DirUtil.DX[d], ny = cy + DirUtil.DY[d];
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                        {
                            int k = ny * w + nx;
                            if (free[k] && !cells.Contains(k)) opts.Add(d);
                        }
                    }
                    if (opts.Count == 0) break;
                    int nd;
                    if (opts.Count > 1 && rnd.NextDouble() < 0.5)
                    {
                        // step towards the most cramped neighbour first, so dead ends do not stay empty
                        int bestDeg = int.MaxValue;
                        cand.Clear();
                        for (int i = 0; i < opts.Count; i++)
                        {
                            int o = opts[i];
                            int dg = FreeDegree(cx + DirUtil.DX[o], cy + DirUtil.DY[o], cells);
                            if (dg < bestDeg) { bestDeg = dg; cand.Clear(); cand.Add(o); }
                            else if (dg == bestDeg) cand.Add(o);
                        }
                        nd = cand[rnd.Next(cand.Count)];
                    }
                    else if (prevDir >= 0 && opts.Contains(prevDir) && rnd.NextDouble() < straight)
                    {
                        nd = prevDir;
                    }
                    else
                    {
                        nd = opts[rnd.Next(opts.Count)];
                    }
                    prevDir = nd;
                    cells.Add((cy + DirUtil.DY[nd]) * w + (cx + DirUtil.DX[nd]));
                }
                return cells;
            }

            /// <summary>An arrow whose HEAD is cell c and points in direction d; its body grows behind the head.</summary>
            List<int> HeadAt(int c, int d, bool[] occ, int target)
            {
                int bx = PX(c) - DirUtil.DX[d], by = PY(c) - DirUtil.DY[d];
                if (bx < 0 || bx >= w || by < 0 || by >= h) return null;
                int b = by * w + bx;
                if (!free[b]) return null;
                List<int> cells = GrowFrom(new List<int> { c, b }, target);
                List<int> q = new List<int>(cells);
                q.Reverse();
                if (q.Count < MinLen) return null;
                if (!RayFree(q, occ)) return null;
                if (FreeAhead(q, free) < 1) return null;
                return q;
            }

            int PickLen(int longMin, int longMax, float scale)
            {
                int t = (int)Math.Round((longMin + rnd.Next(longMax - longMin + 1)) * scale);
                return Math.Max(MinLen, t);
            }

            void Shuffle<T>(List<T> l)
            {
                for (int i = l.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    T t = l[i]; l[i] = l[j]; l[j] = t;
                }
            }

            public void Build(bool[] mask, int longMin, int longMax, int keep, float scale)
            {
                arrows.Clear();
                free = (bool[])mask.Clone();
                bool[] occ = new bool[w * h];
                bool[] dead = new bool[w * h];

                double cx = (w - 1) / 2.0, cy = (h - 1) / 2.0;
                double maxd = Math.Sqrt(cx * cx + cy * cy);
                List<KeyValuePair<int, double>> keyed = new List<KeyValuePair<int, double>>();
                for (int k = 0; k < w * h; k++)
                {
                    if (!mask[k]) continue;
                    double dx = PX(k) - cx, dy = PY(k) - cy;
                    keyed.Add(new KeyValuePair<int, double>(k, Math.Sqrt(dx * dx + dy * dy) + rnd.NextDouble() * maxd * 0.35));
                }
                keyed.Sort((a, b) => a.Value.CompareTo(b.Value));
                List<int> order = new List<int>();
                for (int i = 0; i < keyed.Count; i++) order.Add(keyed[i].Key);

                int ptr = 0;
                List<int> unblocked = new List<int>();
                int guard = 0;
                List<int> dirs = new List<int> { 0, 1, 2, 3 };
                while (guard < w * h * 4)
                {
                    guard++;
                    // seed cells: first the cells in front of unblocked arrows (so they get blocked), then centre-out
                    List<int> seeds = new List<int>();
                    if (unblocked.Count > keep)
                    {
                        Dictionary<int, int> cov = new Dictionary<int, int>();
                        for (int u = 0; u < unblocked.Count; u++)
                        {
                            List<int> ray = RayCells(arrows[unblocked[u]]);
                            for (int r = 0; r < ray.Count; r++)
                            {
                                int k = ray[r];
                                if (free[k] && !dead[k])
                                {
                                    int cur;
                                    cov.TryGetValue(k, out cur);
                                    cov[k] = cur + 1;
                                }
                            }
                        }
                        if (cov.Count > 0)
                        {
                            List<KeyValuePair<int, double>> scored = new List<KeyValuePair<int, double>>();
                            foreach (KeyValuePair<int, int> kv in cov)
                                scored.Add(new KeyValuePair<int, double>(kv.Key, -kv.Value + rnd.NextDouble() * 1.5));
                            scored.Sort((a, b) => a.Value.CompareTo(b.Value));
                            for (int i = 0; i < scored.Count && i < 6; i++) seeds.Add(scored[i].Key);
                        }
                    }
                    while (ptr < order.Count && (!free[order[ptr]] || dead[order[ptr]])) ptr++;
                    if (ptr < order.Count) seeds.Add(order[ptr]);
                    if (seeds.Count == 0) break;

                    for (int s = 0; s < seeds.Count; s++)
                    {
                        int c = seeds[s];
                        List<int> cand = null;
                        int attempt = 0;
                        while (attempt < 8 && cand == null)
                        {
                            attempt++;
                            int t = PickLen(longMin, longMax, scale);
                            Shuffle(dirs);
                            for (int di = 0; di < 4; di++)
                            {
                                List<int> q = HeadAt(c, dirs[di], occ, t);
                                if (q != null) { cand = q; break; }
                            }
                            if (cand == null)
                            {
                                List<int> p = GrowFrom(new List<int> { c }, t);
                                if (p.Count >= MinLen)
                                {
                                    List<int> rev = new List<int>(p);
                                    rev.Reverse();
                                    List<int>[] pair = FreeAhead(p, free) >= FreeAhead(rev, free)
                                        ? new[] { p, rev } : new[] { rev, p };
                                    for (int pi = 0; pi < 2; pi++)
                                    {
                                        if (RayFree(pair[pi], occ) && FreeAhead(pair[pi], free) >= 1) { cand = pair[pi]; break; }
                                    }
                                }
                            }
                        }
                        if (cand != null)
                        {
                            arrows.Add(cand);
                            for (int i = 0; i < cand.Count; i++) { occ[cand[i]] = true; free[cand[i]] = false; }
                            List<int> nf = new List<int>();
                            for (int u = 0; u < unblocked.Count; u++)
                            {
                                bool blocked = false;
                                List<int> ua = arrows[unblocked[u]];
                                for (int i = 0; i < cand.Count; i++)
                                    if (OnRay(ua, cand[i])) { blocked = true; break; }
                                if (!blocked) nf.Add(unblocked[u]);
                            }
                            nf.Add(arrows.Count - 1);
                            unblocked = nf;
                            break;
                        }
                        dead[c] = true;
                    }
                }
            }

            // ---------------------------------------------------------- dependency graph

            /// <summary>deps[i] = arrows currently sitting in front of arrow i (they must leave first).</summary>
            int[][] ComputeDeps()
            {
                int n = arrows.Count;
                int[] stamp = new int[n];
                for (int i = 0; i < n; i++) stamp[i] = -1;
                int[] tmp = new int[n + 1];
                int[][] res = new int[n][];
                for (int i = 0; i < n; i++)
                {
                    List<int> a = arrows[i];
                    int d = DirOf(a);
                    int head = a[a.Count - 1];
                    int x = PX(head) + DirUtil.DX[d], y = PY(head) + DirUtil.DY[d];
                    int cnt = 0;
                    while (x >= 0 && x < w && y >= 0 && y < h)
                    {
                        int o = owner[y * w + x];
                        if (o >= 0 && stamp[o] != i) { stamp[o] = i; tmp[cnt++] = o; }
                        x += DirUtil.DX[d]; y += DirUtil.DY[d];
                    }
                    int[] r = new int[cnt];
                    Array.Copy(tmp, r, cnt);
                    res[i] = r;
                }
                return res;
            }

            /// <summary>True if arrows can all be removed one after another (no loop of arrows blocking each other).</summary>
            bool Acyclic(int[][] deps)
            {
                int n = deps.Length;
                int[] indeg = new int[n];
                List<int>[] rev = new List<int>[n];
                for (int i = 0; i < n; i++) { indeg[i] = deps[i].Length; rev[i] = new List<int>(); }
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < deps[i].Length; k++) rev[deps[i][k]].Add(i);
                int[] stack = new int[n + 1];
                int sp = 0, seen = 0;
                for (int i = 0; i < n; i++) if (indeg[i] == 0) stack[sp++] = i;
                while (sp > 0)
                {
                    int i = stack[--sp];
                    seen++;
                    List<int> us = rev[i];
                    for (int k = 0; k < us.Count; k++)
                    {
                        int u = us[k];
                        indeg[u]--;
                        if (indeg[u] == 0) stack[sp++] = u;
                    }
                }
                return seen == n;
            }

            /// <summary>Paths through empty cells from `start` to a cell next to the TAIL of some arrow.</summary>
            List<KeyValuePair<List<int>, int>> PathsToTail(int start, int maxd)
            {
                Dictionary<int, int> prev = new Dictionary<int, int>();
                Dictionary<int, int> depth = new Dictionary<int, int>();
                prev[start] = -1;
                depth[start] = 0;
                List<int> q = new List<int> { start };
                List<KeyValuePair<List<int>, int>> outp = new List<KeyValuePair<List<int>, int>>();
                int qi = 0;
                while (qi < q.Count)
                {
                    int v = q[qi++];
                    int vx = PX(v), vy = PY(v);
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = vx + DirUtil.DX[d], ny = vy + DirUtil.DY[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        int k = ny * w + nx;
                        int o = owner[k];
                        if (o >= 0 && arrows[o][0] == k)
                        {
                            List<int> p = new List<int>();
                            int c = v;
                            while (c != -1) { p.Add(c); c = prev[c]; }
                            outp.Add(new KeyValuePair<List<int>, int>(p, o));
                        }
                    }
                    int dv = depth[v];
                    if (dv < maxd)
                    {
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = vx + DirUtil.DX[d], ny = vy + DirUtil.DY[d];
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            int k = ny * w + nx;
                            if (free[k] && !prev.ContainsKey(k))
                            {
                                prev[k] = v; depth[k] = dv + 1; q.Add(k);
                            }
                        }
                    }
                }
                Shuffle(outp);
                return outp.OrderBy(e => e.Key.Count).ToList();   // OrderBy is stable
            }

            /// <summary>Grow the tail of arrow b through the empty cells in `path` (listed from the tail side back to the start).</summary>
            bool TryExtend(List<int> path, int b)
            {
                List<int> newCells = new List<int>(path);
                newCells.Reverse();
                List<int> old = arrows[b];
                List<int> nb = new List<int>(newCells.Count + old.Count);
                nb.AddRange(newCells);
                nb.AddRange(old);
                if (SelfBlock(nb)) return false;
                arrows[b] = nb;
                for (int i = 0; i < newCells.Count; i++) owner[newCells[i]] = b;
                if (Acyclic(ComputeDeps()))
                {
                    for (int i = 0; i < newCells.Count; i++) free[newCells[i]] = false;
                    return true;
                }
                arrows[b] = old;
                for (int i = 0; i < newCells.Count; i++) owner[newCells[i]] = -1;
                return false;
            }

            public void Optimize(int keep, Stopwatch sw, long deadlineMs)
            {
                owner = new int[w * h];
                for (int i = 0; i < owner.Length; i++) owner[i] = -1;
                for (int i = 0; i < arrows.Count; i++)
                    for (int k = 0; k < arrows[i].Count; k++) owner[arrows[i][k]] = i;

                int stall = 0, round = 0;
                while (round < 120 && sw.ElapsedMilliseconds < deadlineMs)
                {
                    round++;
                    int[][] deps = ComputeDeps();
                    List<int> fr = new List<int>();
                    for (int i = 0; i < deps.Length; i++) if (deps[i].Length == 0) fr.Add(i);
                    if (fr.Count <= keep) break;
                    Shuffle(fr);
                    bool prog = false;
                    for (int idx = keep; idx < fr.Count; idx++)
                    {
                        if (sw.ElapsedMilliseconds > deadlineMs) break;
                        int a = fr[idx];

                        // 1) point the arrow the other way if that side is blocked (and nothing deadlocks)
                        List<int> old = arrows[a];
                        List<int> rev = new List<int>(old);
                        rev.Reverse();
                        if (!SelfBlock(rev))
                        {
                            arrows[a] = rev;
                            int[][] d2 = ComputeDeps();
                            if (d2[a].Length > 0 && Acyclic(d2)) { prog = true; continue; }
                            arrows[a] = old;
                        }

                        // 2) grow the tail of another arrow into an empty cell in front of this arrow
                        List<int> gaps = new List<int>();
                        List<int> ray = RayCells(arrows[a]);
                        for (int i = 0; i < ray.Count; i++) if (free[ray[i]]) gaps.Add(ray[i]);
                        Shuffle(gaps);
                        bool done = false;
                        for (int gi = 0; gi < gaps.Count && gi < 12 && !done; gi++)
                        {
                            List<KeyValuePair<List<int>, int>> paths = PathsToTail(gaps[gi], 12);
                            for (int pi = 0; pi < paths.Count && pi < 16; pi++)
                            {
                                if (paths[pi].Value != a && TryExtend(paths[pi].Key, paths[pi].Value)) { done = true; break; }
                            }
                        }
                        if (done) prog = true;
                    }
                    if (!prog) { stall++; if (stall >= 3) break; }
                    else stall = 0;
                }

                // fill the remaining holes by growing tails (no new arrows, so no new free arrows)
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int c = 0; c < w * h; c++)
                    {
                        if (!free[c]) continue;
                        List<KeyValuePair<List<int>, int>> paths = PathsToTail(c, 2);
                        for (int pi = 0; pi < paths.Count && pi < 6; pi++)
                            if (TryExtend(paths[pi].Key, paths[pi].Value)) break;
                    }
                }
            }
        }
    }
}
