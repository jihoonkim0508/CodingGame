# 보상

- 표기 : 현재값(증분값)

- '-' : 없음

## 웨이브 클리어 보상

2-1은 1-5의 클리어 보상만 가지고 시작 (1스테이지의 다른 로봇·블록은 넘어가지 않음)

2-4는 2-3 보상의 탱커 로봇·block()으로 길막기를 처음 배움 (2-3 보상)

3-1은 2-10까지 받은 것을 이어받음

3-3의 반사병: 3-2 보상의 for e in get_enemies()·and·reflecting·continue로 `if e.type == Enemy.Reflector and e.reflecting: continue`

3-3의 군단 트리: 3-2 보상의 while 조건·or·left·right로 잎까지 내려가 `while n.left or n.right:` 뒤 `shoot(n)`

4스테이지 '팀 플레이': 팀 보드(모든 로봇이 함께 쓰는 변수)에 `jobs = []`·`priority = {...}`를 두고 로봇마다 다른 코드로 협업. 3-4 보상이 4-1의 리스트·로봇 블록과 weak_point() (트리를 3스테이지에서 left·right로 손수 순회해 본 뒤에 주는 추상화), 4-1 보상이 4-2의 사전 블록, 4-2 보상이 4-3의 role·Robot.종류

| S-W   | 로봇              | 블록                                                                                                                                                                                        | 해금       |
|:-----:|:---------------:|:-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|:--------:|
| start | 근접 로봇 ×1        | slash() × 1                                                                                                                                                                               |          |
| 1-1   | 근접 로봇 ×1        | slash() ×1                                                                                                                                                                                | 일시정지, 배속 |
| 1-2   | -               | while ×2                                                                                                                                                                                  | 레벨업      |
| 1-3   | 근접 로봇 ×1        | slash() ×1, while ×1                                                                                                                                                                      | 회수       |
| 1-4   | 근접 로봇 ×1        | slash() ×1, while ×1                                                                                                                                                                      | -        |
| 1-5   | 원거리 로봇 ×1, 근접 로봇 ×1 | while ×2, shoot() ×1, 변수 = 값 ×1, variable ×1, get_nearest_enemy() ×1, slash() ×1                                                                                                          | -        |
| 2-1   | -               | -                                                                                                                                                                                         | 복사·붙여넣기  |
| 2-2   | -               | if ×1, 비교 ×1, get_my_pos() ×1, variable ×1, my_attack_range() ×1, 변수 = 값 ×1, get_nearest_enemy() ×1                                                                                       | -        |
| 2-3   | 탱커 로봇 ×1       | block() ×1, break_shield() ×1, shield ×2, if ×1, else ×1, 비교 ×1, 숫자 ×1, variable ×2                                                                                                                   | -        |
| 2-4   | -               | 변수 = 값 ×1, get_nearest_enemy() ×1, if ×1, is_dead() ×1, variable ×1                                                                                                                       | -        |
| 2-5   | 원거리 로봇 ×1       | while True ×3, if ×1, break ×2, 비교 ×2, get_my_pos() ×2, my_attack_range() ×2, variable ×2, shoot() ×1                                                                                     | -        |
| 2-6   | -               | if ×2, is_dead() ×1, variable ×2, break ×2                                                                                                                                                | -        |
| 2-7   | -               | if ×2, 비교 ×2, variable ×1, Enemy.Tank ×2, continue ×2                                                                                                                                     | -        |
| 2-8   | 원거리 로봇 ×1, 근접 로봇 ×1 | while ×3, if ×4, 비교 ×3, variable ×4, 변수 = 값 ×2, get_nearest_enemy() ×2, get_my_pos() ×2, my_attack_range() ×2, shoot() ×1, slash() ×1, break ×2, continue ×1, is_dead() ×1, Enemy.Tank ×1 | -        |
| 2-9   | -               | -                                                                                                                                                                                         | -        |
| 2-10  | 근접 로봇 ×1, 유틸 로봇 ×1 | while True ×1, 변수 = 값 ×1, get_nearest_enemy() ×1, variable ×1, slow() ×1                                                                                                                 | -        |
| 3-1   | -               | -                                                                                                                                                                                         | -        |
| 3-2   | -               | for e in get_enemies() ×1, get_enemies() ×1, if ×2, else ×1, 비교 ×1, Enemy.Tank ×1, and ×1, reflecting ×1, continue ×1, break ×1, while 조건 ×1, or ×1, left ×2, right ×2, 변수 = 값 ×3, variable ×3, get_nearest_enemy() ×1, shoot() ×2, my_hp() ×1 | -        |
| 3-3   | -               | for e in get_enemies() ×1, get_enemies() ×1, if ×1, elif ×1, 비교 ×3, Enemy.Tank ×3, 변수 = 값 ×1, variable ×4, break ×1, shoot() ×1, front() ×2, top() ×2| -        |
| 3-4   | -               | [] ×1, weak_point() ×2, append() ×1, pop(0) ×1, len() ×1, in ×1, not ×1, get_robots() ×1, while 조건 ×1, while True ×2, for e in get_enemies() ×2, get_enemies() ×1, if ×2, else ×1, and ×1, 비교 ×2, Enemy.Tank ×1, 변수 = 값 ×3, variable ×14, 숫자 ×2, is_dead() ×1, shoot() ×2, get_nearest_enemy() ×1 | 팀 보드 |
| 4-1   | -               | { } 사전 ×1, d[k] ×2, 변수 = 값 ×3, variable ×6, for e in get_enemies() ×1, get_enemies() ×1, if ×1, 비교 ×1, Enemy.Tank ×3, 숫자 ×3, while True ×1, shoot() ×1, get_nearest_enemy() ×1 | -        |
| 4-2   | -               | weak_point() ×2, role ×1, Robot.Melee ×1, get_robots() ×1, slow() ×1, for e in get_enemies() ×1, while True ×1, variable ×4, 비교 ×1, if ×1, and ×1, 숫자 ×1 | -        |
| 4-3   | -               | -                                                                                                                                                                                         | -        |

## 코인

레벨업 비용 50 × 현재 레벨, 3-1은 2-10의 코인을 이어받음 (스테이지 맵에서 시작하면 100)

| S-W  | 시작 코인 | 처치당 | 처치 코인 (전멸 시) |
|:----:| -----:| ---:| ------------:|
| 1-1  | 100   | 5   | 20           |
| 1-2  | -     | 5   | 20           |
| 1-3  | -     | 5   | 40           |
| 1-4  | -     | 5   | 40           |
| 1-5  | -     | 5   | 20           |
| 2-1  | 100   | 5   | 40           |
| 2-2  | -     | 5   | 45           |
| 2-3  | -     | 5   | 50           |
| 2-4  | -     | 5   | 60           |
| 2-5  | -     | 5   | 35           |
| 2-6  | -     | 5   | 35           |
| 2-7  | -     | 5   | 50           |
| 2-8  | -     | 5   | 60           |
| 2-9  | -     | 5   | 95           |
| 2-10 | -     | 5   | 35           |
| 3-1  | -     | 5   | 40           |
| 3-2  | -     | 5   | 50           |
| 3-3  | -     | 5   | 65           |
| 3-4  | -     | 5   | 115          |
| 4-1  | -     | 5   | 80           |
| 4-2  | -     | 5   | 65           |
| 4-3  | -     | 5   | 75           |
