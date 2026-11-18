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
                 "Новогодний режим: шапочка, снег на панели задач и снежки"]:
        d.ellipse((88, y + 12, 98, y + 22), fill=CLAUDE + (255,))
        d.text((112, y), line, font=font("segoeui.ttf", 22), fill=(200, 205, 222, 255))
        y += 38
    chip_y = 430
    x = 84
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    od = ImageDraw.Draw(ov)
    f = font("seguisb.ttf", 18)
    for chip in ["~90 КБ", "~10 МБ RAM", "без установки", "Windows 10 / 11"]:
        tw = d.textlength(chip, font=f)
        od.rounded_rectangle((x, chip_y, x + tw + 28, chip_y + 36), 18, fill=(255, 255, 255, 28), outline=(255, 255, 255, 70))
        x += tw + 40
    im.alpha_composite(ov)
    x = 84
    for chip in ["~90 КБ", "~10 МБ RAM", "без установки", "Windows 10 / 11"]:
        tw = d.textlength(chip, font=f)
        d.text((x + 14, chip_y + 6), chip, font=f, fill=(235, 238, 248, 255))
        x += tw + 40

    taskbar(im, TB)
    # pet throwing, ball arc, splat
    px = 960
    states = [l.split()[1] for l in open(os.path.join(FR, "throw.txt"), encoding="utf-8")]
    put_pet(im, img("th%02d.png" % (states.index("Throw") + 2)), px, TB + 2)
    hx, hy = px + 6.5 * U + 6, TB - 13 * U
    tx, ty = 1180, 200
    pts = [ball_pos(hx, hy, tx, ty, 0.62, 3000, t / 100 * 0.62) for t in range(0, 101)]
    trail = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(trail)
    for i in range(55, 92, 4):
        x, y = pts[i]
        r = 3 + (i - 55) / 12
        td.ellipse((x - r, y - r, x + r, y + r), fill=(235, 242, 255, int(40 + (i - 55) * 3)))
    im.alpha_composite(trail.filter(ImageFilter.GaussianBlur(1.2)))
    bx, by = pts[93]
    put_center(im, img("ball.png"), bx, by)
    # second, earlier snowball already splatted top-right
    put_center(im, img("sp0_06.png"), 1190, 92)
    # bubble above the pet
    bub = img("b_done_dark.png")
    bub = bub.resize((int(bub.size[0] * 0.62), int(bub.size[1] * 0.62)), Image.LANCZOS)
    im.alpha_composite(bub, (int(px - 50), int(TB - 19.5 * U - bub.size[1] + 10)))
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


hero()
emotions()
notifications()
walk_gif()
snowball_gif()
notify_gif()
for fn in sorted(os.listdir(OUT)):
    print(fn, os.path.getsize(os.path.join(OUT, fn)) // 1024, "KB")
