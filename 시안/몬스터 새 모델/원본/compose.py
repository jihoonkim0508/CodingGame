"""Turns build_monsters.py's raw renders into the mockup sheets in 시안/몬스터 새 모델/.

    python compose.py [--work DIR] [--shot SCREENSHOT.png]

- <stem>.png: each model's 3/4-front hero shot on the navy background, with glow from its emission-only render.
- 시안1_버그군단_인게임.png / 시안2_글리치_인게임.png: the battle-camera renders, outlined like the game's toon outline,
  stood on the road of the in-game screenshot (the old monsters painted out) at the game's scale (42 px per unit).
- 한눈에 비교.png: the eight hero shots, roles across, directions down, a short concept label under each.
"""
import json, os, sys, tempfile
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.dirname(HERE)
ARGS = sys.argv[1:]


def arg(name, default):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else default


WORK = arg('--work', os.path.join(tempfile.gettempdir(), 'monster_mockups'))
SHOT = arg('--shot', os.path.join(OUT, '..', '..', 'Report', '2026-11-19_탱커 몬스터 모델 적용.png'))
FONT, FONT_B = r"C:\Windows\Fonts\malgun.ttf", r"C:\Windows\Fonts\malgunbd.ttf"

DIRS = [('b', '시안1_버그군단', '시안1 버그 군단'), ('g', '시안2_글리치', '시안2 글리치 오류 코드')]
ROLES = [('normal', '일반', '일반'), ('fast', '빠른적', '빠른 적'), ('tank', '탱커병', '탱커병'), ('boss', '보스', '보스')]
LABELS = {
    'b_normal': '등껍질에 { } 괄호가 빛나는 딱정벌레',
    'b_fast': '제트 노즐 두 개로 나는 모기 드론',
    'b_tank': '뿔 달린 장갑 공벌레, 판 틈이 빛남',
    'b_boss': '금관 쓴 여왕 버그, 배에 </>',
    'g_normal': '빨간 제목줄 X 오류 창 큐브',
    'g_fast': '∞ 무한루프 고리와 번개 드론',
    'g_tank': '불타는 방화벽 벽돌 골렘, 자물쇠',
    'g_boss': '블루스크린 :( 404 모니터 거신',
}
# the old monsters in the screenshot (painted out) and where the new ones stand: feet point on the road, 1080p px
OLD = [(528, 260, 596, 354), (262, 396, 334, 494), (310, 378, 418, 503), (474, 395, 564, 490)]
PLACES = [('boss', 322, 470), ('tank', 488, 474), ('normal', 566, 346), ('normal', 690, 336), ('fast', 880, 636)]


def load(name):
    return Image.open(os.path.join(WORK, name)).convert('RGBA')


def glow(img, radii, weights):
    """Blurred, premultiplied copy of an emission render (float RGB 0..1)."""
    a = np.asarray(img, np.float32) / 255
    pre = Image.fromarray(np.uint8(a[..., :3] * a[..., 3:4] * 255))
    return sum(w * np.asarray(pre.filter(ImageFilter.GaussianBlur(r)), np.float32) / 255 for r, w in
               zip(radii, weights))


def screen(img, g):
    b = np.asarray(img.convert('RGB'), np.float32) / 255
    return Image.fromarray(np.uint8(np.clip(1 - (1 - b) * (1 - np.clip(g, 0, 1)), 0, 1) * 255 + 0.5))


def navy(size):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)
    t = np.clip(np.hypot(x - 0.5, (y - 0.42) * 1.1) / 0.75, 0, 1)[..., None]
    c0, c1 = np.array([40, 60, 92], np.float32), np.array([20, 31, 48], np.float32)
    return Image.fromarray(np.uint8(c0 * (1 - t) + c1 * t)).convert('RGBA')


def hero(mid):
    base = Image.alpha_composite(navy(1024), load(mid + '_hero.png'))
    return screen(base, glow(load(mid + '_hero_emit.png'), (4, 12, 30), (0.7, 0.5, 0.3)))


