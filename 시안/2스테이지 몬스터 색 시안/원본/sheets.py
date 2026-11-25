# python sheets.py <renders> <out_dir>
# Lays out render.py's renders (<renders>/imp, /raptor, /tank) into the colour-draft sheets:
#   일반 4 뿔 마귀/ and 빠른 적 3 추격 랩터/: 색 1.png … 색 6.png, 색 6안 비교.png, 전투 카메라 크기.png
#   탱커병 1 방패 1.7배/: 1.png, 방패 전후 비교.png, 전투 카메라 크기.png
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from colours import COLOURWAYS, NAMES, clash

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
ROAD, NAVY, PALE, WARN = (250, 236, 200), (24, 40, 62), (190, 204, 222), (176, 84, 36)
W, H = 1920, 1080
TITLE = {'imp': '일반 4 뿔 마귀', 'raptor': '빠른 적 3 추격 랩터'}
FOLDER = {'imp': '일반 4 뿔 마귀', 'raptor': '빠른 적 3 추격 랩터', 'tank': '탱커병 1 방패 1.7배'}


def load(path):
    im = Image.open(path).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f, nearest=False):
    return im if abs(f - 1) < 1e-3 else im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))),
                                                   Image.NEAREST if nearest else Image.LANCZOS)


def card(d, box, fill=CARD):
    d.rounded_rectangle(box, 24, fill=fill, outline=LINE if fill == CARD else None, width=2)


def hexs(c):
    return '#%02X%02X%02X' % c


