# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / compare / battle / pairs) into the round-3 tank sheets:
# 1.png … 7.png, 탱커병 7안 비교.png and 전투 카메라 크기.png.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import WHITE, LGREY, MGREY, CHAR, GUN, AMBER, EYE, NAMES

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
W, H = 1920, 1080

OPTS = [  # number, name, one line, colours by area (largest first)
    (1, '철방패 거병', '키만 한 곡면 방패를 어깨로 밀며 들어오는 뿔 투구 거병, 방패 끝을 세 갈퀴 손으로 쥔다', [WHITE, GUN, AMBER, LGREY, MGREY]),
    (2, '공성 코뿔소', '등에 다섯 겹 판갑을 두르고 큰 뿔을 앞세워 돌진하는 네 발 공성 짐승', [WHITE, GUN, LGREY, MGREY, AMBER]),
    (3, '철띠 골렘', '쇠띠로 묶은 바윗덩이 골렘, 팔목의 앰버 띠와 가슴에 박힌 작은 코어', [LGREY, MGREY, WHITE, GUN, AMBER]),
    (4, '강철 전갈', '두 집게를 방패처럼 맞대고 앰버 독침 꼬리를 등 위로 치켜든 강철 전갈', [GUN, WHITE, MGREY, LGREY, AMBER]),
    (5, '철완 오우거', '강철 건틀릿 두 개로 땅을 짚고 걷는 흰 가면 오우거, 엄니와 외눈', [GUN, WHITE, LGREY, AMBER, MGREY]),
    (6, '흑철 기사', '뿔 투구와 겹친 어깨판, 전투 망치를 땅에 세운 흑철 중갑 기사', [GUN, WHITE, LGREY, AMBER]),
    (7, '갑주 공벌레', '일곱 겹 등판과 불도저 머리판으로 길을 밀고 오는 갑주 공벌레', [WHITE, GUN, LGREY, MGREY, AMBER]),
]
INFO = json.load(open(os.path.join(R, 'info.json')))
CMP = json.load(open(os.path.join(R, 'compare.json')))['marks']
BAT = json.load(open(os.path.join(R, 'battle.json')))
PAIR = json.load(open(os.path.join(R, 'pairs.json')))
ROBOT_UNITS = CMP['robot_tank_a']['height_units']


def load(name):
    im = Image.open(os.path.join(R, name)).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    return im if abs(f - 1) < 1e-3 else im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def card(d, box):
    d.rounded_rectangle(box, 24, fill=CARD, outline=LINE, width=2)


def hexs(c):
    return '#%02X%02X%02X' % c


def on_road(im, bg=(250, 236, 200)):
    """The battle renders are transparent off the desk: put them on the stage's road colour."""
    c = Image.new('RGBA', im.size, bg + (255,)); c.alpha_composite(im); return c