def sprite(mid):
    """Battle render at 1/4 size: shadow, the game's dark outline, model; plus its glow and the feet anchor."""
    info = json.load(open(os.path.join(WORK, mid + '.json'), encoding='utf-8'))['battle']
    A = np.asarray(load(mid + '_battle.png'), np.float32) / 255
    E = load(mid + '_battle_emit.png')
    cov = np.asarray(E, np.float32)[..., 3] / 255
    shadow = A.copy()
    n = A.shape[0]
    edge = np.minimum.outer(*[np.minimum(np.arange(n), np.arange(n)[::-1]) / (0.12 * n)] * 2)
    shadow[..., 3] *= (1 - cov) * 0.8 * np.clip(edge, 0, 1)  # softer than Cycles' catcher, faded before the frame
    shadow[..., :3] = 0.08
    model = A.copy()
    model[..., 3] = np.minimum(A[..., 3], cov)
    ring = np.asarray(Image.fromarray(np.uint8(cov * 255)).filter(ImageFilter.MaxFilter(13)), np.float32) / 255
    line = np.zeros_like(A)
    line[..., :3] = np.array([24, 18, 32]) / 255
    line[..., 3] = ring
    out = Image.new('RGBA', (A.shape[1], A.shape[0]))
    for layer in (shadow, line, model):
        out.alpha_composite(Image.fromarray(np.uint8(np.clip(layer, 0, 1) * 255)))
    s = out.size[0] // 4
    small = out.convert('RGBa').resize((s, s), Image.LANCZOS).convert('RGBA')
    g = glow(E.convert('RGBa').resize((s, s), Image.LANCZOS).convert('RGBA'), (1.5, 5), (0.55, 0.3))
    top = np.argmax(np.asarray(Image.fromarray(np.uint8(cov * 255)).resize((s, s)))
                    .max(axis=1) > 128)
    return small, g, (info['anchor'][0] / 4, info['anchor'][1] / 4), top


def erase_old(img):
    """Paint the old monsters (greys, red feet, cyan bars) out of the screenshot from the road and wood beside them."""
    a = np.asarray(img.convert('RGB'), np.float32)
    for x0, y0, x1, y1 in OLD:
        X0, X1 = max(x0 - 40, 0), min(x1 + 40, a.shape[1])  # look a little wider for road/wood to fill from
        c = a[y0:y1, X0:X1].copy()
        inner = np.zeros(c.shape[:2], bool)
        inner[:, x0 - X0:x1 - X0] = True
        mx, mn = c.max(-1), c.min(-1)
        sat = (mx - mn) / np.maximum(mx, 1)
        r, g, b = c[..., 0], c[..., 1], c[..., 2]
        warm = (r >= g) & (g >= b) & (r - b > 32) & (sat > 0.15) & (mx > 140) & (g > 0.45 * r)  # cream road, wood
        keep = (g > r + 10) & (g > b + 10)  # the cactus
        mask = (np.asarray(Image.fromarray(np.uint8(~warm * 255)).filter(ImageFilter.MaxFilter(7))) > 0) & ~keep & inner
        bar = np.zeros_like(mask)
        cyan = (g > 170) & (b > 170) & (r < 150) & inner
        if cyan.any():  # the old health bar, dark border included
            ys, xs = np.nonzero(cyan)
            bar[max(ys.min() - 4, 0):ys.max() + 5, max(xs.min() - 4, 0):xs.max() + 5] = True
        mask |= bar
        anchor = warm & ~mask
        v = c.copy()
        xs, ys = np.arange(c.shape[1]), np.arange(c.shape[0])
        for y in ys:  # road edges and wood grain run across the screen: fill each row from road/wood
            m = mask[y] & ~bar[y]
            if m.any() and anchor[y].any():
                for ch in range(3):
                    v[y, m, ch] = np.interp(xs[m], xs[anchor[y]], c[y, anchor[y], ch])
        for x in np.nonzero(bar.any(0))[0]:  # a thin bar lying on the road's edge: fill it from above and below
            m, an = bar[:, x], anchor[:, x]
            if an.any():
                for ch in range(3):
                    v[m, x, ch] = np.interp(ys[m], ys[an], c[an, x, ch])
        a[y0:y1, X0:X1] = v
    return Image.fromarray(np.uint8(np.clip(a, 0, 255))).convert('RGBA')


