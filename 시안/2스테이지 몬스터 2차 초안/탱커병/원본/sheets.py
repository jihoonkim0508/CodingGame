# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / battle) into the draft sheets: 1.png … 7.png, the side-by-side
# comparison with the real tank robot, and the battle-camera frame at true screen size.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import WHITE, LGREY, MGREY, CHAR, ORANGE, CORAL, MAGENTA, YELLOW, CRIMSON, AMBER, RASPBERRY, NAMES

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
W, H = 1920, 1080

OPTS = [  # number, name, one line, accent
    (1, '방패 돌격병', '키만 한 곡면 방패를 앞세우고 몸을 낮춰 밀고 들어오는 돌격병', ORANGE),
    (2, '블록 골렘', '큰 블록 어깨와 땅을 짚은 두 주먹, 어깨 사이에 묻힌 작은 머리', CORAL),
    (3, '돔 거북', '각진 돔 등껍질에 육각 판 하나, 기둥 다리 넷으로 버티는 거북', MAGENTA),
    (4, '불도저', '궤도 두 줄과 넓은 앞날로 길을 밀고 오는 불도저', YELLOW),
    (5, '중갑 기사', '두 겹 어깨 갑옷과 짧은 뿔 투구, 넓게 벌린 짧은 다리', CRIMSON),
    (6, '무게추', '손잡이 달린 사다리꼴 쇳덩이가 발 넷으로 걷는다', AMBER),
    (7, '씨름 장사', '둥근 큰 몸집에 샅바를 매고, 다리를 낮게 벌려 버틴다', RASPBERRY),
]
INFO = json.load(open(os.path.join(R, 'info.json')))


def load(name, view):
    im = Image.open(os.path.join(R, f'{name}_{view}.png')).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    return im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def swatches(d, x, y, cols):
    for i, c in enumerate(cols):
        sx = x + i * 196
        d.rounded_rectangle((sx, y, sx + 52, y + 52), 12, fill=c, outline=(205, 198, 186), width=1)
        d.text((sx + 64, y + 4), NAMES[c], font=F(17, True), fill=INK)
        d.text((sx + 64, y + 30), '#%02X%02X%02X' % c, font=F(14), fill=SUB)


def badge(d, x, y, n, size=48):
    d.rounded_rectangle((x, y, x + size, y + size), 12, fill=INK)
    d.text((x + size / 2, y + size / 2), str(n), font=F(int(size * 0.58), True), fill=CARD, anchor='mm')


def option_sheets():
    names = [f't{n}' for n, *_ in OPTS]
    q = {n: load(n, 'q34') for n in names}
    rob = load('robot_tank', 'q34')
    # one scale for every sheet's big view, so the seven sheets compare too
    floor, top = 890, 200
    f = min((floor - top) / max(i.height for i in q.values()), 1200 / max(i.width for i in q.values()))
    small = [load(n, v) for n in names for v in ('front', 'side', 'back')]
    f2 = min(190 / max(i.height for i in small), 200 / max(i.width for i in small))
    f3 = 200 / rob.height      # the size check: robot and monster at one smaller scale
    for (num, name, line, acc), n in zip(OPTS, names):
        c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
        badge(d, 60, 44, num, 64)
        d.text((144, 76), name, font=F(50, True), fill=INK, anchor='lm')
        d.text((146, 136), line, font=F(22), fill=SUB, anchor='lm')
        d.rounded_rectangle((40, 170, 1380, 1040), 22, fill=CARD, outline=LINE, width=2)
        big = scaled(q[n], f)
        c.alpha_composite(big, (710 - big.width // 2, floor - big.height))
        d.text((710, floor + 14), '3/4 정면', font=F(16), fill=SUB, anchor='mt')
        d.line((70, 940, 1350, 940), fill=LINE, width=2)
        swatches(d, 72, 962, [acc, WHITE, LGREY, MGREY, CHAR])
        d.text((1350, 988), f'삼각형 {INFO[n]["tris"]:,}개', font=F(17), fill=SUB, anchor='rm')
        # small views on the right: front and side, the back, and the size against the real tank robot
        d.rounded_rectangle((1410, 170, 1880, 1040), 22, fill=CARD, outline=LINE, width=2)
        for (v, lab), cx, yb in ((('front', '정면'), 1535, 410), (('side', '측면'), 1755, 410), (('back', '뒤 3/4'), 1645, 680)):
            sm = scaled(load(n, v), f2)
            c.alpha_composite(sm, (cx - sm.width // 2, yb - sm.height))
            d.text((cx, yb + 10), lab, font=F(16), fill=SUB, anchor='mt')
        rs, ms = scaled(rob, f3), scaled(q[n], f3)
        x0 = 1645 - (rs.width + 30 + ms.width) // 2
        c.alpha_composite(rs, (x0, 985 - rs.height))
        c.alpha_composite(ms, (x0 + rs.width + 30, 985 - ms.height))
        d.text((1645, 996), '아군 탱커 로봇과 같은 비율', font=F(16), fill=SUB, anchor='mt')
        c.convert('RGB').save(os.path.join(OUT, f'{num}.png'))


def comparison():
    """All seven at one true scale, the real tank robot at the head of each row."""
    rows = [['robot_tank', 't1', 't2', 't3', 't4'], ['robot_tank', 't5', 't6', 't7']]
    q = {n: load(n, 'q34') for row in rows for n in row}
    cellw, top, rowh = (W - 80) // 5, 70, 500
    f = min(360 / max(i.height for i in q.values()), (cellw - 30) / max(i.width for i in q.values()))
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    label = {f't{n}': (n, name, acc) for n, name, _, acc in OPTS}
    for i, row in enumerate(rows):
        y0 = top + i * rowh; floor = y0 + 400
        d.rounded_rectangle((20, y0, W - 20, y0 + rowh - 24), 22, fill=CARD, outline=LINE, width=2)
        for j, n in enumerate(row):
            im = scaled(q[n], f)
            cx = 40 + cellw * j + cellw // 2
            c.alpha_composite(im, (cx - im.width // 2, floor - im.height))
            if n.startswith('robot'):
                d.text((cx, floor + 22), '아군 탱커 로봇', font=F(20), fill=SUB, anchor='mt')
                continue
            num, name, acc = label[n]
            tw = d.textlength(f'{num}  {name}', font=F(24, True))
            x0 = cx - (tw + 26) / 2
            d.ellipse((x0, floor + 26, x0 + 16, floor + 42), fill=acc)
            d.text((x0 + 26, floor + 34), f'{num}  {name}', font=F(24, True), fill=INK, anchor='lm')
    d.text((40, 36), '모두 같은 비율 · 아군 탱커 로봇과 나란히', font=F(20), fill=SUB, anchor='lm')
    c.convert('RGB').save(os.path.join(OUT, '탱커병 7안 비교.png'))


def battle():
    """The battle-camera render over the stage's navy grid, names and on-screen heights under each."""
    shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
    marks = json.load(open(os.path.join(R, 'battle.json')))
    c = Image.new('RGBA', (W, H), (24, 40, 62)); d = ImageDraw.Draw(c)
    for x in range(-40, W + 40, 64): d.line((x, 0, x, H), fill=(38, 58, 86), width=2)
    for y in range(-20, H, 64): d.line((0, y, W, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    names = {f't{n}': f'{n} {name}' for n, name, *_ in OPTS}
    names.update({'robot_melee': '아군 근접 로봇', 'robot_tank': '아군 탱커 로봇'})
    for k, m in marks.items():
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
