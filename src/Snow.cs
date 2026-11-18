using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Clawdling
{
    static class SnowArt
    {
        static readonly Color Edge = Color.FromArgb(255, 184, 203, 226);
        public static void Ball(Graphics g, float cx, float cy, float r, float alpha)
        {
            if (r < 0.5f) return;
            var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (var pb = new PathGradientBrush(path))
                {
                    pb.CenterColor = U.A(Color.White, alpha);
                    pb.SurroundColors = new[] { U.A(Edge, alpha) };
                    pb.CenterPoint = new PointF(cx - r * 0.38f, cy - r * 0.4f);
                    pb.FocusScales = new PointF(0.35f, 0.35f);
                    g.FillPath(pb, path);
                }
                using (var pen = new Pen(U.A(Color.FromArgb(150, 160, 182, 210), alpha), Math.Max(1f, r * 0.08f)))
                    g.DrawPath(pen, path);
            }
            g.SmoothingMode = old;
        }
    }

    /// <summary>A flying snowball: tiny click-through window.</summary>
    class Snowball : LayeredWindow
    {
        public float x, y, vx, vy, t, T, x0, y0, tx, ty, d, spin;
        public bool aimed, user, held, dodgeChecked, cursorTarget;
        public int splatKind;
        static readonly SolidBrush speck = new SolidBrush(Color.FromArgb(70, 120, 145, 180));

        public Snowball(float diam) : base((int)(diam + 6), (int)(diam + 6), true) { d = diam; }

        public void Draw()
        {
            var g = S.G; g.Clear(Color.Transparent);
            float c = S.W / 2f, r = d / 2f;
            SnowArt.Ball(g, c, c, r, 1f);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < 3; i++)
            {
                float a = spin + i * 2.1f;
                float rr = r * (0.35f + 0.15f * i);
                float sx = c + U.Cos(a) * rr, sy = c + U.Sin(a) * rr * 0.85f;
                float sr = Math.Max(0.8f, r * 0.12f);
                g.FillEllipse(speck, sx - sr, sy - sr, sr * 2, sr * 2);
            }
        }
        public void Place() { Present((int)Math.Round(x - S.W / 2f), (int)Math.Round(y - S.H / 2f), 255); }
    }

    /// <summary>Snow splash. kind 0 = hit the "screen glass" (sticks and slides), 1 = on the taskbar/floor, 2 = small puff (hit the pet).</summary>
    class Splat : LayeredWindow
    {
        readonly int kind; readonly float s, c, dur;
        float t; public bool Dead;
        readonly List<float[]> chunks = new List<float[]>();
        readonly List<float[]> blob = new List<float[]>();
        readonly int sx, sy;
        static readonly SolidBrush bShade = new SolidBrush(Color.White), bTop = new SolidBrush(Color.White);

        static int Sz(float s) { return (int)(130 * s); }

        public Splat(float x, float y, int kind, float s) : base(Sz(s), Sz(s), true)
        {
            this.kind = kind; this.s = s; c = S.W / 2f;
            dur = kind == 0 ? 2.6f : kind == 1 ? 1.5f : 0.8f;
            int n = kind == 2 ? 9 : 15;
            for (int i = 0; i < n; i++)
            {
                double ang = kind == 1 ? U.Rf(-3.0f, -0.15f) : U.Rf(0, 6.283f);
                float sp = U.Rf(50, kind == 2 ? 160 : 230) * s;
                chunks.Add(new[] { 0f, kind == 1 ? -2 * s : 0f, (float)Math.Cos(ang) * sp, (float)Math.Sin(ang) * sp - (kind == 1 ? 40 * s : 0), U.Rf(1.4f, 3.6f) * s });
            }
            if (kind == 0)
            {
                blob.Add(new[] { 0f, 0f, 8.5f * s });
                for (int i = 0; i < 9; i++)
                {
                    float a = U.Rf(0, 6.283f), dd = U.Rf(4, 10) * s;
                    blob.Add(new[] { U.Cos(a) * dd, U.Sin(a) * dd * 0.9f, U.Rf(2.2f, 5.5f) * s });
                }
                for (int i = 0; i < 3; i++) blob.Add(new[] { U.Rf(-6, 6) * s, U.Rf(6, 12) * s, U.Rf(1.5f, 2.6f) * s }); // drips
            }
            else if (kind == 1)
            {
                for (int i = 0; i < 9; i++) blob.Add(new[] { U.Rf(-13, 13) * s, U.Rf(-3, 0) * s, U.Rf(2.0f, 4.5f) * s });
                blob.Add(new[] { 0f, -2 * s, 5 * s });
            }
            sx = (int)Math.Round(x - c); sy = (int)Math.Round(y - c);
            Draw();
        }

        public void Tick(float dt)
        {
            t += dt;
            if (t >= dur) { Dead = true; return; }
            foreach (var ch in chunks)
            {
                ch[0] += ch[2] * dt; ch[1] += ch[3] * dt; ch[3] += 650 * s * dt; ch[2] *= (1 - 1.5f * dt);
                if (kind == 1 && ch[1] > 0) { ch[1] = 0; ch[3] = 0; ch[2] *= 0.5f; }
            }
            Draw();
        }

        void Draw()
        {
            var g = S.G; g.Clear(Color.Transparent); g.SmoothingMode = SmoothingMode.AntiAlias;
            float a;
            if (kind == 0) a = t < 1.4f ? 1 : 1 - (t - 1.4f) / (dur - 1.4f);
            else if (kind == 1) a = t < 0.8f ? 1 : 1 - (t - 0.8f) / (dur - 0.8f);
            else a = 0;
            a = U.Cl(a, 0, 1);
            if (blob.Count > 0 && a > 0)
            {
                float slide = kind == 0 ? Math.Max(0, t - 0.5f) * 9 * s : 0;
                float pop = Math.Min(1, t / 0.07f);
                bShade.Color = Color.FromArgb((int)(235 * a), 196, 212, 234);
                bTop.Color = Color.FromArgb((int)(255 * a), 252, 253, 255);
                foreach (var b in blob)
                {
                    float r = b[2] * pop; float yy = c + b[1] + slide * (b[1] > 5 * s ? 1.6f : 1f);
                    g.FillEllipse(bShade, c + b[0] - r, yy - r + 0.8f * s, r * 2, r * 2);
                }
                foreach (var b in blob)
                {
                    float r = b[2] * pop * 0.86f; float yy = c + b[1] + slide * (b[1] > 5 * s ? 1.6f : 1f);
                    g.FillEllipse(bTop, c + b[0] - r - 0.5f * s, yy - r - 0.6f * s, r * 2, r * 2);
                }
            }
            float ca = U.Cl(1 - t / 0.85f, 0, 1);
            if (ca > 0)
            {
                bShade.Color = Color.FromArgb((int)(220 * ca), 205, 220, 240);
                bTop.Color = Color.FromArgb((int)(255 * ca), 255, 255, 255);
                foreach (var ch in chunks)
                {
                    float r = ch[4];
                    g.FillEllipse(bShade, c + ch[0] - r, c + ch[1] - r + 0.5f, r * 2, r * 2);
                    g.FillEllipse(bTop, c + ch[0] - r * 0.8f - 0.3f, c + ch[1] - r * 0.8f - 0.3f, r * 1.6f, r * 1.6f);
                }
            }
            Present(sx, sy, 255);
        }
    }

    /// <summary>Thin layer of snow lying on top of the taskbar, with a small drift you can grab snowballs from.</summary>
    class SnowStrip : LayeredWindow
    {
        readonly Pet pet; readonly float s;
        public Rectangle Area = Rectangle.Empty;
        float[] h = new float[0];
        readonly List<float[]> sparkles = new List<float[]>();
        float t, redraw;
        public SnowStrip(Pet p, float s) : base(10, 10, false)
        {
            pet = p; this.s = s;
            MouseDown += (o, e) => { if (e.Button == MouseButtons.Left) pet.GrabSnow(); };
            MouseUp += (o, e) => { if (e.Button == MouseButtons.Right) pet.ShowMenu(); };
        }
        public void Setup(Rectangle wa)
        {
            Area = wa;
            int W = wa.Width, H = (int)(20 * s);
            SetCanvas(W, H);
            h = new float[W];
            float p1 = U.Rf(0, 6), p2 = U.Rf(0, 6), p3 = U.Rf(0, 6);
            float d1 = W - 170 * s, d2 = U.Rf(0.15f, 0.45f) * W, d3 = U.Rf(0.55f, 0.75f) * W;
            for (int x = 0; x < W; x++)
            {
                float v = 3.6f * s + U.Sin(x * 0.012 + p1) * 0.9f * s + U.Sin(x * 0.045 + p2) * 0.5f * s + U.Sin(x * 0.13 + p3) * 0.25f * s;
                v += 12 * s * (float)Math.Exp(-Math.Pow((x - d1) / (34 * s), 2));
                v += 4.5f * s * (float)Math.Exp(-Math.Pow((x - d2) / (24 * s), 2));
                v += 3.5f * s * (float)Math.Exp(-Math.Pow((x - d3) / (18 * s), 2));
                h[x] = Math.Min(v, H - 2);
            }
            sparkles.Clear();
            int n = Math.Max(8, W / 90);
            for (int i = 0; i < n; i++) sparkles.Add(new[] { U.Rf(0, W - 1), U.Rf(0.6f, 2.2f), U.Rf(0, 6.28f) });
            Draw();
        }
        public void Tick(float dt)
        {
            t += dt; redraw -= dt;
            if (redraw <= 0) { redraw = 0.2f; Draw(); }
        }
        void Draw()
        {
            var g = S.G; int W = S.W, H = S.H;
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var pts = new List<PointF>(W / 2 + 4);
            pts.Add(new PointF(0, H));
            for (int x = 0; x < W; x += 2) pts.Add(new PointF(x, H - h[x]));
            pts.Add(new PointF(W, H - h[W - 1])); pts.Add(new PointF(W, H));
            var arr = pts.ToArray();
            using (var lg = new LinearGradientBrush(new PointF(0, H - 16 * s), new PointF(0, H), Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 200, 214, 233)))
                g.FillPolygon(lg, arr);
            using (var pen = new Pen(Color.FromArgb(120, 170, 190, 218), 1f)) g.DrawLine(pen, 0, H - 0.5f, W, H - 0.5f);
            using (var br = new SolidBrush(Color.White))
                foreach (var sp in sparkles)
                {
                    float a = U.Sin(t * sp[1] + sp[2]);
                    if (a < 0.55f) continue;
                    a = (a - 0.55f) / 0.45f;
                    int x = (int)sp[0]; float y = H - h[x] + 1.6f * s; float r = 1.6f * s * a;
                    br.Color = Color.FromArgb((int)(230 * a), 150, 205, 255);
                    g.FillRectangle(br, x - r, y - 0.4f * s, r * 2, 0.8f * s);
                    g.FillRectangle(br, x - 0.4f * s, y - r, 0.8f * s, r * 2);
                }
            Present(Area.Left, Area.Bottom - H + (int)Math.Round(1.5f * s), 255);
        }
    }
}
