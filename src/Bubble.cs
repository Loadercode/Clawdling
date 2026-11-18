using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Clawdling
{
    /// <summary>Speech bubble / notification card. Kind: 0 speech, 1 done, 2 attention, 3 ask (buttons), 4 info.</summary>
    partial class Bubble : LayeredWindow
    {
        readonly float s;
        public bool Active, IsClosing; public int Kind;
        string title = "", body = "";
        Action<string> onAns;
        float life, maxLife, t, closeT, cdT;
        bool hovered, below, dirty = true;
        float tailX = -1;
        RectangleF box; RectangleF[] btnRel = new RectangleF[0];
        readonly string[] btnLabels = { "Разрешить", "Отклонить" };
        readonly string[] btnVals = { "allow", "deny" };
        RectangleF closeRel; int hover = -1;
        readonly Font fTitle, fText, fSpeech, fBtn, fIcon;
        readonly float m, pad, tail, iconD;
        SizeF titleSz, textSz;
        bool dark;
        static readonly StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        public Bubble(float dpi) : base(10, 10, false)
        {
            s = dpi; m = 12 * s; pad = 12 * s; tail = 9 * s; iconD = 26 * s;
            fTitle = new Font("Segoe UI Semibold", 13.5f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            fText = new Font("Segoe UI", 12.5f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            fSpeech = new Font("Segoe UI Semibold", 12.5f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            fBtn = new Font("Segoe UI Semibold", 12f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            fIcon = new Font("Segoe UI", 15f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            MouseMove += (o, e) => { hovered = true; int hv = HitTest(e.Location); if (hv != hover) { hover = hv; dirty = true; } Cursor = hv >= 0 ? Cursors.Hand : Cursors.Default; };
            MouseLeave += (o, e) => { hovered = false; if (hover != -1) { hover = -1; dirty = true; } };
            MouseDown += (o, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                int hv = HitTest(e.Location);
                if (hv >= 0 && hv < 2 && Kind == 3) Finish(btnVals[hv]);
                else if (hv == 9) Finish("none");
                else if (Kind != 3) Finish(null);
            };
        }

        public bool IsNotice { get { return Active && Kind != 0; } }

        int HitTest(Point p)
        {
            if (Kind == 3)
                for (int i = 0; i < btnRel.Length; i++)
                {
                    var r = btnRel[i]; r.Offset(box.Location);
                    if (r.Contains(p)) return i;
                }
            if (Kind != 0)
            {
                var c = closeRel; c.Offset(box.Location); c.Inflate(4 * s, 4 * s);
                if (c.Contains(p)) return 9;
            }
            return -1;
        }

        static bool IsDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch { return false; }
        }

        public void Open(int kind, string title, string text, float lifeSec, Action<string> ans)
        {
            if (onAns != null && onAns != ans) { var old = onAns; onAns = null; old("none"); }
            Kind = kind; this.title = title ?? ""; body = text ?? ""; onAns = ans;
            life = maxLife = lifeSec; t = 0; IsClosing = false; Active = true; dirty = true; hover = -1; tailX = -1;
            DoLayout();
        }

        public void Finish(string ans)
        {
            if (!Active || IsClosing) return;
            IsClosing = true; closeT = 0;
            var cb = onAns; onAns = null;
            if (cb != null) cb(ans ?? "none");
        }

        public void Kill()
        {
            var cb = onAns; onAns = null;
            if (cb != null) cb("none");
            Active = false; IsClosing = false;
            if (Visible) Hide();
        }

        void DoLayout()
        {
            dark = IsDark();
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                float bw, bh;
                if (Kind == 0)
                {
                    textSz = g.MeasureString(body, fSpeech, (int)(200 * s));
                    bw = textSz.Width + pad * 1.6f; bh = textSz.Height + pad * 1.1f;
                }
                else
                {
                    float maxW = 250 * s; float cx = pad + iconD + 10 * s;
                    titleSz = g.MeasureString(title, fTitle, (int)maxW);
                    textSz = body.Length > 0 ? g.MeasureString(body, fText, (int)maxW) : SizeF.Empty;
                    float cw = Math.Max(titleSz.Width + 20 * s, textSz.Width);
                    bw = cx + cw + pad;
                    bh = pad + titleSz.Height + (body.Length > 0 ? 1 * s + textSz.Height : 0) + pad;
                    if (bh < pad * 2 + iconD) bh = pad * 2 + iconD;
                    closeRel = new RectangleF(bw - pad - 8 * s, pad - 1 * s, 9 * s, 9 * s);
                    if (Kind == 3)
                    {
                        float bx = cx; float by = bh - pad * 0.3f; float bhh = 28 * s;
                        btnRel = new RectangleF[2];
                        for (int i = 0; i < 2; i++)
                        {
                            float w = g.MeasureString(btnLabels[i], fBtn).Width + 26 * s;
                            btnRel[i] = new RectangleF(bx, by, w, bhh);
                            bx += w + 8 * s;
                        }
                        bh = by + bhh + pad + 4 * s;
                        bw = Math.Max(bw, bx - 8 * s + pad);
                        closeRel = new RectangleF(bw - pad - 8 * s, pad - 1 * s, 9 * s, 9 * s);
                    }
                }
                box = new RectangleF(m, m, (float)Math.Ceiling(bw), (float)Math.Ceiling(bh));
                SetCanvas((int)Math.Ceiling(box.Width + 2 * m), (int)Math.Ceiling(box.Height + 2 * m + tail));
            }
        }

        /// <param name="hx">head x (screen)</param><param name="hy">head top y</param><param name="fy">feet y</param>
        public void Tick(float dt, float hx, float hy, float fy, Rectangle area)
        {
            if (!Active) return;
            t += dt;
            if (IsClosing)
            {
                closeT += dt;
                if (closeT >= 0.18f) { Active = false; IsClosing = false; Hide(); return; }
            }
            else
            {
                if (!hovered) life -= dt;
                if (life <= 0) Finish(null);
                if (Kind == 3) { cdT += dt; if (cdT > 0.1f) { cdT = 0; dirty = true; } }
            }
            bool b = hy - (S.H - m) < area.Top + 2;
            if (b != below) { below = b; dirty = true; }
            float W = S.W;
            float nx = U.Cl(hx - W / 2f, area.Left - m + 2, area.Right - W + m - 2);
            float ny = below ? fy + 4 * s - m : hy - 2 * s - (S.H - m);
            float ntx = hx - nx;
            if (Math.Abs(ntx - tailX) > 0.5f) { tailX = ntx; dirty = true; }
            float a = IsClosing ? 1 - closeT / 0.18f : Math.Min(1, t / 0.16f);
            float slide = (1 - a) * 8 * s * (below ? -1 : 1);
            if (dirty) { Draw(); dirty = false; }
            Present((int)Math.Round(nx), (int)Math.Round(ny + slide), (byte)(255 * U.Cl(a, 0, 1)));
            if (!Visible) Show();
        }

        void Draw()
        {
            var g = S.G;
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            box.Y = below ? m + tail : m;
            Color bg = dark ? Color.FromArgb(255, 40, 37, 35) : Color.FromArgb(255, 255, 253, 249);
            Color border = dark ? Color.FromArgb(255, 74, 68, 63) : Color.FromArgb(255, 228, 217, 208);
            Color fg = dark ? Color.FromArgb(255, 243, 237, 232) : Color.FromArgb(255, 43, 36, 32);
            Color sub = dark ? Color.FromArgb(255, 196, 188, 181) : Color.FromArgb(255, 98, 88, 82);
            float rad = (Kind == 0 ? 11 : 13) * s;

            using (var sh = new SolidBrush(Color.FromArgb(9, 0, 0, 0)))
                for (int i = 6; i >= 1; i--)
                {
                    var r = box; r.Inflate(i * 1.3f * s, i * 1.3f * s); r.Offset(0, 2.2f * s);
                    using (var p = U.RR(r, rad + i * 1.3f * s)) g.FillPath(sh, p);
                }

            float tx = U.Cl(tailX, box.X + rad + 6 * s, box.Right - rad - 6 * s);
            PointF[] tri = below
                ? new[] { new PointF(tx - 7 * s, box.Y + 1), new PointF(tx + 7 * s, box.Y + 1), new PointF(tx + 1 * s, box.Y - tail) }
                : new[] { new PointF(tx - 7 * s, box.Bottom - 1), new PointF(tx + 7 * s, box.Bottom - 1), new PointF(tx + 1 * s, box.Bottom + tail) };

            using (var bgB = new SolidBrush(bg))
            using (var bp = new Pen(border, Math.Max(1f, s)))
            {
                using (var p = U.RR(box, rad)) { g.FillPath(bgB, p); g.DrawPath(bp, p); }
                g.DrawLine(bp, tri[0], tri[2]); g.DrawLine(bp, tri[1], tri[2]);
                g.FillPolygon(bgB, new[] { new PointF(tri[0].X + 0.8f * s, tri[0].Y + (below ? 1.5f * s : -1.5f * s)), new PointF(tri[1].X - 0.8f * s, tri[1].Y + (below ? 1.5f * s : -1.5f * s)), tri[2] });
            }

            using (var fgB = new SolidBrush(fg))
            using (var subB = new SolidBrush(sub))
            {
                if (Kind == 0)
                {
                    g.DrawString(body, fSpeech, fgB, new RectangleF(box.X + pad * 0.8f, box.Y + pad * 0.55f, textSz.Width + 2, textSz.Height + 2));
                    return;
                }
                Color acc = Kind == 1 ? Color.FromArgb(255, 52, 168, 90) : Kind == 2 ? Color.FromArgb(255, 217, 119, 87) : Kind == 3 ? Color.FromArgb(255, 132, 100, 236) : Color.FromArgb(255, 60, 136, 228);
                var ic = new RectangleF(box.X + pad, box.Y + pad, iconD, iconD);
                using (var ab = new SolidBrush(acc)) g.FillEllipse(ab, ic);
                if (Kind == 1)
                    using (var wp = new Pen(Color.White, 2.6f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(wp, new[] { new PointF(ic.X + iconD * 0.28f, ic.Y + iconD * 0.52f), new PointF(ic.X + iconD * 0.44f, ic.Y + iconD * 0.68f), new PointF(ic.X + iconD * 0.73f, ic.Y + iconD * 0.36f) });
                else
                    using (var wb = new SolidBrush(Color.White))
                    {
                        var r2 = ic; r2.Offset(0.5f * s, 0.5f * s);
                        g.DrawString(Kind == 2 ? "!" : Kind == 3 ? "?" : "i", fIcon, wb, r2, center);
                    }
                float cx = box.X + pad + iconD + 10 * s;
                g.DrawString(title, fTitle, fgB, new RectangleF(cx, box.Y + pad - 2 * s, titleSz.Width + 2, titleSz.Height + 2));
                if (body.Length > 0)
                    g.DrawString(body, fText, subB, new RectangleF(cx, box.Y + pad - 2 * s + titleSz.Height + 1 * s, textSz.Width + 2, textSz.Height + 2));

                var cr = closeRel; cr.Offset(box.Location);
                using (var cp = new Pen(hover == 9 ? fg : U.A(sub, 0.7f), 1.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(cp, cr.Left, cr.Top, cr.Right, cr.Bottom);
                    g.DrawLine(cp, cr.Right, cr.Top, cr.Left, cr.Bottom);
                }

                if (Kind == 3)
                {
                    for (int i = 0; i < btnRel.Length; i++)
                    {
                        var r = btnRel[i]; r.Offset(box.Location);
                        Color fill = i == 0
                            ? (hover == 0 ? Color.FromArgb(255, 232, 138, 106) : Color.FromArgb(255, 217, 119, 87))
                            : (dark ? (hover == 1 ? Color.FromArgb(255, 78, 72, 68) : Color.FromArgb(255, 62, 57, 54)) : (hover == 1 ? Color.FromArgb(255, 228, 219, 211) : Color.FromArgb(255, 240, 233, 227)));
                        using (var fb = new SolidBrush(fill))
                        using (var p = U.RR(r, 8 * s)) g.FillPath(fb, p);
                        using (var tb = new SolidBrush(i == 0 ? Color.White : fg))
                            g.DrawString(btnLabels[i], fBtn, tb, r, center);
                    }
                    float frac = U.Cl(life / maxLife, 0, 1);
                    using (var pb = new SolidBrush(U.A(acc, 0.55f)))
                        g.FillRectangle(pb, box.X + rad, box.Bottom - 5 * s, (box.Width - rad * 2) * frac, 2.5f * s);
                }
            }
        }
    }
}
