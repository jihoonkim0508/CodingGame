# 명령어 블록 → Python 3

## 현재 상태

파서, UGUI/TMP 편집 컴포넌트, Enemy 거리 메서드를 작성했다. Apply는 Python 문자열을
`Debug.Log`로 출력하고 `onApplied` 이벤트로 전달한다. Python 인터프리터 실행이나 게임 전투 연결은 하지 않는다.

**IDE 씬에 테스트 UI와 예제 네 줄을 구성했다. 씬을 열고 Play를 누르면 즉시 편집할 수 있다.**
팔레트의 블록을 줄/인자 슬롯으로 드래그하고, 줄 배경을 좌클릭/우클릭해서 들여쓰기를 조절한다.
`+ Add line`으로 줄을 추가하고, ×로 블록/줄을 삭제한다. `Apply function`을 누르면 Console에 코드가 출력된다.
씬과 프리팹의 Inspector 참조는 Unity MCP로 연결했다. 코드가 참조를 자동 검색하지 않는다.
새 줄/블록은 연결된 프리팹을 복제하며, 런타임에 UI 계층을 코드로 조립하지 않는다.

## Editor 구성

대상: `Assets/!_Project/Scenes/IDE.unity`. 원래 카메라와 조명을 유지하고 다음 UI를 추가했다.
프리팹은 `Assets/!_Project/UI/BlockCoding`에 있으며, `Blocks` 폴더에 14종 블록이 있다.

- Canvas: Screen Space Overlay, CanvasScaler Scale With Screen Size / 1600×900 / Expand, GraphicRaycaster.
- EventSystem: 프로젝트의 Input System UI Input Module 연결.
- 왼쪽 Command Blocks 팔레트: 명령/조건/적 정보/숫자 블록을 종류별 색상으로 구분.
- 오른쪽 패널: 타이틀 `def`, 메서드명 TMP_InputField, `():`, 줄 ScrollRect, Add Line 버튼,
  Python 미리보기 ScrollRect + TMP_Text, 상태 TMP_Text, Apply 버튼.
- 색상 참고: 줄 배경 하늘색, 패널 진청색, Attack 분홍, Wait 청회색,
  If/Elif/Else 보라, While 노랑, Enemy/좌표 초록, 숫자 파랑, Apply 민트.
- 긴 블록과 들여쓰기를 위해 줄 영역은 가로/세로 스크롤을 제공한다.

패널 루트의 `BlockCodingPanel`에 메서드 입력, 미리보기, 상태, 두 버튼, 줄 컨테이너,
줄 프리팹, 초기 줄 리스트를 모두 연결한다. 초기 줄 네 개를 씬에 배치하고 리스트 순서대로 연결한다.
`Awake`에서 정적 카운터로 f0, f1, f2…를 부여한다. 입력값을 바꿔 이름을 수정할 수 있다.

### 줄 프리팹

루트에 Image(raycasts 활성), HorizontalLayoutGroup, `CodeLineView`를 둔다.
자식으로 줄 번호 TMP_Text, 들여쓰기 공간 LayoutElement, 블록 컨테이너, 삭제 버튼을 배치한다.
블록 컨테이너에 HorizontalLayoutGroup과 `BlockDropZone`을 둔다.
`BlockDropZone.content`는 해당 컨테이너, `argumentSlot=false`, `blocks`는 초기 배치 순서로 연결한다.
`CodeLineView`에 네 참조를 연결한다. 줄 배경 좌클릭은 들여쓰기 +1, 우클릭은 -1(최소 0).
입력 필드/버튼/블록 클릭은 들여쓰기를 변경하지 않는다.

### 코드 블록 프리팹

각 블록 루트에 Image, CanvasGroup, HorizontalLayoutGroup, `CodeBlockView`를 두고
종류, CanvasGroup, 삭제 버튼, 필요한 입력 필드와 인자 슬롯 배열을 Inspector에서 연결한다.
번호/비교 외 블록에는 해당 입력 필드가 필요하지 않다. TMP 라벨은 코드 토큰/괄호를 표시한다.

