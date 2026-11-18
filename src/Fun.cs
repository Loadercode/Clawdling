// Extra fun: bouncy throws, bumping into the mate, cookies and more duo games
// (tug of war, staring contest, rock-paper-scissors).
using System;
using System.Drawing;

namespace Clawdling
{
    partial class Pet
    {
        internal bool flung; int bounces; float squashT, bumpCd; bool pendingBump;
        float ropeLen; int rpsSign = -1;
        static readonly SolidBrush bRope = new SolidBrush(Color.FromArgb(255, 168, 120, 72));
        static readonly SolidBrush bCookie = new SolidBrush(Color.FromArgb(255, 214, 160, 92));
        static readonly SolidBrush bChip = new SolidBrush(Color.FromArgb(255, 92, 58, 36));
        static readonly SolidBrush bStone = new SolidBrush(Color.FromArgb(255, 150, 152, 162));
        static readonly SolidBrush bRps = new SolidBrush(Color.FromArgb(255, 226, 60, 70));
        static readonly SolidBrush bPaper = new SolidBrush(Color.FromArgb(255, 120, 170, 230));

        // =====================================================================
        //  Bouncy throws
        // =====================================================================
        /// <summary>A thrown pet hit a surface at height ly: bounce back up instead of landing.</summary>
        bool TryBounce(float ly)
        {
            if (!flung || vy < 380 * k || bounces >= 14) return false;
            float imp = vy;
            py = ly - 1; vy = -imp * 0.8f; vx *= 0.9f; bounces++; squashT = 0.13f;
            for (int i = 0; i < 6; i++) Sp(6, U.Rf(-6, 6), -0.5f, U.Rf(-6, 6), U.Rf(-3, -1), 0.6f, U.Rf(1.5f, 2.8f), Color.FromArgb(255, 210, 205, 200));
            var L = leader ?? this;
            if (cfg.NewYear && L.strip != null && L.strip.Visible && ly >= wa.Bottom - 2) SnowPuff((int)Math.Min(10, imp / (120 * k)));
            if (bounces == 1 && imp > 900 * k && U.Ch(0.6))
                Say(codex ? U.Pick("Бо-о-инь!", "Я мячик!", "Ещё! Ещё!") : U.Pick("Бо-инг!", "Пружиню!", "Хоп-хоп-хоп!"), 1.2f);
            return true;
        }

        /// <summary>A flying thrown pet knocks the other pet away (and it bounces too).</summary>
        void BumpMate(float dt, float nx, float ny)
        {
            if (bumpCd > 0) { bumpCd -= dt; return; }
            if (!flung) return;
            Pet m = leader ?? buddy;
            if (m == null || !m.Visible || m.fsHidden || m.dragging || m.HiddenState || m.bumpCd > 0) return;
            if (Math.Abs(vx) + Math.Abs(vy) < 450 * k) return;
            if (Math.Abs(m.px - nx) > 11 * u || Math.Abs(m.py - ny) > 9 * u) return;
            bumpCd = 0.7f; m.bumpCd = 0.7f;
            float side = Math.Sign(m.px - nx); if (side == 0) side = 1;
            Pet L = leader ?? this; if (L.game != Game.None) L.EndGame();
            m.flung = true; m.bounces = 0; m.pendingBump = true;
            m.StartFall(side * Math.Max(380 * k, Math.Abs(vx) * 0.7f), -Math.Max(420 * k, Math.Abs(vy) * 0.5f));
            vx = -side * Math.Abs(vx) * 0.45f; vy = -Math.Abs(vy) * 0.35f - 150 * k;
            for (int i = 0; i < 5; i++) Sp(7, side * 6, -7, side * U.Rf(2, 7), U.Rf(-7, -2), 0.6f, 0.5f, Color.FromArgb(255, 255, 210, 70));
            m.Say(m.codex ? U.Pick("Ай! Страйк!", "Эй, я не кегля!", "Уи-и-и!") : U.Pick("Ой! За что?!", "Эй, осторожнее!", "Кодекс, ты летишь в меня!"), 1.5f);
        }

