# The robots' colour scheme (sRGB), shared by the Blender models and the PIL sheets: white, mid / light grey and
# charcoal, plus ONE accent. Every runner takes the same red, away from the robots' sky blue, green and lilac, so the
# fast role never clashes with the other enemy roles. The body leans white, grey or dark gunmetal per option.
WHITE = (246, 246, 246)
LGREY = (174, 174, 174)
MGREY = (108, 108, 108)
CHAR = (33, 33, 33)
GUN = (64, 66, 74)               # dark gunmetal armour: a step above charcoal so its cel bands still show
RED = (240, 44, 52)
GLOW = (255, 92, 80)             # eye slits and thruster glow: the accent, self-lit

# key, number, Korean name, one-line concept, body tone (for the caption)
OPTIONS = [
    ('hound', 1, '강철 사냥개', '판금을 겹친 몸을 길게 뻗어 질주하는 기계 사냥개, 벌린 턱과 뒤로 젖힌 칼날 귀', '화이트 판금 + 건메탈 골격'),
    ('panther', 2, '흑철 표범', '앞발을 내지르며 덮치는 육중한 기계 표범, 붉은 칼날 갈기와 검치', '차콜과 건메탈 판금 + 붉은 칼날 갈기'),
    ('raptor', 3, '추격 랩터', '몸을 수평으로 눕혀 달리는 두 발 랩터, 갈고리 발톱과 붉은 볏', '라이트 그레이 + 화이트 판금'),
    ('mantis', 4, '낫 사마귀', '네 다리로 낮게 달리며 붉은 낫 두 개를 얼굴 앞에 세운 기계 사마귀', '건메탈 배 + 화이트 가슴과 팔'),
    ('hawk', 5, '강철 매', '날개를 뒤로 접고 길 위를 스치듯 내리꽂는 기계 맹금, 앞으로 뻗은 발톱', '화이트 날개 + 차콜 몸통'),
    ('shark', 6, '칼날 상어', '다리 없이 낮게 떠서 미끄러지는 호버 상어, 톱니 턱과 칼날 지느러미', '건메탈 등 + 화이트 배'),
    ('centipede', 7, '강철 지네', '마디마다 판금과 붉은 가시를 단 긴 몸을 S자로 꿈틀대며 달리는 기계 지네', '차콜 골격 + 라이트 그레이 마디 판금'),
]
