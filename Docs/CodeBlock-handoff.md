# CodeBlock 작업 인계 — 2026-09-18

## 최신 변경: 자유 배치 / 수치 통합 / 무한 반복

- Program CommandDropZone의 serialized freePlacement=true (MCP로 씬에 저장). 루트는 자유 좌표를 유지하고, 30 UI 단위 안에서 명령 박스의 위·아래 가장자리를 맞추면 연결한다. next 연결 사전으로 묶음을 보존하고 내부 크기 변경 시 연결된 후속 블록만 재배치한다. 중첩 본문/인자 슬롯은 기존 자동 레이아웃을 사용한다.
- 루트 명령 묶음은 잡은 블록부터 아래 연결된 블록을 함께 이동한다. 분리된 묶음이나 남은 값/조건 블록을 포함한 채 Apply할 수 없다. 패널 안에서 슬롯 드롭에 실패하면 자유 위치에 놓인다. 패널 밖은 삭제, 우클릭 삭제 없음. 드래그 시작 위치의 잡은 오프셋을 유지한다.
- 삼각 결합부 제거. 명령은 평평한 박스, 제어는 C 모양. 값 pill/조건 hexagon과 타입 제약은 유지한다.
- Float 목록 인스턴스/참조 제거: 고정 목록 17개, Number 하나. `.`이 있으면 float, 없으면 int. f/F 접미사와 지수 표기는 거부. 이전 Float.prefab 파일은 참조되지 않는 이전 자산으로 남아 있다.
- While.prefab/씬: `while True:` 라벨, arguments 빈 배열, 기존 조건 슬롯과 별도 콜론 라벨 비활성화. PythonTreeCompiler도 while 인자 0개, 무조건 `while True:` 출력. 빈 본문은 기존처럼 pass 허용.
- Comparison.prefab/씬: 연산자 폭 96, 글자 22, 밝은 옵션 배경과 어두운 글자, 항목 높이 42. 실제 펼친 화면과 선택 동작 확인.
- Changed/Apply 실패 시 AppliedSource도 비워 이전 정상 문자열을 남기지 않는다. 변수 탐색은 자유 묶음의 연결 순서를 따른다.

검증: dotnet 검사, check_tree_python.py, check_python.py 통과. Play Mode에서 자유 좌표 유지, 분리 시 미완성, 재연결 Apply, 묶음 이동/삭제, 떨어진 값 검출, if/elif/else 조건 보존, nested break 분리/연결, int/float range 판정, PointerEventData 드래그 오프셋과 패널 내부/외부 처리, dropdown 펼치기/선택/if에 연결 후 Apply 통과. Inspector의 freePlacement, 숫자 항목 1개, while 인자 0개와 라벨, 드롭다운 폰트/색 재확인. 마지막 상태 Edit Mode, 씬 저장 완료.

MCP는 Play Mode 전환 직후 일시적으로 네트워크 실패했고 재연결했다. 그때 Unity Pipeline의 `Failed to handle /api/exec request: Thread was being aborted` 오류 1개가 콘솔에 기록되었다(기능 스크립트 오류 아님, 스택 확인). 재연결 후 모든 후속 Play Mode 테스트 성공. 컴파일 실패 없음. 캡처는 Application.runInBackground=true를 eval에서 설정하고 게임 프레임을 진행한 뒤 수행한다. 이 설정은 PlayerSettings에 저장하지 않았다.

## 최신 조작 변경

아래는 자유 배치 변경 이전의 검증 기록이다. 패널 내부 드롭 실패 동작은 위 최신 변경이 우선한다.

목록의 TMP_InputField/TMP_Dropdown은 Bind 시 interactable과 enabled를 끈다. 이벤트 핸들러도 비활성화해 숫자 입력창 위에서 드래그해도 블록으로 전달된다. 배치된 복사본은 다시 활성화된다. 우클릭 삭제를 제거하고 OnEndDrag에서 함수 정의 패널 전체(definitionPanel, /CodeBlockCanvas/Editor) 밖에 놓은 배치 블록을 삭제한다. 성공한 드롭은 삭제하지 않는다. 내부 드롭 실패/타이틀바/취소는 보존, 목록 원본을 밖에 놓는 것은 취소만 한다. 연결된 분기 묶음, 변수 참조 정리도 기존 삭제 경로로 처리한다. 씬 안내 문구 및 사용 문서 갱신.