        /// <summary>Landing after being knocked by the mate: get angry, maybe chase it.</summary>
        bool AfterBump()
        {
            if (!pendingBump) return false;
            pendingBump = false;
            Go(St.Angry, 0.9f);
            Pet L = leader ?? this;
            if (L.buddy != null && L.game == Game.None && U.Ch(0.5)) { L.nextGame = Game.Tag; L.nextA = this; L.gCd = Math.Min(L.gCd, 1.5f); }
            return true;
        }

        // =====================================================================
        //  Cookies
        // =====================================================================
        void GiveCookies()
        {
            if (game != Game.None) EndGame();
            foreach (var p in new[] { this, buddy, kid })
            {
                if (p == null || !p.Visible || !p.OnGround || p.HiddenState) continue;
                bool gulp = p.codex && U.Ch(0.35);
                p.Go(St.Eat, gulp ? 1.0f : 3.2f);
                if (gulp) p.SayLater(1.4f, U.Pick("Ам! А где печенька?", "Я её проглотил целиком", "Клод, ты будешь доедать?"));
            }
        }

        // =====================================================================
        //  States: tug, stare, rock-paper-scissors, eat
        // =====================================================================
        void UpdateFun(float dt)
        {
            switch (st)
            {
                case St.Tug:
                    legPhase += dt * 4;
                    if (partT <= 0) { partT = 0.25f; Sp(6, -dir * 5, -0.5f, -dir * U.Rf(1, 3), -0.8f, 0.6f, U.Rf(1.2f, 2f), Color.FromArgb(255, 205, 200, 195)); }
                    break;
                case St.Stare: blinkT = 0; blinkIn = 2; break;
                case St.Eat:
                    if (partT <= 0 && t > 0.3f) { partT = 0.35f; Sp(3, dir * 7, -6, dir * U.Rf(-2, 2), U.Rf(-3, 0), 0.8f, 0.6f, Color.FromArgb(255, 200, 150, 90)); }
                    if (t >= dur)
                    {
                        Go(St.Happy, 1f);
                        if (laterText == null) Say(codex ? U.Pick("Ням! А ещё есть?", "Вкусно! Это была последняя?") : U.Pick("Вкусно!", "Спасибо!", "М-м-м!"), 1.5f);
                    }
                    break;
            }
            if (st != St.Eat && t >= dur) GoIdle();
        }

        void PoseFun(float T, int ds, ref float bob, ref float sx, ref float sy, ref float lean, ref int legMode, ref int aL, ref int aR, ref Emo emo, ref int mouth)
        {
            switch (st)
            {
                case St.Tug:
                    lean = -ds * (1.1f + U.Sin(T * 9) * 0.2f); legMode = 1;
                    if (ds > 0) aR = 6; else aL = 6;
                    emo = (int)(T * 2) % 3 == 0 ? Emo.Angry : Emo.Focus; mouth = 3; break;
                case St.Stare:
                    emo = Emo.Surprised; mouth = 3; lean = ds * 0.4f;
                    if (T > 2) lean += U.Sin(T * 40) * 0.08f;
                    break;
                case St.Rps:
                    if (rpsSign < 0)
                    {
                        bob = -Math.Abs(U.Sin(T * 6)) * 0.8f;
                        int a = U.Sin(T * 6) > 0 ? 1 : 5;
                        if (ds > 0) aR = a; else aL = a;
                        emo = Emo.Focus;
                    }
                    else { if (ds > 0) aR = 6; else aL = 6; emo = Emo.Excited; }
                    break;
                case St.Eat:
                    if (ds > 0) aR = 1; else aL = 1;
                    bool chew = (int)(T * 2.5f) % 2 == 0;
                    emo = chew ? Emo.Happy : Emo.Closed; mouth = chew ? 3 : 0; break;
            }
        }

