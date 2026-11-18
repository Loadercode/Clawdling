// Codex: the second (slightly goofy) pet, and the games Claude and Codex play together.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Clawdling
{
    enum Game { None, Tag, Fight, Snow, Leap, High5, Dance, Prank, Race, Hug, Seek, Mimic, Nap, HighJump, Tug, Stare, Rps }

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
                    case St.Juggle: case St.Stretch: case St.Zen:
                        return true;
                }
                return false;
            }
        }

        bool OkInGame
        {
            get
            {
                if (!Visible || fsHidden || cfg.Hidden || dragging || (HiddenState && !hideOk) || rot != 0) return false;
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
        float gWait, gBest; int gHits, gMove; Pet gWin; bool hideOk;

        static readonly string[] gameNames = { "", "Догонялки", "Драка", "Снежная битва", "Чехарда", "Дай пять", "Танец", "Розыгрыш «БУ!»",
                                               "Гонки", "Обнимашки", "Прятки", "Повторялка", "Тихий час", "Кто выше прыгнет",
                                               "Перетягивание каната", "Гляделки", "Камень-ножницы-бумага" };
        static readonly string[] playIds = { "", "tag", "fight", "snow", "leap", "five", "dance", "prank", "race", "hug", "seek", "copy", "nap", "jump", "tug", "stare", "rps" };
        static readonly Game[] menuGames = { Game.Tag, Game.Fight, Game.Snow, Game.Race, Game.Seek, Game.Leap, Game.HighJump,
                                             Game.Tug, Game.Rps, Game.Stare, Game.Mimic, Game.High5, Game.Hug, Game.Dance, Game.Nap, Game.Prank };

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
            if (!gA.OkInGame || !gB.OkInGame || gA.wa != gB.wa || gT > (game == Game.Seek ? 75 : 45)) { EndGame(); return; }
            switch (game)
            {
                case Game.Tag: TickTag(dt); break;
                case Game.Fight: TickFight(dt); break;
                case Game.Snow: TickSnow(dt); break;
                case Game.Leap: TickLeap(dt); break;
                case Game.High5: case Game.Dance: case Game.Hug: TickMeet(dt); break;
                case Game.Prank: TickPrank(dt); break;
                case Game.Race: TickRace(dt); break;
                case Game.Seek: TickSeek(dt); break;
                case Game.Mimic: TickMimic(dt); break;
                case Game.Nap: TickNap(dt); break;
                case Game.HighJump: TickJump(dt); break;
                case Game.Tug: TickTug(dt); break;
                case Game.Stare: TickStare(dt); break;
                case Game.Rps: TickRps(dt); break;
            }
        }

        bool SnowAllowed { get { return cfg.NewYear && cfg.Snow && strip != null && strip.Visible; } }
        bool SeekAllowed { get { return wa.Bottom < sb.Bottom; } } // taskbar at the bottom to hide in

        Game PickGame()
        {
            //          tag fight snow                  leap hi5 dance prank race hug  seek                  copy nap jump
            float[] w = { 0, 26, 16, SnowAllowed ? 30 : 0, 12, 10, 8, 0, 14, 9, SeekAllowed ? 13 : 0, 12, 5, 11, 11, 8, 10 };
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
            if (g == Game.Seek && !SeekAllowed) g = Game.Race;
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
                case Game.Race:
                    gA.Say(gA.codex ? U.Pick("Наперегонки! Я быстрый!", "Спорим, обгоню?") : U.Pick("Наперегонки!", "Кто быстрее?", "Гонка!"), 1.6f);
                    break;
                case Game.Hug:
                    gA.Say(gA.codex ? U.Pick("Обнимашки!", "Клод, иди сюда!") : U.Pick("Обнимашки?", "Иди обниму!"), 1.5f);
                    break;
                case Game.Seek:
                    gA.Say(U.Pick("Прятки! Я вожу!", "Считаю до десяти!", "Прячься!"), 1.6f);
                    break;
                case Game.Mimic:
                    gGoal = U.R.Next(3, 5);
                    break;
                case Game.Nap:
                    gA.Say(U.Pick("Тихий час...", "Что-то я устал...", "Может, поспим?"), 1.6f);
                    break;
                case Game.HighJump:
                    gGoal = 4; gA.gBest = gB.gBest = 0;
                    gA.Say(U.Pick("Кто выше прыгнет?", "Прыжки в высоту!"), 1.6f);
                    break;
                case Game.Prank:
                    if (gA.codex) { var tmp = gA; gA = gB; gB = tmp; }
                    if (gA.st != St.Sleep) gA.Go(St.Sleep, 40);
                    break;
            }
        }

        internal void EndGame()
        {
            if (game == Game.None) return;
            foreach (var p in new[] { gA, gB })
            {
                if (p == null) continue;
                p.inGame = false; p.hideOk = false;
                if (p.st == St.Hidden) { p.t = 0; p.dur = 0.8f; }
                else if (p.st == St.Peek) p.dur = p.t + 0.3f;
                if (p.OnGround && (p.st == St.Walk || p.st == St.Brawl || p.st == St.Duck || p.st == St.Idle || p.st == St.Cover || p.st == St.Tug || p.st == St.Stare || p.st == St.Rps)) { p.GoIdle(); }
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
                        else if (gPhT > 16) EndGame();
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
            float gapWant = game == Game.High5 ? 12 * u : game == Game.Hug ? 9 * u : 16 * u;
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
                else if (game == Game.Hug)
                {
                    A.Go(St.Hug, 2.6f); B.Go(St.Hug, 2.6f);
                    B.Say(B.codex ? U.Pick("Ты мой лучший друг!", "Не отпущу!", "Тёплый!") : U.Pick("Я тоже рад!", "Обнимашки!", "Ты лучший, Кодекс"), 1.6f);
                }
                else
                {
                    A.Go(St.Dance, 4.2f); B.Go(St.Dance, 4.2f);
                    B.Say("♪ ♫ ♪", 1.6f);
                }
                EndGame();
            }
            else if (gPhT > 16) EndGame();
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
                        if (gT < 1.5f) break;
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

        // ---------- shared: walk up to the mate ----------
        bool GetTogether(float gap)
        {
            Pet A = gA, B = gB;
            float spot = B.px - gS * gap;
            if (spot < A.Lo || spot > A.Hi) spot = B.px + gS * gap;
            if (B.Ready && B.st == St.Walk) B.GoIdle();
            if (B.Ready) B.Face(A);
            if (A.Ready) A.RunTo(spot, 100 * k);
            if (A.Ready && A.st == St.Idle && Math.Abs(A.px - spot) < 3 && B.Ready && B.st != St.Walk) { A.Face(B); B.Face(A); return true; }
            if (gPhT > 16) EndGame();
            return false;
        }

        void DoMove(int m)
        {
            switch (m)
            {
                case 0: Go(St.Wave, 1.4f); break;
                case 1: Go(St.Dance, 2.2f); break;
                case 2: leaping = false; Go(St.Prep, 0.18f); break;
                case 3: Go(St.Love, 1.6f); break;
                case 4: Go(St.Celebrate, 1.4f); break;
                case 5: Go(St.Spin, 1.2f); break;
                default: Go(St.Stretch, 2f); break;
            }
        }

        // ---------- race ----------
        void TickRace(float dt)
        {
            Pet A = gA, B = gB;
            switch (gPh)
            {
                case 0:
                    if (gN == 0)
                    {
                        gN = 1;
                        float c = (A.px + B.px) / 2, d = (A.Hi - c) > (c - A.Lo) ? 1 : -1;
                        A.gWait = U.Cl(c - d * 8 * u, A.Lo, A.Hi); B.gWait = U.Cl(A.gWait + d * 16 * u, B.Lo, B.Hi);
                        float room = d > 0 ? B.Hi - B.gWait : B.gWait - B.Lo;
                        float L = Math.Min(480 * k, room - 4 * u);
                        if (L < 150 * k) { EndGame(); return; }
                        gS = d; gX = A.gWait + d * L; gMid = B.gWait + d * L;
                    }
                    if (A.Ready) A.RunTo(A.gWait, 110 * k);
                    if (B.Ready) B.RunTo(B.gWait, 110 * k);
                    if (A.Ready && B.Ready && A.st == St.Idle && B.st == St.Idle && Math.Abs(A.px - A.gWait) < 4 && Math.Abs(B.px - B.gWait) < 4)
                    {
                        A.dir = B.dir = (int)gS;
                        A.Say(U.Pick("На старт... внимание...", "Три... два... один..."), 1.6f);
                        if (B.codex && U.Ch(0.4)) B.SayLater(0.8f, "Я готов! Наверное");
                        gPh = 1; gPhT = 0;
                    }
                    else if (gPhT > 12) EndGame();
                    break;
                case 1:
                    if (gPhT > 1.7f)
                    {
                        A.Say("Марш!", 1f);
                        A.gWait = U.Rf(118, 140) * k; B.gWait = U.Rf(118, 140) * k;
                        foreach (var p in new[] { A, B }) if (p.codex) p.tripCd = U.Rf(0, 1.5f);
                        gPh = 2; gPhT = 0; gWin = null;
                    }
                    break;
                case 2:
                    foreach (var p in new[] { A, B })
                    {
                        if (p.gHits > 0 || !p.Ready) continue;
                        float f = p == A ? gX : gMid;
                        if (Math.Abs(p.px - f) < 4)
                        {
                            p.gHits = 1;
                            if (gWin == null)
                            {
                                gWin = p; p.Go(St.Celebrate, 1.6f);
                                p.Say(p.codex ? U.Pick("Я первый?! Я первый!", "Скорость света!", "Кодекс — чемпион!") : U.Pick("Первый!", "Я быстрее!", "Финиш!"), 1.6f);
                            }
                            else
                            {
                                p.Go(p.codex ? St.Sad : St.Laugh, 1.3f);
                                p.SayLater(1.8f, p.codex ? U.Pick("Я споткнулся, не считается!", "Реванш!") : U.Pick("Тебе просто повезло!", "В следующий раз!"));
                            }
                        }
                        else p.RunTo(f, p.gWait);
                    }
                    if ((A.gHits > 0 && B.gHits > 0) || gPhT > 20) EndGame();
                    break;
            }
        }

        // ---------- hide and seek ----------
        void TickSeek(float dt)
        {
            Pet A = gA, B = gB; // A seeks, B hides in the taskbar
            switch (gPh)
            {
                case 0:
                    if (gN == 0)
                    {
                        gN = 1;
                        float side = (A.Hi - A.px) > (A.px - A.Lo) ? 1 : -1;
                        gX = U.Cl(A.px + side * U.Rf(220, 480) * k, B.Lo, B.Hi);
                        A.Face(B); A.Go(St.Cover, 99);
                        B.hideOk = true;
                    }
                    if (B.Ready) B.RunTo(gX, 125 * k);
                    if (B.Ready && ((B.st == St.Idle && Math.Abs(B.px - gX) < 4) || gPhT > 10))
                    {
                        B.Go(St.Dive, 0.8f); gPh = 1; gPhT = 0;
                        if (U.Ch(0.5)) B.Say(B.codex ? "Хи-хи!" : "Тсс...", 1f);
                    }
                    break;
                case 1:
                    if (B.st == St.Hidden) B.dur = 999;
                    if (B.st == St.Hidden && gPhT > 1.2f)
                    {
                        A.GoIdle(); A.dur = 0.5f;
                        A.Say(A.codex ? U.Pick("Иду искать! А кого?", "Готов или нет — иду!") : U.Pick("Кто не спрятался — я не виноват!", "Иду искать!"), 1.6f);
                        gN = 0; gGoal = U.R.Next(1, 3); PickDecoy(); gPh = 2; gPhT = 0;
                    }
                    else if (gPhT > 5) EndGame();
                    break;
                case 2:
                    {
                        if (A.Ready && gPhT > 0.5f && Math.Abs(A.px - B.px) < 15 * u)
                        {
                            A.Face(B); B.sinkStart = B.sink; B.Go(St.PopOut, 0.18f); B.hideOk = false;
                            B.Say(B.codex ? U.Pick("Ой! Как ты меня нашёл?!", "Нечестно, я хорошо спрятался!") : U.Pick("Ку-ку!", "Нашёл всё-таки!"), 1.6f);
                            A.Go(St.Celebrate, 1.4f); A.Say(U.Pick("Нашёл!", "Попался!", "Вот ты где!"), 1.4f);
                            EndGame(); return;
                        }
                        if (gPhT > 40) { EndGame(); return; }
                        if (!A.Ready) break;
                        float target = gN < gGoal ? gMid : B.px;
                        if (Math.Abs(A.px - target) < 4 && A.st == St.Idle)
                        {
                            gN++;
                            A.Go(U.Ch(0.5) ? St.Look : St.Think, 1.4f);
                            if (U.Ch(0.7)) A.Say(U.Pick("Хм... не тут", "Где же он?", "Куда он делся?", "Тут пусто"), 1.4f);
                            if (B.codex && B.st == St.Hidden && U.Ch(0.5)) B.Go(St.Peek, 99);
                            PickDecoy();
                        }
                        else A.RunTo(target, 75 * k);
                        break;
                    }
            }
        }

        void PickDecoy()
        {
            for (int i = 0; i < 12; i++)
            {
                gMid = U.Rf(gA.Lo, gA.Hi);
                bool wrongSide = Math.Sign(gMid - gA.px) != Math.Sign(gB.px - gA.px);
                if (Math.Abs(gMid - gB.px) > 90 * k && Math.Abs(gMid - gA.px) > 70 * k && (gN > 0 || wrongSide || i > 8)) return;
            }
            gMid = gA.px;
        }

        // ---------- copy me ----------
        void TickMimic(float dt)
        {
            Pet A = gA, B = gB;
            switch (gPh)
            {
                case 0:
                    if (!GetTogether(18 * u)) return;
                    A.Say(A.codex ? U.Pick("Повторяй за мной! Если сможешь", "Делай как я!") : U.Pick("Повторяй за мной!", "Делай как я!"), 1.6f);
                    gPh = 1; gPhT = 0; break;
                case 1:
                    if (gPhT > 1.2f && A.Ready && A.st == St.Idle && B.Ready && B.st == St.Idle)
                    { gMove = U.R.Next(7); A.Face(B); A.DoMove(gMove); gPh = 2; gPhT = 0; }
                    break;
                case 2:
                    if (gPhT > 0.5f && A.Ready && A.st == St.Idle && B.Ready)
                    {
                        B.Face(A);
                        if (B.codex && U.Ch(0.33))
                        {
                            B.gHits++;
                            if (U.Ch(0.4)) B.Go(St.Trip, 1.9f);
                            else { B.DoMove((gMove + 1 + U.R.Next(6)) % 7); B.SayLater(1.3f, U.Pick("Ой, это не то...", "Так? Нет?", "Я художник, я так вижу")); }
                        }
                        else { B.DoMove(gMove); if (U.Ch(0.3)) B.Say(U.Pick("Легко!", "Вот так?", "Повторил!"), 1.2f); }
                        gPh = 3; gPhT = 0;
                    }
                    break;
                case 3:
                    if (gPhT > 0.5f && B.Ready && B.st == St.Idle && A.Ready)
                    {
                        gN++;
                        if (gN >= gGoal)
                        {
                            if (B.gHits > 0) { A.Go(St.Laugh, 1.3f); A.Say(U.Pick("Ну почти!", "Ха-ха, засчитано!", "Старайся лучше!"), 1.6f); B.Go(St.Happy, 1f); }
                            else { A.Go(St.Happy, 1.1f); A.Say(U.Pick("Молодец!", "Всё верно!", "Отлично!"), 1.5f); B.Go(St.Celebrate, 1.4f); }
                            EndGame(); return;
                        }
                        gPh = 1; gPhT = 0;
                    }
                    break;
            }
        }

        // ---------- nap together ----------
        void TickNap(float dt)
        {
            Pet A = gA, B = gB;
            if (gPh == 0)
            {
                if (!GetTogether(13 * u)) return;
                A.Go(St.Yawn, 1.6f); B.Go(St.Yawn, 1.6f); gPh = 1; gPhT = 0; return;
            }
            if (gPhT > 1.7f)
            {
                A.Go(St.Sleep, U.Rf(18, 35)); B.Go(St.Sleep, U.Rf(18, 35));
                Pet snorer = B.codex ? B : A;
                snorer.SayLater(2.5f, U.Pick("Хр-р-р... пс-с-с...", "Хр-р... баги... хр-р...", "Zzz... ещё пять минуток..."));
                EndGame();
            }
        }

        // ---------- who jumps higher ----------
        void TickJump(float dt)
        {
            Pet A = gA, B = gB;
            if (gPh == 0) { if (!GetTogether(20 * u)) return; gPh = 1; gPhT = 0; gN = 0; return; }
            bool calm = A.Ready && B.Ready && A.st == St.Idle && B.st == St.Idle;
            if (!calm || gPhT < 0.6f) return;
            if (gN >= gGoal)
            {
                Pet win = A.gBest == B.gBest ? null : (A.gBest > B.gBest ? A : B);
                if (win == null) { A.Go(St.Laugh, 1.2f); B.Go(St.Laugh, 1.2f); A.Say("Ничья!", 1.4f); }
                else
                {
                    Pet lose = win == A ? B : A;
                    win.Go(St.Celebrate, 1.6f);
                    win.Say(win.codex ? U.Pick("Я выше! Я почти взлетел!", "Кодекс в космосе!") : U.Pick("Я выше!", "Рекорд!"), 1.6f);
                    lose.Go(lose.codex ? St.Sad : St.Laugh, 1.3f);
                }
                EndGame(); return;
            }
            Pet J = gN % 2 == 0 ? A : B, O = J == A ? B : A;
            float v = U.Rf(380, 660);
            bool flop = J.codex && U.Ch(0.25);
            if (flop) v = 200;
            J.gBest = Math.Max(J.gBest, v);
            J.Face(O);
            J.leaping = true; J.leapVx = 0; J.leapVy = -v * k; J.Go(St.Prep, 0.25f);
            if (flop) J.SayLater(1.2f, U.Pick("Это была разминка!", "Не считается!"));
            else if (v > 580) O.SayLater(0.7f, U.Pick("Ого!", "Вот это да!", "Высоко!"));
            else if (U.Ch(0.3)) J.Say(U.Pick("Хоп!", "Смотри!", "Йе!"), 1f);
            gN++; gPhT = 0;
        }
    }
}
