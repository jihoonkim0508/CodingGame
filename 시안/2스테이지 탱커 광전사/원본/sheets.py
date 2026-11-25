# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / battle) into the draft sheets: 1.png 2.png 3.png, the line-up with
# the real tank robot and round-2 tanks 1 and 5, and the battle-camera frame at true screen size.
# Views are rendered at their sheet size, so they are pasted 1:1 (only shrunk if one would not fit).
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import WHITE, LGREY, MGREY, CHAR, GUN, CRIMSON, SCARLET, GARNET, ORANGE, NAMES

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
NAVY, GRID = (24, 40, 62), (38, 58, 86)
W, H = 1920, 1080

OPTS = [  # key, number, name, one line, colours (accent first)
    ('b1', 1, '뿔투구 광전사', '뿔 달린 투구 아래 찡그린 눈, 붉은 어깨 갑옷을 걸치고 큰 도끼를 땅에 끌며 다가온다',
     [CRIMSON, WHITE, LGREY, MGREY, CHAR]),
    ('b2', 2, '쌍날도끼 광전사', '건메탈 중갑에 붉은 볏, 양날 도끼를 어깨 위로 치켜들고 내려칠 자세',
     [SCARLET, GUN, LGREY, MGREY, CHAR]),
    ('b3', 3, '거대 견갑 광전사', '한쪽 어깨만 겹겹이 두른 거대 견갑, 사슬로 손목에 묶은 큰 식칼을 앞에 낮게 든다',
     [GARNET, WHITE, LGREY, MGREY, CHAR]),
]
OLD = {'t1': (1, '방패 돌격병', ORANGE), 't5': (5, '중갑 기사', CRIMSON)}
BATTLE = json.load(open(os.path.join(R, 'battle.json')))
PAIRS = json.load(open(os.path.join(R, 'pairs.json')))


def load(name, view):
    im = Image.open(os.path.join(R, f'{name}_{view}.png')).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    if abs(f - 1) < 1e-3: return im
    return im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def navy(size):
    c = Image.new('RGBA', size, NAVY); d = ImageDraw.Draw(c)
    for x in range(-40, size[0] + 40, 64): d.line((x, 0, x, size[1]), fill=GRID, width=2)
    for y in range(-20, size[1], 64): d.line((0, y, size[0], y), fill=GRID, width=2)
    return c


def swatches(d, x, y, cols):
    for i, c in enumerate(cols):
        sx = x + i * 196
        d.rounded_rectangle((sx, y, sx + 52, y + 52), 12, fill=c, outline=(205, 198, 186), width=1)
        d.text((sx + 64, y + 4), NAMES[c], font=F(17, True), fill=INK)
        d.text((sx + 64, y + 30), '#%02X%02X%02X' % c, font=F(14), fill=SUB)


def badge(d, x, y, n, size=48):
    d.rounded_rectangle((x, y, x + size, y + size), 12, fill=INK)
    d.text((x + size / 2, y + size / 2), str(n), font=F(int(size * 0.58), True), fill=CARD, anchor='mm')


def pair_crop(key):
    """The tank robot and this berserker through the battle camera, at true size, over the stage's navy grid."""
    shot = Image.open(os.path.join(R, f'pair_{key}.png')).convert('RGBA')
    full = navy(shot.size); full.alpha_composite(shot)
    boxes = [PAIRS[key][k]['box'] for k in ('robot_tank', key)]
    x0 = min(b[0] for b in boxes) - 18; x1 = max(b[2] for b in boxes) + 18
    y0 = min(b[1] for b in boxes) - 16; y1 = max(b[3] for b in boxes) + 16
    return full.crop(tuple(round(v) for v in (x0, y0, x1, y1)))