        void DrawFunExtras(float bx, float bW, float armY, float headTop, int ds)
        {
            if (st == St.Tug && ropeLen > 0)
            {
                float x0 = bx + ds * (bW / 2 + 2.6f), x1 = ds * ropeLen, y = armY + 0.3f;
                float a = Math.Min(x0, x1), b = Math.Max(x0, x1);
                if (b > a) Bx(bRope, a, y, b - a, 0.7f);
                if (!codex) Bx(bRps, x1 - 0.4f, y - 0.6f, 0.8f, 1.9f);
            }
            else if (st == St.Rps && rpsSign >= 0)
            {
                float cx = bx, cy = headTop - 5.5f;
                Bx(bWhite, cx - 2.8f, cy - 2.8f, 5.6f, 5.6f); Bx(bWhite, cx - 3.2f, cy - 2.4f, 6.4f, 4.8f);
                switch (rpsSign)
                {
                    case 0: // rock
                        Bx(bStone, cx - 1.8f, cy - 1.2f, 3.6f, 2.4f); Bx(bStone, cx - 1.2f, cy - 1.8f, 2.4f, 3.6f); break;
                    case 1: // scissors
                        for (int i = -2; i <= 1; i++)
                        {
                            Bx(bEye, cx + i * 0.6f - 0.3f, cy + i * 0.6f - 0.3f, 0.6f, 0.6f);
                            Bx(bEye, cx - i * 0.6f - 0.3f, cy + i * 0.6f - 0.3f, 0.6f, 0.6f);
                        }
                        Bx(bRps, cx - 2.0f, cy + 0.9f, 1.1f, 1.1f); Bx(bRps, cx + 0.9f, cy + 0.9f, 1.1f, 1.1f); break;
                    default: // paper
                        Bx(bPaper, cx - 1.5f, cy - 2f, 3f, 4f);
                        for (int i = 0; i < 3; i++) Bx(bWhite, cx - 1.0f, cy - 1.2f + i, 2f, 0.4f);
                        break;
                }
            }
            else if (st == St.Eat)
            {
                float left = 1 - Math.Min(0.75f, (float)Math.Floor(t / Math.Max(0.1f, dur) * 4) * 0.25f);
                float cx = bx + ds * (bW / 2 + 1.0f), cy = armY - 3.4f, s = 2.6f * left;
                Bx(bCookie, cx - s / 2, cy - s / 2, s, s);
                if (left > 0.4f) { Bx(bChip, cx - 0.6f, cy - 0.5f, 0.5f, 0.5f); Bx(bChip, cx + 0.3f, cy + 0.2f, 0.5f, 0.5f); }
            }
        }

        // =====================================================================
        //  Games
        // =====================================================================
        void TickTug(float dt)
        {
            Pet A = gA, B = gB;
            if (gPh == 0)
            {
                if (!GetTogether(24 * u)) return;
                A.Go(St.Tug, 99); B.Go(St.Tug, 99);
                gMid = (A.px + B.px) / 2; gS = Math.Sign(B.px - A.px); if (gS == 0) gS = 1;
                A.ropeLen = B.ropeLen = 12;
                A.Say(U.Pick("Тяни!", "Раз-два, взяли!", "Перетягивание!"), 1.4f);
                Pet cx = A.codex ? A : B; gWin = U.Ch(0.4) ? cx : (cx == A ? B : A);
                gX = U.Rf(4, 7); gPh = 1; gPhT = 0;
                return;
            }
            if (A.st != St.Tug || B.st != St.Tug) { EndGame(); return; }
            float drift = U.Sin(gPhT * 2.1f) * 2.5f * u + U.Sin(gPhT * 5.3f) * 0.8f * u;
            float pull = Math.Max(0, gPhT - gX * 0.6f) / (gX * 0.4f) * 5 * u * (gWin == A ? -gS : gS);
            float c = U.Cl(gMid + drift + pull, A.Lo + 13 * u, A.Hi - 13 * u);
            A.px = c - gS * 12 * u; B.px = c + gS * 12 * u;
            A.Face(B); B.Face(A);
            if (gPhT > gX)
            {
                Pet lose = gWin == A ? B : A;
                lose.Go(St.Trip, 1.9f);
                gWin.Go(St.Celebrate, 1.6f);
                gWin.Say(gWin.codex ? U.Pick("Я сильнее! Кто бы мог подумать", "Кодекс — силач!") : U.Pick("Перетянул!", "Победа!"), 1.6f);
                EndGame();
            }
        }

