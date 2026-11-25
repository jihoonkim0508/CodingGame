# python sheets.py <renders> <out_dir> <robot icon png>
# Lays out the Blender renders (render.py views / battle) into the draft sheets.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import USED, NAMES

R, OUT, ICON = sys.argv[1:4]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
W, H = 1920, 1080

ROLES = [('normal', '일반'), ('fast', '빠른 적'), ('tank', '탱커병'), ('boss', '보스')]
FAMILY = {'a': '갈림길 버그봇', 'b': '순서도 오류 기호'}
CONCEPT = {
    'normal_a': 'if 갈림길 안테나를 단 꼬마 버그봇',
    'normal_b': '뚜껑에 x가 붙은 빈 변수 상자 버그',
    'fast_a': '날개 달린 데이터 패킷 날벌레',
    'fast_b': 'Y / N 날개를 편 판단 다이아 드론',
    'tank_a': '데이터 블록 등딱지를 진 거북 버그',
    'tank_b': '경고 표지 방패를 든 게 버그',
    'boss_a': '분기 트리 왕관을 쓴 여왕 버그봇',
    'boss_b': 'NaN 표를 단 판단 다이아 거인',
}


def load(name, view):
    im = Image.open(os.path.join(R, f'{name}_{view}.png')).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    return im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def icon_at(robot_h):
    """The robot icon sized so its robot is as tall as the robot stand-in rendered at the same scale."""
    im = Image.open(ICON).convert('RGBA'); im = im.crop(im.getbbox())
    return scaled(im, robot_h / im.height)


def palette(d, x, y, name):
    for i, c in enumerate(USED[name]):
        sx = x + i * 104
        d.rounded_rectangle((sx, y, sx + 60, y + 60), 12, fill=c, outline=(205, 198, 186), width=1)
        d.text((sx, y + 68), NAMES[c], font=F(15), fill=INK)
        d.text((sx, y + 90), '#%02X%02X%02X' % c, font=F(13), fill=SUB)


