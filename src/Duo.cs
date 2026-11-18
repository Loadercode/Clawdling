// Codex: the second (slightly goofy) pet, and the games Claude and Codex play together.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Clawdling
{
    enum Game { None, Tag, Fight, Snow, Leap, High5, Dance, Prank }

    partial class Pet
    {
        // ---------- buddy lifecycle ----------
        /// <summary>Codex buddy. Shares config, menu, snow strip and timer with the main pet.</summary>
        Pet(Pet lead) : base(10, 10, false)
        {
            leader = lead; codex = true;
            cfg = lead.cfg; dpi = lead.dpi; myPid = lead.myPid;
            timer = null; pipe = null;
            ApplySkin();
            ApplySize();
            wa = lead.wa; sb = lead.sb;
            float side = lead.px > wa.Left + wa.Width / 2f ? -1 : 1;
            px = U.Cl(lead.px + side * U.Rf(140, 300) * k, wa.Left + 10 * u, wa.Right - 10 * u);
            py = wa.Top + 20 * u;
            StartFall(0, 0);
            bubble = new Bubble(dpi);
            clock = lead.clock; blinkIn = 1.3f;
        }

        void ApplySkin()
        {
            if (!codex) return;
            // OpenAI / GPT green
            bBody.Color = Color.FromArgb(255, 16, 163, 127);
            bDark.Color = Color.FromArgb(255, 10, 118, 92);
            bLight.Color = Color.FromArgb(255, 74, 199, 162);
        }

        internal void SetBuddy(bool on)
        {
            if (leader != null) return;
            if (on && buddy == null)
            {
                buddy = new Pet(this);
                if (!cfg.Hidden) { buddy.Show(); ReassertZ(); }
                gCd = U.Rf(8, 14);
            }
            else if (!on && buddy != null)
            {
                EndGame();
                var b = buddy; buddy = null;
                b.Shutdown();
            }
        }

        void Shutdown()
        {
            bubble.Kill();
            foreach (var b in balls) b.Close(); balls.Clear();
            foreach (var s in splats) s.Close(); splats.Clear();
            Close();
        }

        void SummonBuddy()
        {
            if (buddy == null) { cfg.Codex = true; cfg.Save(); SetBuddy(true); return; }
            if (cfg.Hidden || buddy.fsHidden) return;
            var b = buddy;
            b.wa = wa; b.sb = sb; b.rot = 0;
            b.px = U.Cl(px + (px > wa.Left + wa.Width / 2f ? -1 : 1) * U.Rf(90, 180) * k, wa.Left + 10 * u, wa.Right - 10 * u);
            b.py = wa.Top + 16 * u; b.StartFall(0, 200 * k);
            b.Say(U.Pick("Я тут!", "Звали?", "Бегу-бегу!"), 1.6f);
        }

        void PlayNow(Game g)
        {
            if (buddy == null || cfg.Hidden) return;
            EndGame();
            if (buddy.wa != wa) SummonBuddy();
            foreach (var p in new[] { this, buddy })
                if (p.OnGround && (p.st == St.Sleep || p.st == St.Sit || p.st == St.Think)) { p.GoIdle(); p.dur = 0.5f; }
                else if (p.OnGround && (p.st == St.Hidden || p.st == St.Peek || p.st == St.Dive)) { p.sinkStart = p.sink; p.Go(St.PopOut, 0.18f); }
            nextGame = g; nextA = U.Ch(0.5) ? this : buddy; gCd = 0.3f;
        }

        string laterText; float laterT;
        void SayLater(float delay, string text) { laterText = text; laterT = delay; }
        void TickLater(float dt) { if (laterText == null) return; laterT -= dt; if (laterT <= 0) { Say(laterText, 1.6f); laterText = null; } }

        // ---------- small movement helpers ----------
        float Lo { get { return wa.Left + 8 * u; } }
        float Hi { get { return wa.Right - 8 * u; } }

        bool Ready { get { return OnGround && surf == 0 && (st == St.Idle || st == St.Walk); } }

        internal bool Free
        {
            get
            {
                if (!Visible || fsHidden || cfg.Hidden || dragging || surf != 0 || HiddenState || rot != 0 || inGame) return false;
                if (bubble.IsNotice) return false;
                switch (st)
                {
                    case St.Idle: case St.Walk: case St.Sit: case St.Look: case St.Think: case St.Wave: case St.Yawn:
                    case St.Dance: case St.Love: case St.Happy: case St.Laugh: case St.Shiver: case St.Land: case St.Sad:
                        return true;
                }
                return false;
            }
        }

        bool OkInGame
        {
            get
            {
                if (!Visible || fsHidden || cfg.Hidden || dragging || HiddenState || rot != 0) return false;
                if (st == St.Notify || st == St.Ask || bubble.IsNotice) return false;
                return surf == 0 || st == St.Jump || st == St.Fall || st == St.Prep;
            }
        }

        void RunTo(float x, float spd)
        {
            x = U.Cl(x, Lo, Hi);
            if (Math.Abs(x - px) < 2.5f) { if (st == St.Walk) { GoIdle(); dur = 0.4f; } return; }
            if (st != St.Walk) Go(St.Walk, 999);
            climbNext = false; walkTo = x; walkSpd = spd; running = spd > 90 * k;
        }

        void Face(Pet o) { if (Math.Abs(o.px - px) > 2) dir = Math.Sign(o.px - px); }

        void HopTo(float x)
        {
            x = U.Cl(x, Lo, Hi);
            float vy0 = -500 * k, T = 2 * 500 * k / Gr;
            leapVy = vy0; leapVx = (x - px) / T; leaping = true;
            if (Math.Abs(x - px) > 2) dir = Math.Sign(x - px);
            Go(St.Prep, 0.16f);
        }

        bool MateLook(float dc, out float tlx, out float tly)
        {
            tlx = 0; tly = 0;
            Pet m = leader ?? buddy;
            if (m == null || !m.Visible || m.fsHidden || rot != 0) return false;
            float dx = m.px - px;
            if (!inGame && (dc < 260 * k || Math.Abs(dx) > 320 * k || Math.Abs(m.py - py) > 80 * k)) return false;
            tlx = Math.Sign(dx) * 0.85f; tly = U.Cl((m.py - py) / (120 * k), -1, 1) * 0.4f + 0.05f;
            return true;
        }

        // =====================================================================
        //  Games (run by the main pet)
        // =====================================================================
        internal Game game;
        Game nextGame; Pet nextA;
        Pet gA, gB; int gPh, gN, gGoal; float gT, gPhT, gCd, gX, gMid, gS;
        float gWait; int gHits;

        static readonly string[] gameNames = { "", "Догонялки", "Драка", "Снежная битва", "Чехарда", "Дай пять", "Танец", "" };

        void UpdateDuo(float dt)
        {
            var a = this; var b = buddy;
            if (game == Game.None)
            {
                gCd -= dt;
                if (gCd > 0) return;
                if (a.wa != b.wa || !a.Visible || !b.Visible) { gCd = 4; return; }
                if (nextGame == Game.None && b.Free && a.st == St.Sleep && a.surf == 0 && a.t > 4 && !a.fsHidden && U.Ch(0.6))
                { StartGame(Game.Prank, a, b); return; }
                if (!a.Free || !b.Free) { gCd = 1.2f; return; }
                Game g = nextGame; Pet first = nextA;
                nextGame = Game.None; nextA = null;
                if (g == Game.None) g = PickGame();
                if (first == null) first = U.Ch(0.5) ? a : b;
                StartGame(g, first, first == a ? b : a);
                return;
            }
            gT += dt; gPhT += dt;
            if (!gA.OkInGame || !gB.OkInGame || gA.wa != gB.wa || gT > 45) { EndGame(); return; }
            switch (game)
            {
                case Game.Tag: TickTag(dt); break;
                case Game.Fight: TickFight(dt); break;
                case Game.Snow: TickSnow(dt); break;
                case Game.Leap: TickLeap(dt); break;
                case Game.High5: case Game.Dance: TickMeet(dt); break;
                case Game.Prank: TickPrank(dt); break;
            }
        }

        bool SnowAllowed { get { return cfg.NewYear && cfg.Snow && strip != null && strip.Visible; } }

        Game PickGame()
        {
            float[] w = { 0, 30, 18, SnowAllowed ? 34 : 0, 14, 12, 10 };
            float sum = 0; foreach (var x in w) sum += x;
            float r = U.Rf(0, sum);
            for (int i = 1; i < w.Length; i++) { r -= w[i]; if (r <= 0) return (Game)i; }
            return Game.Tag;
        }

        internal void StartGame(Game g, Pet first, Pet second)
        {
            if (buddy == null) return;
            if (game != Game.None) EndGame();
            if (g == Game.Snow && !SnowAllowed) g = Game.Tag;
            game = g; gA = first; gB = second; gT = 0; gPh = 0; gPhT = 0; gN = 0; gWait = 0;
            gA.inGame = gB.inGame = true;
            gA.gThrows = gB.gThrows = 0; gA.gHits = gB.gHits = 0;
            gS = Math.Sign(gB.px - gA.px); if (gS == 0) gS = U.Ch(0.5) ? 1 : -1;
            switch (g)
            {
                case Game.Tag:
                    gGoal = U.R.Next(3, 6);
                    gA.Say(gA.codex ? U.Pick("Ты водишь! Ой, то есть я!", "Догоню-у-у!", "Салки!") : U.Pick("Догоню!", "Сейчас поймаю!", "Салки!"), 1.6f);
                    break;
                case Game.Fight:
                    gA.Say(gA.codex ? U.Pick("Э, ты чего?!", "Ща как дам!", "Я тебя не боюсь!") : U.Pick("Ну всё, держись!", "Иди сюда!", "Кодекс, ты баг!"), 1.6f);
                    break;
                case Game.Snow:
                    gGoal = U.R.Next(6, 11);
                    gA.Say(U.Pick("Снежная битва!", "Война снежками!", "Лови снежок!"), 1.6f);
                    break;
                case Game.Leap:
                    gGoal = U.R.Next(4, 7);
                    gA.Say(U.Pick("Чехарда!", "Давай прыгать!", "Пригнись!"), 1.5f);
                    break;
                case Game.High5:
                    gA.Say(U.Pick("Дай пять!", "Пять!", "Йо!"), 1.4f);
                    break;
                case Game.Dance:
                    gA.Say(U.Pick("Потанцуем?", "Музыка!", "Танцуем!"), 1.4f);
                    break;
            }
        }

        internal void EndGame()
        {
            if (game == Game.None) return;
            foreach (var p in new[] { gA, gB })
            {
                if (p == null) continue;
                p.inGame = false;
                if (p.OnGround && (p.st == St.Walk || p.st == St.Brawl || p.st == St.Duck || p.st == St.Idle)) { p.GoIdle(); }
            }
            game = Game.None;
            gCd = nextGame != Game.None ? 3.5f : U.Rf(16, 40);
        }

        // ---------- tag ----------
        void TickTag(float dt)
        {
            Pet C = gA, R = gB;
            float away = Math.Sign(R.px - C.px); if (away == 0) away = U.Ch(0.5) ? 1 : -1;
            float gap = Math.Abs(R.px - C.px);
            if (R.Ready)
            {
                bool cornered = (away > 0 && R.Hi - R.px < 60 * k) || (away < 0 && R.px - R.Lo < 60 * k);
                if (cornered && gap < 110 * k && gap > 12 * u && C.OnGround) { R.HopTo(C.px - away * 24 * u); if (U.Ch(0.4)) R.Say(U.Pick("Хоп!", "Оп!", "Не поймаешь!"), 1.1f); }
                else if (cornered) { R.Face(C); if (R.st == St.Walk) { R.GoIdle(); R.dur = 0.3f; } }
                else R.RunTo(R.px + away * 220 * k, (R.codex ? 104 : 112) * k);
            }
            if (C.Ready) C.RunTo(R.px, (C.codex ? 124 : 132) * k);
            if (gPhT > 0.9f && C.OnGround && R.OnGround && gap < 9 * u)
            {
                gN++; gPhT = 0;
                C.Say(C.codex ? U.Pick("Осалил! Кажется...", "Попался!", "Ты водишь!") : U.Pick("Осалил!", "Попался!", "Ты водишь!"), 1.3f);
                if (gN >= gGoal)
                {
                    C.Go(St.Laugh, 1.4f); R.Go(U.Ch(0.5) ? St.Laugh : St.Happy, 1.2f);
                    if (U.Ch(0.5)) R.SayLater(1.5f, U.Pick("Ха-ха, весело!", "Ещё разок потом!", "Уф, устал!"));
                    EndGame();
                    return;
                }
                gA = R; gB = C;
                R.Face(C); R.Go(St.Look, 0.7f);
            }
        }

        // ---------- fight ----------
        void TickFight(float dt)
        {
            Pet A = gA, B = gB;
            switch (gPh)
            {
                case 0: // approach
                    {
                        gMid = U.Cl((A.px + B.px) / 2, A.Lo + 12 * u, A.Hi - 12 * u);
                        if (A.Ready) A.RunTo(gMid - gS * 6 * u, 105 * k);
                        if (B.Ready) B.RunTo(gMid + gS * 6 * u, 95 * k);
                        if (Math.Abs(A.px - B.px) < 14 * u && A.Ready && B.Ready)
                        {
                            A.Face(B); B.Face(A); A.Go(St.Angry, 0.8f); B.Go(St.Angry, 0.8f);
                            if (U.Ch(0.6)) B.Say(B.codex ? U.Pick("Ой-ой...", "Мама!", "Сам такой!") : U.Pick("Ну давай!", "Сам напросился!"), 1.2f);
                            gPh = 1; gPhT = 0;
                        }
                        else if (gPhT > 9) EndGame();
                        break;
                    }
                case 1: // face-off
                    if (gPhT > 0.8f) { gMid = (A.px + B.px) / 2; A.Go(St.Brawl, 99); B.Go(St.Brawl, 99); gPh = 2; gPhT = 0; }
                    break;
                case 2: // dust cloud
                    {
                        if (A.st != St.Brawl || B.st != St.Brawl) { EndGame(); break; }
                        float c = U.Cos(gPhT * 6.5f) * 4.5f * u;
                        A.px = U.Cl(gMid - gS * c + U.Rf(-0.6f, 0.6f) * u, A.Lo, A.Hi);
                        B.px = U.Cl(gMid + gS * c + U.Rf(-0.6f, 0.6f) * u, B.Lo, B.Hi);
                        A.Face(B); B.Face(A);
                        if (gPhT > 3f)
                        {
                            Pet loser = U.Ch(0.6) ? (A.codex ? A : B) : (A.codex ? B : A), winner = loser == A ? B : A;
                            loser.hitBySnow = false; loser.hitFrom = null; loser.Go(St.Hit, 1.5f);
                            loser.px = U.Cl(loser.px + Math.Sign(loser.px - winner.px) * 4 * u, loser.Lo, loser.Hi);
                            winner.Go(St.Laugh, 1.4f);
                            winner.Say(winner.codex ? U.Pick("Я победил?! Я победил!", "Ха! Кодекс сильнее!", "Ура! Это баг или фича?") : U.Pick("Победа!", "Вот так!", "Не связывайся с Клодом!"), 1.8f);
                            loser.SayLater(2f, loser.codex ? U.Pick("Ой... звёздочки...", "Я поддался!", "Перезагрузка...") : U.Pick("Ладно, ничья...", "Реванш!", "Ух..."));
                            if (U.Ch(0.45)) { nextGame = Game.Tag; nextA = loser; }
                            EndGame();
                        }
                        break;
                    }
            }
        }

        // ---------- snowball fight ----------
        void TickSnow(float dt)
        {
            Pet A = gA, B = gB;
            if (gPh == 0)
            {
                if (gPhT < 0.05f)
                {
                    float D = U.Rf(230, 400) * k;
                    gX = A.px + gS * D;
                    if (gX < B.Lo || gX > B.Hi) gX = A.px - gS * D;
                    if (gX < B.Lo || gX > B.Hi) { EndGame(); return; }
                }
                if (B.Ready) B.RunTo(gX, 115 * k);
                if (A.Ready && A.st == St.Walk) { A.GoIdle(); }
                if ((B.Ready && Math.Abs(B.px - gX) < 4) || gPhT > 8)
                {
                    if (A.Ready && B.Ready) { A.Face(B); B.Face(A); if (B.st == St.Walk) B.GoIdle(); gPh = 1; gPhT = 0; A.gWait = 0.2f; B.gWait = U.Rf(0.5f, 1.2f); }
                }
                return;
            }
            int thrown = A.gThrows + B.gThrows;
            foreach (var p in new[] { A, B })
            {
                Pet o = p == A ? B : A;
                if (thrown >= gGoal) break;
                if (p.st == St.Idle && p.OnGround && p.surf == 0 && p.t > p.gWait)
                {
                    p.StartSnowballAt(o); p.gWait = U.Rf(0.3f, 1.3f);
                    if (U.Ch(0.15) && !p.bubble.Active) p.Say(p.codex ? U.Pick("Лови!", "Эй, сюда!", "Пиу!") : U.Pick("Лови!", "Держи!", "На!"), 1.1f);
                    thrown++;
                }
            }
            bool flying = A.balls.Count > 0 || B.balls.Count > 0;
            if ((thrown >= gGoal && !flying && A.st == St.Idle && B.st == St.Idle) || gT > 38)
            {
                Pet win = A.gHits == B.gHits ? null : (A.gHits < B.gHits ? A : B);
                if (win == null) { A.Go(St.Laugh, 1.3f); B.Go(St.Laugh, 1.3f); A.Say(U.Pick("Ничья!", "Ха-ха, ничья!"), 1.6f); }
                else
                {
                    Pet lose = win == A ? B : A;
                    win.Go(St.Celebrate, 1.6f);
                    win.Say(win.codex ? U.Pick("Я выиграл! Наверное...", "Кодекс — чемпион!") : U.Pick("Победа!", "Я выиграл!"), 1.8f);
                    lose.Go(lose.codex ? St.Sad : St.Laugh, 1.4f);
                }
                EndGame();
            }
        }

        // ---------- leapfrog ----------
        void TickLeap(float dt)
        {
            Pet J = gA, K = gB; // J jumps over K, travelling in direction gS
            if (gPh == 0)
            {
                gS = Math.Sign(K.px - J.px); if (gS == 0) gS = 1;
                if (K.Hi - K.px < 60 * k && gS > 0 || K.px - K.Lo < 60 * k && gS < 0) { gS = -gS; var tmp = J; gA = J = K; gB = K = tmp; }
                gPh = 1;
            }
            if (gPh == 1)
            {
                if (K.Ready) { K.Face(J); K.Go(St.Duck, 99); }
                float stand = K.px - gS * 13 * u;
                if (J.Ready) J.RunTo(stand, 95 * k);
                if (J.Ready && J.st == St.Idle && Math.Abs(J.px - stand) < 3 && K.st == St.Duck)
                {
                    float land = K.px + gS * 13 * u;
                    if (land < J.Lo || land > J.Hi) { Finish(); return; }
                    J.HopTo(land); gPh = 2; gPhT = 0;
                    if (U.Ch(0.35)) J.Say(U.Pick("Хоп!", "Оп-ля!", "Уиии!"), 1f);
                }
                else if (gPhT > 10) { Finish(); return; }
            }
            else if (gPh == 2)
            {
                if (J.OnGround && J.surf == 0 && (J.st == St.Idle || J.st == St.Land) && gPhT > 0.3f)
                {
                    gN++;
                    K.GoIdle(); K.dur = 0.3f;
                    if (gN >= gGoal) { Finish(); return; }
                    gA = K; gB = J; gPh = 1; gPhT = 0;
                }
            }
        }

        void Finish()
        {
            gA.Go(St.Laugh, 1.2f); gB.Go(St.Happy, 1.2f);
            if (U.Ch(0.6)) gB.Say(U.Pick("Ура!", "Весело!", "Ещё!"), 1.4f);
            EndGame();
        }

        // ---------- high five / dance ----------
        void TickMeet(float dt)
        {
            Pet A = gA, B = gB;
            float gapWant = game == Game.High5 ? 12 * u : 16 * u;
            float spot = B.px - gS * gapWant;
            if (spot < A.Lo || spot > A.Hi) spot = B.px + gS * gapWant;
            if (B.Ready && B.st == St.Walk) { B.GoIdle(); }
            if (B.Ready) B.Face(A);
            if (A.Ready) A.RunTo(spot, 100 * k);
            if (A.Ready && A.st == St.Idle && Math.Abs(A.px - spot) < 3 && B.Ready)
            {
                A.Face(B); B.Face(A);
                if (game == Game.High5)
                {
                    A.Go(St.HighFive, 1f); B.Go(St.HighFive, 1f);
                    B.Say(B.codex ? U.Pick("Йе-е!", "Пять!", "Ой, промахнулся! Шучу!") : U.Pick("Йе!", "Есть пять!"), 1.3f);
                }
                else
                {
                    A.Go(St.Dance, 4.2f); B.Go(St.Dance, 4.2f);
                    B.Say("♪ ♫ ♪", 1.6f);
                }
                EndGame();
            }
            else if (gPhT > 9) EndGame();
        }

        // ---------- Codex sneaks up on sleeping Claude ----------
        void TickPrank(float dt)
        {
            Pet A = gA, B = gB; // A sleeps (Claude), B pranks (Codex)
            switch (gPh)
            {
                case 0:
                    {
                        if (A.st != St.Sleep) { EndGame(); return; }
                        float side = Math.Sign(B.px - A.px); if (side == 0) side = 1;
                        float spot = A.px + side * 11 * u;
                        if (spot < B.Lo || spot > B.Hi) spot = A.px - side * 11 * u;
                        if (B.Ready) B.RunTo(spot, 34 * k);
                        if (B.Ready && B.st == St.Idle && Math.Abs(B.px - spot) < 3)
                        {
                            B.Face(A); B.Go(St.Happy, 1.1f); B.Say("БУ!", 1.2f);
                            A.vx = 0; A.vy = -470 * k; A.st = St.Jump; A.t = 0; A.surf = -1; A.sink = 0;
                            gPh = 1; gPhT = 0;
                        }
                        else if (gPhT > 25) EndGame();
                        break;
                    }
                case 1:
                    if (A.Ready && gPhT > 0.4f)
                    {
                        A.Face(B); A.Go(St.Angry, 1.1f); A.Say(U.Pick("Кодекс!!", "Ну всё, тебе конец!", "Я же спал!"), 1.4f);
                        gPh = 2; gPhT = 0;
                    }
                    else if (gPhT > 4) EndGame();
                    break;
                case 2:
                    if (gPhT > 1.1f)
                    {
                        B.Say(U.Pick("Хе-хе, не догонишь!", "Ой-ой, бегу!"), 1.4f);
                        game = Game.Tag; gA = A; gB = B; gPh = 0; gPhT = 0; gN = 0; gGoal = U.R.Next(2, 4);
                    }
                    break;
            }
        }
    }
}