        void TickStare(float dt)
        {
            Pet A = gA, B = gB;
            if (gPh == 0)
            {
                if (!GetTogether(14 * u)) return;
                A.Go(St.Stare, 99); B.Go(St.Stare, 99);
                A.Say(U.Pick("Гляделки! Кто моргнёт — проиграл", "Смотри мне в глаза!"), 1.8f);
                gX = U.Rf(3.5f, 7f); gPh = 1; gPhT = 0;
                return;
            }
            if (A.st != St.Stare || B.st != St.Stare) { EndGame(); return; }
            if (gPhT > gX)
            {
                Pet cx = A.codex ? A : B, cl = cx == A ? B : A;
                Pet lose = U.Ch(0.65) ? cx : cl, win = lose == A ? B : A;
                lose.Go(lose.codex ? St.Laugh : St.Sad, 1.3f); lose.blinkT = 0.3f;
                lose.Say(lose.codex ? U.Pick("Ай! Глаз зачесался!", "Я моргнул от восторга!", "Это была муха!") : U.Pick("Эх, моргнул...", "Не выдержал!"), 1.6f);
                win.Go(St.Celebrate, 1.4f); win.SayLater(1.6f, U.Pick("Я победил!", "Железные глаза!"));
                EndGame();
            }
        }

        void TickRps(float dt)
        {
            Pet A = gA, B = gB;
            switch (gPh)
            {
                case 0:
                    if (!GetTogether(18 * u)) return;
                    gPh = 1; gPhT = 0; gN = 0; break;
                case 1:
                    if (gN > 0 && gPhT < 1f) break;
                    A.rpsSign = B.rpsSign = -1; A.Go(St.Rps, 99); B.Go(St.Rps, 99);
                    A.Say("Камень, ножницы, бумага!", 1.6f); gPh = 2; gPhT = 0; break;
                case 2:
                    if (A.st != St.Rps || B.st != St.Rps) { EndGame(); return; }
                    if (gPhT > 1.7f) { A.rpsSign = U.R.Next(3); B.rpsSign = U.R.Next(3); gPh = 3; gPhT = 0; }
                    break;
                case 3:
                    if (gPhT < 1.6f) break;
                    {
                        int a = A.rpsSign, b = B.rpsSign;
                        if (a == b)
                        {
                            gN++;
                            if (gN >= 3) { A.Go(St.Laugh, 1.2f); B.Go(St.Laugh, 1.2f); A.Say("Опять ничья! Ладно...", 1.6f); EndGame(); return; }
                            B.Say(U.Pick("Ничья! Ещё раз!", "Переигрываем!"), 1.2f);
                            gPh = 1; gPhT = 0; return;
                        }
                        Pet win = (a + 1) % 3 == b ? A : B, lose = win == A ? B : A;
                        string[] nm = { "Камень", "Ножницы", "Бумага" };
                        string sign = nm[win.rpsSign];
                        win.Go(St.Celebrate, 1.5f); lose.Go(lose.codex ? St.Sad : St.Laugh, 1.3f);
                        win.Say(win.codex ? U.Pick("Я выиграл! Я гений!", sign + " рулит!") : U.Pick("Победа!", sign + " побеждает!"), 1.6f);
                        if (lose.codex) lose.SayLater(1.8f, U.Pick("Я хотел показать другое!", "Давай до трёх побед!"));
                        EndGame();
                    }
                    break;
            }
        }
    }
}
