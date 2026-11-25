# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / battle) into the draft sheets: 1.png … 7.png, the 7-way comparison
# and the battle-camera sheet.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import OPTIONS

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
ROAD, NAVY = (250, 236, 200), (24, 40, 62)
W, H = 1920, 1080


def load(name, view):
    im = Image.open(os.path.join(R, f'{name}_{view}.png')).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f, nearest=False):
    return im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.NEAREST if nearest else Image.LANCZOS)


def hexs(c):
    return '#%02X%02X%02X' % c


Q = {k: load(k, 'q34') for k, *_ in OPTIONS}
S = {k: load(k, 'side') for k, *_ in OPTIONS}
ROBOT = load('robot', 'q34')
shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
marks = json.load(open(os.path.join(R, 'battle.json')))


def battle_crop(key, pad=14):
    x0, y0, x1, y1 = marks[key]['box']
    box = (int(x0) - pad, int(y0) - pad, int(x1) + pad, int(y1) + pad)
    c = Image.new('RGBA', (box[2] - box[0], box[3] - box[1]), ROAD)
    c.alpha_composite(shot.crop(box))
    return c


def option_sheet(key, num, name, desc, acc, acc_name):
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    # caption: number, name, one line, accent chip
    d.rounded_rectangle((60, 40, 140, 120), 18, fill=INK)
    d.text((100, 80), str(num), font=F(52, True), fill=CARD, anchor='mm')
    d.text((165, 52), name, font=F(50, True), fill=INK)
    d.text((168, 128), desc, font=F(24), fill=SUB)
    d.rounded_rectangle((1490, 58, 1546, 114), 14, fill=acc, outline=(205, 198, 186), width=1)
    d.text((1562, 64), f'포인트 색 · {acc_name}', font=F(22, True), fill=INK)
    d.text((1562, 94), hexs(acc), font=F(18), fill=SUB)
    # big 3/4 view (one scale for all seven sheets)
    d.rounded_rectangle((40, 180, 1220, 1040), 26, fill=CARD, outline=LINE, width=2)
    f = min(1080 / max(i.width for i in Q.values()), 720 / max(i.height for i in Q.values()))
    big = scaled(Q[key], f)
    c.alpha_composite(big, (630 - big.width // 2, 610 - big.height // 2))
    d.text((630, 1000), '3/4 뷰 · 진행 방향 →', font=F(20), fill=SUB, anchor='mm')
    # side view (one scale for all seven)
    d.rounded_rectangle((1250, 180, 1880, 560), 26, fill=CARD, outline=LINE, width=2)
    f2 = min(560 / max(i.width for i in S.values()), 260 / max(i.height for i in S.values()))
    sd = scaled(S[key], f2)
    c.alpha_composite(sd, (1565 - sd.width // 2, 470 - sd.height))
    d.line((1290, 472, 1840, 472), fill=LINE, width=2)
    d.text((1565, 520), '측면 · 진행 방향 →', font=F(20), fill=SUB, anchor='mm')
    # the battle camera at real size: the robot and this one, then 2x
    d.rounded_rectangle((1250, 590, 1880, 1040), 26, fill=NAVY)
    d.text((1280, 618), '전투 화면 실제 크기', font=F(22, True), fill=(236, 242, 250))
    rb, mb = battle_crop('robot2.6' if num <= 4 else 'robot-1.6'), battle_crop(key)
    y = 660 + max(rb.height, mb.height)
    c.alpha_composite(rb, (1290, y - rb.height)); c.alpha_composite(mb, (1290 + rb.width + 24, y - mb.height))
    d.text((1290 + rb.width // 2, y + 8), '로봇 (1배)', font=F(16), fill=(190, 204, 222), anchor='mt')
    d.text((1290 + rb.width + 24 + mb.width // 2, y + 8), f'{num}안 (1배)', font=F(16), fill=(190, 204, 222), anchor='mt')
    k = min(2, 560 / mb.width, (1000 - (y + 44)) / mb.height)
    z = scaled(mb, k, nearest=True)
    c.alpha_composite(z, (1565 - z.width // 2, y + 44))
    d.text((1860, 1026), f'{num}안 {k:.1f}배 확대', font=F(16), fill=(190, 204, 222), anchor='rs')
    c.convert('RGB').save(os.path.join(OUT, f'{num}.png'))


def comparison():
    """All seven 3/4 views at one true scale, the real robot leading each row."""
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    d.text((60, 34), '빠른 적 7안 비교', font=F(44, True), fill=INK)
    d.text((62, 98), '모두 같은 비율 · 왼쪽은 실제 근접 로봇 (AttackRobot.fbx + 텍스처)', font=F(20), fill=SUB)
    robw, cellw, rowh, top = 250, (W - 60 - 250 - 20) // 4, 450, 140
    f = min((cellw - 30) / max(i.width for i in Q.values()), 300 / ROBOT.height)
    rob = scaled(ROBOT, f)
    for row in range(2):
        y0 = top + row * rowh; floor = y0 + 360
        d.rounded_rectangle((30, y0, W - 30, y0 + rowh - 20), 22, fill=CARD, outline=LINE, width=2)
        c.alpha_composite(rob, (30 + robw // 2 - rob.width // 2, floor - rob.height))
        d.text((30 + robw // 2, floor + 18), '로봇', font=F(20, True), fill=SUB, anchor='mt')
        for j, (key, num, name, _, acc, _) in enumerate(OPTIONS[row * 4:row * 4 + 4]):
            im = scaled(Q[key], f)
            cx = 30 + robw + cellw * j + cellw // 2
            c.alpha_composite(im, (cx - im.width // 2, floor - im.height))
            d.rounded_rectangle((cx - 120, floor + 12, cx - 96, floor + 36), 6, fill=acc)
            d.text((cx - 86, floor + 24), f'{num}  {name}', font=F(22, True), fill=INK, anchor='lm')
    c.convert('RGB').save(os.path.join(OUT, '빠른 적 7안 비교.png'))


def battle():
    """The battle-camera render over the stage's navy grid, names under each."""
    c = Image.new('RGBA', (W, H), NAVY); d = ImageDraw.Draw(c)
    for x in range(-40, W + 40, 64): d.line((x, 0, x, H), fill=(38, 58, 86), width=2)
    for y in range(-20, H, 64): d.line((0, y, W, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    names = {k: f'{n} {name}' for k, n, name, *_ in OPTIONS}
    for k, m in marks.items():
        x0, y0, x1, y1 = m['box']
        label = names.get(k, '로봇')
        x, y = (x0 + x1) / 2, y1 + 10
        box = d.textbbox((x, y), label, font=F(17, True), anchor='mt')
        d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=NAVY + (230,))
        d.text((x, y), label, font=F(17, True), fill=(240, 244, 250), anchor='mt')
    d.text((40, 1040), '전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px · 모두 길을 따라 왼쪽으로 달리는 방향',
           font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
for o in OPTIONS: option_sheet(*o)
comparison()
battle()