def health_bar(d, cx, y, w):
    d.rounded_rectangle((cx - w / 2 - 2, y - 2, cx + w / 2 + 2, y + 5), 3, fill=(14, 36, 54))
    d.rounded_rectangle((cx - w / 2, y, cx + w / 2, y + 3), 2, fill=(84, 228, 240))


def ingame(prefix):
    scene = erase_old(Image.open(SHOT))
    g = np.zeros((scene.height, scene.width, 3), np.float32)
    bars = []
    for role, x, y in sorted(PLACES, key=lambda p: p[2]):
        spr, gl, (ax, ay), top = sprite(f'{prefix}_{role}')
        px, py = int(round(x - ax)), int(round(y - ay))
        if role == 'boss':  # the game's violet boss aura, roughly
            aura = Image.new('L', scene.size)
            ImageDraw.Draw(aura).ellipse((x - 85, y - 34, x + 85, y + 30), fill=150)
            aura = np.asarray(aura.filter(ImageFilter.GaussianBlur(18)), np.float32)[..., None] / 255
            g += aura * np.array([0.55, 0.2, 0.95], np.float32) * 0.4
        scene.alpha_composite(spr, (px, py))
        h, w = gl.shape[:2]
        g[py:py + h, px:px + w] += gl[:max(0, min(h, scene.height - py)), :max(0, min(w, scene.width - px))]
        bars.append((x, py + top - 12, {'normal': 40, 'fast': 36, 'tank': 52, 'boss': 84}[role]))
    out = screen(scene, g)
    d = ImageDraw.Draw(out)
    for cx, by, w in bars:
        health_bar(d, cx, by, w)
    return out


def sheet(heroes):
    cell, gap, left, head, lab = 400, 18, 230, 64, 56
    W = left + 4 * cell + 5 * gap
    H = head + 2 * (cell + lab) + 3 * gap
    im = Image.new('RGB', (W, H), (15, 23, 36))
    d = ImageDraw.Draw(im)
    fb, fl, fr = ImageFont.truetype(FONT_B, 30), ImageFont.truetype(FONT, 22), ImageFont.truetype(FONT_B, 28)
    for c, (_, _, title) in enumerate(ROLES):
        x = left + gap + c * (cell + gap) + cell / 2
        d.text((x, head / 2 + 4), title, font=fb, fill=(232, 238, 247), anchor='mm')
    for r, (p, _, title) in enumerate(DIRS):
        y = head + gap + r * (cell + lab + gap)
        lines = title.split(' ', 1)
        d.text((left / 2 + 4, y + cell / 2 - 20), lines[0], font=fr, fill=(232, 238, 247), anchor='mm')
        d.text((left / 2 + 4, y + cell / 2 + 20), lines[1], font=fr, fill=(255, 120, 200) if p == 'b' else
               (255, 110, 110), anchor='mm')
        for c, (role, _, _) in enumerate(ROLES):
            x = left + gap + c * (cell + gap)
            im.paste(heroes[f'{p}_{role}'].resize((cell, cell), Image.LANCZOS), (x, y))
            d.text((x + cell / 2, y + cell + lab / 2), LABELS[f'{p}_{role}'], font=fl, fill=(190, 204, 224),
                   anchor='mm')
    return im


if __name__ == '__main__':
    heroes = {}
    for p, stem, _ in DIRS:
        for role, name, _ in ROLES:
            heroes[f'{p}_{role}'] = hero(f'{p}_{role}')
            heroes[f'{p}_{role}'].save(os.path.join(OUT, f'{stem}_{name}.png'))
        ingame(p).convert('RGB').save(os.path.join(OUT, f'{stem}_인게임.png'))
    sheet(heroes).save(os.path.join(OUT, '한눈에 비교.png'))
    print('saved to', OUT)
