# python sheets.py <renders> <out_dir>
# Lays out the Blender renders (render.py views / compare / battle) into the boss draft sheets.
import sys, os, json
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from palette import USED, NAMES, ACCENT

R, OUT = sys.argv[1:3]
F = lambda size, bold=False: ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if bold else 'C:/Windows/Fonts/malgun.ttf', size)
BG, CARD, INK, SUB, LINE = (240, 235, 226), (250, 248, 243), (42, 50, 64), (122, 130, 144), (222, 214, 200)
# names and one-liners, kept here so the sheets run without Blender
NAME = {1: '철갑 대왕', 2: '중장 메카', 3: '야수 군주', 4: '보행 사령관', 5: '철벽 거신'}
LINES = {
    1: '붉은 망토에 금관을 쓰고 대검을 땅에 꽂은 기사 왕',
    2: '팔 하나는 대포, 하나는 주먹인 외눈 중장갑 메카',
    3: '판금 갈기와 큰 뿔을 단 네 발 야수 왕',
    4: '네 다리 보행기 위 왕좌에서 지휘봉을 든 사령관',
    5: '등에 선돌 왕관을 지고 큰 주먹을 땅에 짚은 돌 거인',
}
ROBOT_H = 1.55


def load(name):
    im = Image.open(os.path.join(R, name)).convert('RGBA')
    return im.crop(im.getbbox())


def scaled(im, f):
    return im if f == 1 else im.resize((max(1, round(im.width * f)), max(1, round(im.height * f))), Image.LANCZOS)


def hexs(c):
    return '#%02X%02X%02X' % c


cmp = json.load(open(os.path.join(R, 'compare.json')))['marks']
bat = json.load(open(os.path.join(R, 'battle.json')))
tris = json.load(open(os.path.join(R, 'tris.json')))
ratio = {k: m['height_units'] / ROBOT_H for k, m in cmp.items()}


