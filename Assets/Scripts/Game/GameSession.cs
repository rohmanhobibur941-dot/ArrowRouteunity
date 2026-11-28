using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace ArrowRoute
{
    /// <summary>An arrow that is sliding out of the board.</summary>
    public sealed class Ghost
    {
        public Arrow arrow;
        public float t;
        public float dur;
        public float Progress { get { return GameSession.Ease(Mathf.Clamp01(t / dur)); } }
    }

    /// <summary>
    /// The game rules (port of the Kotlin GameState): hearts, stars, hints, wrong taps, levels.
    /// Levels are generated on a background thread; call Tick(dt) every frame.
    /// </summary>
    public sealed class GameSession
    {
        readonly Fx fx;
        readonly float aspect;

        public int Level;
        public int Hints;
        public int Hearts = 3;
        public int W = 1, H = 1;
        public int TotalArrows = 1;
        public int Gen;            // changes when a new board starts
        public int Version;        // changes whenever the arrows / flashes on the board change
        public int Theme;
        public int Stars;
        public int BlockedId = -1;
        public int HintId = -1;
        public int BumpArrowId = -1;
        public float BumpShift;
        public bool Loading = true;
        public bool HintClaimed;
        public bool ReviveUsed;
        public GameStatus Status = GameStatus.Playing;
        public ShapeType Shape = ShapeType.Square;
        public bool[] Mask = new bool[1];
        public List<Arrow> Arrows = new List<Arrow>();
        public readonly List<Ghost> Ghosts = new List<Ghost>();

        public event Action Changed;

        readonly HashSet<int> penalized = new HashSet<int>();   // arrows that already cost a heart
        readonly HashSet<int> hinted = new HashSet<int>();      // arrows already shown by a hint

        LevelData current;
        int currentNo = -1;
        Task<LevelData> pending;
        int pendingNo = -1;
        Task<LevelData> prefetch;
        int prefetchNo = -1;

        bool bumping;
        float bumpT, bumpTravel, blockedTimer, hintTimer;

        public GameSession(Fx fx, float aspect)
        {
            this.fx = fx;
            this.aspect = aspect;
            Level = Prefs.Level;
            Hints = Prefs.Hints;
            Theme = Prefs.Theme;
        }

        public int Difficulty { get { return LevelGen.DifficultyOf(Level); } }
        public bool Animating { get { return Ghosts.Count > 0 || bumping || HintId >= 0; } }

        public static float Ease(float t) { return t * t * (3f - 2f * t); }

        void Fire() { if (Changed != null) Changed(); }

        // ------------------------------------------------------------ levels

        public void Begin() { StartLevel(); }

        /// <summary>Starts (or restarts) the current level. A restart plays the same puzzle again.</summary>
        public void StartLevel()
        {
            if (current != null && currentNo == Level) { Apply(current); return; }
            Request(Level);
        }

        void Request(int no)
        {
            if (pending != null && pendingNo == no) return;
            if (prefetch != null && prefetchNo == no)
            {
                pending = prefetch;
                prefetch = null;
            }
            else
            {
                float asp = aspect;
                pending = Task.Run(() => LevelGen.GenerateSafe(no, asp, null));
            }
            pendingNo = no;
            Loading = true;
            Arrows = new List<Arrow>();
            Ghosts.Clear();
            HintId = -1; BlockedId = -1; BumpArrowId = -1; bumping = false;
            Version++;
            Fire();
        }

        void StartPrefetch(int no)
        {
            if (prefetchNo == no) return;
            prefetchNo = no;
            float asp = aspect;
            prefetch = Task.Run(() => LevelGen.GenerateSafe(no, asp, null));
        }

        void Apply(LevelData lv)
        {
            W = lv.w;
            H = lv.h;
            Arrows = new List<Arrow>(lv.arrows);
            Mask = lv.mask;
            Shape = lv.shape;
            TotalArrows = Math.Max(1, lv.arrows.Count);
            Hearts = 3;
            Status = GameStatus.Playing;
            Ghosts.Clear();
            BlockedId = -1;
            BumpArrowId = -1;
            bumping = false;
            HintId = -1;
            HintClaimed = false;
            ReviveUsed = false;
            Stars = 0;
            penalized.Clear();
            hinted.Clear();
            Loading = false;
            Gen++;
            Version++;
            Fire();
        }

        // ------------------------------------------------------------ per frame

        public void Tick(float dt)
        {
            if (pending != null && pending.IsCompleted)
            {
                LevelData lv = null;
                if (!pending.IsFaulted && !pending.IsCanceled) lv = pending.Result;
                if (lv == null) lv = LevelGen.GenerateSafe(pendingNo, aspect, null);
                current = lv;
                currentNo = pendingNo;
                pending = null;
                Apply(lv);
                StartPrefetch(currentNo + 1);
            }

            bool changed = false;
            for (int i = Ghosts.Count - 1; i >= 0; i--)
            {
                Ghost g = Ghosts[i];
                g.t += dt;
                if (g.t >= g.dur) { Ghosts.RemoveAt(i); changed = true; }
            }
            if (bumping)
            {
                bumpT += dt;
                const float up = 0.11f, down = 0.26f;
                if (bumpT < up) BumpShift = bumpTravel * Ease(bumpT / up);
                else if (bumpT < up + down) BumpShift = bumpTravel * (1f - Ease((bumpT - up) / down));
                else { bumping = false; BumpShift = 0f; BumpArrowId = -1; changed = true; }
            }
            if (BlockedId >= 0)
            {
                blockedTimer -= dt;
                if (blockedTimer <= 0f) { BlockedId = -1; changed = true; }
            }
            if (HintId >= 0)
            {
                hintTimer -= dt;
                if (hintTimer <= 0f) { HintId = -1; changed = true; }
            }
            if (changed) { Version++; Fire(); }
        }

        // ------------------------------------------------------------ player actions

        public void OnTap(Arrow a)
        {
            if (Status != GameStatus.Playing || Loading) return;
            HashSet<int> occ = LevelGen.Occupancy(Arrows, W);
            if (LevelGen.CanExit(a, W, H, occ))
            {
                fx.Pop();
                Arrows = Arrows.Where(x => x.id != a.id).ToList();
                int dist = LevelGen.ExitDistance(a, W, H);
                Ghost g = new Ghost();
                g.arrow = a;
                g.t = 0f;
                g.dur = Mathf.Clamp((200f + (dist + a.cells.Length) * 25f) / 1000f, 0.25f, 0.9f);
                Ghosts.Add(g);
                if (HintId == a.id) HintId = -1;
                if (Arrows.Count == 0)
                {
                    Stars = Mathf.Clamp(Hearts, 1, 3);   // 3 hearts left = 3 stars ... 1 heart = 1 star
                    Status = GameStatus.Won;
                    fx.Win();
                }
            }
            else
            {
                // one heart per arrow: tapping the same blocked arrow again does not cost another one
                if (penalized.Add(a.id)) Hearts -= 1;
                fx.Error();
                BlockedId = a.id;
                blockedTimer = 0.75f;
                if (!bumping)
                {
                    // the arrow dashes (fast) into the arrow in front of it, then goes back to its place
                    int k = LevelGen.BlockerDistance(a, W, H, occ);
                    bumpTravel = Mathf.Max(0.15f, k - 0.7f);
                    BumpArrowId = a.id;
                    bumping = true;
                    bumpT = 0f;
                    BumpShift = 0f;
                }
                if (Hearts <= 0) Status = GameStatus.Lost;
            }
            Version++;
            Fire();
        }

        /// <summary>True if pressing hint costs nothing now: every arrow that can leave was already hinted.</summary>
        public bool HintIsFree()
        {
            if (Status != GameStatus.Playing) return false;
            HashSet<int> occ = LevelGen.Occupancy(Arrows, W);
            List<Arrow> free = Arrows.Where(x => LevelGen.CanExit(x, W, H, occ)).ToList();
            return free.Count > 0 && free.All(x => hinted.Contains(x.id));
        }

        /// <summary>
        /// Shows an arrow that can leave right now. A new arrow costs 1 hint and is remembered; press hint again
        /// (without taking it) and another arrow is shown. When every free arrow was shown already, they are shown again for free.
        /// </summary>
        public void UseHint()
        {
            if (Status != GameStatus.Playing || Loading) return;
            HashSet<int> occ = LevelGen.Occupancy(Arrows, W);
            List<Arrow> free = Arrows.Where(x => LevelGen.CanExit(x, W, H, occ)).ToList();
            if (free.Count == 0) return;
            List<Arrow> fresh = free.Where(x => !hinted.Contains(x.id)).ToList();
            Arrow a;
            if (fresh.Count > 0)
            {
                if (Hints <= 0) return;
                a = fresh[0];
                Hints -= 1;
                Prefs.Hints = Hints;
                hinted.Add(a.id);
            }
            else
            {
                List<Arrow> others = free.Where(x => x.id != HintId).ToList();
                a = others.Count > 0 ? others[0] : free[0];
            }
            HintId = a.id;
            hintTimer = 2.6f;
            Version++;
            Fire();
        }

        public void AddHint()
        {
            Hints += 1;
            Prefs.Hints = Hints;
            Fire();
        }

        /// <summary>Rewarded ad after a win: 1 hint, once per level.</summary>
        public void ClaimWinHint()
        {
            if (HintClaimed || Status != GameStatus.Won) return;
            HintClaimed = true;
            AddHint();
        }

        /// <summary>Rewarded ad after a loss: continue with 1 heart (once per level).</summary>
        public void Revive()
        {
            if (Status != GameStatus.Lost || ReviveUsed) return;
            ReviveUsed = true;
            Hearts = 1;
            Status = GameStatus.Playing;
            Fire();
        }

        public void NextLevel()
        {
            Level += 1;
            Prefs.Level = Level;
            StartLevel();
        }

        public void SetTheme(int theme)
        {
            Theme = theme;
            Prefs.Theme = theme;
            Version++;
            Fire();
        }
    }
}
