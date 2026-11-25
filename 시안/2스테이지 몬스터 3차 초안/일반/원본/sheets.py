# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / battle) into 1.png ... 7.png, 일반 7안 비교.png and 전투 카메라 크기.png.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import NAMES, NAME, DESC, WHITE, LGREY, MGREY, SLATE, CHAR, ORANGE

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
W, H = 1920, 1080
OPTS = [str(i) for i in range(1, 8)]
stats = json.load(open(os.path.join(R, 'stats.json')))
marks = json.load(open(os.path.join(R, 'battle.json')))
shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')


def load(name, view):
    im = Image.open(os.path.join(R, f'{name}_{view}.png')).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    return im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def badge(d, x, y, n, size=64):
    d.rounded_rectangle((x, y, x + size, y + size), size // 5, fill=INK)
    d.text((x + size // 2, y + size // 2), n, font=F(int(size * 0.56), True), fill=CARD, anchor='mm')


def colours(n):
    cs = [tuple(c) for c in stats[n]['colours']]
    return [c for c in [ORANGE, WHITE, LGREY, MGREY, SLATE, CHAR] if c in cs]


def battle_crop(key, pad=10):
    m = marks[key]
    return shot.crop((int(m['left']) - pad, int(m['top']) - pad, int(m['right']) + pad, int(m['bottom']) + pad))


def option_sheet(n):
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    # left: the big 3/4 view
    d.rounded_rectangle((40, 40, 1230, 1040), 24, fill=CARD, outline=LINE, width=2)
    badge(d, 72, 72, n, 76)
    d.text((170, 110), NAME[n], font=F(50, True), fill=INK, anchor='lm')
    d.text((74, 180), DESC[n], font=F(24), fill=SUB, anchor='lm')
    big = load(n, 'big')
    f = min(1.0, 740 / big.height, 1080 / big.width)
    big = scaled(big, f)
    c.alpha_composite(big, (635 - big.width // 2, 1000 - big.height))
    # right: back 3/4, battle size, palette
    x0, x1 = 1260, 1880
    d.rounded_rectangle((x0, 40, x1, 470), 24, fill=CARD, outline=LINE, width=2)
    d.text((x0 + 28, 74), '뒤쪽 3/4', font=F(22, True), fill=SUB, anchor='lm')
    back = load(n, 'back'); back = scaled(back, min(1.0, 340 / back.height, 560 / back.width))
    c.alpha_composite(back, ((x0 + x1) // 2 - back.width // 2, 450 - back.height))

    d.rounded_rectangle((x0, 490, x1, 800), 24, fill=CARD, outline=LINE, width=2)
    d.text((x0 + 28, 524), '전투 화면 실제 크기 (오른쪽은 2배)', font=F(22, True), fill=SUB, anchor='lm')
    rob, me = battle_crop('robot_face'), battle_crop(f'{n}_face')
    floor = 770
    c.alpha_composite(rob, (x0 + 34, floor - rob.height))
    c.alpha_composite(me, (x0 + 44 + rob.width, floor - me.height))
    me2 = me.resize((me.width * 2, me.height * 2), Image.NEAREST)
    c.alpha_composite(me2, (x1 - 40 - me2.width, floor + 14 - me2.height))

    d.rounded_rectangle((x0, 820, x1, 1040), 24, fill=CARD, outline=LINE, width=2)
    d.text((x0 + 28, 854), '색', font=F(22, True), fill=SUB, anchor='lm')
    for i, col in enumerate(colours(n)):
        sx = x0 + 28 + i * 98
        d.rounded_rectangle((sx, 880, sx + 56, 936), 12, fill=col, outline=(205, 198, 186), width=1)
        d.text((sx, 944), NAMES[col], font=F(16), fill=INK)
        d.text((sx, 966), '#%02X%02X%02X' % col, font=F(14), fill=SUB)
    d.text((x0 + 28, 1012), f'렌더 메시 삼각형 약 {round(stats[n]["tris"], -3):,}개 (이미지 초안용, 게임 예산 아님)', font=F(17), fill=SUB, anchor='lm')
    c.convert('RGB').save(os.path.join(OUT, f'{n}.png'))


def compare():
    ims = {n: load(n, 'cmp') for n in ['robot', *OPTS]}
    gap = 26
    total = sum(i.width for i in ims.values()) + gap * (len(ims) - 1)
    f = min(1.0, (W - 120) / total, 640 / max(i.height for i in ims.values()))
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    d.text((60, 70), '일반 7안 비교 (3차)', font=F(44, True), fill=INK, anchor='lm')
    d.text((60, 124), '모두 같은 비율 · 왼쪽은 실제 근거리 로봇 (AttackRobot.fbx)', font=F(22), fill=SUB, anchor='lm')
    floor = 750
    d.rounded_rectangle((30, 180, W - 30, 1040), 24, fill=CARD, outline=LINE, width=2)
    d.line((60, floor + 4, W - 60, floor + 4), fill=LINE, width=2)
    x = (W - round(total * f)) // 2
    for n, im in ims.items():
        im = scaled(im, f)
        c.alpha_composite(im, (x, floor - im.height))
        cx = x + im.width // 2
        if n == 'robot':
            d.text((cx, floor + 40), '로봇', font=F(26, True), fill=INK, anchor='mm')
            d.text((cx, floor + 76), '근거리', font=F(18), fill=SUB, anchor='mm')
        else:
            badge(d, cx - 20, floor + 22, n, 40)
            d.text((cx, floor + 92), NAME[n], font=F(22, True), fill=INK, anchor='mm')
            d.rounded_rectangle((cx - 30, floor + 118, cx + 30, floor + 138), 8, fill=ORANGE)
        x += im.width + round(gap * f)
    c.convert('RGB').save(os.path.join(OUT, '일반 7안 비교.png'))


def battle():
    c = Image.new('RGBA', (W, H), (24, 40, 62)); d = ImageDraw.Draw(c)
    for x in range(-40, W + 40, 64): d.line((x, 0, x, H), fill=(38, 58, 86), width=2)
    for y in range(-20, H, 64): d.line((0, y, W, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    for k, m in marks.items():
        n, row = k.rsplit('_', 1)
        label = '로봇' if n == 'robot' else f'{n} {NAME[n]}'
        x, y = (m['left'] + m['right']) / 2, m['bottom'] + 10
        box = d.textbbox((x, y), label, font=F(17, True), anchor='mt')
        d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=(24, 40, 62, 230))
        d.text((x, y), label, font=F(17, True), fill=(240, 244, 250), anchor='mt')
    rows = {'walk': '길 따라 왼쪽으로 걷는 모습', 'face': '카메라 쪽으로 돈 모습'}
    for row, text in rows.items():
        y = sum(marks[f'{n}_{row}']['top'] for n in OPTS) / 7 - 44
        d.text((marks[f'7_{row}']['right'] + 40, y), text, font=F(20, True), fill=(110, 80, 50), anchor='rm')
    gp = sum(marks[f'{n}_walk']['px'] for n in OPTS if n != '7') / 6
    d.text((40, 1040), f'전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px · '
           f'로봇 약 {marks["robot_walk"]["px"]:.0f} px, 일반 약 {gp:.0f} px', font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
for n in OPTS: option_sheet(n)
compare()
battle()