def badge(d, x, y, n, size):
    d.rounded_rectangle((x, y, x + size, y + size), size // 5, fill=INK)
    d.text((x + size // 2, y + size // 2), str(n), font=F(int(size * 0.6), True), fill=CARD, anchor='mm')


def crop(shot, box, pad=14):
    x0, y0, x1, y1 = (int(v) for v in box)
    c = Image.new('RGBA', (x1 - x0 + 2 * pad, y1 - y0 + 2 * pad), ROAD + (255,))
    c.alpha_composite(shot.crop((x0 - pad, y0 - pad, x1 + pad, y1 + pad)))
    return c


def grid_bg(w, h):
    c = Image.new('RGBA', (w, h), NAVY); d = ImageDraw.Draw(c)
    for x in range(-40, w + 40, 64): d.line((x, 0, x, h), fill=(38, 58, 86), width=2)
    for y in range(-20, h, 64): d.line((0, y, w, y), fill=(38, 58, 86), width=2)
    return c


def tag(d, x, y, lines):
    """Navy name tags under a figure on the battle sheets."""
    for t, font in lines:
        box = d.textbbox((x, y), t, font=font, anchor='mt')
        d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=NAVY + (230,))
        d.text((x, y), t, font=font, fill=(240, 244, 250), anchor='mt')
        y += box[3] - box[1] + 16


def clash_line(who, cw):
    c = clash(who, cw)
    return (f'{", ".join(c)}와 같은 포인트 색: 한 웨이브에 같이 나오면 겹침', WARN) if c else ('다른 두 적과 포인트 색이 겹치지 않음', SUB)


# ---------------------------------------------------------------- imp and raptor colourways

def colour_set(who):
    CW = COLOURWAYS[who]; r = os.path.join(R, who); out = os.path.join(OUT, FOLDER[who]); os.makedirs(out, exist_ok=True)
    shot = Image.open(os.path.join(r, 'battle.png')).convert('RGBA')
    marks = json.load(open(os.path.join(r, 'battle.json')))
    if who == 'imp': key = lambda i: (f'robot_face', f'{i}_face')
    else: key = lambda i: ('robot_a' if i <= 3 else 'robot_b', f'{i}_{"a" if i <= 3 else "b"}')
    big = {i: load(os.path.join(r, f'cw{i}_big.png')) for i in range(1, 7)}
    fb = min(1.0, 1120 / max(i.width for i in big.values()), 800 / max(i.height for i in big.values()))   # one scale for all six
    px = lambda i: marks[key(i)[1]][3] - marks[key(i)[1]][1]

    for i, cw in enumerate(CW, 1):
        c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
        badge(d, 40, 34, i, 84)
        d.text((148, 56), cw['name'], font=F(46, True), fill=INK, anchor='lm')
        d.text((150, 104), f'{TITLE[who]} · 색 시안 {i}/6 · {cw["note"]}', font=F(22), fill=SUB, anchor='lm')
        d.rounded_rectangle((1296, 44, 1352, 100), 12, fill=cw['accent'], outline=(205, 198, 186), width=1)
        d.text((1368, 50), f'포인트 · {NAMES[cw["accent"]]} {hexs(cw["accent"])}', font=F(21, True), fill=INK)
        t, col = clash_line(who, cw)
        d.text((1368, 80), t, font=F(16), fill=col)
        # big 3/4 view
        card(d, (40, 146, 1270, 1046))
        im = scaled(big[i], fb)
        floor = 975
        c.alpha_composite(im, (655 - im.width // 2, floor - im.height))
        d.text((655, 1010), '3/4 정면' if who == 'imp' else '3/4 뷰 · 진행 방향 →', font=F(18), fill=SUB, anchor='mm')
        # battle size beside the robot, then 2x
        card(d, (1296, 146, 1880, 1046), NAVY)
        d.text((1324, 172), '전투 화면 실제 크기 (1:1)', font=F(21, True), fill=(236, 242, 250))
        rk, mk = key(i)
        rb, mb = crop(shot, marks[rk], 8), crop(shot, marks[mk], 8)
        gap = 22; x0 = 1588 - (rb.width + gap + mb.width) // 2; base = 222 + max(rb.height, mb.height)
        c.alpha_composite(rb, (x0, base - rb.height)); c.alpha_composite(mb, (x0 + rb.width + gap, base - mb.height))
        d.text((x0 + rb.width // 2, base + 8), f'로봇 {marks[rk][3] - marks[rk][1]:.0f} px', font=F(15), fill=PALE, anchor='mt')
        d.text((x0 + rb.width + gap + mb.width // 2, base + 8), f'색 {i} · {px(i):.0f} px', font=F(15), fill=PALE, anchor='mt')
        z = scaled(mb, 2, nearest=True)
        zy = (base + 74 + 800 - z.height) // 2      # the 2x block centred in the space above the swatches
        d.text((1588 - z.width // 2, zy - 30), '2배 확대', font=F(17, True), fill=PALE)
        c.alpha_composite(z, (1588 - z.width // 2, zy))
        # colour swatches, largest area first
        sy = 838
        d.line((1324, sy - 18, 1852, sy - 18), fill=(52, 72, 100), width=2)
        d.text((1324, sy), '색 (넓은 면적 순)', font=F(17, True), fill=PALE)
        for k, col in enumerate(cw['swatch']):
            sx = 1324 + k * 90
            d.rounded_rectangle((sx, sy + 30, sx + 46, sy + 76), 10, fill=col, outline=(90, 108, 134), width=1)
            d.text((sx, sy + 84), NAMES[col], font=F(13, True), fill=(236, 242, 250))
            d.text((sx, sy + 104), hexs(col), font=F(12), fill=PALE)
            d.text((sx, sy + 122), '%d,%d,%d' % col, font=F(12), fill=PALE)
        c.convert('RGB').save(os.path.join(out, f'색 {i}.png'))

    # all six at one scale with the robot
    rob = load(os.path.join(r, 'robot_cmp.png'))
    cmp = {i: load(os.path.join(r, f'cw{i}_cmp.png')) for i in range(1, 7)}
    rows = [[1, 2, 3], [4, 5, 6]]
    gap = 44                                    # px between figures, after scaling
    n = len(rows[0])
    wmax = max(rob.width + sum(cmp[i].width for i in row) for row in rows)
    f = min(1.0, (W - 140 - gap * n) / wmax, (860 // len(rows) - 180) / max(rob.height, *(i.height for i in cmp.values())))
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    d.text((60, 62), f'{TITLE[who]} · 색 6안 비교', font=F(44, True), fill=INK, anchor='lm')
    d.text((62, 114), '모두 같은 비율 · 3/4 뷰 · 각 줄 맨 왼쪽은 실제 근접 로봇 (AttackRobot.fbx + 텍스처)'
           + (' · 뿔 마귀는 3차의 1.25배' if who == 'imp' else ''), font=F(21), fill=SUB, anchor='lm')
    rh = 860 // len(rows)
    for ri, row in enumerate(rows):
        y0 = 170 + ri * rh
        card(d, (30, y0, W - 30, y0 + rh - 16))
        floor = y0 + rh - 150
        ims = [scaled(rob, f)] + [scaled(cmp[i], f) for i in row]
        x = (W - sum(im.width for im in ims) - gap * (len(ims) - 1)) // 2
        for im, i in zip(ims, [0] + row):
            c.alpha_composite(im, (x, floor - im.height))
            cx = x + im.width // 2
            if i == 0:
                d.text((cx, floor + 30), '로봇', font=F(22, True), fill=INK, anchor='mt')
            else:
                cw = CW[i - 1]
                badge(d, cx - 18, floor + 18, i, 36)
                d.text((cx, floor + 66), cw['name'], font=F(19, True), fill=INK, anchor='mt')
                t, col = clash_line(who, cw)
                d.rounded_rectangle((cx - 28, floor + 100, cx + 28, floor + 116), 7, fill=cw['accent'])
                if clash(who, cw): d.text((cx, floor + 122), f'{"·".join(clash(who, cw))}와 겹침', font=F(14), fill=WARN, anchor='mt')
            x += im.width + gap
    c.convert('RGB').save(os.path.join(out, '색 6안 비교.png'))

    # battle camera
    c = grid_bg(W, H); d = ImageDraw.Draw(c); c.alpha_composite(shot)
    for k, b in marks.items():
        n = k.rsplit('_', 1)[0]
        lines = [('로봇', F(17, True))] if n == 'robot' else [(f'색 {n}', F(17, True)), (f'{b[3] - b[1]:.0f} px', F(14))]
        tag(d, (b[0] + b[2]) / 2, b[3] + 10, lines)
    if who == 'imp':
        for row, text in (('walk', '길 따라 왼쪽으로 걷는 모습'), ('face', '카메라 쪽으로 돈 모습')):
            y = min(marks[f'{i}_{row}'][1] for i in range(1, 7)) - 40
            d.text((marks[f'6_{row}'][2] + 40, y), text, font=F(20, True), fill=(110, 80, 50), anchor='rm')
        rpx = marks['robot_walk'][3] - marks['robot_walk'][1]
        wpx = sum(marks[f'{i}_walk'][3] - marks[f'{i}_walk'][1] for i in range(1, 7)) / 6
        fpx = sum(marks[f'{i}_face'][3] - marks[f'{i}_face'][1] for i in range(1, 7)) / 6
        foot = f'로봇 약 {rpx:.0f} px · 뿔 마귀 1.25배: 걷는 옆모습 약 {wpx:.0f} px, 카메라 쪽 약 {fpx:.0f} px (3차 모델의 1.25배)'
    else:
        foot = '모두 길을 따라 왼쪽으로 달리는 방향 · 모델 크기는 3차 그대로'
    d.text((40, 1040), f'전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px · {foot}', font=F(18),
           fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(out, '전투 카메라 크기.png'))


# ---------------------------------------------------------------- tank: the 1.7x shield

TANK_COLS = [((246, 246, 246), '화이트'), ((66, 70, 80), '건메탈'), ((255, 190, 0), '앰버 옐로'), ((174, 174, 174), '라이트 그레이'),
             ((108, 108, 108), '미드 그레이')]


def tank():
    r = os.path.join(R, 'tank'); out = os.path.join(OUT, FOLDER['tank']); os.makedirs(out, exist_ok=True)
    info = json.load(open(os.path.join(r, 'info.json')))
    cmpj = json.load(open(os.path.join(r, 'compare.json'))); CM = cmpj['marks']
    bat = json.load(open(os.path.join(r, 'battle.json')))
    pair = json.load(open(os.path.join(r, 'pairs.json')))
    hb = lambda k: bat[k][3] - bat[k][1]
    robot_u = CM['robot_tank_q34']['height_units']
    old_s, new_s, mid_s = (CM[f'{k}_q34']['shield_units'] for k in ('t1', 't1big', 't1mid'))
    body_u = CM['t1_q34']['height_units']

    # 1.png: round 3's tank sheet with the new shield
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    badge(d, 40, 34, 1, 84)
    d.text((148, 56), '철방패 거병 · 방패 1.7배', font=F(48, True), fill=INK, anchor='lm')
    d.text((150, 104), '방패를 3차의 1.7배로 키운 안: 땅에 닿는 높이는 그대로, 엄니에 닿지 않게 조금 앞으로, 왼팔을 뻗어 새 가장자리를 세 갈퀴로 쥔다',
           font=F(21), fill=SUB, anchor='lm')
    card(d, (40, 146, 1270, 1046))
    big = load(os.path.join(r, 't1big_q34.png'))
    big = scaled(big, min(1.0, 820 / big.height, 1150 / big.width))
    c.alpha_composite(big, (655 - big.width // 2, 990 - big.height))
    d.text((655, 1008), '3/4 정면', font=F(18), fill=SUB, anchor='mt')
    card(d, (1296, 146, 1880, 1046))
    back = load(os.path.join(r, 't1big_back.png')); back = scaled(back, min(1.0, 190 / back.height, 520 / back.width))
    d.text((1320, 166), '뒤 3/4', font=F(19, True), fill=SUB)
    c.alpha_composite(back, (1588 - back.width // 2, 388 - back.height))
    d.line((1320, 404, 1856, 404), fill=LINE, width=2)
    d.text((1320, 416), '실제 전투 화면 크기 (1:1) · 아군 탱커 로봇 옆', font=F(19, True), fill=SUB)
    shot = Image.open(os.path.join(r, 't1big_pair.png')).convert('RGBA')
    rb, mb = pair['robot'], pair['monster']
    box = (min(rb[0], mb[0]) - 24, min(rb[1], mb[1]) - 18, max(rb[2], mb[2]) + 24, max(rb[3], mb[3]) + 18)
    pc = crop(shot, box, 0)
    cx, cy = 1588 - pc.width // 2, 448
    c.alpha_composite(pc, (cx, cy)); d.rounded_rectangle((cx, cy, cx + pc.width, cy + pc.height), 10, outline=LINE, width=2)
    ty = cy + pc.height + 22
    d.line((1320, ty, 1856, ty), fill=LINE, width=2)
    facts = [('방패', f'3차의 1.7배 · 높이 {old_s:.2f} → {new_s:.2f} (몸 {body_u:.2f}보다 큼)'),
             ('키', f'아군 탱커 로봇의 {CM["t1big_q34"]["height_units"] / robot_u:.1f}배 (방패 끝까지, 3차 {body_u / robot_u:.1f}배)'),
             ('전투 화면 키', f'약 {hb("t1big"):.0f} px (3차 {hb("t1"):.0f} px · 탱커 로봇 {hb("robot_tank"):.0f} px)'),
             ('삼각형', f'기본 모델 {info["t1big"]["tris"]:,} · 이 이미지의 렌더 메시 약 {info["t1big"]["render_tris"] / 10000:.1f}만')]
    for i, (k, v) in enumerate(facts):
        y = ty + 12 + i * 46
        d.text((1320, y), k, font=F(16, True), fill=SUB)
        d.text((1320, y + 20), v, font=F(19), fill=INK)
    y = ty + 12 + 4 * 46 + 4
    d.text((1320, y), '색 (넓은 면적 순) · 3차 그대로, 포인트는 앰버 옐로 하나', font=F(16, True), fill=SUB)
    for i, (col, name) in enumerate(TANK_COLS):
        sx = 1320 + i * 108
        d.rounded_rectangle((sx, y + 28, sx + 44, y + 72), 10, fill=col, outline=(205, 198, 186), width=1)
        d.text((sx, y + 78), name, font=F(13), fill=INK)
        d.text((sx, y + 96), hexs(col), font=F(12), fill=SUB)
    c.convert('RGB').save(os.path.join(out, '1.png'))

    # 방패 전후 비교: both rows at one scale
    rows = [('q34', '3/4 정면'), ('side', '옆모습 · 왼쪽이 앞')]
    ims = {k: Image.open(os.path.join(r, f'compare_{k}.png')).convert('RGBA') for k, _ in rows}
    top, rh = 150, ims['q34'].height + 120
    Ht = top + rh * 2 + 30
    c = Image.new('RGBA', (W, Ht), BG); d = ImageDraw.Draw(c)
    card(d, (20, 20, W - 20, Ht - 20))
    d.text((60, 66), '철방패 거병 · 방패 전후 비교', font=F(44, True), fill=INK, anchor='lm')
    d.text((60, 114), '모두 같은 비율 · 각 줄 맨 왼쪽은 실제 아군 탱커 로봇 (TankRobot.fbx, 방어 자세) · 맨 오른쪽은 비교용 참고안', font=F(21), fill=SUB, anchor='lm')
    labels = {'robot_tank': ('아군 탱커 로봇', '실제 모델', INK),
              't1': ('3차 (기존)', f'방패 높이 {old_s:.2f}', INK),
              't1big': ('새 안 · 방패 1.7배', f'방패 높이 {new_s:.2f} · 몸({body_u:.2f})보다 큼', INK),
              't1mid': ('참고 · 1.3배 (면적 1.7배)', f'방패 높이 {mid_s:.2f} · 뿔은 보임', SUB)}
    x0 = (W - ims['q34'].width) // 2
    for ri, (row, cap) in enumerate(rows):
        y = top + ri * rh
        d.text((60, y + 6), cap, font=F(19, True), fill=SUB)
        if ri: d.line((60, y - 14, W - 60, y - 14), fill=LINE, width=2)
        c.alpha_composite(ims[row], (x0, y + 20))
        for k, (a, b, col) in labels.items():
            m = CM[f'{k}_{row}']
            x, ly = x0 + m['x'], y + 20 + ims[row].height + 10
            d.text((x, ly), a, font=F(21, True), fill=col, anchor='mt')
            d.text((x, ly + 30), b, font=F(15), fill=SUB, anchor='mt')
    c.convert('RGB').save(os.path.join(out, '방패 전후 비교.png'))

    # battle camera
    shot = Image.open(os.path.join(r, 'battle.png')).convert('RGBA')
    c = grid_bg(W, H); d = ImageDraw.Draw(c); c.alpha_composite(shot)
    names = {'robot_melee': '아군 근접 로봇', 'robot_tank': '아군 탱커 로봇', 't1': '3차 (기존)', 't1big': '새 안 · 방패 1.7배',
             't1mid': '참고 · 1.3배'}
    for k, b in bat.items():
        tag(d, (b[0] + b[2]) / 2, b[3] + 12, [(names[k], F(18, True)), (f'{b[3] - b[1]:.0f} px', F(14))])
    d.text((40, 1040), '전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px · 셋 다 길을 따라 왼쪽으로 걸으며 카메라 쪽으로 조금 돈 모습 · px는 화면 속 실루엣 높이',
           font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(out, '전투 카메라 크기.png'))


colour_set('imp')
colour_set('raptor')
tank()
