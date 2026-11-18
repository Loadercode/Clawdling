using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Clawdling
{
    enum St { Idle, Walk, Sit, Sleep, Look, Wave, Dance, Prep, Jump, Fall, Land, Drag, Climb, Dive, Hidden, Peek, PopOut, Celebrate, Notify, Ask, Hit, Shake, Scoop, Roll, WindUp, Throw, Yawn, Laugh, Love, Think, Shiver, Happy, Sad, Angry, Trip, Brawl, HighFive, Duck, Juggle, Spin, Stretch, Sneeze, Zen, Hug, Cover }
    enum Emo { Normal, Happy, Closed, Sleepy, Surprised, Angry, Sad, Love, Dizzy, Wink, Excited, Focus, Pleading, Laugh }

    class Part { public int k; public float x, y, vx, vy, life, max, size; public Color c; }
    class WinInfo { public IntPtr h; public N.RECT r; }

    partial class Pet : LayeredWindow
    {
        // ---------- config / geometry ----------
        readonly Cfg cfg;
        readonly Pet leader;   // null for Claude (the main pet); set for the Codex buddy
        internal Pet buddy;    // Codex, owned by the main pet
        internal bool codex;
        readonly float dpi;
        float u, k;            // pixels per sprite unit, speed scale
        int CW, CH; float Ox, Oy;
        const float G0 = 1500f, HIDE_SINK = 19f;

        // ---------- state ----------
        float px, py, vx, vy; int dir = 1;
        St st = St.Fall; float t, dur;
        int surf = -1; IntPtr sHwnd; N.RECT sRect; float sVis, sOcc;
        float walkTo, walkSpd, legPhase; bool climbNext, running;
        int climbSide, climbPhase; float climbTo;
        float sink, sinkStart, rot;
        float blinkIn = 3, blinkT;
        float lookX, lookY;
        readonly List<Part> parts = new List<Part>();
        float hatV, hatA, hatTip = -3.2f;
        float snowOnBody; int snowSide = 1; bool hitBySnow;
        float ballR; bool holding; internal int gThrows;
        bool targetCursor; PointF target; Pet targetPet;
        float clock, lastThrow = -20, throwCd = 30, idleTotal, partT;
        bool mouseIsDown, dragging; Point downPt; float grabDx, grabDy, dragVx;
        readonly List<float[]> dragHist = new List<float[]>();
        readonly List<float[]> heldHist = new List<float[]>();
        int pokes; float lastClick = -10, lastPoke = -10;
        bool dodged, leaping, greeted; float leapVx, leapVy; int pendingReact;
        Rectangle wa, sb; float monT;
        int curX, curY;
        List<WinInfo> wins = new List<WinInfo>(); float winsT = -10;
        readonly uint myPid;

        // ---------- helpers ----------
        readonly Bubble bubble;
        readonly Queue<object[]> queue = new Queue<object[]>();
        string pendingSpeech;
        readonly List<Snowball> balls = new List<Snowball>();
        readonly List<Splat> splats = new List<Splat>();
        Snowball held;
        SnowStrip strip;
        NotifyIcon tray; ContextMenuStrip menu;
        ToolStripMenuItem miNY, miSnow, miThrow, miAuto, miHide, miCodex; ToolStripMenuItem[] miSize, miGames;
        readonly System.Windows.Forms.Timer timer;
        internal bool inGame; internal Pet hitFrom; float tripCd = 20;
        readonly Stopwatch sw = Stopwatch.StartNew(); double last;
        bool fsHidden; float fsT, zT = 1, trimT = 4;
        readonly PipeServer pipe;

        // ---------- drawing ----------
        Graphics G; float ox, oy;
        SolidBrush bBody = new SolidBrush(Color.FromArgb(255, 217, 119, 87));
        SolidBrush bDark = new SolidBrush(Color.FromArgb(255, 184, 92, 62));
        SolidBrush bLight = new SolidBrush(Color.FromArgb(255, 229, 145, 116));
        readonly SolidBrush bEye = new SolidBrush(Color.FromArgb(255, 35, 24, 21));
        readonly SolidBrush bWhite = new SolidBrush(Color.White);
        readonly SolidBrush bBlush = new SolidBrush(Color.FromArgb(120, 255, 120, 130));
        readonly SolidBrush bRed = new SolidBrush(Color.FromArgb(255, 232, 64, 84));
        readonly SolidBrush bYellow = new SolidBrush(Color.FromArgb(255, 255, 201, 60));
        readonly SolidBrush bPurple = new SolidBrush(Color.FromArgb(255, 132, 100, 236));
        readonly SolidBrush bPink = new SolidBrush(Color.FromArgb(255, 255, 122, 150));
        readonly SolidBrush bTear = new SolidBrush(Color.FromArgb(230, 110, 180, 255));
        readonly SolidBrush bShadow = new SolidBrush(Color.FromArgb(38, 0, 0, 0));
        readonly SolidBrush bTmp = new SolidBrush(Color.White);
        static readonly Color[] confetti = { Color.FromArgb(255, 232, 64, 84), Color.FromArgb(255, 255, 201, 60), Color.FromArgb(255, 80, 190, 120), Color.FromArgb(255, 70, 150, 240), Color.FromArgb(255, 170, 110, 240), Color.FromArgb(255, 217, 119, 87) };

        public Pet() : base(10, 10, false)
        {
            cfg = Cfg.Load();
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) dpi = g.DpiX / 96f;
            myPid = (uint)Process.GetCurrentProcess().Id;
            ApplySize();
            var scr = Screen.PrimaryScreen; wa = scr.WorkingArea; sb = scr.Bounds;
            px = wa.Left + wa.Width * U.Rf(0.55f, 0.85f); py = wa.Top + 20 * u;
            StartFall(0, 0);
            bubble = new Bubble(dpi);
            BuildMenu(); BuildTray();
            timer = new System.Windows.Forms.Timer(); timer.Interval = 33; timer.Tick += (s, e) => Tick(); timer.Start();
            pipe = new PipeServer(this); pipe.Start();
            N.RegisterHotKey(Handle, 1, 0x0002 | 0x0001 | 0x4000, (uint)Keys.S); // Ctrl+Alt+S
            gCd = U.Rf(10, 18);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (cfg.Hidden) Hide();
            UpdateStrip();
            if (leader == null && cfg.Codex) SetBuddy(true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) { UserThrowAtPet(); return; } // WM_HOTKEY
            base.WndProc(ref m);
        }

        void ApplySize()
        {
            float[] bases = { 2f, 3f, 4f };
            u = (float)Math.Max(2, Math.Round(bases[cfg.Size] * dpi));
            k = u / 3f;
            CW = (int)(34 * u); CH = (int)(34 * u);
            Ox = CW / 2f; Oy = CH - 2 * u;
            SetCanvas(CW, CH);
        }

        // =====================================================================
        //  Main loop
        // =====================================================================
        void Tick()
        {
            double now = sw.Elapsed.TotalSeconds; float dt = (float)(now - last); last = now;
            if (dt > 0.1f) dt = 0.1f; if (dt <= 0) dt = 0.001f;
            trimT -= dt; if (trimT <= 0) { trimT = 90; N.TrimMemory(); }
            Frame(dt);
            if (buddy != null) { buddy.Frame(dt); UpdateDuo(dt); }
            bool fast = Busy || (buddy != null && buddy.Busy);
            bool allHidden = cfg.Hidden || (fsHidden && (buddy == null || buddy.fsHidden));
            int want = allHidden ? 250 : fast ? 15 : 33;
            if (timer.Interval != want) timer.Interval = want;
        }

        bool Busy { get { return balls.Count > 0 || splats.Count > 0 || dragging || st == St.Fall || st == St.Jump || held != null || st == St.Brawl || st == St.Spin; } }

        void Frame(float dt)
        {
            clock += dt;
            fsT -= dt;
            if (fsT <= 0) { fsT = 0.5f; bool fs = DetectFullscreen(); if (fs != fsHidden) { fsHidden = fs; ApplyVisibility(); } }
            if (bubble == null || fsHidden || cfg.Hidden) return;

            var cp = Cursor.Position; curX = cp.X; curY = cp.Y;
            monT -= dt; if (monT <= 0) { monT = 0.5f; RefreshMonitor(); }
            zT -= dt; if (zT <= 0) { zT = 2f; ReassertZ(); }

            UpdatePet(dt);
            UpdateLook(dt);
            UpdateParticles(dt);
            RenderPet();
            TickLater(dt);
            UpdateBubble(dt);
            UpdateBalls(dt);
            if (strip != null && strip.Visible) strip.Tick(dt);
        }

        void RefreshMonitor()
        {
            var scr = Screen.FromPoint(new Point((int)px, (int)(py - 4)));
            wa = scr.WorkingArea; sb = scr.Bounds;
            UpdateStrip();
        }

        void ReassertZ()
        {
            N.BringTop(Handle);
            if (strip != null && strip.Visible) N.BringTop(strip.Handle);
            if (bubble.Visible) N.BringTop(bubble.Handle);
            foreach (var b in balls) N.BringTop(b.Handle);
        }

        static readonly string[] shellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };
        bool DetectFullscreen()
        {
            // QUNS_BUSY (2) is unreliable (also reported for Do-Not-Disturb etc.), so only trust D3D-fullscreen (3) and presentation mode (4).
            int qs = 0;
            N.SHQueryUserNotificationState(out qs);
            if (qs == 4) return true;
            IntPtr fg = N.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            uint pid; N.GetWindowThreadProcessId(fg, out pid);
            if (pid == myPid) return false;
            string cls = N.Cls(fg);
            foreach (var c in shellClasses) if (cls == c) return false;
            var fgScreen = Screen.FromHandle(fg);
            var petScreen = Screen.FromPoint(new Point((int)px, (int)py - 4));
            if (fgScreen.DeviceName != petScreen.DeviceName) return false; // fullscreen on another monitor: keep walking here
            if (qs == 3) return true;
            N.RECT r; if (!N.Rect(fg, out r)) return false;
            var mb = fgScreen.Bounds;
            if (!(r.L <= mb.Left && r.T <= mb.Top && r.R >= mb.Right && r.B >= mb.Bottom)) return false;
            int style = N.GetWindowLong(fg, -16);
            bool maximizedWithCaption = (style & 0x01000000) != 0 && (style & 0x00C00000) == 0x00C00000; // WS_MAXIMIZE + WS_CAPTION
            return !maximizedWithCaption;
        }

        void ApplyVisibility()
        {
            bool show = !fsHidden && !cfg.Hidden;
            if (show)
            {
                if (!Visible) Show();
                RefreshMonitor();
                if (surf == 0) py = wa.Bottom;
            }
            else
            {
                if (Visible) Hide();
                if (bubble.Active) { if (bubble.Kind != 0 && bubble.Kind != 3 && queue.Count < 5) { } bubble.Kill(); }
                foreach (var b in balls) b.Close(); balls.Clear(); held = null;
                foreach (var s in splats) s.Close(); splats.Clear();
                if (dragging) { dragging = false; mouseIsDown = false; StartFall(0, 0); }
            }
            UpdateStrip();
        }

        void UpdateStrip()
        {
            if (leader != null) return; // the snow strip belongs to the main pet
            bool want = cfg.NewYear && cfg.Snow && !fsHidden && !cfg.Hidden;
            var ps = Screen.PrimaryScreen;
            if (want && ps.WorkingArea.Bottom >= ps.Bounds.Bottom) want = false; // taskbar is not at the bottom
            if (want)
            {
                if (strip == null) strip = new SnowStrip(this, dpi);
                if (strip.Area != ps.WorkingArea) strip.Setup(ps.WorkingArea);
                if (!strip.Visible) { strip.Show(); ReassertZ(); }
            }
            else if (strip != null && strip.Visible) strip.Hide();
        }

        // =====================================================================
        //  Behaviour
        // =====================================================================
        void Go(St s, float d)
        {
            if (s != St.Roll && s != St.WindUp) holding = false;
            st = s; t = 0; dur = d;
        }
        void GoIdle() { Go(St.Idle, U.Rf(1.2f, 3.5f)); }
        float Gr { get { return G0 * k; } }

        void StartFall(float vx0, float vy0)
        {
            st = St.Fall; t = 0; surf = -1; vx = vx0; vy = vy0; sink = 0; holding = false;
        }

        bool OnGround { get { return !dragging && surf >= 0 && st != St.Climb && st != St.Fall && st != St.Jump; } }
        bool HiddenState { get { return st == St.Hidden || st == St.Dive || st == St.Peek || st == St.PopOut; } }

        void UpdatePet(float dt)
        {
            t += dt;
            if (snowOnBody > 0 && st != St.Hit && st != St.Shake) snowOnBody = Math.Max(0, snowOnBody - dt * 0.25f);
            blinkIn -= dt; if (blinkIn <= 0) { blinkT = 0.13f; blinkIn = U.Rf(1.8f, 5f); }
            if (blinkT > 0) blinkT -= dt;
            if (dragging) { UpdateDrag(dt); return; }
            if (st == St.Fall || st == St.Jump) { UpdateAir(dt); return; }
            if (st == St.Climb) { UpdateClimb(dt); return; }

            if (surf == 1)
            {
                if (!ValidateWindow(dt)) { StartFall(st == St.Walk ? dir * walkSpd : 0, -60 * k); return; }
            }
            else if (surf == 0)
            {
                py = wa.Bottom;
                float lo = wa.Left + 7 * u, hi = wa.Right - 7 * u;
                if (px < lo) px = lo; if (px > hi) px = hi;
            }
            else { StartFall(0, 0); return; }
            idleTotal += dt;
            UpdateGroundState(dt);
        }

        void UpdateGroundState(float dt)
        {
            partT -= dt;
            switch (st)
            {
                case St.Idle:
                    if (t >= dur && !inGame) ChooseNext();
                    break;
                case St.Walk:
                    {
                        float d = walkTo - px, step = walkSpd * dt;
                        if (Math.Abs(d) <= step)
                        {
                            px = walkTo;
                            if (climbNext && surf == 0) { climbNext = false; EnterClimb(walkTo < wa.Left + wa.Width / 2 ? -1 : 1); }
                            else GoIdle();
                        }
                        else { dir = Math.Sign(d); px += dir * step; legPhase += dt * walkSpd / (1.6f * u); }
                        tripCd -= dt;
                        if (codex && running && tripCd <= 0 && U.Ch(dt * 0.35f)) { tripCd = U.Rf(12, 30); Go(St.Trip, 1.9f); break; }
                        if (cfg.NewYear && running && partT <= 0 && surf == 0 && strip != null && strip.Visible) { partT = 0.08f; Sp(4, -dir * 3, 0, -dir * U.Rf(2, 6), U.Rf(-8, -4), 0.6f, U.Rf(0.5f, 0.9f), Color.White); }
                        break;
                    }
                case St.Sit:
                    if (t >= dur) { if (U.Ch(0.35)) Go(St.Sleep, U.Rf(15, 45)); else GoIdle(); }
                    break;
                case St.Sleep:
                    if (partT <= 0) { partT = 1.1f; Sp(1, 3, -11, 1.2f, -2.4f, 2.6f, 0.45f, Color.FromArgb(255, 120, 140, 200)); }
                    if (t >= dur) Go(St.Yawn, 1.6f);
                    break;
                case St.Yawn:
                    if (t >= dur) { if (U.Ch(0.25)) Go(St.Sleep, U.Rf(15, 35)); else GoIdle(); }
                    break;
                case St.Dance:
                    if (partT <= 0) { partT = 0.45f; Sp(2, U.Rf(-7, 7), -12, U.Rf(-1.5f, 1.5f), -3.5f, 1.6f, 1, U.Ch(0.5) ? Color.FromArgb(255, 132, 100, 236) : Color.FromArgb(255, 70, 150, 240)); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Love:
                    if (partT <= 0) { partT = 0.35f; Sp(0, U.Rf(-5, 5), -12, U.Rf(-1, 1), -3, 1.5f, 0.45f, Color.FromArgb(255, 232, 64, 84)); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Celebrate:
                    if (partT <= 0 && t < 1.2f) { partT = 0.05f; Sp(3, U.Rf(-6, 6), -14, U.Rf(-7, 7), U.Rf(-12, -6), 1.6f, U.Rf(0.6f, 0.9f), confetti[U.R.Next(confetti.Length)]); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Angry:
                    if (partT <= 0) { partT = 0.3f; Sp(6, U.Rf(-4, 4), -12, 0, -3, 0.9f, 1.2f, Color.FromArgb(255, 255, 255, 255)); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Shiver:
                    if (partT <= 0) { partT = 0.55f; Sp(6, dir * 2.5f, -6.5f, dir * 3f, -1.2f, 1.2f, 0.9f, Color.White); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Happy:
                    if (t < 0.05f && parts.Count < 60) Sp(0, U.Rf(-3, 3), -12, U.Rf(-1, 1), -3, 1.3f, 0.45f, Color.FromArgb(255, 232, 64, 84));
                    if (t >= dur) GoIdle();
                    break;
                case St.Look: case St.Wave: case St.Think: case St.Laugh: case St.Sad: case St.Notify: case St.Land: case St.Ask:
                    if (t >= dur) GoIdle();
                    break;
                case St.Prep:
                    if (t >= dur)
                    {
                        if (leaping) { vx = leapVx; vy = leapVy; leaping = false; } else { vx = 0; vy = -440 * k; }
                        st = St.Jump; t = 0; surf = -1;
                    }
                    break;
                case St.Dive:
                    sink = Math.Min(HIDE_SINK, t / dur * HIDE_SINK);
                    if (t < 0.1f && cfg.NewYear) SnowPuff(6);
                    if (t >= dur) { sink = HIDE_SINK; Go(St.Hidden, U.Rf(8, 25)); }
                    break;
                case St.Hidden:
                    if (t >= dur) Go(St.Peek, 2.8f);
                    break;
                case St.Peek:
                    sink = HIDE_SINK - (HIDE_SINK - 5.5f) * Math.Min(1, t / 0.5f);
                    if (t >= dur) { sinkStart = sink; Go(St.PopOut, 0.18f); }
                    break;
                case St.PopOut:
                    sink = sinkStart * Math.Max(0, 1 - t / dur);
                    if (t >= dur) { sink = 0; vx = 0; vy = -420 * k; st = St.Jump; t = 0; surf = -1; if (cfg.NewYear) SnowPuff(8); }
                    break;
                case St.Hit:
                    if (hitBySnow && t < 0.05f) { }
                    if (t >= dur) Go(St.Shake, 0.6f);
                    break;
                case St.Shake:
                    if (snowOnBody > 0) snowOnBody = Math.Max(0, 1 - t / dur);
                    if (partT <= 0 && hitBySnow) { partT = 0.04f; Sp(4, snowSide * 4 + U.Rf(-2, 2), -6 + U.Rf(-2, 2), snowSide * U.Rf(3, 9), U.Rf(-8, -2), 0.7f, U.Rf(0.5f, 0.9f), Color.White); }
                    if (t >= dur)
                    {
                        snowOnBody = 0;
                        if (inGame) { hitBySnow = false; hitFrom = null; GoIdle(); dur = U.Rf(0.3f, 0.9f); break; }
                        if (hitBySnow && hitFrom != null && hitFrom != this && hitFrom.Visible && cfg.NewYear && cfg.Snow && U.Ch(0.7))
                        {
                            Say(codex ? U.Pick("Эй! Это нечестно!", "Ну всё!", "Ах ты!") : U.Pick("Кодекс!", "Ах так?!", "Ответочка!"), 1.6f);
                            StartSnowballAt(hitFrom);
                        }
                        else if (hitBySnow && cfg.NewYear && cfg.Snow && U.Ch(0.75))
                        {
                            Say(U.Pick("Ах так?!", "Ну держись!", "Моя очередь!", "Ответочка!"), 1.8f);
                            StartSnowball(true);
                        }
                        else if (hitBySnow) { Go(St.Laugh, 1f); if (U.Ch(0.5)) Say("Ха-ха!", 1.4f); }
                        else GoIdle();
                        hitBySnow = false; hitFrom = null;
                    }
                    break;
                case St.Scoop:
                    if (partT <= 0) { partT = 0.05f; Sp(4, U.Rf(-4, 4), -0.5f, U.Rf(-5, 5), U.Rf(-9, -4), 0.6f, U.Rf(0.5f, 0.9f), Color.White); }
                    if (t >= dur) { holding = true; ballR = 0.6f; Go(St.Roll, 1.3f); holding = true; }
                    break;
                case St.Roll:
                    ballR = 0.6f + 1.5f * Math.Min(1, t / dur);
                    if (partT <= 0) { partT = 0.12f; Sp(4, U.Rf(-2, 2), -2.5f, U.Rf(-3, 3), U.Rf(-4, -1), 0.5f, 0.5f, Color.White); }
                    if (t >= dur) { FaceTarget(); Go(St.WindUp, 0.42f); holding = true; }
                    break;
                case St.WindUp:
                    if (t >= dur) { ThrowBall(); Go(St.Throw, 0.35f); }
                    break;
                case St.Throw:
                    if (t >= dur) { if (U.Ch(0.35)) Go(St.Laugh, 0.9f); else GoIdle(); }
                    break;
                case St.Trip:
                    if (t > 0.3f && t - dt <= 0.3f) { Sp(6, dir * 6, -1, dir * 3, -1, 0.9f, 3.5f, Color.FromArgb(255, 205, 200, 195)); Sp(6, dir * 3, -1, -dir * 2, -1.5f, 0.8f, 3f, Color.FromArgb(255, 205, 200, 195)); }
                    if (t >= dur) { GoIdle(); Say(U.Pick("Я в порядке!", "Так и задумано!", "Пол скользкий...", "Ой... никто не видел?"), 1.6f); }
                    break;
                case St.Brawl:
                    if (partT <= 0)
                    {
                        partT = 0.07f;
                        Sp(6, U.Rf(-8, 8), U.Rf(-12, -1), U.Rf(-3, 3), U.Rf(-3, 0), U.Rf(0.5f, 0.8f), U.Rf(3, 6), Color.FromArgb(255, U.R.Next(200, 232), U.R.Next(198, 226), U.R.Next(195, 220)));
                        if (U.Ch(0.25)) Sp(7, U.Rf(-9, 9), U.Rf(-15, -4), U.Rf(-6, 6), U.Rf(-6, -2), 0.6f, 0.5f, Color.FromArgb(255, 255, 210, 70));
                    }
                    if (t >= dur) GoIdle();
                    break;
                case St.HighFive:
                    if (t > 0.32f && t - dt <= 0.32f) for (int i = 0; i < 8; i++) Sp(3, dir * 8 + U.Rf(-1, 1), -14, U.Rf(-5, 5), U.Rf(-10, -4), 1.1f, U.Rf(0.6f, 0.9f), confetti[U.R.Next(confetti.Length)]);
                    if (t >= dur) Go(St.Happy, 0.9f);
                    break;
                case St.Juggle:
                    if (t >= dur)
                    {
                        if (codex && U.Ch(0.5)) { hitBySnow = false; hitFrom = null; Go(St.Hit, 1.2f); Say(U.Pick("Ай! Прямо в макушку", "Ой-ой-ой...", "Это всё гравитация!"), 1.6f); }
                        else { Go(St.Happy, 0.9f); if (U.Ch(0.4)) Say("Та-да!", 1.2f); }
                    }
                    break;
                case St.Spin:
                    legPhase += dt * 12; dir = U.Sin(t * 16) > 0 ? 1 : -1;
                    if (partT <= 0) { partT = 0.1f; Sp(6, U.Rf(-5, 5), -0.5f, U.Rf(-3, 3), -0.6f, 0.6f, U.Rf(1.5f, 2.5f), Color.FromArgb(255, 205, 200, 195)); }
                    if (t >= dur)
                    {
                        hitBySnow = false; hitFrom = null; Go(St.Hit, 1.1f);
                        if (U.Ch(0.5)) Say(codex ? U.Pick("Всё кружится...", "Где я?", "Хвост не нашёл...") : U.Pick("Ух, голова кружится!", "Вжух!"), 1.4f);
                    }
                    break;
                case St.Sneeze:
                    if (t > 0.85f && t - dt <= 0.85f)
                    {
                        for (int i = 0; i < 7; i++) Sp(6, dir * 7, -6, dir * U.Rf(5, 11), U.Rf(-3, 1), 0.7f, U.Rf(1.2f, 2.4f), Color.FromArgb(255, 235, 240, 248));
                        px -= dir * (codex ? 4 : 2) * u;
                        Say(codex ? U.Pick("АПЧХИ!!!", "Апчхи! Ой, это был я?") : U.Pick("Апчхи!", "Апч-хи!"), 1.3f);
                    }
                    if (t >= dur) GoIdle();
                    break;
                case St.Zen:
                    if (partT <= 0) { partT = 0.4f; Sp(7, U.Rf(-8, 8), U.Rf(-13, -3), 0, -1.6f, 1.3f, 0.45f, Color.FromArgb(255, 176, 140, 255)); }
                    if (t >= dur) GoIdle();
                    break;
                case St.Hug:
                    if (partT <= 0) { partT = 0.3f; Sp(0, dir * 5 + U.Rf(-2, 2), -12, U.Rf(-1, 1), -3, 1.5f, 0.45f, Color.FromArgb(255, 232, 64, 84)); }
                    if (t >= dur) Go(St.Love, 1.2f);
                    break;
                default:
                    if (t >= dur) GoIdle();
                    break;
            }
        }

        void ChooseNext()
        {
            var opts = new List<KeyValuePair<int, float>>();
            Action<int, float> add = (c, w) => { if (w > 0) opts.Add(new KeyValuePair<int, float>(c, w)); };
            bool floor = surf == 0;
            bool tb = floor && wa.Bottom < sb.Bottom;
            bool near = U.Hyp(curX - px, curY - (py - 8 * u)) < 140 * k;
            bool snowOn = cfg.NewYear && cfg.Snow && floor && strip != null && strip.Visible;
            add(0, 34); add(1, 8); add(2, 6); add(3, 4); add(4, near ? 10 : 2); add(5, 3); add(6, 4); add(7, 3); add(8, near ? 7 : 1);
            add(9, idleTotal > 90 ? 6 : 1);
            if (tb) add(10, 3);
            if (floor) add(11, 5);
            if (snowOn && clock - lastThrow > throwCd) add(12, 16);
            if (cfg.NewYear) { add(13, 2); add(15, 1.5f); }
            if (floor) add(14, 3);
            if (codex) add(16, 4);
            add(17, 2.5f); add(18, codex ? 3 : 1); add(19, 2); add(20, cfg.NewYear ? 2.5f : 1.2f);
            if (!codex) add(21, 2.5f);
            Pet mate = leader ?? buddy;
            if (mate != null && mate.Visible && !mate.fsHidden && Math.Abs(mate.px - px) < 320 * k && Math.Abs(mate.py - py) < 60 * k) add(22, 4);
            float sum = 0; foreach (var o in opts) sum += o.Value;
            float r = U.Rf(0, sum); int pick = 0;
            foreach (var o in opts) { r -= o.Value; if (r <= 0) { pick = o.Key; break; } }
            switch (pick)
            {
                case 0: StartWalk(false); break;
                case 1: Go(St.Look, 2.6f); break;
                case 2: Go(St.Sit, U.Rf(4, 9)); break;
                case 3: Go(St.Think, 2.6f); break;
                case 4: Go(St.Wave, 1.6f); if (near && U.Ch(0.4)) Say(U.Pick("Привет!", "Хей!", "О, курсор!"), 1.6f); break;
                case 5: Go(St.Dance, 3.2f); break;
                case 6: Go(St.Prep, 0.18f); break;
                case 7: Go(St.Yawn, 1.6f); break;
                case 8: Go(St.Love, 2.2f); break;
                case 9: Go(St.Sit, 1.2f); dur = 0.01f; Go(St.Sleep, U.Rf(15, 45)); break;
                case 10: Go(St.Dive, 0.8f); break;
                case 11: if (!TryLeap()) StartWalk(false); break;
                case 12: StartSnowball(false); break;
                case 13: Go(St.Shiver, 2.2f); if (U.Ch(0.4)) Say(U.Pick("Брр, холодно!", "Зима близко..."), 1.6f); break;
                case 14: StartWalk(true); break;
                case 15: Go(St.Happy, 1.2f); Say(U.Pick("Хо-хо-хо!", "С наступающим!", "Снежок?"), 1.8f); break;
                case 17: Go(St.Juggle, U.Rf(3, 4.5f)); if (U.Ch(0.4)) Say(codex ? U.Pick("Смотри, как я умею!", "Жонглирую! Почти...") : U.Pick("Оп-оп-оп!", "Цирк приехал!"), 1.6f); break;
                case 18: Go(St.Spin, 1.5f); if (codex && U.Ch(0.6)) Say(U.Pick("Где мой хвост?!", "Вжу-у-ух!"), 1.4f); break;
                case 19: Go(St.Stretch, 3.2f); if (U.Ch(0.5)) Say(U.Pick("Зарядка!", "Раз-два, раз-два!", "Разминочка"), 1.5f); break;
                case 20: Go(St.Sneeze, 1.5f); break;
                case 21: Go(St.Zen, U.Rf(5, 9)); if (U.Ch(0.5)) Say(U.Pick("Ом-м-м...", "Дзен...", "Я спокоен"), 1.6f); break;
                case 22:
                    Face(leader ?? buddy); Go(St.Wave, 1.6f);
                    Say(codex ? U.Pick("Клод! Привет!", "Эй, Клод, смотри!", "Я тут, если что!") : U.Pick("Привет, Кодекс!", "Как дела, зелёный?", "Не скучаешь?"), 1.6f);
                    break;
                case 16: Go(St.Think, 2.4f); Say(U.Pick("Э-э-э...", "А что я делал?", "Бип-буп!", "Хе-хе", "Я забыл, зачем пришёл", "Где мой курсор?"), 1.8f); break;
            }
        }

        void StartWalk(bool toClimb)
        {
            float lo, hi;
            if (surf == 1) { lo = sRect.L + 5 * u; hi = sRect.R - 5 * u; }
            else { lo = wa.Left + 7 * u; hi = wa.Right - 7 * u; }
            climbNext = false;
            if (toClimb && surf == 0)
            {
                walkTo = (px - lo < hi - px) ? lo : hi; climbNext = true;
            }
            else if (surf == 1 && U.Ch(0.25))
            {
                walkTo = U.Ch(0.5) ? sRect.L - 8 * u : sRect.R + 8 * u; // walk off the edge
            }
            else
            {
                walkTo = U.Rf(lo, Math.Max(lo + 1, hi));
                if (Math.Abs(walkTo - px) < 40 * k) walkTo = U.Cl(px + (U.Ch(0.5) ? 1 : -1) * U.Rf(80, 300) * k, lo, hi);
            }
            running = U.Ch(codex ? 0.3 : 0.15);
            walkSpd = (running ? 125 : U.Rf(38, 54)) * k;
            if (Math.Abs(walkTo - px) < 2) { GoIdle(); return; }
            Go(St.Walk, 999);
        }

        bool TryLeap()
        {
            RefreshWins();
            var cands = new List<float[]>();
            foreach (var w in wins)
            {
                float top = w.r.T;
                if (top > py - 50 * k || top < py - 330 * k) continue;
                if (top < wa.Top + 30 * u) continue;
                float tx = U.Cl(px + U.Rf(-120, 120) * k, w.r.L + 20 * k, w.r.R - 20 * k);
                if (Math.Abs(tx - px) > 380 * k || w.r.R - w.r.L < 60 * k) continue;
                if (!TopVisible(w.h, tx, top)) continue;
                cands.Add(new[] { tx, top });
            }
            if (cands.Count == 0) return false;
            var c = cands[U.R.Next(cands.Count)];
            float h = py - c[1], extra = 35 * k, g = Gr;
            float vy0 = -(float)Math.Sqrt(2 * g * (h + extra));
            float ta = -vy0 / g, tb2 = (float)Math.Sqrt(2 * extra / g);
            leapVy = vy0; leapVx = (c[0] - px) / (ta + tb2);
            leaping = true; dir = Math.Sign(leapVx) == 0 ? dir : Math.Sign(leapVx);
            Go(St.Prep, 0.22f);
            return true;
        }

        void EnterClimb(int side)
        {
            climbSide = side; climbPhase = 0;
            rot = side < 0 ? 90 : -90;
            px = side < 0 ? wa.Left : wa.Right;
            py = wa.Bottom - 8 * u;
            climbTo = wa.Top + wa.Height * U.Rf(0.15f, 0.6f);
            surf = 2; st = St.Climb; t = 0;
        }

        void UpdateClimb(float dt)
        {
            if (climbPhase == 0)
            {
                py -= 38 * k * dt; legPhase += dt * 5;
                if (py <= climbTo) { climbPhase = 1; t = 0; }
            }
            else if (climbPhase == 1)
            {
                if (t > 2.4f)
                {
                    if (U.Ch(0.5)) climbPhase = 2;
                    else
                    {
                        px = px - climbSide * 7 * u;
                        StartFall(-climbSide * U.Rf(160, 260) * k, -U.Rf(120, 260) * k); st = St.Jump;
                        if (U.Ch(0.4)) Say(U.Pick("Уиии!", "Прыгаю!"), 1.4f);
                    }
                }
            }
            else
            {
                py += 52 * k * dt; legPhase += dt * 6;
                if (py >= wa.Bottom - 8 * u)
                {
                    rot = 0; px = climbSide < 0 ? wa.Left + 7 * u : wa.Right - 7 * u; py = wa.Bottom; surf = 0;
                    dir = -climbSide; GoIdle();
                }
            }
        }

        void UpdateAir(float dt)
        {
            float g = Gr;
            vy += g * dt; if (vy > 2400 * k) vy = 2400 * k;
            float nx = px + vx * dt, ny = py + vy * dt;
            var vs = SystemInformation.VirtualScreen;
            if (nx < vs.Left + 7 * u) { nx = vs.Left + 7 * u; vx = Math.Abs(vx) * 0.5f; }
            if (nx > vs.Right - 7 * u) { nx = vs.Right - 7 * u; vx = -Math.Abs(vx) * 0.5f; }
            if (ny - 14 * u < vs.Top) { ny = vs.Top + 14 * u; if (vy < 0) vy = -vy * 0.3f; }
            rot += (0 - rot) * Math.Min(1, 7 * dt);
            if (Math.Abs(rot) < 0.5f) rot = 0;
            if (Math.Abs(vx) > 20 * k) dir = Math.Sign(vx);
            if (vy > 0)
            {
                float ly; IntPtr h; N.RECT r;
                if (FindLanding(nx, py, ny, out ly, out h, out r)) { px = nx; py = ly; Land(h, r); return; }
            }
            px = nx; py = ny;
        }

        void Land(IntPtr h, N.RECT r)
        {
            float impact = vy; vy = 0; vx = 0; rot = 0; sink = 0;
            if (h == IntPtr.Zero) surf = 0; else { surf = 1; sHwnd = h; sRect = r; sVis = 0; sOcc = 0.5f; }
            RefreshMonitor();
            if (surf == 0 && cfg.NewYear && strip != null && strip.Visible && impact > 200 * k) SnowPuff((int)Math.Min(14, impact / (80 * k)));
            if (pendingReact > 0) { int pr = pendingReact; pendingReact = 0; React(pr); return; }
            if (!greeted) { greeted = true; Go(St.Wave, 1.8f); Say(codex ? (cfg.NewYear ? "Хо-хо! Я Кодекс!" : "Привет! Я Кодекс!") : (cfg.NewYear ? "Хо-хо-хо! Я Клод" : "Привет! Я Клод"), 2.6f); return; }
            if (dodged) { dodged = false; Go(St.Laugh, 1f); Say(U.Pick("Мимо!", "Не попал!", "Ха, промах!"), 1.5f); return; }
            if (impact > 1600 * k) { hitBySnow = false; Go(St.Hit, 1.3f); Say(U.Pick("Ух...", "Голова кружится...", "Ой-ой"), 1.6f); }
            else if (impact > 250 * k) Go(St.Land, 0.22f);
            else GoIdle();
        }

        // ---------- surfaces ----------
        void RefreshWins()
        {
            if (clock - winsT < 0.25f) return;
            winsT = clock;
            var list = new List<WinInfo>();
            N.EnumWindows((h, l) =>
            {
                if (!N.IsWindowVisible(h) || N.IsIconic(h)) return true;
                uint pid; N.GetWindowThreadProcessId(h, out pid); if (pid == myPid) return true;
                int ex = N.GetWindowLong(h, -20); if ((ex & 0x80) != 0 || (ex & 0x20) != 0) return true;
                if (N.GetWindowTextLength(h) == 0) return true;
                if (N.Cloaked(h)) return true;
                string cls = N.Cls(h); foreach (var c in shellClasses) if (cls == c) return true;
                N.RECT r; if (!N.Rect(h, out r)) return true;
                if (r.R - r.L < 150 || r.B - r.T < 80) return true;
                list.Add(new WinInfo { h = h, r = r });
                return true;
            }, IntPtr.Zero);
            wins = list;
        }

        bool TopVisible(IntPtr h, float x, float top)
        {
            IntPtr hit = N.WindowFromPoint(new N.POINT((int)x, (int)top + 4));
            if (hit == IntPtr.Zero) return false;
            IntPtr root = N.GetAncestor(hit, 2);
            if (root == h) return true;
            uint pid; N.GetWindowThreadProcessId(root, out pid);
            return pid == myPid;
        }

        bool FindLanding(float x, float y0, float y1, out float ly, out IntPtr h, out N.RECT r)
        {
            ly = 0; h = IntPtr.Zero; r = new N.RECT();
            var scr = Screen.FromPoint(new Point((int)x, (int)Math.Min(y1, y0 + 1)));
            float floorY = scr.WorkingArea.Bottom;
            float best = float.MaxValue;
            if (y1 >= floorY) best = floorY;
            RefreshWins();
            foreach (var w in wins)
            {
                float top = w.r.T;
                if (top < y0 - 1 || top > y1 || top >= best) continue;
                if (x < w.r.L + 3 * u || x > w.r.R - 3 * u) continue;
                if (top < scr.WorkingArea.Top + 30 * u) continue;
                if (!TopVisible(w.h, x, top)) continue;
                best = top; h = w.h; r = w.r;
            }
            if (best == float.MaxValue) return false;
            if (best == floorY && h != IntPtr.Zero && best != (float)r.T) h = IntPtr.Zero;
            ly = best;
            return true;
        }

        bool ValidateWindow(float dt)
        {
            if (!N.IsWindow(sHwnd)) return false;
            N.RECT r; if (!N.Rect(sHwnd, out r)) return false;
            if (r.T != sRect.T || r.L != sRect.L || r.R != sRect.R)
            {
                bool moved = (r.R - r.L) == (sRect.R - sRect.L);
                if (moved) px += r.L - sRect.L;
                py = r.T;
            }
            sRect = r; py = r.T;
            sVis -= dt;
            if (sVis <= 0)
            {
                sVis = 0.1f;
                if (!N.IsWindowVisible(sHwnd) || N.IsIconic(sHwnd) || N.Cloaked(sHwnd)) return false;
                var scr = Screen.FromPoint(new Point((int)px, r.T));
                if (r.T < scr.WorkingArea.Top + 30 * u) return false;
            }
            if (px < r.L + 2 * u || px > r.R - 2 * u) return false;
            sOcc -= dt;
            if (sOcc <= 0) { sOcc = 0.5f; if (!HiddenState && !TopVisible(sHwnd, px, r.T)) return false; }
            return true;
        }

        // ---------- drag ----------
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            mouseIsDown = true; downPt = Cursor.Position;
            grabDx = downPt.X - px; grabDy = downPt.Y - py;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (mouseIsDown && !dragging)
            {
                var c = Cursor.Position;
                if (Math.Abs(c.X - downPt.X) + Math.Abs(c.Y - downPt.Y) > 5) StartDrag();
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right) { ShowMenu(); return; }
            if (e.Button != MouseButtons.Left) return;
            if (dragging) EndDrag();
            else if (mouseIsDown) OnPoke();
            mouseIsDown = false;
        }

        void StartDrag()
        {
            if (rot != 0)
            {   // grabbed from a wall: re-anchor so the pet stays under the cursor
                rot = 0; grabDx = 0; grabDy = -6 * u;
            }
            dragging = true; st = St.Drag; t = 0; surf = -1; sink = 0; holding = false; climbNext = false; leaping = false;
            dragHist.Clear(); idleTotal = 0;
            if (pendingReact == 0 && U.Ch(0.45)) Say(U.Pick("Ой!", "Эй, куда?", "Уиии!", "Я лечу!"), 1.3f);
        }

        void UpdateDrag(float dt)
        {
            px = curX - grabDx; py = curY - grabDy;
            dragHist.Add(new[] { clock, px, py });
            while (dragHist.Count > 2 && clock - dragHist[0][0] > 0.1f) dragHist.RemoveAt(0);
            if (dragHist.Count >= 2)
            {
                var a = dragHist[0]; var b = dragHist[dragHist.Count - 1];
                float d = Math.Max(0.016f, b[0] - a[0]);
                dragVx += ((b[1] - a[1]) / d - dragVx) * Math.Min(1, 12 * dt);
            }
            if ((Control.MouseButtons & MouseButtons.Left) == 0) EndDrag();
        }

        void EndDrag()
        {
            dragging = false; mouseIsDown = false;
            float rvx = 0, rvy = 0;
            if (dragHist.Count >= 2)
            {
                var a = dragHist[0]; var b = dragHist[dragHist.Count - 1];
                float d = Math.Max(0.016f, b[0] - a[0]);
                rvx = U.Cl((b[1] - a[1]) / d, -2200 * k, 2200 * k);
                rvy = U.Cl((b[2] - a[2]) / d, -2200 * k, 2200 * k);
            }
            StartFall(rvx, rvy);
            if (U.Hyp(rvx, rvy) > 1300 * k) Say(U.Pick("Уиииии!", "Аааа!"), 1.2f);
        }

        void OnPoke()
        {
            idleTotal = 0;
            if (!OnGround) return;
            if (HiddenState) { sinkStart = sink; Go(St.PopOut, 0.15f); return; }
            if (st == St.Sleep || st == St.Sit && t > 3)
            {
                Go(St.Notify, 1.2f); Say(U.Pick("А? Я не сплю!", "Мм? Что?"), 1.6f); return;
            }
            if (st == St.Ask) return;
            if (clock - lastClick < 0.4f) { Go(St.Dance, 3.2f); lastClick = -10; if (U.Ch(0.5)) Say("♪ ♫ ♪", 1.4f); return; }
            lastClick = clock;
            if (clock - lastPoke > 3) pokes = 0;
            pokes++; lastPoke = clock;
            if (pokes >= 6) { Go(St.Angry, 1.6f); Say(codex ? U.Pick("Ай-ай, хватит!", "Я же Кодекс, а не кнопка!") : U.Pick("Ну хватит!", "Эй! Щекотно же!"), 1.6f); pokes = 0; }
            else if (pokes >= 3) { Go(St.Laugh, 1.2f); if (U.Ch(0.5)) Say("Хи-хи", 1.2f); }
            else Go(St.Happy, 1.1f);
        }

        // =====================================================================
        //  Notifications
        // =====================================================================
        public void Notify(string kind, string title, string text, Action<string> reply)
        {
            if (kind.StartsWith("codex-"))
            {
                kind = kind.Substring(6);
                if (buddy != null && buddy.Visible) { buddy.Notify(kind, title, text, reply); return; }
            }
            if (kind == "play")
            {
                int gi = Array.IndexOf(playIds, (title ?? "").ToLowerInvariant());
                if (buddy == null) SetBuddy(true);
                PlayNow(gi > 0 ? (Game)gi : PickGame());
                return;
            }
            if (kind == "say") { Say(text, 2f); return; }
            int kd = kind == "done" ? 1 : kind == "attn" ? 2 : kind == "ask" ? 3 : 4;
            if (fsHidden || cfg.Hidden)
            {
                if (reply != null) { reply("none"); return; }
                if (queue.Count < 6) queue.Enqueue(new object[] { kd, title, text, null });
                return;
            }
            if (kd == 3)
            {
                if (bubble.Active && bubble.Kind == 3 && !bubble.IsClosing) { reply("none"); return; }
                ShowNotice(kd, title, text, reply);
                return;
            }
            if (bubble.Active && bubble.Kind == 3 && kd == 2 && (text ?? "").Contains("подтверждение")) return; // already asking
            if (bubble.IsNotice && !bubble.IsClosing) { if (queue.Count < 6) queue.Enqueue(new object[] { kd, title, text, reply }); return; }
            ShowNotice(kd, title, text, reply);
        }

        void ShowNotice(int kd, string title, string text, Action<string> reply)
        {
            if (string.IsNullOrEmpty(title)) return;
            React(kd);
            float life = kd == 1 ? 8 : kd == 2 ? 14 : kd == 3 ? 45 : 7;
            Action<string> cb = null;
            if (reply != null) cb = a => { reply(a); AfterAnswer(a); };
            bubble.Open(kd, title, text, life, cb);
            ReassertZ();
        }

        void React(int kd)
        {
            idleTotal = 0;
            if (!OnGround) return;
            if (HiddenState) { pendingReact = kd; sinkStart = sink; Go(St.PopOut, 0.18f); return; }
            var boss = leader ?? this;
            if (boss.game != Game.None) boss.EndGame();
            Pet mate = leader ?? buddy;
            if (mate != null && kd == 1 && mate.Free) { mate.Go(St.Happy, 1.2f); if (U.Ch(0.5)) mate.Say(U.Pick("Ура!", "Молодец!", "Ура-а!"), 1.4f); }
            if (kd == 1) Go(St.Celebrate, 1.8f);
            else if (kd == 2) Go(St.Notify, 2.6f);
            else if (kd == 3) Go(St.Ask, 45f);
            else Go(St.Happy, 1.2f);
        }

        void AfterAnswer(string a)
        {
            if (st != St.Ask) return;
            if (a == "allow") { Go(St.Happy, 1.2f); pendingSpeech = U.Pick("Поехали!", "Делаю!", "Принято!"); }
            else if (a == "deny") { Go(St.Sad, 1.6f); pendingSpeech = U.Pick("Ладно...", "Понял, не буду"); }
            else GoIdle();
        }

        public void Say(string text, float life)
        {
            if (bubble == null || fsHidden || cfg.Hidden) return;
            if (!bubble.Active || (bubble.Kind == 0 && !bubble.IsClosing)) bubble.Open(0, "", text, life, null);
            else if (bubble.IsClosing) pendingSpeech = text;
        }

        void UpdateBubble(float dt)
        {
            float badgeH = (st == St.Notify || st == St.Ask) ? 7f : 0f;
            PointF head = L2S(0, -(cfg.NewYear ? 19.5f : 12.5f) - badgeH + sink);
            float footY = Math.Max(py, head.Y);
            if (bubble.Active) bubble.Tick(dt, head.X, head.Y, footY + 2 * u, wa);
            if (!bubble.Active)
            {
                if (queue.Count > 0)
                {
                    var q = queue.Dequeue();
                    if (!string.IsNullOrEmpty(q[1] as string)) ShowNotice((int)q[0], (string)q[1], (string)q[2], (Action<string>)q[3]);
                }
                else if (pendingSpeech != null)
                {
                    bubble.Open(0, "", pendingSpeech, 2f, null); pendingSpeech = null;
                }
            }
        }

        // =====================================================================
        //  Snowballs
        // =====================================================================
        void StartSnowball(bool counter)
        {
            targetPet = null;
            targetCursor = counter || U.Ch(0.5);
            if (!targetCursor)
            {
                for (int i = 0; i < 6; i++)
                {
                    target = new PointF(U.Rf(wa.Left + 40, wa.Right - 40), U.Rf(wa.Top + 60, Math.Max(wa.Top + 61, wa.Bottom - 160 * k)));
                    if (Math.Abs(target.X - px) > 160 * k) break;
                }
            }
            FaceTarget();
            Go(St.Scoop, 0.6f);
        }

        internal void StartSnowballAt(Pet p)
        {
            targetPet = p; targetCursor = false; target = new PointF(p.px, p.py - 6 * p.u);
            FaceTarget();
            Go(St.Scoop, codex ? 0.7f : 0.55f);
        }

        void FaceTarget()
        {
            if (targetPet != null) target = new PointF(targetPet.px, targetPet.py - 6 * targetPet.u);
            float tx = targetCursor ? curX : target.X;
            if (Math.Abs(tx - px) > 4) dir = Math.Sign(tx - px);
        }

        float FloorAt(float x, float y) { return Screen.FromPoint(new Point((int)x, (int)y)).WorkingArea.Bottom; }

        Snowball NewBall()
        {
            var b = new Snowball(4.4f * u);
            balls.Add(b);
            return b;
        }

        void ThrowBall()
        {
            holding = false;
            float tx = targetCursor ? curX : target.X, ty = targetCursor ? curY : target.Y;
            float hx = px + dir * 6.5f * u, hy = py - 13 * u;
            var b = NewBall();
            Pet tp = targetPet; targetPet = null;
            float T0 = 0;
            if (tp != null)
            {
                if (codex && U.Ch(0.12)) { tp = this; tx = px; ty = py - 6 * u; T0 = 1.15f; hx = px; Say(U.Pick("Ой-ой-ой...", "Куда он?!"), 1.4f); } // goofy: throws it straight up
                else
                {
                    PointF c = tp.L2S(0, -6);
                    float T1 = U.Cl(U.Hyp(c.X - hx, c.Y - hy) / (950 * k) + 0.25f, 0.45f, 1.4f);
                    float lead = tp.st == St.Walk ? tp.dir * tp.walkSpd : 0;
                    float miss = codex ? 0.4f : 0.2f;
                    float err = U.Ch(miss) ? U.Rf(13, 24) * u * (U.Ch(0.5) ? 1 : -1) : 0;
                    tx = c.X + lead * T1 + err; ty = c.Y;
                }
            }
            b.aimed = true; b.x = b.x0 = hx; b.y = b.y0 = hy; b.tx = tx; b.ty = ty; b.cursorTarget = targetCursor; b.targetPet = tp; b.thrower = this;
            float dist = U.Hyp(tx - hx, ty - hy);
            b.T = T0 > 0 ? T0 : U.Cl(dist / (950 * k) + 0.25f, 0.45f, 1.4f);
            b.vx = (tx - hx) / b.T; b.vy = (ty - hy) / b.T - 0.5f * Gr * b.T;
            b.splatKind = ty >= FloorAt(tx, ty) - 6 * k ? 1 : 0;
            b.Draw(); b.Place(); b.Show();
            lastThrow = clock; throwCd = U.Rf(35, 90);
            if (tp != null) gThrows++;
        }

        public void GrabSnow()
        {
            if (!cfg.NewYear || !cfg.Snow || held != null || fsHidden) return;
            var b = NewBall(); b.held = true; b.user = true; b.x = curX; b.y = curY;
            heldHist.Clear();
            b.Draw(); b.Place(); b.Show();
            held = b;
            if (OnGround && !HiddenState && st != St.Ask && U.Ch(0.35)) Say(U.Pick("Эй, ты что задумал?", "Ой-ой...", "Только не в меня!"), 1.6f);
        }

        void ReleaseHeld()
        {
            var b = held; held = null; b.held = false;
            float rvx = 0, rvy = 0;
            if (heldHist.Count >= 2)
            {
                var a = heldHist[0]; var c = heldHist[heldHist.Count - 1];
                float d = Math.Max(0.016f, c[0] - a[0]);
                rvx = U.Cl((c[1] - a[1]) / d, -2600 * k, 2600 * k);
                rvy = U.Cl((c[2] - a[2]) / d, -2600 * k, 2600 * k);
            }
            b.vx = rvx; b.vy = rvy; b.t = 0;
        }

        void UserThrowAtPet()
        {
            if (cfg.Hidden) return;
            if (!cfg.NewYear) { Say("Сначала включи новогодний режим!", 2f); return; }
            if (leader == null && buddy != null && buddy.Visible && !buddy.fsHidden &&
                (fsHidden || U.Hyp(buddy.px - curX, buddy.py - curY) < U.Hyp(px - curX, py - curY))) { buddy.curX = curX; buddy.curY = curY; buddy.UserThrowAtPet(); return; }
            if (fsHidden) return;
            PointF c = L2S(0, -6);
            float lead = st == St.Walk ? dir * walkSpd : 0;
            float sx0 = curX, sy0 = curY;
            float dist = U.Hyp(c.X - sx0, c.Y - sy0);
            if (dist < 30 * k) { sy0 -= 120 * k; dist = U.Hyp(c.X - sx0, c.Y - sy0); }
            float T = U.Cl(dist / (1100 * k), 0.35f, 1.0f);
            float tx = c.X + lead * T;
            var b = (leader ?? this).NewBall(); b.user = true; b.x = sx0; b.y = sy0;
            b.vx = (tx - sx0) / T; b.vy = (c.Y - sy0) / T - 0.5f * Gr * T;
            b.Draw(); b.Place(); b.Show();
        }

        bool PetHittable { get { return !HiddenState && sink < 3; } }
        bool CanDodge
        {
            get
            {
                return OnGround && !HiddenState && st != St.Hit && st != St.Shake && st != St.Ask && st != St.Sleep && st != St.Prep
                       && st != St.Scoop && st != St.Roll && st != St.WindUp;
            }
        }

        void Dodge()
        {
            dodged = true; vx = 0; vy = -560 * k; st = St.Jump; t = 0; surf = -1; holding = false;
        }

        void OnHit(float bvx, float bx, float by) { OnHit(bvx, bx, by, null); }
        void OnHit(float bvx, float bx, float by, Pet from)
        {
            idleTotal = 0; hitFrom = from; if (inGame) gHits++;
            snowOnBody = 1; snowSide = bx < px ? -1 : 1;
            for (int i = 0; i < 8; i++) Sp(4, snowSide * 5 + U.Rf(-2, 2), -6 + U.Rf(-3, 3), snowSide * U.Rf(2, 10), U.Rf(-10, -2), 0.8f, U.Rf(0.5f, 1f), Color.White);
            if (!OnGround) { Say(U.Pick("Ай!", "Эй!"), 1.2f); return; }
            holding = false; hitBySnow = true;
            Go(St.Hit, 1.25f);
            if (from == this) Say(U.Pick("Бум!", "Ой... сам себя", "Я так и хотел!"), 1.6f);
            else if (from != null && codex) Say(U.Pick("Ай!", "Эй!", "Холодно!", "Нечестно!"), 1.4f);
            else Say(U.Pick("Ай!", "Эй!", "Ух!", "Попал...", "Холодно!"), 1.5f);
        }

        void SnowPuff(int n)
        {
            for (int i = 0; i < n; i++) Sp(4, U.Rf(-6, 6), -0.5f, U.Rf(-8, 8), U.Rf(-10, -3), 0.7f, U.Rf(0.5f, 1f), Color.White);
        }

        void Splash(float x, float y, int kind)
        {
            if (splats.Count > 14) return;
            var s = new Splat(x, y, kind, k);
            splats.Add(s); s.Show();
        }

        void UpdateBalls(float dt)
        {
            float g = Gr;
            if (held != null)
            {
                var b = held;
                b.x = curX; b.y = curY; b.spin += dt * 2;
                heldHist.Add(new[] { clock, b.x, b.y });
                while (heldHist.Count > 2 && clock - heldHist[0][0] > 0.09f) heldHist.RemoveAt(0);
                if ((Control.MouseButtons & MouseButtons.Left) == 0) ReleaseHeld();
                else { b.Draw(); b.Place(); }
            }
            var vs = SystemInformation.VirtualScreen;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var b = balls[i];
                if (b == held) continue;
                b.t += dt; b.spin += dt * 9;
                bool kill = false;
                if (b.aimed)
                {
                    float tt = Math.Min(b.t, b.T);
                    b.x = b.x0 + b.vx * tt; b.y = b.y0 + b.vy * tt + 0.5f * g * tt * tt;
                    if (b.targetPet != null && !b.dodgeChecked && b.t > b.T - 0.32f)
                    {
                        b.dodgeChecked = true; var tp = b.targetPet;
                        if (tp != b.thrower && tp.CanDodge && U.Ch(tp.codex ? 0.18 : 0.3)) tp.Dodge();
                    }
                    if (b.t >= b.T && b.targetPet != null)
                    {
                        var tp = b.targetPet; PointF c = tp.L2S(0, -6);
                        if (U.Hyp(c.X - b.tx, c.Y - b.ty) < 8 * u && tp.PetHittable && tp.Visible)
                        {
                            Splash(b.x, b.y, 2); tp.OnHit(b.vx, b.x, b.y, b.thrower);
                            var th = b.thrower;
                            if (th != tp && th.OnGround && U.Ch(0.4) && !th.bubble.Active) th.Say(th.codex ? U.Pick("Попал! Попал!", "Хе-хе!", "Бдыщ!") : U.Pick("Попал!", "Есть!", "В яблочко!"), 1.3f);
                            kill = true;
                        }
                        else { b.aimed = false; b.vy = b.vy + g * b.T; b.targetPet = null; }
                    }
                    else if (b.t >= b.T)
                    {
                        Splash(b.tx, b.ty, b.splatKind);
                        if (b.cursorTarget && U.Hyp(curX - b.tx, curY - b.ty) < 45 * k && OnGround && st != St.Ask)
                        { Go(St.Laugh, 1.1f); Say(U.Pick("Попал!", "Ха! Попал!", "Бдыщ!"), 1.5f); }
                        kill = true;
                    }
                }
                else
                {
                    b.vy += g * dt; b.x += b.vx * dt; b.y += b.vy * dt;
                    if (b.user)
                    {
                        Pet hp = this;
                        if (buddy != null && buddy.Visible && !buddy.fsHidden)
                        {
                            PointF c1 = L2S(0, -6), c2 = buddy.L2S(0, -6);
                            if (fsHidden || !Visible || U.Hyp(b.x - c2.X, b.y - c2.Y) < U.Hyp(b.x - c1.X, b.y - c1.Y)) hp = buddy;
                        }
                        PointF c = hp.L2S(0, -6);
                        float dist = U.Hyp(b.x - c.X, b.y - c.Y), rr = 7 * hp.u + b.d / 2;
                        float sp = U.Hyp(b.vx, b.vy);
                        float closing = ((c.X - b.x) * b.vx + (c.Y - b.y) * b.vy);
                        if (!b.dodgeChecked && closing > 0 && dist < rr + sp * 0.28f && hp.CanDodge)
                        { b.dodgeChecked = true; if (U.Ch(hp.codex ? 0.2 : 0.3)) hp.Dodge(); }
                        if (dist < rr && hp.PetHittable && !hp.fsHidden && hp.Visible)
                        {
                            hp.OnHit(b.vx, b.x, b.y); Splash(b.x, b.y, 2); kill = true;
                        }
                    }
                    if (!kill)
                    {
                        float fl = FloorAt(b.x, b.y);
                        if (b.y + b.d / 2 >= fl && b.vy > 0) { Splash(b.x, fl, 1); kill = true; }
                        else if (b.x < vs.Left - 60 || b.x > vs.Right + 60 || b.y > vs.Bottom + 60 || b.t > 8) kill = true;
                    }
                }
                if (kill) { b.Close(); balls.RemoveAt(i); continue; }
                b.Draw(); b.Place();
            }
            for (int i = splats.Count - 1; i >= 0; i--)
            {
                splats[i].Tick(dt);
                if (splats[i].Dead) { splats[i].Close(); splats.RemoveAt(i); }
            }
        }

        // =====================================================================
        //  Look / particles
        // =====================================================================
        void UpdateLook(float dt)
        {
            float tlx, tly;
            float hx = px, hy = py - 7 * u;
            float dxc = curX - hx, dyc = curY - hy, dc = U.Hyp(dxc, dyc);
            if (st == St.Walk) { tlx = dir * 0.8f; tly = 0; }
            else if (st == St.Look || st == St.Peek) { tlx = U.Sin(t * 2.3f) * 1f; tly = -0.1f; }
            else if (st == St.Think) { tlx = 0.8f; tly = -0.8f; }
            else if (st == St.Scoop || st == St.Roll) { tlx = 0; tly = 0.8f; }
            else if (st == St.Climb) { tlx = 0; tly = climbPhase == 2 ? 0.5f : -0.6f; }
            else if (MateLook(dc, out tlx, out tly)) { }
            else if (dc < 450 * k && rot == 0) { tlx = U.Cl(dxc / (120 * k), -1, 1) * 0.85f; tly = U.Cl(dyc / (120 * k), -1, 1) * 0.6f; }
            else { tlx = dir * 0.3f; tly = 0.1f; }
            float a = Math.Min(1, 10 * dt);
            lookX += (tlx - lookX) * a; lookY += (tly - lookY) * a;

            // hat physics (springy tip that flops behind)
            float evx = dragging ? dragVx : (st == St.Walk ? dir * walkSpd : vx);
            float targetA = U.Cl(-evx * 0.035f / k, -40, 40);
            hatV += (targetA - hatA) * 90 * dt; hatV *= Math.Max(0, 1 - 6 * dt); hatA += hatV * dt;
            hatTip += (-dir * 3.2f - hatTip) * Math.Min(1, 5 * dt);
        }

        void Sp(int kind, float x, float y, float pvx, float pvy, float life, float size, Color c)
        {
            if (parts.Count > 90) return;
            parts.Add(new Part { k = kind, x = x, y = y, vx = pvx, vy = pvy, life = life, max = life, size = size, c = c });
        }

        void UpdateParticles(float dt)
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                p.life -= dt;
                if (p.life <= 0) { parts.RemoveAt(i); continue; }
                switch (p.k)
                {
                    case 0: case 2: p.vx += U.Sin(p.life * 7) * 4 * dt; break;
                    case 3: p.vy += 22 * dt; p.vx *= (1 - 1.5f * dt); break;
                    case 4: p.vy += 34 * dt; break;
                    case 5: p.vy += 20 * dt; break;
                    case 6: p.size += dt * 1.6f; break;
                }
                p.x += p.vx * dt; p.y += p.vy * dt;
            }
        }

        // =====================================================================
        //  Rendering
        // =====================================================================
        PointF L2S(float lx, float ly)
        {
            double r = rot * Math.PI / 180;
            float x = lx * u, y = ly * u + sink * u;
            return new PointF(px + (float)(x * Math.Cos(r) - y * Math.Sin(r)), py + (float)(x * Math.Sin(r) + y * Math.Cos(r)));
        }

        float PX(float v) { return ox + v * u; }
        float PY(float v) { return oy + v * u; }

        void Bx(Brush b, float x, float y, float w, float h)
        {
            float x1 = (float)Math.Round(ox + x * u), y1 = (float)Math.Round(oy + y * u);
            float x2 = (float)Math.Round(ox + (x + w) * u), y2 = (float)Math.Round(oy + (y + h) * u);
            if (x2 > x1 && y2 > y1) G.FillRectangle(b, x1, y1, x2 - x1, y2 - y1);
        }

        void RenderPet()
        {
            var g = S.G; G = g;
            g.ResetTransform(); g.ResetClip();
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            float cx = CW / 2f, cy = CH / 2f;
            if (rot != 0) { g.TranslateTransform(cx, cy); g.RotateTransform(rot); g.TranslateTransform(-cx, -cy); }
            ox = Ox; oy = Oy + sink * u;
            if (sink > 0.01f) g.SetClip(new RectangleF(0, 0, CW, Oy));
            if (st != St.Hidden) DrawPet();
            g.ResetClip(); g.ResetTransform();
            ox = Ox; oy = Oy;
            DrawParticles();

            double r = rot * Math.PI / 180;
            float fx = Ox - cx, fy = Oy - cy;
            float rx = (float)(fx * Math.Cos(r) - fy * Math.Sin(r)), ry = (float)(fx * Math.Sin(r) + fy * Math.Cos(r));
            Present((int)Math.Round(px - cx - rx), (int)Math.Round(py - cy - ry), 255);
        }

        void DrawPet()
        {
            float bob = 0, sx = 1, sy = 1, lean = 0, sit = 0, wig = 0, armYo = 0;
            int legMode = 0, aL = 0, aR = 0;
            Emo emo = Emo.Normal; int mouth = 0; // 1 O, 2 laugh, 3 small, 4 yawn
            int badge = 0; bool stars = false, dots = false, juggle = false, cover = false;
            float T = t, C = clock;
            int ds = dir >= 0 ? 1 : -1;

            switch (st)
            {
                case St.Idle:
                    sy = 1 + U.Sin(C * 2.2f) * 0.025f;
                    if (idleTotal > 25 && U.Sin(C * 0.7f) > 0.6f) emo = Emo.Sleepy;
                    break;
                case St.Walk:
                    legMode = 1; bob = -Math.Abs(U.Sin(legPhase)) * 0.45f; lean = ds * 0.3f; armYo = U.Sin(legPhase) * 0.35f;
                    emo = running ? Emo.Excited : Emo.Normal;
                    break;
                case St.Sit: sit = 1; sy = 0.96f; sx = 1.03f; break;
                case St.Sleep: sit = 1; emo = Emo.Closed; sy = 0.93f + U.Sin(C * 1.7f) * 0.035f; sx = 1.04f; break;
                case St.Look: break;
                case St.Wave: if (ds > 0) aR = 1; else aL = 1; wig = U.Sin(T * 14); emo = Emo.Happy; break;
                case St.Dance:
                    bob = -Math.Abs(U.Sin(T * 6.3f)) * 1.4f; lean = U.Sin(T * 3.15f) * 1.2f;
                    if (U.Sin(T * 6.3f) > 0) { aL = 5; aR = 0; } else { aL = 0; aR = 5; }
                    emo = T % 1.6f < 0.8f ? Emo.Happy : Emo.Excited; break;
                case St.Prep: sx = 1.12f; sy = 0.8f; emo = Emo.Focus; break;
                case St.Jump: case St.Fall:
                    legMode = 2;
                    if (vy < 0) { sy = 1.08f; sx = 0.94f; aL = aR = 5; emo = dodged ? Emo.Wink : Emo.Happy; }
                    else { aL = aR = 1; wig = U.Sin(C * 18) * 0.6f; emo = vy > 700 * k ? Emo.Surprised : Emo.Excited; mouth = vy > 700 * k ? 1 : 0; }
                    break;
                case St.Land: { float q = 1 - Math.Min(1, T / 0.22f); sy = 1 - 0.25f * q; sx = 1 + 0.2f * q; } break;
                case St.Drag:
                    legMode = 2; sy = 1.06f; sx = 0.97f;
                    aL = U.Sin(C * 10) > 0 ? 1 : 4; aR = U.Sin(C * 10 + 1.5f) > 0 ? 1 : 4;
                    emo = T < 0.45f ? Emo.Surprised : (Math.Abs(dragVx) > 900 * k ? Emo.Excited : Emo.Happy);
                    mouth = T < 0.45f ? 1 : 0;
                    break;
                case St.Climb:
                    legMode = climbPhase == 1 ? 0 : 1;
                    if (climbPhase != 1) { if (U.Sin(legPhase) > 0) aL = 1; else aR = 1; emo = Emo.Focus; }
                    else { emo = Emo.Happy; if (T > 0.6f && T < 1.8f) { aR = 1; wig = U.Sin(T * 14); } }
                    break;
                case St.Dive: emo = Emo.Wink; aL = aR = 1; break;
                case St.Peek: break;
                case St.PopOut: emo = Emo.Excited; aL = aR = 5; break;
                case St.Celebrate: bob = -Math.Abs(U.Sin(T * 8)) * 2.2f; aL = aR = 5; emo = Emo.Excited; mouth = 3; break;
                case St.Notify:
                    if (T < 0.4f) { bob = -Math.Abs(U.Sin(T * 9)) * 2f; sy = 1.1f; emo = Emo.Surprised; mouth = 1; }
                    else { emo = Emo.Pleading; if (ds > 0) aR = 1; else aL = 1; wig = U.Sin(T * 12); }
                    badge = 1; break;
                case St.Ask:
                    emo = Emo.Pleading; if (ds > 0) aR = 1; else aL = 1; wig = U.Sin(T * 6) * 0.7f;
                    bob = -Math.Abs(U.Sin(T * 3)) * 0.3f; badge = 2; break;
                case St.Hit: emo = Emo.Dizzy; lean = U.Sin(T * 7) * 0.5f; aL = aR = 4; stars = true; break;
                case St.Shake: lean = U.Sin(T * 45) * 0.7f; emo = Emo.Closed; break;
                case St.Scoop: sy = 0.78f; sx = 1.12f; aL = aR = 4; emo = Emo.Focus; break;
                case St.Roll: aL = aR = 2; armYo = U.Sin(T * 12) * 0.3f; emo = Emo.Focus; break;
                case St.WindUp: lean = -ds * 0.9f; if (ds > 0) aR = 1; else aL = 1; emo = Emo.Wink; break;
                case St.Throw: lean = ds * 0.9f * (1 - T / Math.Max(0.01f, dur)); if (ds > 0) aR = 6; else aL = 6; emo = Emo.Excited; mouth = 3; break;
                case St.Yawn:
                    emo = Emo.Closed; mouth = 4;
                    if (T > 0.2f && T < 1.2f) aL = aR = 1;
                    sy = 1 + 0.08f * U.Sin(Math.PI * Math.Min(1, T / Math.Max(0.01f, dur))); break;
                case St.Laugh: bob = -Math.Abs(U.Sin(T * 14)) * 0.7f; emo = Emo.Laugh; mouth = 2; aL = aR = 2; break;
                case St.Love: emo = Emo.Love; aL = aR = 2; bob = U.Sin(T * 3) * 0.3f; break;
                case St.Think: aR = 2; dots = true; break;
                case St.Shiver: lean = U.Sin(T * 50) * 0.25f; emo = Emo.Closed; aL = aR = 2; break;
                case St.Happy: bob = T < 0.6f ? -Math.Abs(U.Sin(T * 9)) * 1.2f : 0; emo = Emo.Happy; if (T < 0.5f) aL = aR = 5; break;
                case St.Sad: emo = Emo.Sad; sy = 0.94f; aL = aR = 4; break;
                case St.Angry: emo = Emo.Angry; bob = -Math.Abs(U.Sin(T * 14)) * 0.5f; lean = U.Sin(T * 30) * 0.25f; mouth = 3; break;
                case St.Trip:
                    if (T < 0.3f) { lean = ds * 1.6f * (T / 0.3f); legMode = 2; aL = aR = 5; emo = Emo.Surprised; mouth = 1; }
                    else if (T < dur - 0.45f) { sit = 1; sy = 0.62f; sx = 1.16f; lean = ds * 0.8f; aL = aR = 4; emo = Emo.Dizzy; stars = true; }
                    else { float q = (dur - T) / 0.45f; sit = q; sy = 1 - 0.38f * q; sx = 1 + 0.16f * q; emo = Emo.Closed; }
                    break;
                case St.Brawl:
                    bob = -Math.Abs(U.Sin(T * 17 + (codex ? 1 : 0))) * 1.3f; lean = U.Sin(T * 23) * 0.8f; legMode = 1;
                    if (U.Sin(T * 21 + (codex ? 2 : 0)) > 0) { aL = 6; aR = 1; } else { aL = 1; aR = 6; }
                    emo = (int)(T * 3) % 3 == 2 ? Emo.Focus : Emo.Angry; mouth = 3; break;
                case St.HighFive:
                    if (T < 0.32f) bob = -U.Sin(Math.PI * T / 0.32f) * 2.2f;
                    if (ds > 0) aR = 5; else aL = 5;
                    emo = T < 0.32f ? Emo.Excited : Emo.Happy; mouth = 3; break;
                case St.Duck: sit = 1; sy = 0.72f; sx = 1.1f; aL = aR = 4; emo = Emo.Closed; mouth = 3; break;
                case St.Juggle:
                    if (U.Sin(C * 9) > 0) { aL = 5; aR = 1; } else { aL = 1; aR = 5; }
                    emo = codex ? Emo.Excited : Emo.Focus; juggle = true; bob = -Math.Abs(U.Sin(C * 9)) * 0.3f; break;
                case St.Spin:
                    sx = 0.72f + 0.28f * Math.Abs(U.Cos(T * 16)); legMode = 1; aL = aR = 1;
                    emo = T > 0.8f ? Emo.Dizzy : Emo.Excited; bob = -Math.Abs(U.Sin(T * 8)) * 0.5f; break;
                case St.Stretch:
                    if ((int)(T * 1.5f) % 2 == 0) { aL = aR = 1; sy = 1.1f; sx = 0.95f; emo = Emo.Happy; }
                    else { sy = 0.82f; sx = 1.1f; aL = aR = 6; emo = Emo.Focus; }
                    break;
                case St.Sneeze:
                    if (T < 0.85f) { float q = T / 0.85f; lean = -ds * 0.7f * q; sy = 1 + 0.08f * q; emo = Emo.Pleading; mouth = 1; }
                    else { float q = Math.Max(0, 1 - (T - 0.85f) / 0.4f); lean = ds * q; sy = 1 - 0.12f * q; emo = Emo.Closed; mouth = 3; aL = aR = 4; }
                    break;
                case St.Zen: sit = 1; bob = -2.2f - U.Sin(C * 2) * 0.6f; emo = Emo.Closed; aL = aR = 2; break;
                case St.Hug:
                    lean = ds * 0.6f + U.Sin(T * 4) * 0.15f;
                    if (ds > 0) { aR = 6; aL = 5; } else { aL = 6; aR = 5; }
                    emo = Emo.Happy; break;
                case St.Cover: cover = true; emo = Emo.Closed; mouth = 3; bob = -Math.Abs(U.Sin(T * 2.5f)) * 0.3f; break;
            }

            float legH = 2f * (1 - sit);
            float bW = 12 * sx, bH = 8 * sy, bx = lean;
            float bBot = -legH + bob, bTop = bBot - bH;

            // ground shadow
            if (surf >= 0 && surf != 2 && sink < 0.1f)
            {
                G.SmoothingMode = SmoothingMode.AntiAlias;
                G.FillEllipse(bShadow, PX(-6.5f), PY(-0.55f), 13 * u, 1.3f * u);
                G.SmoothingMode = SmoothingMode.None;
            }

            // legs
            if (legH > 0.05f)
            {
                float[] lxs = { -4.5f, -2.5f, 1.5f, 3.5f };
                for (int i = 0; i < 4; i++)
                {
                    float lift = 0, swing = 0, lh = legH - bob;
                    if (legMode == 1) { float sv = U.Sin(legPhase); lift = (i % 2 == 0 ? Math.Max(0, sv) : Math.Max(0, -sv)) * 0.9f; }
                    else if (legMode == 2) { lh = legH * 1.3f; swing = U.Sin(C * 9 + i * 0.9f) * 0.3f - U.Cl(dragVx / (2000 * k), -0.6f, 0.6f); }
                    if (lh - lift > 0.05f) Bx(bDark, bx * 0.3f + lxs[i] * sx + swing, bBot, 1, lh - lift);
                }
            }

            // body
            Bx(bBody, bx - bW / 2, bTop, bW, bH);
            Bx(bLight, bx - bW / 2, bTop, bW, 0.55f);
            Bx(bDark, bx - bW / 2, bBot - 0.75f, bW, 0.75f);

            // arms
            float armY = bTop + 3.4f * sy;
            DrawArm(-1, aL, bx, bW, armY + armYo, ds < 0 ? wig : 0);
            DrawArm(1, aR, bx, bW, armY - armYo, ds > 0 ? wig : 0);

            // face
            DrawFace(emo, mouth, bx, bTop, sx, sy);
            if (cover)
                for (int s = -1; s <= 1; s += 2)
                {
                    float hx = bx + s * 2.5f * sx, hy = bTop + 2.2f * sy;
                    Bx(bDark, hx - 1.3f, hy, 2.6f, 2.0f);
                    Bx(bLight, hx - 1.3f, hy, 2.6f, 0.45f);
                }

            // snow stuck on the body
            if (snowOnBody > 0.02f)
            {
                G.SmoothingMode = SmoothingMode.AntiAlias;
                float a = snowOnBody;
                float cx0 = PX(bx + snowSide * 3.4f), cy0 = PY(bTop + 3.8f);
                bTmp.Color = U.A(Color.FromArgb(255, 205, 218, 236), a);
                G.FillEllipse(bTmp, cx0 - 2.4f * u, cy0 - 1.9f * u + 0.3f * u, 4.8f * u, 3.8f * u);
                bTmp.Color = U.A(Color.White, a);
                G.FillEllipse(bTmp, cx0 - 2.1f * u, cy0 - 1.8f * u, 4.0f * u, 3.2f * u);
                G.FillEllipse(bTmp, cx0 + snowSide * 1.6f * u - 0.8f * u, cy0 + 1.2f * u, 1.6f * u, 1.6f * u);
                G.SmoothingMode = SmoothingMode.None;
            }

            // held snowball
            if (holding)
            {
                float bcx, bcy, rr = ballR;
                if (st == St.WindUp) { bcx = bx + ds * (bW / 2 + 1.4f); bcy = armY - 4.4f; rr = 2.1f; }
                else { bcx = bx; bcy = bBot - 0.6f - rr; }
                SnowArt.Ball(G, PX(bcx), PY(bcy), rr * u, 1f);
                G.SmoothingMode = SmoothingMode.None;
            }

            float headTop = bTop;
            if (cfg.NewYear) { DrawHat(bx, bTop, sx); headTop = bTop - 7.8f; }

            // badges / effects
            if (badge == 1 || badge == 2)
            {
                float by = headTop - 6f - Math.Abs(U.Sin(C * 6)) * 0.8f, cxb = bx + (cfg.NewYear ? -ds * -1.5f : 0);
                Bx(badge == 1 ? bYellow : bPurple, cxb - 1.5f, by, 3, 4.6f);
                Bx(badge == 1 ? bYellow : bPurple, cxb - 1.1f, by - 0.4f, 2.2f, 5.4f);
                if (badge == 1) { Bx(bEye, cxb - 0.4f, by + 0.7f, 0.8f, 2.1f); Bx(bEye, cxb - 0.4f, by + 3.3f, 0.8f, 0.8f); }
                else
                {
                    float c = 0.6f, x0 = cxb - 0.9f, y0 = by + 0.5f;
                    Bx(bWhite, x0, y0, c * 3, c); Bx(bWhite, x0 + c * 2, y0 + c, c, c); Bx(bWhite, x0 + c, y0 + c * 2, c, c);
                    Bx(bWhite, x0 + c, y0 + c * 4, c, c);
                }
            }
            if (stars)
                for (int i = 0; i < 3; i++)
                {
                    double ang = C * 5 + i * 2.094;
                    float sxp = bx + (float)Math.Cos(ang) * 5.5f, syp = headTop - 1.2f + (float)Math.Sin(ang) * 1.2f;
                    Bx(bYellow, sxp - 0.3f, syp - 0.9f, 0.6f, 1.8f); Bx(bYellow, sxp - 0.9f, syp - 0.3f, 1.8f, 0.6f);
                }
            if (juggle)
                for (int i = 0; i < 3; i++)
                {
                    double ang = C * 4.5 + i * 2.094;
                    float jx = bx + (float)Math.Cos(ang) * 4.2f, jy = headTop - 3.8f + (float)Math.Sin(ang) * 3f;
                    bTmp.Color = cfg.NewYear ? Color.White : jugC[i];
                    Bx(bTmp, jx - 0.8f, jy - 0.8f, 1.6f, 1.6f);
                }
            if (dots)
            {
                int n = (int)(T * 2.5f) % 4;
                bTmp.Color = Color.FromArgb(220, 120, 110, 105);
                for (int i = 0; i < n; i++) Bx(bTmp, bx + 6.5f + i * 1.4f, headTop - 1.5f - i * 1f, 0.9f, 0.9f);
            }
        }

        static readonly Color[] jugC = { Color.FromArgb(255, 255, 196, 64), Color.FromArgb(255, 90, 160, 240), Color.FromArgb(255, 232, 64, 84) };

        void ArmRect(Brush b, int s, float xEdge, float off, float y, float w, float h)
        {
            if (s > 0) Bx(b, xEdge + off, y, w, h); else Bx(b, xEdge - off - w, y, w, h);
        }

        void DrawArm(int s, int mode, float bx, float bW, float armY, float wig)
        {
            float xe = bx + s * bW / 2;
            switch (mode)
            {
                case 0: ArmRect(bBody, s, xe, 0, armY, 2, 2); break;
                case 1: ArmRect(bBody, s, xe, 0, armY, 1.6f, 1.8f); ArmRect(bBody, s, xe, 0.3f + wig * 0.5f, armY - 2.8f, 1.5f, 3.0f); break;
                case 2: ArmRect(bBody, s, xe, 0, armY + 1.4f, 1.2f, 1.6f); ArmRect(bDark, s, xe, -2.4f, armY + 2.2f, 2.2f, 1.1f); break;
                case 4: ArmRect(bBody, s, xe, 0, armY + 0.6f, 1.6f, 2.8f); break;
                case 5: ArmRect(bBody, s, xe, 0, armY - 0.8f, 1.6f, 1.8f); ArmRect(bBody, s, xe, 1.3f, armY - 2.8f, 1.6f, 2.2f); break;
                case 6: ArmRect(bBody, s, xe, 0, armY - 0.2f, 3.4f, 1.6f); break;
            }
        }

        void Heart(Brush b, float cx, float cy, float c)
        {
            float x0 = cx - 2.5f * c, y0 = cy - 2f * c;
            Bx(b, x0 + c, y0, c, c); Bx(b, x0 + 3 * c, y0, c, c);
            Bx(b, x0, y0 + c, 5 * c, c);
            Bx(b, x0 + c, y0 + 2 * c, 3 * c, c);
            Bx(b, x0 + 2 * c, y0 + 3 * c, c, c);
        }

        void DrawFace(Emo e, int mouth, float bx, float bTop, float sx, float sy)
        {
            float ex = 2.5f * sx, ec = bTop + 3.1f * sy;
            float lx = lookX * 0.8f, ly = lookY * 0.6f;
            bool blink = blinkT > 0 && (e == Emo.Normal || e == Emo.Pleading || e == Emo.Excited || e == Emo.Angry || e == Emo.Sad || e == Emo.Focus);
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = bx + side * ex + lx, cy = ec + ly;
                Emo ee = e;
                if (e == Emo.Wink) ee = side > 0 ? Emo.Happy : Emo.Normal;
                if (blink) { Bx(bEye, cx - 0.6f, cy + 0.4f, 1.2f, 0.45f); continue; }
                switch (ee)
                {
                    case Emo.Normal:
                        if (!codex) { Bx(bEye, cx - 0.5f, cy - 1, 1, 2 * sy); break; }
                        if (side < 0) Bx(bEye, cx - 0.45f, cy - 0.7f, 0.9f, 1.5f * sy);          // small eye
                        else { float wx = cx - lx * 0.6f; Bx(bEye, wx - 0.75f, cy - 1.25f, 1.5f, 2.5f * sy); Bx(bWhite, wx - 0.55f, cy - 1.05f, 0.5f, 0.5f); } // big lazy eye
                        break;
                    case Emo.Closed: Bx(bEye, cx - 0.7f, cy + 0.4f, 1.4f, 0.45f); break;
                    case Emo.Sleepy: Bx(bEye, cx - 0.7f, cy + 0.2f, 1.4f, 0.6f); break;
                    case Emo.Surprised: Bx(bEye, cx - 0.7f, cy - 1.2f, 1.4f, 2.4f); Bx(bWhite, cx - 0.5f, cy - 1f, 0.45f, 0.45f); break;
                    case Emo.Excited: case Emo.Pleading:
                        Bx(bEye, cx - 0.7f, cy - 1.1f, 1.4f, 2.2f); Bx(bWhite, cx - 0.55f, cy - 0.95f, 0.5f, 0.5f);
                        if (ee == Emo.Pleading) Bx(bWhite, cx + 0.15f, cy + 0.3f, 0.3f, 0.3f);
                        break;
                    case Emo.Happy: case Emo.Laugh:
                        { float c = 0.62f; Bx(bEye, cx - 1.5f * c, cy, c, c); Bx(bEye, cx - 0.5f * c, cy - c, c, c); Bx(bEye, cx + 0.5f * c, cy, c, c); }
                        break;
                    case Emo.Angry:
                        Bx(bEye, cx - 0.5f, cy - 0.7f, 1, 1.7f);
                        Bx(bEye, cx - side * 1.1f - 0.45f, cy - 2.2f, 0.9f, 0.5f); Bx(bEye, cx - side * 0.2f - 0.45f, cy - 1.8f, 0.9f, 0.5f);
                        break;
                    case Emo.Sad:
                        Bx(bEye, cx - 0.5f, cy - 0.6f, 1, 1.7f);
                        Bx(bEye, cx - side * 1.1f - 0.45f, cy - 1.7f, 0.9f, 0.5f); Bx(bEye, cx - side * 0.2f - 0.45f, cy - 2.1f, 0.9f, 0.5f);
                        if (side < 0) { float ty = (clock * 2.2f) % 3f; Bx(bTear, cx - 0.3f, cy + 1.2f + ty, 0.6f, 0.9f); }
                        break;
                    case Emo.Love: Heart(bRed, cx, cy, 0.46f); break;
                    case Emo.Dizzy:
                        { float c = 0.55f; Bx(bEye, cx - 1.5f * c, cy - 1.5f * c, c, c); Bx(bEye, cx + 0.5f * c, cy - 1.5f * c, c, c); Bx(bEye, cx - 0.5f * c, cy - 0.5f * c, c, c); Bx(bEye, cx - 1.5f * c, cy + 0.5f * c, c, c); Bx(bEye, cx + 0.5f * c, cy + 0.5f * c, c, c); }
                        break;
                    case Emo.Focus: Bx(bEye, cx - 0.6f, cy - 0.3f, 1.2f, 1.1f); break;
                }
            }
            if (e == Emo.Happy || e == Emo.Laugh || e == Emo.Love || e == Emo.Pleading || e == Emo.Wink)
            {
                Bx(bBlush, bx - ex - 1.6f, ec + 1.3f, 1.5f, 0.6f);
                Bx(bBlush, bx + ex + 0.1f, ec + 1.3f, 1.5f, 0.6f);
            }
            float my = bTop + 5.5f * sy;
            if (codex && mouth == 0 && (e == Emo.Normal || e == Emo.Happy || e == Emo.Excited || e == Emo.Wink || e == Emo.Focus || e == Emo.Dizzy))
            {
                float mx = bx + lx * 0.4f + 0.3f;
                Bx(bEye, mx - 1f, my - 0.2f, 2f, 0.5f);
                Bx(bPink, mx - 0.1f, my + 0.3f, 0.9f, 0.9f + 0.15f * U.Sin(clock * 3));
            }
            switch (mouth)
            {
                case 1: Bx(bEye, bx + lx * 0.5f - 0.5f, my - 0.2f, 1f, 1f); break;
                case 2: Bx(bEye, bx - 1.1f, my - 0.4f, 2.2f, 1.1f); Bx(bRed, bx - 0.6f, my + 0.3f, 1.2f, 0.4f); break;
                case 3: Bx(bEye, bx - 0.7f, my - 0.1f, 1.4f, 0.6f); break;
                case 4: Bx(bEye, bx - 0.7f, my - 0.6f, 1.4f, 1.6f); break;
            }
        }

        void DrawHat(float bx, float bTop, float sx)
        {
            var g = G;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float tip = hatTip + U.Sin(hatA * Math.PI / 180) * 4f;
            float tipX = bx + tip, tipY = bTop - 7.6f + Math.Abs(tip) * 0.2f;
            using (var path = new GraphicsPath())
            {
                path.AddBezier(PX(bx - 5.4f * sx), PY(bTop - 0.4f), PX(bx - 4.4f * sx), PY(bTop - 4.8f), PX(tipX - 1.4f), PY(tipY + 0.4f), PX(tipX), PY(tipY));
                path.AddBezier(PX(tipX), PY(tipY), PX(tipX + 1.8f), PY(tipY + 1.8f), PX(bx + 4.4f * sx), PY(bTop - 5f), PX(bx + 5.4f * sx), PY(bTop - 0.4f));
                path.CloseFigure();
                using (var lg = new LinearGradientBrush(new PointF(PX(bx - 6), 0), new PointF(PX(bx + 6.5f), 0), Color.FromArgb(255, 236, 62, 62), Color.FromArgb(255, 168, 28, 36)))
                    g.FillPath(lg, path);
            }
            var tr = new RectangleF(PX(bx - 6.7f * sx), PY(bTop - 1.8f), 13.4f * sx * u, 2.2f * u);
            using (var p = U.RR(tr, 1.1f * u)) g.FillPath(bWhite, p);
            bTmp.Color = Color.FromArgb(255, 214, 222, 232);
            g.FillRectangle(bTmp, tr.X + u * 0.6f, tr.Bottom - 0.55f * u, tr.Width - 1.2f * u, 0.55f * u);
            float pr = 1.4f * u;
            g.FillEllipse(bTmp, PX(tipX) - pr, PY(tipY) - pr + 0.2f * u, pr * 2, pr * 2);
            g.FillEllipse(bWhite, PX(tipX) - pr * 0.9f - 0.15f * u, PY(tipY) - pr * 0.95f, pr * 1.8f, pr * 1.8f);
            g.SmoothingMode = SmoothingMode.None;
        }

        void DrawParticles()
        {
            foreach (var p in parts)
            {
                float a = Math.Min(1, p.life / (p.max * 0.4f));
                bTmp.Color = U.A(p.c, a);
                float c = p.size;
                switch (p.k)
                {
                    case 0: Heart(bTmp, p.x, p.y, c); break;
                    case 1:
                        Bx(bTmp, p.x, p.y, 4 * c, c); Bx(bTmp, p.x + 2 * c, p.y + c, c, c); Bx(bTmp, p.x + c, p.y + 2 * c, c, c);
                        Bx(bTmp, p.x, p.y + 3 * c, 4 * c, c); break;
                    case 2:
                        Bx(bTmp, p.x + 1.1f * c, p.y - 2.4f * c, 0.5f * c, 2.4f * c); Bx(bTmp, p.x, p.y - 0.5f * c, 1.4f * c, 1f * c);
                        Bx(bTmp, p.x + 1.5f * c, p.y - 2.4f * c, 0.9f * c, 0.5f * c); break;
                    case 3: Bx(bTmp, p.x, p.y, c, c * (0.4f + 0.5f * Math.Abs(U.Sin(p.life * 9)))); break;
                    case 4:
                        bTmp.Color = U.A(Color.FromArgb(255, 200, 214, 234), a); Bx(bTmp, p.x, p.y + 0.15f, c, c);
                        bTmp.Color = U.A(Color.White, a); Bx(bTmp, p.x, p.y, c * 0.8f, c * 0.8f); break;
                    case 5: Bx(bTmp, p.x, p.y, 0.8f * c, 1.2f * c); break;
                    case 7:
                        Bx(bTmp, p.x - 0.3f * c, p.y - 0.9f * c, 0.6f * c, 1.8f * c); Bx(bTmp, p.x - 0.9f * c, p.y - 0.3f * c, 1.8f * c, 0.6f * c); break;
                    case 6:
                        G.SmoothingMode = SmoothingMode.AntiAlias;
                        bTmp.Color = U.A(p.c, a * 0.55f);
                        G.FillEllipse(bTmp, PX(p.x - c / 2), PY(p.y - c / 2), c * u, c * u);
                        G.SmoothingMode = SmoothingMode.None; break;
                }
            }
        }

        // =====================================================================
        //  Menu / tray
        // =====================================================================
        public void ShowMenu() { if (leader != null) { leader.ShowMenu(); return; } menu.Show(Cursor.Position); }

        void BuildMenu()
        {
            menu = new ContextMenuStrip();
            var head = new ToolStripMenuItem("Clawdling") { Enabled = false };
            menu.Items.Add(head);
            menu.Items.Add(new ToolStripSeparator());
            miNY = new ToolStripMenuItem("Новогодний режим (шапочка)", null, (s, e) =>
            {
                cfg.NewYear = !cfg.NewYear; cfg.Save(); UpdateStrip();
                if (cfg.NewYear && OnGround && !HiddenState) { Go(St.Celebrate, 1.8f); Say("Хо-хо-хо!", 1.8f); }
            });
            miSnow = new ToolStripMenuItem("Снежки и сугроб на панели задач", null, (s, e) => { cfg.Snow = !cfg.Snow; cfg.Save(); UpdateStrip(); });
            miThrow = new ToolStripMenuItem("Кинуть снежок", null, (s, e) => UserThrowAtPet()) { ShortcutKeyDisplayString = "Ctrl+Alt+S" };
            menu.Items.Add(miNY); menu.Items.Add(miSnow); menu.Items.Add(miThrow);
            menu.Items.Add(new ToolStripSeparator());

            var size = new ToolStripMenuItem("Размер");
            string[] names = { "Маленький", "Средний", "Большой" };
            miSize = new ToolStripMenuItem[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                miSize[i] = new ToolStripMenuItem(names[i], null, (s, e) => { cfg.Size = idx; cfg.Save(); ApplySize(); if (buddy != null) buddy.ApplySize(); });
                size.DropDownItems.Add(miSize[i]);
            }
            menu.Items.Add(size);

            var act = new ToolStripMenuItem("Действия");
            act.DropDownItems.Add("Помахать", null, (s, e) => { if (OnGround) Go(St.Wave, 1.8f); });
            act.DropDownItems.Add("Станцевать", null, (s, e) => { if (OnGround) Go(St.Dance, 3.5f); });
            act.DropDownItems.Add("Пожонглировать", null, (s, e) => { if (OnGround) Go(St.Juggle, 4f); });
            act.DropDownItems.Add("Сделать зарядку", null, (s, e) => { if (OnGround) Go(St.Stretch, 3.2f); });
            act.DropDownItems.Add("Помедитировать", null, (s, e) => { if (OnGround) Go(St.Zen, 8f); });
            act.DropDownItems.Add("Уложить спать", null, (s, e) => { if (OnGround) Go(St.Sleep, 40); });
            act.DropDownItems.Add("Слепить снежок", null, (s, e) => { if (OnGround && cfg.NewYear) StartSnowball(false); });
            act.DropDownItems.Add("Позвать сюда", null, (s, e) => Summon());
            act.DropDownItems.Add("Позвать Кодекса", null, (s, e) => SummonBuddy());
            act.DropDownItems.Add(new ToolStripSeparator());
            act.DropDownItems.Add("Тест: «работа готова»", null, (s, e) => Notify("done", "Готово!", "Я закончил работу\n«demo-project»", null));
            act.DropDownItems.Add("Тест: «нужно внимание»", null, (s, e) => Notify("attn", "Claude ждёт тебя", "Нужно твоё подтверждение: Bash", null));
            act.DropDownItems.Add("Тест: запрос разрешения", null, (s, e) => Notify("ask", "Разрешить действие?", "Bash: npm install --save lodash", a => { }));
            menu.Items.Add(act);

            var duo = new ToolStripMenuItem("Кодекс");
            miCodex = new ToolStripMenuItem("Показывать Кодекса", null, (s, e) => { cfg.Codex = !cfg.Codex; cfg.Save(); SetBuddy(cfg.Codex); });
            duo.DropDownItems.Add(miCodex);
            duo.DropDownItems.Add(new ToolStripSeparator());
            var play = new ToolStripMenuItem("Играть");
            miGames = new ToolStripMenuItem[menuGames.Length];
            for (int i = 0; i < menuGames.Length; i++)
            {
                var g = menuGames[i];
                miGames[i] = new ToolStripMenuItem(gameNames[(int)g], null, (s, e) => PlayNow(g));
                play.DropDownItems.Add(miGames[i]);
            }
            duo.DropDownItems.Add(play);
            menu.Items.Add(duo);

            var hooks = new ToolStripMenuItem("Подключение к Claude Code");
            hooks.DropDownItems.Add("Скопировать настройки хуков", null, (s, e) => CopyHooks());
            menu.Items.Add(hooks);
            menu.Items.Add(new ToolStripSeparator());
            miAuto = new ToolStripMenuItem("Запускать вместе с Windows", null, (s, e) => { Cfg.Autostart = !Cfg.Autostart; });
            miHide = new ToolStripMenuItem("Спрятать питомцев", null, (s, e) => { cfg.Hidden = !cfg.Hidden; ApplyVisibility(); if (buddy != null) buddy.ApplyVisibility(); });
            menu.Items.Add(miAuto); menu.Items.Add(miHide);
            menu.Items.Add("Выход", null, (s, e) => ExitApp());
            menu.Opening += (s, e) => SyncMenu();
        }

        void SyncMenu()
        {
            miNY.Checked = cfg.NewYear;
            miSnow.Checked = cfg.Snow; miSnow.Enabled = cfg.NewYear;
            miThrow.Enabled = cfg.NewYear;
            for (int i = 0; i < 3; i++) miSize[i].Checked = cfg.Size == i;
            miAuto.Checked = Cfg.Autostart;
            miHide.Text = cfg.Hidden ? "Показать питомцев" : "Спрятать питомцев";
            miCodex.Checked = buddy != null;
            for (int i = 0; i < miGames.Length; i++)
                miGames[i].Enabled = buddy != null && (menuGames[i] != Game.Snow || (cfg.NewYear && cfg.Snow)) && (menuGames[i] != Game.Seek || SeekAllowed);
        }

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Icon = IconMaker.TrayIcon((int)Math.Round(16 * dpi));
            tray.Text = "Clawdling";
            tray.ContextMenuStrip = menu;
            tray.MouseDoubleClick += (s, e) => { if (cfg.Hidden) { cfg.Hidden = false; ApplyVisibility(); if (buddy != null) buddy.ApplyVisibility(); } Summon(); };
            tray.Visible = true;
        }

        void Summon()
        {
            if (bubble == null || fsHidden || cfg.Hidden) return;
            var scr = Screen.FromPoint(Cursor.Position);
            wa = scr.WorkingArea; sb = scr.Bounds;
            px = U.Cl(Cursor.Position.X, wa.Left + 8 * u, wa.Right - 8 * u); py = wa.Top + 16 * u; rot = 0;
            StartFall(0, 200 * k);
        }

        void CopyHooks()
        {
            string exe = Application.ExecutablePath.Replace("\\", "/");
            string cmd = "\\\"" + exe + "\\\" hook";
            string json =
                "{\n  \"hooks\": {\n" +
                "    \"Stop\": [{ \"hooks\": [{ \"type\": \"command\", \"command\": \"" + cmd + "\" }] }],\n" +
                "    \"Notification\": [{ \"hooks\": [{ \"type\": \"command\", \"command\": \"" + cmd + "\" }] }],\n" +
                "    \"PermissionRequest\": [{ \"hooks\": [{ \"type\": \"command\", \"command\": \"" + cmd + "\", \"timeout\": 60 }] }]\n" +
                "  }\n}";
            try { Clipboard.SetText(json); } catch { }
            MessageBox.Show("Настройки хуков скопированы в буфер обмена.\n\nВставь блок \"hooks\" в файл\n%USERPROFILE%\\.claude\\settings.json\n\n" +
                            "Stop — Клод сообщит, что закончил работу.\nNotification — Claude ждёт твоего ответа.\nPermissionRequest — запрос разрешения с кнопками прямо в пузыре (необязательно).\n\n" + json,
                            "Clawdling", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ExitApp()
        {
            try { N.UnregisterHotKey(Handle, 1); } catch { }
            bubble.Kill();
            if (buddy != null) buddy.bubble.Kill();
            tray.Visible = false; tray.Dispose();
            Application.Exit();
        }
    }
}