def option_sheets():
    q = {n: load(f'{n}_q34.png') for n in NAME}
    robot = load('robot_q34.png')
    for n in NAME:
        # each boss fills its sheet; the robot beside it is at that same scale (the five compare in 보스 5안 비교)
        f = min(1.0, 800 / q[n].height, (1160 - robot.width - 50) / q[n].width)
        rob = scaled(robot, f)
        c = Image.new('RGBA', (1920, 1080), BG); d = ImageDraw.Draw(c)
        d.rounded_rectangle((30, 30, 1250, 1050), 24, fill=CARD, outline=LINE, width=2)
        d.rounded_rectangle((60, 58, 144, 142), 16, fill=INK)
        d.text((102, 98), str(n), font=F(56, True), fill=CARD, anchor='mm')
        d.text((168, 78), NAME[n], font=F(50, True), fill=INK, anchor='lm')
        d.text((170, 126), LINES[n], font=F(24), fill=SUB, anchor='lm')
        floor = 990
        big = scaled(q[n], f)
        x0 = 60 + rob.width + 50
        bx = x0 + (1220 - x0 - big.width) // 2
        c.alpha_composite(rob, (70, floor - rob.height))
        c.alpha_composite(big, (bx, floor - big.height))
        d.text((70 + rob.width // 2, floor + 14), '로봇 (같은 비율)', font=F(17), fill=SUB, anchor='mt')
        d.text((bx + big.width // 2, floor + 14), '3/4 정면', font=F(17), fill=SUB, anchor='mt')
        # right column: front view, colours, facts
        d.rounded_rectangle((1280, 30, 1890, 1050), 24, fill=CARD, outline=LINE, width=2)
        fr = load(f'{n}_front.png')
        fr = scaled(fr, min(1.0, 360 / fr.height, 540 / fr.width))
        c.alpha_composite(fr, (1585 - fr.width // 2, 440 - fr.height))
        d.text((1585, 456), '정면', font=F(17), fill=SUB, anchor='mt')
        d.line((1310, 506, 1860, 506), fill=LINE, width=2)
        d.text((1312, 526), '색 (넓은 면적 순)', font=F(19, True), fill=SUB)
        for i, col in enumerate(USED[n]):
            sx = 1312 + i * 108
            d.rounded_rectangle((sx, 566, sx + 64, 630), 12, fill=col, outline=(205, 198, 186), width=1)
            d.text((sx, 640), NAMES[col], font=F(15), fill=INK)
            d.text((sx, 662), hexs(col), font=F(13), fill=SUB)
        d.line((1310, 712, 1860, 712), fill=LINE, width=2)
        acc = ACCENT[n]
        facts = [('포인트 색', f'{NAMES[acc]} {hexs(acc)}'),
                 ('키', f'로봇의 {ratio[str(n)]:.1f}배'),
                 ('전투 화면 키', f'약 {bat[str(n)]["height_px"]:.0f} px (로봇 약 {bat["robot_a"]["height_px"]:.0f} px)'),
                 ('삼각형', f'약 {round(tris[str(n)], -2):,.0f}개 (초안 기본 도형 그대로)')]
        for i, (k, v) in enumerate(facts):
            y = 740 + i * 62
            d.text((1312, y), k, font=F(19, True), fill=SUB)
            d.text((1312, y + 26), v, font=F(21), fill=INK)
        d.rounded_rectangle((1800, 742, 1850, 792), 10, fill=acc)
        c.convert('RGB').save(os.path.join(OUT, f'{n}.png'))


def compare_sheet():
    im = Image.open(os.path.join(R, 'compare.png')).convert('RGBA')
    top, pad = 150, 40
    H = top + im.height + 120
    c = Image.new('RGBA', (1920, H), BG); d = ImageDraw.Draw(c)
    d.rounded_rectangle((20, 20, 1900, H - 20), 24, fill=CARD, outline=LINE, width=2)
    d.text((60, 62), '보스 5안 비교', font=F(44, True), fill=INK, anchor='lm')
    d.text((60, 112), '모두 같은 비율 · 3/4 정면 · 실제 근접 로봇 모델과 일반병 크기 블록을 옆에 두고', font=F(21), fill=SUB, anchor='lm')
    x0 = (1920 - im.width) // 2
    c.alpha_composite(im, (x0, top))
    y = top + max(m['bottom'] for m in cmp.values()) + 16      # one baseline for every label
    for k, m in cmp.items():
        x = x0 + m['x']
        if k == 'robot': a, b = '로봇', '실제 모델'
        elif k == 'grunt': a, b = '일반병 크기', f'로봇의 {ratio[k]:.1f}배'
        else: a, b = f'{k}  {NAME[int(k)]}', f'로봇의 {ratio[k]:.1f}배'
        d.text((x, y), a, font=F(22, True), fill=INK, anchor='mt')
        d.text((x, y + 32), b, font=F(16), fill=SUB, anchor='mt')
    c.convert('RGB').save(os.path.join(OUT, '보스 5안 비교.png'))


def battle_sheet():
    shot = Image.open(os.path.join(R, 'battle.png')).convert('RGBA')
    W, H = shot.size
    c = Image.new('RGBA', (W, H), (24, 40, 62)); d = ImageDraw.Draw(c)
    for x in range(-40, W + 40, 64): d.line((x, 0, x, H), fill=(38, 58, 86), width=2)
    for y in range(-20, H, 64): d.line((0, y, W, y), fill=(38, 58, 86), width=2)
    c.alpha_composite(shot)
    for k, m in bat.items():
        label = '로봇' if k.startswith('robot') else f'{k}  {NAME[int(k)]}'
        x, y = m['bottom'][0], min(m['box'][3], m['bottom'][1]) + 12
        sub = f'{m["height_px"]:.0f} px'
        for t, fy, font in ((label, y, F(19, True)), (sub, y + 30, F(15))):
            box = d.textbbox((x, fy), t, font=font, anchor='mt')
            d.rounded_rectangle((box[0] - 10, box[1] - 5, box[2] + 10, box[3] + 6), 9, fill=(24, 40, 62, 230))
            d.text((x, fy), t, font=font, fill=(240, 244, 250), anchor='mt')
    d.text((40, H - 34), '전투 카메라 (47.5° 내려다봄, 세로 화각 19°, 세로 1080p) · 실제 화면 크기 · 외곽선 1.8 px · '
           '다섯을 한 화면에 두려고 가로만 넓힘 (배율은 그대로) · px는 화면 속 실루엣 높이', font=F(18), fill=(200, 212, 228), anchor='ls')
    c.convert('RGB').save(os.path.join(OUT, '전투 카메라 크기.png'))


os.makedirs(OUT, exist_ok=True)
option_sheets()
compare_sheet()
battle_sheet()