def option_sheets():
    for num, name, line, cols in OPTS:
        n = f't{num}'
        c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
        # header
        d.rounded_rectangle((40, 34, 124, 118), 18, fill=INK)
        d.text((82, 74), str(num), font=F(54, True), fill=CARD, anchor='mm')
        d.text((148, 56), name, font=F(48, True), fill=INK, anchor='lm')
        d.text((150, 104), line, font=F(23), fill=SUB, anchor='lm')
        # big 3/4 front, shown 1:1 (outline 2 px) unless it has to shrink to fit
        card(d, (40, 146, 1270, 1046))
        big = load(f'{n}_q34.png')
        f = min(1.0, 820 / big.height, 1150 / big.width)
        big = scaled(big, f)
        floor = 990
        c.alpha_composite(big, (655 - big.width // 2, floor - big.height))
        d.text((655, floor + 18), '3/4 정면', font=F(18), fill=SUB, anchor='mt')
        # right column: 3/4 back, battle-size crop beside the real tank robot, facts
        card(d, (1296, 146, 1880, 1046))
        back = load(f'{n}_back.png')
        back = scaled(back, min(1.0, 230 / back.height, 520 / back.width))
        d.text((1320, 166), '뒤 3/4', font=F(19, True), fill=SUB)
        c.alpha_composite(back, (1588 - back.width // 2, 420 - back.height))
        d.line((1320, 440, 1856, 440), fill=LINE, width=2)
        d.text((1320, 454), '실제 전투 화면 크기 (1:1) · 아군 탱커 로봇 옆', font=F(19, True), fill=SUB)
        pair = Image.open(os.path.join(R, f'{n}_pair.png')).convert('RGBA')
        rb, mb = PAIR[n]['robot'], PAIR[n]['monster']
        x0, y0 = min(rb[0], mb[0]) - 24, min(rb[1], mb[1]) - 18
        x1, y1 = max(rb[2], mb[2]) + 24, max(rb[3], mb[3]) + 18
        crop = on_road(pair.crop((int(x0), int(y0), int(x1), int(y1))))
        cx, cy = 1588 - crop.width // 2, 488
        c.alpha_composite(crop, (cx, cy))
        d.rounded_rectangle((cx, cy, cx + crop.width, cy + crop.height), 10, outline=LINE, width=2)
        ty = cy + crop.height + 22
        d.line((1320, ty, 1856, ty), fill=LINE, width=2)
        facts = [('삼각형', f'기본 모델 {INFO[n]["tris"]:,} · 이 이미지의 렌더 메시 약 {INFO[n]["render_tris"] / 10000:.1f}만'),
                 ('키', f'아군 탱커 로봇의 {CMP[n + ("_a" if num <= 4 else "_b")]["height_units"] / ROBOT_UNITS:.1f}배'),
                 ('전투 화면 키', f'약 {BAT[n]["h"]:.0f} px (탱커 로봇 {BAT["robot_tank"]["h"]:.0f} px)')]
        for i, (k, v) in enumerate(facts):
            y = ty + 14 + i * 50
            d.text((1320, y), k, font=F(17, True), fill=SUB)
            d.text((1320, y + 21), v, font=F(21), fill=INK)
        y = ty + 14 + 3 * 50 + 4
        d.text((1320, y), '색 (넓은 면적 순) · 포인트는 앰버 옐로 하나', font=F(17, True), fill=SUB)
        for i, col in enumerate(cols):
            sx = 1320 + i * 108
            d.rounded_rectangle((sx, y + 30, sx + 48, y + 78), 10, fill=col, outline=(205, 198, 186), width=1)
            d.text((sx, y + 84), NAMES[col], font=F(13), fill=INK)
            d.text((sx, y + 102), hexs(col), font=F(12), fill=SUB)
        c.convert('RGB').save(os.path.join(OUT, f'{num}.png'))


def compare_sheet():
    rows = [load('compare_a.png'), load('compare_b.png')]
    raw = [Image.open(os.path.join(R, f'compare_{r}.png')).convert('RGBA') for r in 'ab']
    top = 150
    Ht = top + sum(r.height + 110 for r in rows) + 30
    c = Image.new('RGBA', (W, Ht), BG); d = ImageDraw.Draw(c)
    card(d, (20, 20, W - 20, Ht - 20))
    d.text((60, 66), '탱커병 7안 비교', font=F(44, True), fill=INK, anchor='lm')
    d.text((60, 114), '모두 같은 비율 · 3/4 정면 · 각 줄 맨 왼쪽은 실제 아군 탱커 로봇 · 포인트 색은 모두 앰버 옐로', font=F(21), fill=SUB, anchor='lm')
    y = top
    for r, row, im, full in zip('ab', rows, rows, raw):
        bb = full.getbbox()
        x0 = (W - im.width) // 2
        c.alpha_composite(im, (x0, y))
        for k, mk in CMP.items():
            if mk['row'] != r: continue
            key = k[:-2]
            x = x0 + mk['x'] - bb[0]
            ly = y + im.height + 14
            if key == 'robot_tank': a, b = '아군 탱커 로봇', '실제 모델'
            else:
                num = int(key[1:])
                a, b = f'{num}  {OPTS[num - 1][1]}', f'아군 탱커 로봇의 {mk["height_units"] / ROBOT_UNITS:.1f}배'
            d.text((x, ly), a, font=F(22, True), fill=INK, anchor='mt')
            d.text((x, ly + 32), b, font=F(16), fill=SUB, anchor='mt')
        y += im.height + 110
    c.convert('RGB').save(os.path.join(OUT, '탱커병 7안 비교.png'))


def battle_sheet():
    shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
    Wb, Hb = shot.size
    c = Image.new('RGBA', (Wb, Hb), (24, 40, 62)); d = ImageDraw.Draw(c)
    for x in range(-40, Wb + 40, 64): d.line((x, 0, x, Hb), fill=(38, 58, 86), width=2)
    for y in range(-20, Hb, 64): d.line((0, y, Wb, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    for k, m in BAT.items():
        label = {'robot_melee': '아군 근접 로봇', 'robot_tank': '아군 탱커 로봇'}.get(k) or f'{k[1:]}  {OPTS[int(k[1:]) - 1][1]}'
        x, y = (m['box'][0] + m['box'][2]) / 2, m['box'][3] + 14
        for t, fy, font in ((label, y, F(19, True)), (f'{m["h"]:.0f} px', y + 30, F(15))):
            box = d.textbbox((x, fy), t, font=font, anchor='mt')
            d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=(24, 40, 62, 230))
            d.text((x, fy), t, font=font, fill=(240, 244, 250), anchor='mt')
    d.text((40, Hb - 34), '전투 카메라 (47.5° 내려다봄, 세로 화각 19°, 세로 1080p) · 실제 화면 크기 · 외곽선 1.8 px · '
           '일곱을 한 화면에 두려고 가로만 넓힘 (배율은 그대로) · px는 화면 속 실루엣 높이', font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
option_sheets()
compare_sheet()
battle_sheet()
