# Soft pastels over slate (sRGB), shared by the Blender models and the PIL sheets.
CORAL = (240, 174, 166)
PEACH = (250, 214, 190)
SLATE = (138, 152, 176)
VISOR = (102, 114, 140)
BUTTER = (250, 230, 162)
LILAC = (212, 198, 240)
LAVENDER = (188, 174, 226)
POWDER = (230, 222, 247)
EYE = (255, 248, 224)
WHITE = (255, 255, 255)
BLUE = (17, 204, 255)
GREY = (104, 110, 122)

NAMES = {CORAL: '더스티 코랄', PEACH: '소프트 피치', SLATE: '슬레이트', VISOR: '화면 슬레이트', BUTTER: '버터 옐로',
         LILAC: '라일락', LAVENDER: '라벤더', POWDER: '파우더 라일락', EYE: '눈 크림'}

# each design's colours, largest area first
USED = {
    'normal_a': [CORAL, PEACH, SLATE, VISOR, BUTTER, LILAC],
    'fast_a': [CORAL, PEACH, LILAC, SLATE, BUTTER],
    'tank_a': [SLATE, CORAL, PEACH, BUTTER, LILAC],
    'boss_a': [CORAL, PEACH, SLATE, LILAC, BUTTER],
    'normal_b': [LAVENDER, POWDER, BUTTER, SLATE, VISOR],
    'fast_b': [LAVENDER, BUTTER, PEACH, SLATE, VISOR],
    'tank_b': [LAVENDER, BUTTER, CORAL, SLATE, POWDER],
    'boss_b': [LAVENDER, POWDER, BUTTER, PEACH, SLATE],
}