| Kind | 표시 | 인자 슬롯 순서 |
|---|---|---|
| Attack | attack( [enemy] ) | Enemy |
| Wait | wait( [seconds] ) | Number |
| While / If / Elif | while / if / elif | 없음. 같은 줄 오른쪽에 조건 블록을 놓음 |
| Else | else: | 없음 |
| NearestEnemy | get_nearest_enemy() | 없음 |
| Distance | [enemy].get_distance( [x], [y] ) | Enemy, Number, Number |
| PositionX / PositionY | get_pos_x() / get_pos_y() | 없음 |
| Number | 숫자 입력, 초기 0 | 없음 |
| Comparison | 드롭다운, 초기 == | 없음 |
| True / False | True / False | 없음 |

각 인자 슬롯은 별도 `BlockDropZone`이며 `argumentSlot=true`, `acceptedValue`를 표처럼 지정한다.
슬롯의 Content와 초기 블록 리스트도 연결한다. 공격 대상/거리 대상 기본 블록은 NearestEnemy,
Wait의 기본값은 Number(0), 거리 x/y 기본 블록은 PositionX/PositionY로 배치한다.
인자 슬롯은 값 블록을 하나만 받으며 새 블록을 놓으면 이전 값이 교체된다.

프리팹 자체는 `palette=false`. 팔레트에 배치한 인스턴스만 `palette=true`로 변경하고
`palettePrefab`에 원본 프리팹을 연결한다. 팔레트 블록을 줄/슬롯으로 드래그하면 프리팹 복사본이 생성된다.
배치된 블록은 드래그로 이동·재정렬하고 × 버튼으로 삭제한다.

## 코드 규칙

조건 줄 예: `If` + `Distance` + `Comparison(<)` + `Number(10)`.
다음 줄의 배경을 한 번 클릭하고 Attack을 넣으면 다음과 같이 출력된다.

```python
def f0():
    if get_nearest_enemy().get_distance(get_pos_x(), get_pos_y()) < 10:
        attack(get_nearest_enemy())
```

줄의 들여쓰기는 블록 종류와 무관하게 편집한다. Apply는 잘못된 들여쓰기, 빈 조건/인자,
잘못된 elif/else 연결, 숫자/메서드명 오류를 줄 번호와 함께 알려 준다.
정상 Python 문법에 맞게 함수 본문은 추가 4칸, 사용자 들여쓰기 한 단계는 4칸으로 변환한다.
완전히 빈 함수는 `pass`를 출력한다. 조건문 본문이 없으면 오류다.

while은 매 반복 조건을 검사하고, 처음부터 또는 반복 중 조건이 거짓이면
`while ... else: raise RuntimeError("while condition is false")`로 예외를 발생시킨다.
Apply에서는 조건을 평가하거나 무한 루프를 실행하지 않는다.

숫자는 부호/소수/지수 표기를 지원하며 길이 및 값 범위 제한 없이 문자열로 유지한다.
선행 0은 Python 문법에 맞게 정리한다. Python 인터프리터 자체의 숫자 제한은 이후 실행기 설정에 따른다.

`Enemy.get_distance(x, y)`는 Unity 월드 XY 평면 거리를 float로 반환한다.
생성된 코드가 사용하는 attack, wait, get_nearest_enemy, get_pos_x/y 및 Enemy 바인딩은
후속 Python 실행기에서 제공해야 한다. 가장 가까운 적이 없는 경우의 정책도 그 실행기에서 정한다.

## 검증

프로젝트 루트에서:

```powershell
rtk dotnet run --project Tests/BlockCoding/BlockCoding.Check.csproj
rtk python Tests/BlockCoding/check_python.py
```

.NET 10 SDK, Python 3, 로컬 Unity 6000.3.21f1과 import된 UGUI/TMP DLL을 사용한다.
다른 Unity 경로는 `-p:UnityEditorPath=...`로 지정한다.
C# 9로 모든 새 스크립트를 컴파일하고, 파서 검증 후 실제 생성된 문자열을 Python에서 파싱/실행한다.
분기, 중첩, while의 초기 false/반복 중 false, 숫자, 잘못된 입력을 검사한다.
추가로 Unity MCP를 통해 Play Mode에서 Apply 버튼, 줄 추가, 실제 UI raycast 경로의 팔레트 드롭,
숫자 변경, 좌우 클릭 들여쓰기, 드롭다운 열기/선택 및 수정된 코드 출력을 확인했다.
Unity에서 1600×900 UI를 렌더링하여 네 줄과 코드 미리보기의 배치를 검수했다.
