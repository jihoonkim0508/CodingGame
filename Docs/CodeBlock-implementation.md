# CodeBlock 사용 및 구현 안내

대상 씬: `Assets/!_Project/Scenes/CodeBlock.unity`

## 사용법

1. `CodeBlock.unity`를 열고 Play를 실행한다.
2. 왼쪽에서 제어/명령/값/논리 카테고리를 선택한다.
3. 블록을 가운데 작업 영역의 원하는 위치에 자유롭게 놓는다. 명령 박스의 위·아래 가장자리를 가까이 맞추면 연결된다. 떨어진 블록은 자동 정렬하거나 코드에서 무시하지 않고 미완성으로 처리한다. 값/조건 블록은 맞는 입력 슬롯으로 드래그한다.
4. ㄷ자 내부에 명령을 넣는다. `elif/else`는 `if/elif` 다음에 연결한다. 작업 영역에서 연결된 블록은 잡은 블록부터 아래 묶음이 함께 이동하며, 중간 블록을 떼어 별도 묶음으로 둘 수 있다. `while True:`는 조건 슬롯 없는 무한 반복이다.
5. 배치한 블록에서 숫자·변수명을 입력한다. 목록의 입력창과 드롭다운은 비활성 상태다. 배치된 Enemy 변수의 드롭다운에서 `get_distance`를 선택하면 x/y 슬롯이 표시된다.
6. 배치한 블록을 함수 정의 패널 밖으로 드래그해서 놓으면 삭제된다. 패널 내부에서 슬롯에 연결하지 않고 놓으면 자유 배치된다. 변수 선언을 삭제하면 변수 목록과 배치된 참조도 제거된다. 우클릭은 삭제하지 않는다.
7. 하나의 명령 흐름을 완성한 뒤 Apply를 누르면 Python 문자열을 Unity Console에 출력한다. 분리된 묶음, 남은 값/조건 블록, 빈 입력 및 잘못된 분기·타입은 미완성이며 Apply가 거부된다. 빈 제어 본문은 Python의 `pass`로 출력한다.

빈 배경을 드래그하거나 휠을 사용하면 긴 작업 영역과 목록을 스크롤할 수 있다. Python 미리보기도 가로·세로 스크롤을 지원한다. 함수명 기본값은 `f0`, `f1`, ... 이다.

## 코드

`PythonTreeCompiler`는 중첩된 `CodeBlock` 목록을 Python 3 함수 문자열로 변환한다.
`Arguments`에는 입력 블록, `Body`에는 제어 블록 내부의 명령 목록을 넣는다.
`if`, `elif`, `else`는 같은 목록에서 순서대로 연결하며 각자 조건과 본문을 유지한다.
빈 본문은 `pass`로 출력한다. Python을 게임 안에서 실행하는 기능은 포함하지 않는다.

- for/while, break/continue, if/elif/else
- 값/조건/명령 슬롯 구분, 분기 연결 규칙, 메서드 인자 검사
- 변수 선언과 참조, 변수 팔레트용 선언 목록, Python 이름과 숫자 검사
- get_pos_x/y, get_nearest_enemy, enemy.get_distance(x, y), attack, wait
- 수치 블록 하나로 통합: `2`는 int, `2.0`은 float. 소수점 유무로 구분하며 `f` 접미사/지수 표기는 허용하지 않는다.
- 분기 또는 반복문에서만 선언된 변수를 이후에 무조건 사용하는 경우 오류 처리

기존 `IDE.unity`용 `PythonBlockCompiler`의 동작은 유지한다.
새 씬의 Apply는 `PythonTreeCompiler.Compile(functionName, blocks)`를 사용한다.
`CodeBlock`의 기존 enum 값은 보존했으며 끝에 For/Break/Continue만 추가했다.

## 씬과 프리팹

Unity MCP로 `CodeBlockCanvas`와 `EventSystem`, 19개 블록 프리팹을 구성하고 Inspector 참조를 연결했다. 기존 카메라와 조명은 유지한다.

현재 목록은 Float 항목을 제거하여 17개 고정 블록과 동적으로 추가되는 변수 블록을 제공한다. While/Comparison 프리팹 변경도 저장했다. 명령 블록의 삼각 결합부는 제거했고, 논리 드롭다운은 확대된 글자와 밝은 목록 배경/어두운 글자 대비를 사용한다.

- 프리팹: `Assets/!_Project/UI/CommandBlocks/`
- 패널 동작: `CommandCodingPanel.cs`
- 블록 입력·메서드 전환·드래그: `CommandBlockView.cs`
- 슬롯 검사·결합·레이아웃: `CommandDropZone.cs`
- 크기에 따라 늘어나는 결합 모양: `CommandBlockShape.cs`

런타임에는 작성된 프리팹 구조를 복제한다. 임시 Editor 스크립트나 런타임 자동 참조 검색으로 씬 구성을 대체하지 않았다. 명세에 현재 구현할 void 인스턴스 메서드가 없으므로 해당 드롭다운 항목은 제공하지 않는다.

## 실행한 검사

```powershell
rtk proxy dotnet run --project Tests/BlockCoding/BlockCoding.Check.csproj
rtk proxy python Tests/BlockCoding/check_tree_python.py
rtk proxy python Tests/BlockCoding/check_python.py
```

모두 통과. 생성된 Python은 실제 Python 3에서도 실행해 검증했다.

Unity Editor 컴파일과 Play Mode에서는 다음을 확인했다.

- 중첩 for/if/elif/else와 메서드 호출, Apply 버튼의 Console 출력.
- 값/조건 혼용, orphan else, 루프 밖 break 및 자기 자신 내부 배치 거부.
- 분기 묶음 이동 후 조건 유지, 메서드 전환 후 인자 보존, 변수 이름 변경과 선언 삭제 동기화.
- PointerEventData 드래그 핸들러, 실제 GraphicRaycaster로 드롭다운 열기/선택 및 휠 스크롤.
- 15개 분기 UI 구성/Apply와 2041px 작업 영역 스크롤. 컴파일러는 64개 연속 elif도 검사.
- 필수 Inspector 참조 누락 없음, CanvasRenderer 누락 없음, EventSystem 1개.
- 새 Play Mode 검증 시 Console 오류·경고 0개.

MCP 테스트에서 생성한 드롭다운을 같은 프레임에 즉시 열면 TMP.Start 이전이라 오류가 발생할 수 있다. 게임 프레임을 진행한 뒤 실제 레이캐스트 클릭으로 검증했으며 정상 동작했다. 제품 코드에는 이 테스트 타이밍을 우회하는 코드를 추가하지 않았다.