def option_sheets():
    big = {k: load(k, 'q34') for k, *_ in OPTS}
    floor, top = 900, 196
    f = min(1, (floor - top) / max(i.height for i in big.values()), 1260 / max(i.width for i in big.values()))
    small = [load(k, v) for k, *_ in OPTS for v in ('side', 'back')]
    f2 = min(1.25, 270 / max(i.height for i in small), 210 / max(i.width for i in small))
    for key, num, name, line, cols in OPTS:
        c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
        badge(d, 60, 44, num, 64)
        d.text((144, 76), name, font=F(50, True), fill=INK, anchor='lm')
        d.text((146, 136), line, font=F(22), fill=SUB, anchor='lm')
        d.rounded_rectangle((40, 170, 1380, 1040), 22, fill=CARD, outline=LINE, width=2)
        im = scaled(big[key], f)
        c.alpha_composite(im, (710 - im.width // 2, floor - im.height))
        d.text((710, floor + 12), '3/4 정면', font=F(16), fill=SUB, anchor='mt')
        d.line((70, 942, 1350, 942), fill=LINE, width=2)
        swatches(d, 72, 964, cols)
        # right: side and 3/4 back, then the battle-size crop beside the real tank robot
        d.rounded_rectangle((1410, 170, 1880, 1040), 22, fill=CARD, outline=LINE, width=2)
        for (v, lab), cx in ((('side', '측면'), 1535), (('back', '뒤 3/4'), 1755)):
            sm = scaled(load(key, v), f2)
            c.alpha_composite(sm, (cx - sm.width // 2, 500 - sm.height))
            d.text((cx, 512), lab, font=F(16), fill=SUB, anchor='mt')
        d.line((1440, 570, 1850, 570), fill=LINE, width=2)
        crop = pair_crop(key)
        if crop.width > 444: crop = crop.crop(((crop.width - 444) // 2, 0, (crop.width - 444) // 2 + 444, crop.height))
        px, py = 1645 - crop.width // 2, 650
        mask = Image.new('L', crop.size, 0); ImageDraw.Draw(mask).rounded_rectangle((0, 0, crop.width - 1, crop.height - 1), 14, fill=255)
        c.paste(crop, (px, py), mask)
        d.text((1645, 612), '전투 화면 실제 크기 (1:1)', font=F(18, True), fill=INK, anchor='mm')
        h_r, h_b = PAIRS[key]['robot_tank']['h'], PAIRS[key][key]['h']
        d.text((1645, py + crop.height + 16), f'아군 탱커 로봇 {round(h_r)} px · 이 광전사 {round(h_b)} px', font=F(16), fill=SUB, anchor='mt')
        d.text((1645, py + crop.height + 42), f'로봇 높이의 {h_b / h_r:.2f}배', font=F(16), fill=SUB, anchor='mt')
        c.convert('RGB').save(os.path.join(OUT, f'{num}.png'))


def comparison():
    """One row at one scale: the real tank robot, round-2 tanks 1 and 5, then the three berserkers."""
    row = ['robot_tank', 't1', 't5'] + [k for k, *_ in OPTS]
    q = {n: load(n, 'cmp') for n in row}
    gap = 34
    f = min(1, (W - 80 - gap * (len(row) - 1)) / sum(i.width for i in q.values()))
    ims = {n: scaled(q[n], f) for n in row}
    c = Image.new('RGBA', (W, 840), BG); d = ImageDraw.Draw(c)
    d.text((60, 70), '광전사 3안 비교', font=F(46, True), fill=INK, anchor='lm')
    d.text((62, 126), '모두 같은 비율 · 3/4 정면 · 실제 탱커 로봇, 2차 초안에서 고른 탱커 1·5와 한 줄로', font=F(21), fill=SUB, anchor='lm')
    d.rounded_rectangle((20, 180, W - 20, 750), 22, fill=CARD, outline=LINE, width=2)
    floor = 570
    x = (W - sum(i.width for i in ims.values()) - gap * (len(row) - 1)) // 2
    acc = {k: (num, name, cols[0]) for k, num, name, _, cols in OPTS}
    for n in row:
        im = ims[n]; cx = x + im.width // 2
        c.alpha_composite(im, (x, floor - im.height))
        if n == 'robot_tank':
            d.text((cx, floor + 36), '아군 탱커 로봇', font=F(22, True), fill=SUB, anchor='mm')
            d.text((cx, floor + 70), '실제 모델', font=F(17), fill=SUB, anchor='mm')
        else:
            num, name, a = OLD[n] if n in OLD else acc[n]
            label = f'기존 {num}  {name}' if n in OLD else f'{num}  {name}'
            font = F(22, True) if n not in OLD else F(20, True)
            tw = d.textlength(label, font=font)
            x0 = cx - (tw + 26) / 2
            d.ellipse((x0, floor + 28, x0 + 16, floor + 44), fill=a)
            d.text((x0 + 26, floor + 36), label, font=font, fill=INK if n not in OLD else SUB, anchor='lm')
            sub = '2차 초안' if n in OLD else f'전투 화면 {round(BATTLE[n]["h"])} px · 로봇의 {BATTLE[n]["h"] / BATTLE["robot_tank"]["h"]:.2f}배'
            d.text((cx, floor + 70), sub, font=F(17), fill=SUB, anchor='mm')
        x += im.width + gap
    # a thin rule between the round-2 picks and the new ones
    xs = (W - sum(i.width for i in ims.values()) - gap * (len(row) - 1)) // 2 + sum(ims[n].width + gap for n in row[:3]) - gap // 2
    d.line((xs, 230, xs, 700), fill=LINE, width=2)
    d.text((60, 795), '아군 탱커 로봇 전투 화면 높이 140 px 기준 · 2차 초안 탱커 1은 116 px, 5는 106 px', font=F(17), fill=SUB, anchor='lm')
    c.convert('RGB').save(os.path.join(OUT, '광전사 3안 비교.png'))


def battle():
    """The battle-camera render over the stage's navy grid, names and on-screen heights under each."""
    shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
    c = navy((W, H)); d = ImageDraw.Draw(c)
    c.alpha_composite(shot)
    names = {k: f'{num} {name}' for k, num, name, *_ in OPTS}
    names.update({'robot_melee': '아군 근접 로봇', 'robot_tank': '아군 탱커 로봇'})
    for k, m in BATTLE.items():
        x0, y0, x1, y1 = m['box']
        x, y = (x0 + x1) / 2, y1 + 14
        for txt, font, dy in ((names[k], F(17, True), 0), (f'{round(m["h"])} px', F(14), 26)):
            box = d.textbbox((x, y + dy), txt, font=font, anchor='mt')
            d.rounded_rectangle((box[0] - 9, box[1] - 5, box[2] + 9, box[3] + 6), 8, fill=(24, 40, 62, 230))
            d.text((x, y + dy), txt, font=font, fill=(240, 244, 250) if dy == 0 else (190, 204, 222), anchor='mt')
    d.text((40, 1040), '전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px · px = 화면에서 차지하는 세로 길이',
           font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
option_sheets()
comparison()
battle()
