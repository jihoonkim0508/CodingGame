# Six colourways each for 일반 4 뿔 마귀 and 빠른 적 3 추격 랩터 (sRGB), shared by render.py (Blender) and sheets.py (PIL).
# Colourway 1 is round 3 as it was. Each recolours the round-3 model: 'map' swaps a round-3 colour for a new one on
# every part that had it, 'parts' (regex on the part name) wins over it for a few parts, e.g. bone horns on a dark skin.
# The robots' family: white / light grey / mid grey / charcoal masses plus ONE accent, never the robots' own sky blue
# (4,180,254), green (5,246,61) or lilac (217,184,255).
WHITE, LGREY, MGREY, CHAR = (246, 246, 246), (174, 174, 174), (108, 108, 108), (33, 33, 33)
GUN = (64, 66, 74)               # gunmetal: a step above charcoal so its cel bands still show
COAL = (50, 52, 58)              # charcoal for a big mass (pure 33 loses its bands)
DRED = (120, 36, 44)             # dark red skin
DSLATE = (58, 66, 84)            # deep slate skin
ORANGE, RED, AMBER = (255, 128, 0), (240, 44, 52), (255, 190, 0)
MAGENTA, PURPLE = (232, 36, 140), (122, 52, 214)
CREAM = (255, 236, 170)          # the imp's eye slits (round 3)
R_GLOW = (255, 92, 80)           # the raptor's eye slits (round 3): a lighter accent
EYES = {MAGENTA: (255, 120, 196), PURPLE: (168, 120, 255), AMBER: (255, 214, 92), ORANGE: (255, 170, 80)}

NAMES = {WHITE: '흰색', LGREY: '밝은 회색', MGREY: '회색', CHAR: '차콜', GUN: '건메탈', COAL: '짙은 회색', DRED: '검붉은색',
         DSLATE: '짙은 슬레이트', ORANGE: '오렌지', RED: '레드', AMBER: '앰버 옐로', MAGENTA: '마젠타', PURPLE: '진보라',
         CREAM: '눈빛 크림', R_GLOW: '눈빛 레드', **{v: '눈빛 ' + n for v, n in
                                                   ((EYES[MAGENTA], '마젠타'), (EYES[PURPLE], '보라'), (EYES[AMBER], '앰버'), (EYES[ORANGE], '오렌지'))}}

# The accents the other two picks keep (탱커병 1 = amber-yellow, 뿔 마귀 = orange, 추격 랩터 = red): a colourway whose accent
# is another pick's own clashes with it in a wave.
OWN = {'imp': ORANGE, 'raptor': RED, 'tank': AMBER}
WHO = {'imp': '뿔 마귀', 'raptor': '추격 랩터', 'tank': '탱커병'}

# imp parts: skin LGREY, horns / claws / spines / tail tip / wraps / shaft / mouth CHAR, pauldron and spearhead MGREY,
# loincloth and rags ORANGE, fangs WHITE, eye slits CREAM. HORN picks the horn-like dark parts (not the mouth or wraps).
HORN = r'(horn|spine|claw|tclaw|tailtip)'
IMP = [
    dict(name='오렌지 · 밝은 회색 피부', note='3차 그대로', accent=ORANGE, map={},
         swatch=[LGREY, CHAR, MGREY, ORANGE, WHITE, CREAM]),
    dict(name='마젠타 · 흰 피부', note='흰 피부에 검은 뿔, 포인트만 마젠타', accent=MAGENTA, map={LGREY: WHITE, ORANGE: MAGENTA},
         swatch=[WHITE, CHAR, MGREY, MAGENTA, CREAM]),
    dict(name='오렌지 · 건메탈 피부', note='어두운 피부에 흰 뿔과 발톱, 포인트는 그대로 오렌지', accent=ORANGE,
         map={LGREY: GUN, MGREY: LGREY}, parts=[(HORN, WHITE)], swatch=[GUN, WHITE, CHAR, LGREY, ORANGE, CREAM]),
    dict(name='앰버 옐로 · 검붉은 피부', note='검붉은 피부에 흰 뿔, 포인트는 앰버 옐로', accent=AMBER,
         map={LGREY: DRED, ORANGE: AMBER}, parts=[(HORN, WHITE)], swatch=[DRED, WHITE, CHAR, MGREY, AMBER, CREAM]),
    dict(name='진보라 · 밝은 회색 피부', note='3차 몸 그대로, 포인트만 진보라', accent=PURPLE, map={ORANGE: PURPLE},
         swatch=[LGREY, CHAR, MGREY, PURPLE, WHITE, CREAM]),
    dict(name='레드 · 짙은 슬레이트 피부', note='짙은 슬레이트 피부에 밝은 회색 뿔, 포인트는 레드', accent=RED,
         map={LGREY: DSLATE, MGREY: LGREY, ORANGE: RED}, parts=[(HORN, LGREY)], swatch=[DSLATE, LGREY, CHAR, RED, WHITE, CREAM]),
]

