# The robots' colour scheme (sRGB), shared by the Blender models and the PIL sheets: a white body, one strong accent,
# mid / light grey parts and charcoal joints. The enemies keep that scheme with warm accents, away from the robots'
# sky blue (4,180,254), green (5,246,61) and lilac (217,184,255).
WHITE = (255, 255, 255)
LGREY = (174, 174, 174)
MGREY = (108, 108, 108)
CHAR = (33, 33, 33)

# key, number, Korean name, one-line concept, accent, accent name
OPTIONS = [
    ('hound', 1, '돌진 사냥개', '머리를 낮추고 앞발을 뻗은 네발 돌진, 긴 꼬리', (255, 122, 0), '오렌지'),
    ('bullet', 2, '탄환 호버', '다리 없이 낮게 뜬 총알 몸통, 뒤로 젖힌 날개', (240, 44, 52), '레드'),
    ('racer', 3, '바퀴 레이서', '큰 뒷바퀴와 낮은 쐐기 몸, 뒤로 뻗은 스포일러', (255, 200, 0), '옐로'),
    ('gecko', 4, '도마뱀 스프린터', '바닥에 붙은 납작한 몸, 대각선 보폭, S자 꼬리', (226, 30, 172), '마젠타'),
    ('raptor', 5, '랩터 러너', '수평 몸통의 두 발 질주, 뒤로 젖힌 볏과 꼬리', (255, 84, 56), '코랄 레드'),
    ('manta', 6, '화살 가오리', '앞을 가리키는 화살촉 날개로 미끄러지는 호버', (255, 72, 150), '핫 핑크'),
    ('bunny', 7, '토끼 점프봇', '뒷발을 쭉 뻗은 도약, 뒤로 눕힌 긴 귀', (255, 166, 0), '앰버'),
]
ACCENT = {k: a for k, _, _, _, a, _ in OPTIONS}