def role_sheet(key, title):
    names = [f'{key}_a', f'{key}_b']
    q = {n: load(n, 'q34') for n in names}
    fr = {n: load(n, 'front') for n in names}
    sd = {n: load(n, 'side') for n in names}
    robot = load('robot', 'q34')
    # one scale for both 3/4 views and the robot (so sizes and line weights compare), one for the small views
    left, colw, floor = 60, 240, 760
    f = min(570 / max(i.height for i in q.values()), 640 / robot.height, 360 / (robot.width * 0.95))
    rob = icon_at(robot.height * f)
    panel_x = left + max(rob.width, 170) + 40
    pw = (W - 60 - panel_x - 30) // 2
    qw = pw - colw - 70
    f = min(f, qw / max(i.width for i in q.values()))
    rob = icon_at(robot.height * f)
    small = list(fr.values()) + list(sd.values())
    f2 = min(colw / max(i.width for i in small), 235 / max(i.height for i in small), 0.6 * f)

    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    d.text((left, 34), title, font=F(46, True), fill=INK)
    # robot reference, standing on the same floor as the 3/4 views
    rx = left + (max(rob.width, 170) - rob.width) // 2
    c.alpha_composite(rob, (rx, floor - rob.height))
    d.text((left + max(rob.width, 170) // 2, floor + 18), '로봇 아이콘 (같은 비율)', font=F(16), fill=SUB, anchor='mt')
    for k, n in enumerate(names):
        x0 = panel_x + k * (pw + 30)
        d.rounded_rectangle((x0, 110, x0 + pw, 1040), 22, fill=CARD, outline=LINE, width=2)
        opt = n[-1].upper()
        d.rounded_rectangle((x0 + 28, 132, x0 + 72, 176), 10, fill=INK)
        d.text((x0 + 50, 154), opt, font=F(26, True), fill=CARD, anchor='mm')
        d.text((x0 + 90, 154), CONCEPT[n], font=F(28, True), fill=INK, anchor='lm')
        d.text((x0 + 92, 196), FAMILY[n[-1]] + ' 계열', font=F(17), fill=SUB, anchor='lm')
        big = scaled(q[n], f)
        bx = x0 + 30 + (qw - big.width) // 2
        c.alpha_composite(big, (bx, floor - big.height))
        d.text((x0 + 30 + qw // 2, floor + 18), '3/4 정면', font=F(16), fill=SUB, anchor='mt')
        cx = x0 + pw - 30 - colw
        for j, (im, lab) in enumerate(((fr[n], '정면'), (sd[n], '측면'))):
            sm = scaled(im, f2)
            yb = 470 + j * 290
            c.alpha_composite(sm, (cx + (colw - sm.width) // 2, yb - sm.height))
            d.text((cx + colw // 2, yb + 10), lab, font=F(16), fill=SUB, anchor='mt')
        d.line((x0 + 28, 820, x0 + pw - 28, 820), fill=LINE, width=2)
        d.text((x0 + 30, 840), '팔레트', font=F(17, True), fill=SUB)
        palette(d, x0 + 30, 874, n)
    c.convert('RGB').save(os.path.join(OUT, f'{title}.png'))


def overview():
    """Everything at one true scale with the robot, so the role sizes compare too."""
    names = [[f'{k}_{o}' for k, _ in ROLES] for o in 'ab']
    q = {n: load(n, 'q34') for row in names for n in row}
    robot = load('robot', 'q34')
    labw, top, rowh, cellw = 250, 96, 470, (W - 250 - 40) // 4
    f = min(380 / max(i.height for i in q.values()), (cellw - 30) / max(i.width for i in q.values()))
    c = Image.new('RGBA', (W, H), BG); d = ImageDraw.Draw(c)
    for j, (_, title) in enumerate(ROLES):
        d.text((labw + cellw * j + cellw // 2, 50), title, font=F(34, True), fill=INK, anchor='mm')
    for i, row in enumerate(names):
        y0 = top + i * rowh; floor = y0 + 400
        d.rounded_rectangle((20, y0 + 6, W - 20, y0 + rowh - 10), 22, fill=CARD, outline=LINE, width=2)
        o = 'ab'[i]
        d.text((44, y0 + 40), o.upper(), font=F(44, True), fill=INK)
        d.text((44, y0 + 104), FAMILY[o], font=F(20, True), fill=SUB)
        rob = icon_at(robot.height * f)
        c.alpha_composite(rob, (labw // 2 - rob.width // 2 + 10, floor - rob.height))
        d.text((labw // 2 + 10, floor + 12), '로봇 (같은 비율)', font=F(15), fill=SUB, anchor='mt')
        for j, n in enumerate(row):
            im = scaled(q[n], f)
            cx = labw + cellw * j + cellw // 2
            c.alpha_composite(im, (cx - im.width // 2, floor - im.height))
            d.text((cx, floor + 14), CONCEPT[n], font=F(19), fill=INK, anchor='mt')
    c.convert('RGB').save(os.path.join(OUT, '한눈에 비교.png'))


def battle():
    """The battle-camera render over the stage's navy grid, names under each."""
    shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
    marks = json.load(open(os.path.join(R, 'battle.json')))
    c = Image.new('RGBA', (W, H), (24, 40, 62)); d = ImageDraw.Draw(c)
    for x in range(-40, W + 40, 64): d.line((x, 0, x, H), fill=(38, 58, 86), width=2)
    for y in range(-20, H, 64): d.line((0, y, W, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    role = dict(ROLES)
    for k, m in marks.items():
        name, o = k.rsplit('_', 1)
        label = '로봇' if name == 'robot' else f'{role[name]} {o.upper()}'
        x, y = m['bottom'][0], m['bottom'][1] + 10
        box = d.textbbox((x, y), label, font=F(17, True), anchor='mt')
        d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=(24, 40, 62, 230))
        d.text((x, y), label, font=F(17, True), fill=(240, 244, 250), anchor='mt')
    d.text((40, 1040), '전투 카메라 (47.5° 내려다봄, 화각 19°, 1080p) · 실제 화면 크기 · 외곽선 1.8 px', font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
for key, title in ROLES: role_sheet(key, title)
overview()
battle()
