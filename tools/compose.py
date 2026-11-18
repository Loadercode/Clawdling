"""Composes README images from frames rendered by AssetGen.exe.
usage: python tools/compose.py <frames_dir> <out_dir>
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

FR, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
FONTS = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def img(name):
    return Image.open(os.path.join(FR, name)).convert("RGBA")


U = 6                 # sprite unit used by AssetGen
FOOT = (102, 192)     # foot point inside a 204x204 pet frame
CLAUDE = (217, 119, 87)
CODEX = (16, 163, 127)
random.seed(7)


def vgrad(w, h, top, bottom):
    g = Image.new("RGBA", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        g.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,))
    return g.resize((w, h))


def night(w, h, flakes=70, avoid=None):
    bg = vgrad(w, h, (14, 18, 34), (34, 40, 66))
    d = ImageDraw.Draw(bg)
    for _ in range(flakes):
        x, y, r = random.uniform(0, w), random.uniform(0, h * 0.85), random.choice([1, 1, 1.5, 2, 2.5])
        if avoid and avoid[0] < x < avoid[2] and avoid[1] < y < avoid[3]:
            continue
        a = random.randint(60, 170)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(235, 242, 255, a))
    return bg


def taskbar(base, y, snow=True):
    """Windows-11-like taskbar from y to bottom, optional snow on top."""
    w, h = base.size
    bar = Image.new("RGBA", (w, h - y), (28, 28, 32, 255))
    d = ImageDraw.Draw(bar)
    d.line((0, 0, w, 0), fill=(60, 60, 68, 255), width=1)
    n, sz, gap = 9, 26, 18
    x0 = (w - (n * sz + (n - 1) * gap)) // 2
    cols = [(0, 120, 212), (90, 90, 100), (230, 180, 60), (40, 160, 230), (217, 119, 87), (120, 190, 80), (110, 110, 120), (170, 110, 240), (90, 90, 100)]
    cy = (h - y) // 2
    for i in range(n):
        x = x0 + i * (sz + gap)
        d.rounded_rectangle((x, cy - sz // 2, x + sz, cy + sz // 2), 6, fill=cols[i] + (255,))
    d.text((w - 70, cy - 9), "22:45", font=font("segoeui.ttf", 15), fill=(220, 220, 225, 255))
    base.alpha_composite(bar, (0, y))
    if snow:
        strip = img("snow_strip.png")
        sw = strip.size[0]
        for x in range(0, w, sw):
            base.alpha_composite(strip, (x, y - strip.size[1] + 3))


def put_pet(base, frame, foot_x, foot_y, scale=1.0):
    if scale != 1.0:
        frame = frame.resize((int(frame.size[0] * scale), int(frame.size[1] * scale)), Image.NEAREST)
    base.alpha_composite(frame, (int(foot_x - FOOT[0] * scale), int(foot_y - FOOT[1] * scale)))


def put_center(base, im, cx, cy):
    base.alpha_composite(im, (int(cx - im.size[0] / 2), int(cy - im.size[1] / 2)))


def save_gif(frames, name, ms=50):
    pal = [f.convert("RGB").quantize(colors=128, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE) for f in frames]
    pal[0].save(os.path.join(OUT, name), save_all=True, append_images=pal[1:], duration=ms, loop=0, optimize=True, disposal=1)


def ball_pos(x0, y0, tx, ty, T, g, t):
    vx, vy = (tx - x0) / T, (ty - y0) / T - 0.5 * g * T
    return x0 + vx * t, y0 + vy * t + 0.5 * g * t * t


# ---------------------------------------------------------------- hero (1280x640)
def hero():
    W, H, TB = 1280, 640, 576
    im = night(W, H, 140, avoid=(60, 100, 900, 480))
    d = ImageDraw.Draw(im)
    d.text((80, 112), "Clawdling", font=font("seguibl.ttf", 92), fill=(255, 255, 255, 255))
    d.text((84, 232), "Пиксельный питомец для панели задач Windows", font=font("seguisb.ttf", 30), fill=(236, 226, 218, 255))
    y = 292
    for line in ["Гуляет по экрану и окнам, болтает лапками, когда его таскаешь",
                 "Приносит уведомления Claude Code: «готово», «нужен ответ», «можно?»",
                 "Друг Кодекс и 16 совместных игр: догонялки, драки, снежки, прятки"]:
        d.ellipse((88, y + 12, 98, y + 22), fill=(CODEX if "Кодекс" in line else CLAUDE) + (255,))
        d.text((112, y), line, font=font("segoeui.ttf", 22), fill=(200, 205, 222, 255))
        y += 38
    chip_y = 430
    x = 84
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    od = ImageDraw.Draw(ov)
    f = font("seguisb.ttf", 18)
    for chip in ["~120 КБ", "~10 МБ RAM", "без установки", "Windows 10 / 11"]:
        tw = d.textlength(chip, font=f)
        od.rounded_rectangle((x, chip_y, x + tw + 28, chip_y + 36), 18, fill=(255, 255, 255, 28), outline=(255, 255, 255, 70))
        x += tw + 40
    im.alpha_composite(ov)
    x = 84
    for chip in ["~120 КБ", "~10 МБ RAM", "без установки", "Windows 10 / 11"]:
        tw = d.textlength(chip, font=f)
        d.text((x + 14, chip_y + 6), chip, font=f, fill=(235, 238, 248, 255))
        x += tw + 40

    taskbar(im, TB)
    # Claude throws a snowball at Codex
    px, cxp = 900, 1150
    states = [l.split()[1] for l in open(os.path.join(FR, "throw.txt"), encoding="utf-8")]
    put_pet(im, img("th%02d.png" % (states.index("Throw") + 2)), px, TB + 2)
    put_pet(im, img("cn_hit.png"), cxp, TB + 2)
    hx, hy = px + 6.5 * U + 6, TB - 13 * U
    tx, ty = cxp - 4 * U, TB - 10 * U
    pts = [ball_pos(hx, hy, tx, ty, 0.5, 3000, t / 100 * 0.5) for t in range(0, 101)]
    trail = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(trail)
    for i in range(20, 90, 5):
        x, y = pts[i]
        r = 2 + (i - 20) / 18
        td.ellipse((x - r, y - r, x + r, y + r), fill=(235, 242, 255, int(30 + (i - 20) * 2)))
    im.alpha_composite(trail.filter(ImageFilter.GaussianBlur(1.2)))
    put_center(im, img("sp0_03.png"), tx, ty)
    # Codex complains
    bub = img("b_codex_dark.png")
    bub = bub.resize((int(bub.size[0] * 0.62), int(bub.size[1] * 0.62)), Image.LANCZOS)
    im.alpha_composite(bub, (int(min(W - bub.size[0] - 10, cxp - 60)), int(TB - 21 * U - bub.size[1] + 10)))
    im.convert("RGB").save(os.path.join(OUT, "hero.png"), optimize=True)


# ---------------------------------------------------------------- emotions grid
def emotions():
    cells = [("e_idle", "Спокоен"), ("e_happy", "Радуется"), ("e_laugh", "Смеётся"), ("e_love", "Влюблён"),
             ("e_sleep", "Спит"), ("e_surprised", "Удивлён"), ("e_ask", "Спрашивает"), ("e_dizzy", "Кружится голова"),
             ("e_angry", "Сердится"), ("e_sad", "Грустит"), ("e_dance", "Танцует"), ("e_think", "Думает"),
             ("n_idle", "В шапочке"), ("n_roll", "Лепит снежок"), ("n_windup", "Замахивается"), ("n_hit", "В него попали")]
    cols, cw, ch = 4, 290, 250
    W, H = cols * cw + 80, 4 * ch + 150
    im = vgrad(W, H, (26, 23, 21), (36, 31, 28))
    d = ImageDraw.Draw(im)
    d.text((40, 32), "Эмоции и анимации", font=font("seguibl.ttf", 40), fill=(255, 255, 255, 255))
    d.text((42, 86), "Каждая поза нарисована тем же кодом, что и в программе", font=font("segoeui.ttf", 20), fill=(190, 182, 176, 255))
    lf = font("seguisb.ttf", 20)
    for i, (name, label) in enumerate(cells):
        cx, cy = 40 + (i % cols) * cw, 130 + (i // cols) * ch
        ny = name.startswith("n_")
        d.rounded_rectangle((cx + 8, cy + 8, cx + cw - 8, cy + ch - 8), 22,
                            fill=(48, 56, 86, 255) if ny else (52, 46, 42, 255),
                            outline=(80, 92, 130, 255) if ny else (72, 64, 58, 255))
        put_pet(im, img(name + ".png"), cx + cw / 2, cy + ch - 62)
        tw = d.textlength(label, font=lf)
        d.text((cx + cw / 2 - tw / 2, cy + ch - 50), label, font=lf, fill=(236, 230, 224, 255))
    im.convert("RGB").save(os.path.join(OUT, "emotions.png"), optimize=True)


# ---------------------------------------------------------------- notification bubbles
def notifications():
    names = ["b_done", "b_attn", "b_ask"]
    dark = [img(n + "_dark.png") for n in names]
    light = [img(n + "_light.png") for n in names]
    sc = 0.75
    dark = [b.resize((int(b.size[0] * sc), int(b.size[1] * sc)), Image.LANCZOS) for b in dark]
    light = [b.resize((int(b.size[0] * sc), int(b.size[1] * sc)), Image.LANCZOS) for b in light]
    colw = max(b.size[0] for b in dark + light) + 60
    hh = sum(b.size[1] for b in dark) + 40 * 2
    W, H = colw * 2, hh + 120
    im = Image.new("RGBA", (W, H))
    im.alpha_composite(vgrad(colw, H, (24, 22, 21), (34, 30, 28)), (0, 0))
    im.alpha_composite(vgrad(colw, H, (238, 234, 229), (226, 220, 214)), (colw, 0))
    d = ImageDraw.Draw(im)
    tf = font("seguisb.ttf", 22)
    d.text((30, 28), "Тёмная тема", font=tf, fill=(230, 224, 218, 255))
    d.text((colw + 30, 28), "Светлая тема", font=tf, fill=(60, 52, 48, 255))
    for col, arr in enumerate((dark, light)):
        y = 80
        for b in arr:
            im.alpha_composite(b, (col * colw + 20, y))
            y += b.size[1] + 20
    im.convert("RGB").save(os.path.join(OUT, "notifications.png"), optimize=True)


# ---------------------------------------------------------------- GIFs
def walk_gif():
    W, H, TB = 720, 240, 190
    base = night(W, H, 40)
    taskbar(base, TB)
    frames = []
    speed = 45 * 2 * 0.05  # walkSpd * k * dt at u=6
    x = 180
    for i in range(80):
        f = base.copy(); put_pet(f, img("wnr%02d.png" % i), x, TB + 2); frames.append(f); x += speed
    for i in range(80):
        f = base.copy(); put_pet(f, img("wnl%02d.png" % i), x, TB + 2); frames.append(f); x -= speed
    save_gif(frames, "walk.gif")


def snowball_gif():
    W, H, TB = 760, 380, 330
    base = night(W, H, 50)
    taskbar(base, TB)
    states = [l.split()[1] for l in open(os.path.join(FR, "throw.txt"), encoding="utf-8")]
    n = len(states)
    throw_i = states.index("Throw")
    px = 170
    hx, hy = px + 6.5 * U, TB - 13 * U
    tx, ty = 660, 90
    T, g = 0.6, 3000
    flight = int(T / 0.05)
    ball = img("ball.png")
    total = max(n, throw_i + flight + 16 + 8)
    frames = []
    for i in range(total):
        f = base.copy()
        put_pet(f, img("th%02d.png" % min(i, n - 1)), px, TB + 2)
        k = i - throw_i
        if 0 <= k < flight:
            bx, by = ball_pos(hx, hy, tx, ty, T, g, k * 0.05)
            put_center(f, ball, bx, by)
        elif flight <= k < flight + 16:
            put_center(f, img("sp0_%02d.png" % (k - flight)), tx, ty)
        frames.append(f)
    save_gif(frames, "snowball.gif")


def notify_gif():
    W, H, TB = 640, 380, 330
    base = night(W, H, 40)
    taskbar(base, TB)
    bub = img("b_done_dark.png")
    bub = bub.resize((int(bub.size[0] * 0.62), int(bub.size[1] * 0.62)), Image.LANCZOS)
    px = 320
    frames = []
    total = 60 + 30
    for i in range(total):
        f = base.copy()
        put_pet(f, img("nt%02d.png" % min(i, 59)), px, TB + 2)
        k = i - 12
        if k >= 0:
            a = min(1.0, (k + 1) / 4) if i < total - 5 else max(0.0, (total - i - 1) / 4)
            b = bub.copy()
            b.putalpha(b.getchannel("A").point(lambda v: int(v * a)))
            f.alpha_composite(b, (int(px - 60), int(TB - 19.5 * U - bub.size[1] + 10 + (1 - a) * 8)))
        frames.append(f)
    save_gif(frames, "notify.gif")


# ---------------------------------------------------------------- Claude & Codex cover (1280x640)
def label_chip(im, cx, y, text, col):
    d = ImageDraw.Draw(im)
    f = font("seguisb.ttf", 24)
    tw = d.textlength(text, font=f)
    d.rounded_rectangle((cx - tw / 2 - 18, y, cx + tw / 2 + 18, y + 40), 20, fill=col + (255,))
    d.text((cx - tw / 2, y + 5), text, font=f, fill=(255, 255, 255, 255))


def duo_cover():
    W, H, TB = 1280, 640, 520
    im = night(W, H, 120, avoid=(40, 60, 640, 470))
    d = ImageDraw.Draw(im)
    d.text((70, 96), "Клод и Кодекс", font=font("seguibl.ttf", 78), fill=(255, 255, 255, 255))
    d.text((74, 200), "Два пиксельных питомца на панели задач", font=font("seguisb.ttf", 28), fill=(236, 226, 218, 255))
    y = 262
    for col, line in [(CLAUDE, "Клод приносит уведомления Claude Code"),
                      (CODEX, "Кодекс — его зелёный и чуть глуповатый друг"),
                      ((240, 196, 64), "Дерутся, кидаются снежками, играют в прятки,"),
                      ((240, 196, 64), "  догонялки, гонки, чехарду и ещё 10 игр")]:
        if not line.startswith("  "):
            d.ellipse((78, y + 12, 88, y + 22), fill=col + (255,))
        d.text((102, y), line.strip(), font=font("segoeui.ttf", 23), fill=(200, 205, 222, 255))
        y += 38
    taskbar(im, TB, snow=False)
    # high five, scaled x2
    gaps = [int(l.split()[1]) for l in open(os.path.join(FR, "duo.txt"), encoding="utf-8")]
    fi = gaps.index(12) + 9
    cx0, gap = 1010, 15 * U * 2
    put_pet(im, img("da%03d.png" % fi), cx0 - gap / 2, TB + 4, 2.0)
    put_pet(im, img("db%03d.png" % fi), cx0 + gap / 2, TB + 4, 2.0)
    bub = img("b_five_dark.png")
    bub = bub.resize((int(bub.size[0] * 0.7), int(bub.size[1] * 0.7)), Image.LANCZOS)
    im.alpha_composite(bub, (int(cx0 - gap / 2 - 120), 150))
    label_chip(im, cx0 - gap / 2, TB + 12, "Клод", CLAUDE)
    label_chip(im, cx0 + gap / 2 + 10, TB + 12, "Кодекс", CODEX)
    im.convert("RGB").save(os.path.join(OUT, "duo.png"), optimize=True)


# ---------------------------------------------------------------- games grid
S = 0.85


def gpet(c, name, x, foot, sc=S):
    put_pet(c, img(name + ".png"), x, foot, sc)


def g_dust(c, x, f):
    o = Image.new("RGBA", c.size, (0, 0, 0, 0))
    od = ImageDraw.Draw(o)
    for _ in range(14):
        rx, ry, r = x + random.uniform(-60, 60), f - random.uniform(10, 70), random.uniform(10, 22)
        od.ellipse((rx - r, ry - r, rx + r, ry + r), fill=(215, 210, 205, 70))
    c.alpha_composite(o.filter(ImageFilter.GaussianBlur(2)))


def g_flag(c, x, f):
    dd = ImageDraw.Draw(c)
    dd.rectangle((x, f - 90, x + 3, f), fill=(200, 200, 205, 255))
    for i in range(4):
        for j in range(3):
            col = (240, 240, 240, 255) if (i + j) % 2 == 0 else (30, 30, 34, 255)
            dd.rectangle((x + 4 + i * 8, f - 90 + j * 8, x + 11 + i * 8, f - 83 + j * 8), fill=col)


def g_hide(c, x, f):
    bar_top = f - 6
    layer = Image.new("RGBA", c.size, (0, 0, 0, 0))
    gpet(layer, "c_idle", x + 60, bar_top + 7.2 * U * S)
    layer.paste((0, 0, 0, 0), (0, int(bar_top), c.size[0], c.size[1]))
    c.alpha_composite(layer)
    dd = ImageDraw.Draw(c)
    dd.rectangle((x - 140, bar_top, x + 140, bar_top + 22), fill=(28, 28, 32, 255))
    dd.line((x - 140, bar_top, x + 140, bar_top), fill=(60, 60, 68, 255))
    gpet(c, "e_cover", x - 60, bar_top)


def g_boo(c, x, f):
    gpet(c, "e_surprised", x - 45, f - 30)
    gpet(c, "c_happy", x + 45, f)
    ImageDraw.Draw(c).text((x + 62, f - 150), "БУ!", font=font("seguibl.ttf", 30), fill=(255, 220, 90, 255))


GAME_CARDS = [
    ("Догонялки", lambda c, x, f: (gpet(c, "e_walk", x - 55, f), gpet(c, "c_walk", x + 55, f))),
    ("Драка", lambda c, x, f: (g_dust(c, x, f), gpet(c, "e_brawl", x - 26, f), gpet(c, "c_brawl", x + 26, f))),
    ("Снежная битва", lambda c, x, f: (gpet(c, "n_windup", x - 62, f), gpet(c, "cn_hit", x + 62, f))),
    ("Гонки", lambda c, x, f: (g_flag(c, x + 112, f), gpet(c, "c_walk", x + 20, f), gpet(c, "e_walk", x - 78, f))),
    ("Прятки", g_hide),
    ("Чехарда", lambda c, x, f: (gpet(c, "c_duck", x + 10, f), gpet(c, "e_jump", x - 18, f - 46))),
    ("Кто выше прыгнет", lambda c, x, f: (gpet(c, "e_jump", x - 45, f - 70), gpet(c, "c_lookup", x + 55, f))),
    ("Дай пять", lambda c, x, f: (gpet(c, "e_high5", x - 31, f), gpet(c, "c_high5", x + 31, f))),
    ("Обнимашки", lambda c, x, f: (gpet(c, "e_hug", x - 23, f), gpet(c, "c_hug", x + 23, f))),
    ("Танец", lambda c, x, f: (gpet(c, "e_dance", x - 45, f), gpet(c, "c_dance", x + 45, f))),
    ("Тихий час", lambda c, x, f: (gpet(c, "e_sleep", x - 36, f), gpet(c, "c_sleep", x + 36, f))),
    ("Розыгрыш «БУ!»", g_boo),
    ("Перетягивание каната", lambda c, x, f: (gpet(c, "e_tug", x - 61, f), gpet(c, "c_tug", x + 61, f))),
    ("Гляделки", lambda c, x, f: (gpet(c, "e_stare", x - 36, f), gpet(c, "c_stare", x + 36, f))),
    ("Камень-ножницы-бумага", lambda c, x, f: (gpet(c, "e_rps", x - 46, f), gpet(c, "c_rps", x + 46, f))),
]


def games():
    cols, cw, ch = 5, 300, 250
    W, H = cols * cw + 80, 3 * ch + 150
    im = vgrad(W, H, (22, 30, 30), (30, 38, 40))
    d = ImageDraw.Draw(im)
    d.text((40, 32), "Во что играют Клод и Кодекс", font=font("seguibl.ttf", 40), fill=(255, 255, 255, 255))
    d.text((42, 86), "Игры начинаются сами каждые 15–40 секунд, а любую можно запустить из меню",
           font=font("segoeui.ttf", 20), fill=(180, 196, 192, 255))
    lf = font("seguisb.ttf", 20)
    for i, (label, draw) in enumerate(GAME_CARDS):
        x0, y0 = 40 + (i % cols) * cw, 130 + (i // cols) * ch
        snow = label == "Снежная битва"
        d.rounded_rectangle((x0 + 8, y0 + 8, x0 + cw - 8, y0 + ch - 8), 22,
                            fill=(48, 56, 86, 255) if snow else (40, 52, 52, 255),
                            outline=(80, 92, 130, 255) if snow else (62, 80, 78, 255))
        card = Image.new("RGBA", (cw - 20, ch - 20), (0, 0, 0, 0))
        draw(card, (cw - 20) / 2, ch - 82)
        mask = Image.new("L", card.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, card.size[0] - 1, card.size[1] - 1), 18, fill=255)
        card.putalpha(Image.composite(card.getchannel("A"), Image.new("L", card.size, 0), mask))
        im.alpha_composite(card, (x0 + 10, y0 + 10))
        tw = d.textlength(label, font=lf)
        d.text((x0 + cw / 2 - tw / 2, y0 + ch - 52), label, font=lf, fill=(232, 240, 236, 255))
    im.convert("RGB").save(os.path.join(OUT, "games.png"), optimize=True)


# ---------------------------------------------------------------- solo actions
def solo():
    cells = [("e_juggle", "Жонглирует"), ("e_zen", "Медитирует"), ("e_stretch", "Делает зарядку"), ("e_eat", "Ест печеньку"),
             ("c_juggle", "Жонглирует... почти"), ("c_spin", "Ловит свой хвост"), ("c_sneeze", "Чихает"), ("e_squash", "Пружинит от пола")]
    cols, cw, ch = 4, 290, 240
    W, H = cols * cw + 80, 2 * ch + 150
    im = vgrad(W, H, (26, 23, 21), (36, 31, 28))
    d = ImageDraw.Draw(im)
    d.text((40, 32), "Когда скучно одному", font=font("seguibl.ttf", 40), fill=(255, 255, 255, 255))
    d.text((42, 86), "А ещё машут друг другу, зевают, а если кинуть одного в другого — сбивает его как кеглю",
           font=font("segoeui.ttf", 20), fill=(190, 182, 176, 255))
    lf = font("seguisb.ttf", 20)
    for i, (name, label) in enumerate(cells):
        cx, cy = 40 + (i % cols) * cw, 130 + (i // cols) * ch
        cxd = name.startswith("c_")
        d.rounded_rectangle((cx + 8, cy + 8, cx + cw - 8, cy + ch - 8), 22,
                            fill=(40, 56, 52, 255) if cxd else (52, 46, 42, 255),
                            outline=(62, 92, 84, 255) if cxd else (72, 64, 58, 255))
        put_pet(im, img(name + ".png"), cx + cw / 2, cy + ch - 62)
        tw = d.textlength(label, font=lf)
        d.text((cx + cw / 2 - tw / 2, cy + ch - 50), label, font=lf, fill=(236, 230, 224, 255))
    im.convert("RGB").save(os.path.join(OUT, "solo.png"), optimize=True)


def duo_gif():
    W, H, TB = 760, 300, 250
    base = night(W, H, 40)
    taskbar(base, TB, snow=False)
    gaps = [int(l.split()[1]) for l in open(os.path.join(FR, "duo.txt"), encoding="utf-8")]
    frames = []
    for i, g in enumerate(gaps):
        f = base.copy()
        put_pet(f, img("da%03d.png" % i), W / 2 - g * U / 2, TB + 2)
        put_pet(f, img("db%03d.png" % i), W / 2 + g * U / 2, TB + 2)
        frames.append(f)
    save_gif(frames, "duo.gif")


hero()
duo_cover()
games()
solo()
duo_gif()
emotions()
notifications()
walk_gif()
snowball_gif()
notify_gif()
for fn in sorted(os.listdir(OUT)):
    print(fn, os.path.getsize(os.path.join(OUT, fn)) // 1024, "KB")
