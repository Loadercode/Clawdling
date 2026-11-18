// Renders README images using the real drawing code of Clawdling.
// Build: tools\make-assets.cmd  (compiled together with src\*.cs, not part of Clawdling.exe)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Clawdling
{
    partial class Pet
    {
        internal Pet(int unit, bool ny) : base(10, 10, false)
        {
            cfg = new Cfg(); cfg.NewYear = ny; cfg.Snow = true;
            dpi = 1; myPid = 0; bubble = null; timer = null; pipe = null;
            u = unit; k = u / 3f;
            CW = (int)(34 * u); CH = (int)(34 * u); Ox = CW / 2f; Oy = CH - 2 * u;
            SetCanvas(CW, CH);
            wa = new Rectangle(0, 0, 100000, 3000); sb = wa;
            px = 50000; py = 3000; surf = 0; greeted = true; curX = -100000; curY = -100000;
        }

        internal void Pose(St s, int d)
        {
            parts.Clear(); dir = d; sink = 0; rot = 0; snowOnBody = 0; holding = false; hitBySnow = false;
            blinkT = 0; blinkIn = 9999; idleTotal = 0; running = false; lookX = d * 0.3f; lookY = 0.1f;
            hatTip = -d * 3.2f; hatA = 0; hatV = 0; vx = 0; vy = 0; partT = 0;
            walkTo = px + d * 100000; walkSpd = 45 * k;
            targetCursor = false; target = new PointF(px + d * 1000, py - 600);
            Go(s, 9999); clock = 0;
            if (s == St.Roll || s == St.WindUp) { holding = true; ballR = 2.1f; }
        }

        internal void Step(float dt, bool look)
        {
            t += dt; clock += dt;
            UpdateGroundState(dt);
            UpdateParticles(dt);
            if (look) UpdateLook(dt);
        }

        internal void MakeCodex() { codex = true; ApplySkin(); }
        internal void SetLook(float lx, float ly) { lookX = lx; lookY = ly; }
        internal void SetSnow(float a, int side) { snowOnBody = a; snowSide = side; }
        internal void SetDur(float d) { dur = d; }
        internal St State { get { return st; } }
        internal float Unit { get { return u; } }
        internal int Dir { get { return dir; } }

        internal Bitmap Frame()
        {
            var g = S.G; G = g;
            g.ResetTransform(); g.ResetClip(); g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half;
            ox = Ox; oy = Oy;
            DrawPet();
            DrawParticles();
            g.Flush(FlushIntention.Sync);
            var b = new Bitmap(CW, CH, PixelFormat.Format32bppArgb);
            using (var gg = Graphics.FromImage(b)) gg.DrawImage(S.Bmp, 0, 0, CW, CH);
            return b;
        }
    }

    partial class Bubble
    {
        internal Bitmap Snap(int kind, string ttl, string txt, bool darkMode)
        {
            Open(kind, ttl, txt, 60, null);
            dark = darkMode; below = false; tailX = 40 * s; hover = -1;
            Draw();
            S.G.Flush(FlushIntention.Sync);
            var b = new Bitmap(S.W, S.H, PixelFormat.Format32bppArgb);
            using (var gg = Graphics.FromImage(b)) gg.DrawImage(S.Bmp, 0, 0, S.W, S.H);
            return b;
        }
    }

    static class AssetGen
    {
        static string outDir;
        static void Save(Bitmap b, string name) { b.Save(Path.Combine(outDir, name), ImageFormat.Png); b.Dispose(); }

        static Bitmap Copy(Surface s)
        {
            s.G.Flush(FlushIntention.Sync);
            var b = new Bitmap(s.W, s.H, PixelFormat.Format32bppArgb);
            using (var gg = Graphics.FromImage(b)) gg.DrawImage(s.Bmp, 0, 0, s.W, s.H);
            return b;
        }

        static void Still(Pet p, string name, St s, float time, int d, Action<Pet> tweak)
        {
            p.Pose(s, d);
            float dt = 1 / 30f;
            for (float tt = 0; tt < time; tt += dt) p.Step(dt, false);
            if (tweak != null) tweak(p);
            Save(p.Frame(), name);
        }

        [STAThread]
        static void Main(string[] args)
        {
            outDir = args.Length > 0 ? args[0] : "frames";
            Directory.CreateDirectory(outDir);
            var plain = new Pet(6, false);
            var ny = new Pet(6, true);

            // ---- emotions ----
            Still(plain, "e_idle.png", St.Idle, 0.2f, 1, p => p.SetLook(0.3f, 0.1f));
            Still(plain, "e_wave.png", St.Wave, 0.35f, 1, null);
            Still(plain, "e_happy.png", St.Happy, 0.12f, 1, null);
            Still(plain, "e_laugh.png", St.Laugh, 0.33f, 1, null);
            Still(plain, "e_love.png", St.Love, 1.25f, 1, null);
            Still(plain, "e_sleep.png", St.Sleep, 2.4f, 1, null);
            Still(plain, "e_surprised.png", St.Notify, 0.18f, 1, null);
            Still(plain, "e_ask.png", St.Ask, 0.6f, 1, null);
            Still(plain, "e_dizzy.png", St.Hit, 0.35f, 1, null);
            Still(plain, "e_angry.png", St.Angry, 0.62f, 1, null);
            Still(plain, "e_sad.png", St.Sad, 0.45f, 1, null);
            Still(plain, "e_dance.png", St.Dance, 0.95f, 1, null);
            Still(plain, "e_celebrate.png", St.Celebrate, 0.55f, 1, null);
            Still(plain, "e_think.png", St.Think, 1.3f, 1, null);
            Still(plain, "e_yawn.png", St.Yawn, 0.7f, 1, null);
            Still(plain, "e_walk.png", St.Walk, 0.3f, 1, null);
            // New Year
            Still(ny, "n_idle.png", St.Idle, 0.2f, 1, p => p.SetLook(0.3f, 0.1f));
            Still(ny, "n_happy.png", St.Happy, 0.12f, 1, null);
            Still(ny, "n_roll.png", St.Roll, 0.7f, 1, null);
            Still(ny, "n_windup.png", St.WindUp, 0.1f, 1, null);
            Still(ny, "n_hit.png", St.Hit, 0.3f, -1, p => p.SetSnow(1, -1));
            Still(ny, "n_shiver.png", St.Shiver, 0.8f, 1, null);
            Still(ny, "n_celebrate.png", St.Celebrate, 0.55f, 1, null);
            Still(ny, "n_done.png", St.Celebrate, 0.4f, -1, null);

            // Codex
            var cx = new Pet(6, false); cx.MakeCodex();
            var cxn = new Pet(6, true); cxn.MakeCodex();
            Still(cx, "c_idle.png", St.Idle, 0.2f, -1, p => p.SetLook(-0.3f, 0.1f));
            Still(cx, "c_happy.png", St.Happy, 0.12f, -1, null);
            Still(cx, "c_trip.png", St.Trip, 0.9f, -1, null);
            Still(cx, "c_brawl.png", St.Brawl, 0.4f, -1, null);
            Still(cx, "c_high5.png", St.HighFive, 0.5f, -1, null);
            Still(cx, "c_duck.png", St.Duck, 0.5f, -1, null);
            Still(cx, "c_think.png", St.Think, 1.3f, -1, null);
            Still(cx, "c_laugh.png", St.Laugh, 0.33f, -1, null);
            Still(cxn, "cn_idle.png", St.Idle, 0.2f, -1, p => p.SetLook(-0.3f, 0.1f));
            Still(cxn, "cn_windup.png", St.WindUp, 0.1f, -1, null);
            Still(plain, "e_brawl.png", St.Brawl, 0.4f, 1, null);
            Still(plain, "e_high5.png", St.HighFive, 0.5f, 1, null);

            // ---- animations (20 fps) ----
            float fdt = 0.05f;
            // walk cycle (new year + plain)
            foreach (var pp in new[] { ny, plain })
            {
                string pre = pp == ny ? "wn" : "wp";
                pp.Pose(St.Walk, 1);
                for (int i = 0; i < 6; i++) pp.Step(fdt, true);
                for (int i = 0; i < 80; i++) { pp.Step(fdt, true); Save(pp.Frame(), pre + "r" + i.ToString("00") + ".png"); }
                pp.Pose(St.Walk, -1);
                for (int i = 0; i < 6; i++) pp.Step(fdt, true);
                for (int i = 0; i < 80; i++) { pp.Step(fdt, true); Save(pp.Frame(), pre + "l" + i.ToString("00") + ".png"); }
            }
            // snowball sequence: scoop -> roll -> windup -> throw -> laugh
            {
                ny.Pose(St.Scoop, 1); ny.SetDur(0.6f);
                int i = 0; float tot = 0;
                using (var log = new StreamWriter(Path.Combine(outDir, "throw.txt")))
                {
                    while (tot < 3.6f)
                    {
                        ny.Step(fdt, false); tot += fdt;
                        if (ny.State == St.Idle || ny.State == St.Laugh) ny.SetDur(99);
                        log.WriteLine(i + " " + ny.State);
                        Save(ny.Frame(), "th" + i.ToString("00") + ".png"); i++;
                    }
                }
            }
            // notification: idle -> celebrate
            {
                ny.Pose(St.Idle, 1);
                for (int i = 0; i < 12; i++) { ny.Step(fdt, false); Save(ny.Frame(), "nt" + i.ToString("00") + ".png"); }
                ny.Pose(St.Celebrate, 1); ny.SetDur(1.8f);
                for (int i = 12; i < 60; i++) { ny.Step(fdt, false); if (ny.State == St.Idle) ny.SetDur(99); Save(ny.Frame(), "nt" + i.ToString("00") + ".png"); }
            }

            // ---- bubbles ----
            var bub = new Bubble(2f);
            Save(bub.Snap(1, "Готово!", "Я закончил работу\n«my-project»", true), "b_done_dark.png");
            Save(bub.Snap(2, "Claude ждёт тебя", "Нужно твоё подтверждение: Bash", true), "b_attn_dark.png");
            Save(bub.Snap(3, "Разрешить действие?", "Bash: npm install --save lodash\n«my-project»", true), "b_ask_dark.png");
            Save(bub.Snap(1, "Готово!", "Я закончил работу\n«my-project»", false), "b_done_light.png");
            Save(bub.Snap(2, "Claude ждёт тебя", "Нужно твоё подтверждение: Bash", false), "b_attn_light.png");
            Save(bub.Snap(3, "Разрешить действие?", "Bash: npm install --save lodash\n«my-project»", false), "b_ask_light.png");
            Save(bub.Snap(0, "", "Хо-хо-хо!", true), "b_speech_dark.png");
            Save(bub.Snap(0, "", "Ах так?! Ну держись!", true), "b_speech2_dark.png");
            Save(bub.Snap(0, "", "Привет! Я Клод", true), "b_hello_dark.png");

            // ---- snow ----
            var strip = new SnowStrip(plain, 2f);
            strip.Setup(new Rectangle(0, 0, 1280, 700));
            Save(Copy(strip.S), "snow_strip.png");
            var ball = new Snowball(26); ball.spin = 0.8f; ball.Draw();
            Save(Copy(ball.S), "ball.png");
            for (int kind = 0; kind <= 1; kind++)
            {
                var sp = new Splat(0, 0, kind, 2f);
                for (int i = 0; i < 16; i++) { sp.Tick(0.05f); Save(Copy(sp.S), "sp" + kind + "_" + i.ToString("00") + ".png"); }
            }
            Console.WriteLine("assets -> " + outDir);
        }
    }
}