# raptor parts: frame / jaw / thighs LGREY, armour plates / skull / guards WHITE, spine / mouth / joints CHAR,
# belly / helm / arms / feet MGREY, crest and tail blades RED, eye slits R_GLOW. BITS keeps teeth and claws white.
BITS = r'(tooth|aclaw|foot-?1c|sickle)'
RAPTOR = [
    dict(name='레드 · 흰 판금', note='3차 그대로', accent=RED, map={}, swatch=[WHITE, LGREY, MGREY, CHAR, RED, R_GLOW]),
    dict(name='마젠타 · 흰 판금', note='3차 몸 그대로, 포인트만 마젠타', accent=MAGENTA, map={RED: MAGENTA, R_GLOW: EYES[MAGENTA]},
         swatch=[WHITE, LGREY, MGREY, CHAR, MAGENTA, EYES[MAGENTA]]),
    dict(name='레드 · 건메탈 판금', note='판금을 건메탈로, 골격은 회색, 배와 투구는 밝은 회색', accent=RED,
         map={WHITE: GUN, LGREY: MGREY, MGREY: LGREY}, parts=[(BITS, WHITE)], swatch=[GUN, MGREY, LGREY, CHAR, RED, R_GLOW]),
    dict(name='진보라 · 밝은 회색 판금', note='밝은 회색 판금, 회색 골격, 건메탈 투구와 팔', accent=PURPLE,
         map={WHITE: LGREY, LGREY: MGREY, MGREY: GUN, RED: PURPLE, R_GLOW: EYES[PURPLE]}, parts=[(BITS, WHITE)],
         swatch=[LGREY, MGREY, GUN, CHAR, PURPLE, EYES[PURPLE]]),
    dict(name='앰버 옐로 · 흰 판금 + 짙은 회색 몸', note='흰 판금 아래 몸을 짙은 회색으로, 포인트는 앰버 옐로', accent=AMBER,
         map={LGREY: COAL, RED: AMBER, R_GLOW: EYES[AMBER]}, swatch=[WHITE, COAL, MGREY, CHAR, AMBER, EYES[AMBER]]),
    dict(name='오렌지 · 회색 판금 + 흰 몸', note='판금은 회색, 그 아래 몸은 흰색, 투구와 팔은 건메탈, 포인트는 오렌지', accent=ORANGE,
         map={WHITE: MGREY, LGREY: WHITE, MGREY: GUN, RED: ORANGE, R_GLOW: EYES[ORANGE]}, parts=[(BITS, WHITE)],
         swatch=[MGREY, WHITE, GUN, CHAR, ORANGE, EYES[ORANGE]]),
]
COLOURWAYS = {'imp': IMP, 'raptor': RAPTOR}


def clash(who, cw):
    """The other picks whose own accent this colourway takes."""
    return [WHO[k] for k, c in OWN.items() if k != who and c == cw['accent']]


assert all(len(v) == 6 for v in COLOURWAYS.values())
assert all(c in NAMES for v in COLOURWAYS.values() for cw in v for c in cw['swatch'])