검증: C# 빌드 통과. Play Mode에서 목록 입력·드롭다운 비활성, 배치 후 활성, 실제 숫자 입력 영역 raycast의 IDragHandler가 CommandBlockView인 것, 배치 dropdown.Show, 패널 외부/내부/타이틀바/취소/우클릭/성공 드롭, 변수 선언 폐기 및 분기 묶음 폐기 통과. 카테고리 전환 직후 같은 프레임의 raycast는 이전 레이아웃을 검사하므로 프레임 진행 후 검증했다. Console 오류 0. 별개 Unity AI Account API 네트워크 경고 1개가 있으며 기능 오류는 아니다.

## 후속 검증 완료

아래 내용은 사용량 임계점 당시의 기록이다. 이후 작업을 재개하여 남은 확인을 완료했다. 최신 사용/구현 안내는 `CodeBlock-implementation.md`를 참고한다.

- 드롭다운 오류 원인: TMP_Dropdown.Start에서 초기화하는 m_AlphaTweenRunner가 아직 null이었다. Unity가 백그라운드에서 게임 프레임을 진행하지 않아 Time.frameCount가 2에 머무른 상태에서 같은 프레임 Show를 호출한 테스트 문제. 검증 중 Application.runInBackground=true로 게임 프레임을 진행한 뒤 실제 RaycastAll→pointerClick으로 열기/옵션 선택에 성공했다. 영구 PlayerSettings는 변경하지 않았다.
- if/elif/else 폭이 모두 808.16으로 일치함을 측정하고 화면을 확인했다.
- 15개 분기 UI Apply 성공. 2041px 작업 영역에서 실제 raycast→scroll 이벤트로 anchoredPosition 0→1280 이동 확인.
- 필수 serialized object 참조 누락 0, CanvasRenderer 누락 0, EventSystem 1개.
- Console을 비우고 새 Play Mode 실행 후 errors=0/warnings=0 확인. 기존 컴파일러 회귀 Python 테스트도 다시 통과.
- 최종 화면은 Assets/Temp/CodeBlock-final.png. 테스트 프로그램은 Play Mode에만 만들며 저장 씬은 빈 프로그램이다.

## 목표와 현재 상태

Entry 방식 Python 3 블록 코딩을 Assets/!_Project/Scenes/CodeBlock.unity에 구현 중이다. 사용자는 사용량 5% 이하에서 컨텍스트를 Docs에 저장하도록 요청했다. 직전 확인은 5시간 사용량 98% 사용이었다. 이전 요약 저장은 apply_patch에서 동일 파일 delete/add 오류로 실패했고 이 문서가 실제 저장본이다.

씬과 19개 프리팹은 저장되었으며 마지막 상태는 Edit Mode, CodeBlock 씬이다. 원래 Main Camera/Directional Light를 보존하고 CodeBlockCanvas/EventSystem을 추가했다. UI는 블록 목록/조립 영역/Python 미리보기의 3열이다.

## 규칙과 도구

- Unity MCP mcp__unity__* 정상 동작. 예전 8080 주소는 사용하지 말 것.
- 씬/프리팹/Inspector 수정은 MCP 사용. 임시 Editor 스크립트, runtime Find로 연결 대체 금지. 이번 구성은 MCP native batch로 제작했다.
- 셸은 rtk 접두사. PowerShell 파일 읽기는 UTF8.
- ponytail, unity, ui, ui-ugui 및 scrollview 가이드 읽음. 서브에이전트 없음.
- MCP batch.operations[].params 및 set_component_properties.properties는 스키마와 달리 실제 객체를 보내야 동작.
- 배열 크기 변경은 set_serialized_field에서 field=palette.Array.size, value=숫자. 요소는 set_component_properties 가능.
- TMP 줄바꿈 필드 m_TextWrappingMode. m_enableWordWrapping은 없음.

## 구현

Assets/!_Project/Scripts/BlockCoding/:

- BlockProgram.cs: enum 기존 인덱스 보존, For=16/Break=17/Continue=18 추가, CodeBlock.Body 추가. 기존 IDE용 컴파일러 유지.
- PythonTreeCompiler.cs: 중첩 AST→Python, 값/조건/명령 구분, 연결/인자 검증, 루프/분기, 변수 경로 검사. 0f→0.0. Python 실행 엔진은 범위 밖.
- CommandBlockShape.cs: C자/삼각 결합/값 pill/논리 hexagon MaskableGraphic. CanvasRenderer RequireComponent 추가로 초기 도형 미표시 해결.
- CommandBlockView.cs: 프리팹 참조, 크기 계산, 입력, 드롭다운 변수↔get_distance, 드래그. Copy는 프리팹 구조 Instantiate 후 활성화.
- CommandDropZone.cs: 타입별 드롭, 순환 금지, 분기 그룹 이동, 크기/연결 레이아웃. 마지막 변경은 if/elif/else 폭 통일.
- CommandCodingPanel.cs: 카테고리, f0/f1 기본 이름, 변수 목록과 참조 갱신, Apply Debug.Log, 미리보기, 우클릭 삭제.

Assets/!_Project/UI/CommandBlocks/: For, While, If, Elif, Else, Break, Continue, Attack, Wait, Number, Float, DeclareVariable, PositionX, PositionY, NearestEnemy, True, False, Comparison, Variable 프리팹.

변수는 선언 시 생성되고 삭제 시 배치된 참조까지 제거. Enemy 변수에서 get_distance 드롭다운. void 인스턴스 메서드는 현재 정의되지 않음. 모든 UI는 Inspector에서 편집 가능한 authored 구조.

## 검증

- rtk proxy dotnet run --project Tests/BlockCoding/BlockCoding.Check.csproj 통과.
- rtk proxy python Tests/BlockCoding/check_tree_python.py 통과 (실제 Python 실행, 64 elif, 루프, 메서드 등).
- 기존 check_python.py 통과.
- Unity recompile failed=false. 마지막 Play Mode groundTruth errors=0/warnings=0. 과거 도구 오류 로그는 버퍼에 남아 있으니 since cursor로 구별.
- Play Mode에서 enemy 선언→for→if enemy.get_distance(...)<=5→attack / else wait 구성 및 Apply 성공.
- 잘못된 슬롯, orphan else, 루프 밖 break, self nesting 거부 확인.
- 분기 그룹 이동 후 조건 보존, 메서드 dropdown 왕복 시 인자 보존, 드래그 취소, 이름 변경 참조 갱신 확인.
- PointerEventData로 실제 OnBeginDrag/OnDrag/OnDrop/OnEndDrag 호출해 For 추가, loop 안 Break 및 Apply 버튼 onClick 확인.
- 선언 삭제 시 팔레트/배치 참조 제거, 빈 함수 Apply 확인.
- TMP dropdown.Show/Hide 별도 호출 성공.

## 남은 확인

1. 긴 eval에서 같은 프레임에 변수 생성 직후 dropdown.Show까지 호출하면 NullReference 한 번 발생. stack 미수집. 다음 별도 호출 Show/Hide 성공. 프레임 초기화 타이밍인지 확인하고 실제 사용자 클릭 경로 재현 시 수정.
2. 마지막 분기 폭 통일 변경 후 스크린샷 재검증.
3. Raycast를 거친 실제 입력/스크롤/드롭다운과 긴 elif UI 연결 최종 확인.
4. Inspector 참조 최종 감사, Console 확인, 최종 보고.

샘플 캡처 Assets/Temp/CodeBlock-sample.png (폭 통일 변경 전). CodeBlock-initial.png는 수정 전 화면. MCP capture save_path는 Assets 기준.

테스트는 MCP eval에서 GameObject.Find("CodeBlockCanvas").GetComponent<CommandCodingPanel>()로 조회했다. 이는 검사 코드이며 런타임 소스에 Find 추가 금지. Program/Palette/VariablePalette 공개 API로 조립 가능. 씬에는 빈 프로그램 저장, 테스트 샘플은 Play Mode 한정.

사용자의 기존 미커밋 파일이 많다. 일괄 reset/stage/delete 금지.
