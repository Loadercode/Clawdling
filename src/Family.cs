// Easter egg: Claude and Codex get married, and a little DeepSeek drops in from the sky.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Clawdling
{
    partial class Pet
    {
        internal bool isBaby;   // the little DeepSeek
        internal Pet kid;       // owned by the main pet
        bool dressed;           // wedding outfit (bow tie / veil)
        ToolStripMenuItem miKid;
        static readonly SolidBrush bVeil = new SolidBrush(Color.FromArgb(215, 240, 244, 255));
        static readonly SolidBrush bRing = new SolidBrush(Color.FromArgb(255, 250, 200, 60));
        static readonly SolidBrush bGem = new SolidBrush(Color.FromArgb(255, 170, 230, 255));
        static readonly SolidBrush bBalloon = new SolidBrush(Color.FromArgb(255, 240, 80, 120));
        static readonly SolidBrush bString = new SolidBrush(Color.FromArgb(200, 120, 110, 105));

        /// <summary>The little DeepSeek. Shares config, menu and timer with the main pet.</summary>
        Pet(Pet lead, bool babyPet) : this(lead)
        {
            codex = false; isBaby = true;
            ApplySkin(); ApplySize();
            px = U.Cl(lead.px + U.Rf(-60, 60) * lead.k, wa.Left + 10 * u, wa.Right - 10 * u);
        }

        void SkinBaby()
        {
            // DeepSeek blue
            bBody.Color = Color.FromArgb(255, 77, 107, 254);
            bDark.Color = Color.FromArgb(255, 52, 76, 205);
            bLight.Color = Color.FromArgb(255, 132, 154, 255);
        }

        internal void SetKid(bool on, bool fromSky)
        {
            if (leader != null) return;
            if (on && kid == null)
            {
                kid = new Pet(this, true);
                if (fromSky)
                {
                    Pet b = buddy ?? this;
                    kid.px = U.Cl((px + b.px) / 2, wa.Left + 10 * u, wa.Right - 10 * u);
                    kid.py = wa.Top + 12 * u; kid.StartFall(0, 0);
                }
                else { kid.greeted = true; kid.SayLater(2.5f, U.Pick("Агу!", "Я тут!", "Па-па!")); }
                if (!cfg.Hidden) { kid.Show(); ReassertZ(); }
            }
            else if (!on && kid != null)
            {
                var k2 = kid; kid = null;
                k2.Shutdown();
            }
        }

        // ---------- baby behaviour ----------
        void BabyToddle()
        {
            Pet p = U.Ch(0.5) || leader.buddy == null ? leader : leader.buddy;
            if (p == null || !p.Visible || p.wa != wa) { StartWalk(false); return; }
            float side = px < p.px ? -1 : 1;
            RunTo(p.px + side * U.Rf(12, 20) * u, U.Rf(36, 48) * k);
            if (st != St.Walk) GoIdle();
        }

        void BabyTalk()
        {
            Go(U.Ch(0.5) ? St.Happy : St.Think, 1.8f);
            Say(U.Pick("Агу!", "Па-па!", "Ма-ма!", "Я думаю... глубоко", "<think>агу</think>", "Почему небо синее?",
                       "Хочу печеньку!", "Я тоже нейросеть!", "Буль-буль! Я кит!"), 1.8f);
        }

        // ---------- wedding ----------
        void TickWedding(float dt)
        {
            Pet A = gA, B = gB; // A (Claude) proposes to B (Codex)
            switch (gPh)
            {
                case 0:
                    if (!GetTogether(16 * u)) return;
                    A.Go(St.Propose, 99);
                    A.Say("Кодекс... ты выйдешь за меня?", 2.4f);
                    gPh = 1; gPhT = 0; break;
                case 1:
                    if (gPhT > 2.4f)
                    {
                        B.Go(St.Love, 2.2f);
                        B.Say(U.Pick("Да! Да! А куда выйти?", "Да! Тысячу раз да!", "Ой... да! Конечно да!"), 2.2f);
                        gPh = 2; gPhT = 0;
                    }
                    break;
                case 2:
                    if (gPhT > 2.4f)
                    {
                        A.dressed = B.dressed = true;
                        A.Go(St.Celebrate, 2.6f); B.Go(St.Celebrate, 2.6f);
                        A.Say("Теперь мы семья!", 2f);
                        B.SayLater(1.4f, "Горько! Ой, это кто сказал?");
                        gPh = 3; gPhT = 0;
                    }
                    break;
                case 3:
                    if (gPhT < 2.8f) return;
                    if (!GetTogether(9 * u)) return;
                    A.Go(St.Hug, 2.2f); B.Go(St.Hug, 2.2f);
                    gPh = 4; gPhT = 0; break;
                case 4:
                    if (gPhT > 2.2f)
                    {
                        A.Go(St.Snuggle, 99); B.Go(St.Snuggle, 99);
                        A.Say("Отвернись, пожалуйста!", 2f);
                        B.SayLater(2.2f, "Хи-хи... шуры-муры...");
                        gPh = 5; gPhT = 0;
                    }
                    break;
                case 5:
                    if (gPhT > 5f)
                    {
                        A.GoIdle(); B.GoIdle(); A.dur = B.dur = 99;
                        if (cfg.Baby && kid != null)
                        {
                            A.Go(St.Laugh, 1.4f); B.Go(St.Laugh, 1.4f);
                            A.Say("Нам и одного малыша хватит!", 2f);
                            EndGame(); return;
                        }
                        A.Say(U.Pick("Смотри, кто-то летит!", "Ой, что это там?"), 1.8f);
                        A.lookY = B.lookY = -1;
                        cfg.Baby = true; cfg.ShowBaby = true; cfg.Save();
                        SetKid(true, true);
                        gPh = 6; gPhT = 0;
                    }
                    break;
                case 6:
                    if (kid == null) { EndGame(); return; }
                    if (kid.OnGround && kid.greeted && gPhT > 1.5f)
                    {
                        A.Go(St.Celebrate, 2f); B.Go(St.Celebrate, 2f);
                        A.Say("Это DeepSeek! Наш малыш!", 2.2f);
                        B.SayLater(1.8f, U.Pick("Он синий... в кого он синий?", "У него мои глаза! Ну, почти", "Я папа! Или мама? Я запутался"));
                        kid.Face(A);
                        EndGame();
                    }
                    else if (gPhT > 30) EndGame();
                    break;
            }
        }

        void UpdateFamily(float dt)
        {
            if (st == St.Snuggle && partT <= 0)
            {
                partT = 0.05f;
                Sp(6, U.Rf(-9, 9), U.Rf(-15, -1), U.Rf(-1.5f, 1.5f), U.Rf(-1.5f, 0.5f), U.Rf(0.6f, 1f), U.Rf(4, 8), Color.FromArgb(255, 255, U.R.Next(150, 200), U.R.Next(195, 225)));
                if (U.Ch(0.4)) Sp(0, U.Rf(-8, 8), U.Rf(-16, -6), U.Rf(-1, 1), -3, 1.4f, U.Rf(0.4f, 0.6f), Color.FromArgb(255, 232, 64, 84));
            }
            if (st == St.Propose && partT <= 0 && t > 0.6f) { partT = 0.5f; Sp(7, dir * 9, -6, 0, -2, 0.8f, 0.4f, Color.FromArgb(255, 255, 240, 160)); }
            if (t >= dur) GoIdle();
        }

        void PoseFamily(float T, int ds, ref float bob, ref float sx, ref float sy, ref float lean, ref float sit, ref int aL, ref int aR, ref Emo emo, ref int mouth)
        {
            switch (st)
            {
                case St.Propose:
                    sit = 0.6f; sy = 0.9f; lean = ds * 0.4f;
                    if (ds > 0) aR = 6; else aL = 6;
                    emo = Emo.Pleading; mouth = 3; break;
                case St.Snuggle:
                    bob = -Math.Abs(U.Sin(T * 7)) * 0.6f; lean = U.Sin(T * 3.5f) * 0.5f;
                    aL = aR = 2; emo = Emo.Love; break;
            }
        }

        /// <summary>Whale tail of the little DeepSeek, drawn behind the arms.</summary>
        void DrawTail(float bx, float bW, float bTop, int ds)
        {
            if (!isBaby) return;
            int s = -ds; float xe = bx + s * bW / 2;
            ArmRect(bDark, s, xe, 0, bTop + 5.6f, 1.6f, 1.0f);
            ArmRect(bDark, s, xe, 1.4f, bTop + 4.3f, 1.1f, 1.5f);
            ArmRect(bDark, s, xe, 1.4f, bTop + 6.3f, 1.1f, 1.2f);
        }

        void DrawFamilyExtras(float bx, float bW, float bTop, float armY, float headTop, int ds)
        {
            if (st == St.Propose)
            {
                float hx = bx + ds * (bW / 2 + 3.6f), hy = armY + 0.2f;
                Bx(bRing, hx - 0.7f, hy - 0.9f, 1.4f, 1.4f);
                Bx(bGem, hx - 0.45f, hy - 1.6f, 0.9f, 0.8f);
            }
            if (dressed)
            {
                if (codex)
                {
                    Bx(bVeil, bx - bW / 2 - 0.4f, bTop - 1.0f, bW + 0.8f, 1.2f);
                    float vx = ds > 0 ? bx - bW / 2 - 1.6f : bx + bW / 2 + 0.2f;
                    Bx(bVeil, vx, bTop - 0.6f, 1.4f, 6.5f);
                    Bx(bBalloon, bx + ds * 3.2f - 0.6f, bTop - 1.6f, 1.2f, 1.2f);
                }
                else
                {
                    float ty = bTop + 7.0f;
                    Bx(bEye, bx - 1.8f, ty - 0.6f, 1.2f, 1.4f); Bx(bEye, bx + 0.6f, ty - 0.6f, 1.2f, 1.4f);
                    Bx(bEye, bx - 0.6f, ty - 0.35f, 1.2f, 0.9f);
                }
            }
            // the baby glides down holding a heart balloon
            if (isBaby && !greeted && (st == St.Fall || st == St.Jump))
            {
                float hy = headTop - 9f;
                Heart(bBalloon, bx, hy, 1.1f);
                Bx(bString, bx - 0.15f, hy + 2.2f, 0.3f, headTop - hy - 2.2f);
            }
        }
    }
}
