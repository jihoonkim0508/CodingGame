# The robots' scheme (white body, mid/light grey parts, charcoal joints, one saturated accent), with warm enemy accents
# that stay clear of the robots' sky blue (4,180,254), green (5,246,61) and lilac (217,184,255). sRGB.
WHITE = (246, 246, 246)
LGREY = (174, 174, 174)
MGREY = (108, 108, 108)
CHAR = (33, 33, 33)
SLATE = (62, 62, 68)             # dark armour: a step above charcoal so its cel bands still show
RED = (236, 46, 64)
GOLD = (255, 196, 24)
ORANGE = (255, 124, 0)
MAGENTA = (236, 36, 150)
YELLOW = (255, 206, 0)
PURPLE = (124, 52, 222)
EYE = (255, 236, 170)            # warm eye / core light, a small self-lit spot

NAMES = {WHITE: '화이트', LGREY: '라이트 그레이', MGREY: '미드 그레이', CHAR: '차콜', SLATE: '다크 아머',
         RED: '크림슨 레드', GOLD: '왕관 골드', ORANGE: '오렌지', MAGENTA: '마젠타', YELLOW: '경고 옐로',
         PURPLE: '딥 퍼플', EYE: '눈빛 크림'}

# each design's colours, largest area first (the accent is the one named in the caption)
USED = {
    1: [WHITE, RED, CHAR, GOLD, LGREY],
    2: [ORANGE, WHITE, CHAR, MGREY, EYE],
    3: [SLATE, MAGENTA, LGREY, WHITE, EYE],
    4: [YELLOW, WHITE, LGREY, CHAR, MGREY],
    5: [SLATE, MGREY, PURPLE, LGREY, EYE],
}
ACCENT = {1: RED, 2: ORANGE, 3: MAGENTA, 4: YELLOW, 5: PURPLE}
