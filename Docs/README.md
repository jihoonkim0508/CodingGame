# CodingGame 문서

블록 코딩으로 로봇 행동을 작성하는 Python 입문용 타워 디펜스(Unity 6, URP). 2026-10-08 기준 코드와 맞춘 문서다.

| 문서 | 내용 |
|---|---|
| [게임_기획.md](게임_기획.md) | 콘셉트, 대상, 코어 루프, 스테이지 커리큘럼, 로봇·적·블록, 밸런스 규칙 |
| [구조.md](구조.md) | 폴더, 주요 스크립트 책임, 데이터 asset, `Defense.unity` 오브젝트, 규칙 |
| [튜토리얼.md](튜토리얼.md) | Stage 1(1-1~1-5) 학습 흐름, 진행 조건, JSON 콘텐츠, 대사 수정, UX 결정 |
| [개발_테스트.md](개발_테스트.md) | 헤드리스·Unity 검사 실행, 개발 에디터 사용법, 알려진 공백 |
| [flow_stage1/](flow_stage1/) | Stage 1 대사 JSON 5개(게임이 쓰는 원본, 에디터가 Unity로 자동 동기화)와 같은 내용의 읽기용 Markdown, JSON 형식 안내서 |

## 원본 자료

- `CodinggameDoc.pdf`: 최초 기획서.
- `References/기획서 프롬포트_v4_Python_수정본.pdf`: 원 기획 프롬프트(20쪽).
- `ReferencesImg/`: UI 프로토타입, 블록 모양(값·논리·제어·메서드·변수 선언), 킹덤러쉬·명일방주 화면 참고.

## 코드와 테스트 위치

| 위치 | 내용 |
|---|---|
| `Assets/!_Project/Scenes/Defense.unity` | 유일한 씬이자 유일한 빌드 씬 |
| `Assets/!_Project/Scripts/Defense/` | 전투·HUD·진행·튜토리얼 런타임 |
| `Assets/!_Project/Scripts/BlockCoding/` | 블록 모델, Python 변환기, 블록 편집 UI |
| `Assets/!_Project/Editor/Defense/` | 개발 에디터 창(`CodingGame > 개발 에디터`) |
| `Assets/!_Project/Defense/` | 로봇·적 정의, 프리팹, 이펙트, Stage 1 캠페인·튜토리얼 데이터 |
| `Assets/ThirdParty/` | Kenney(CC0 UI 프레임·아이콘), Travis Game Assets(피격 이펙트), slash5-HungNguyen(VFX) |
| `Assets/Settings/InputSystem_Actions.inputactions` | 프로젝트 입력 액션 |
| `Tests/Defense`, `Tests/BlockCoding`, `Tests/EditorOptions` | 헤드리스 .NET 검사와 Python 검사 |
| `Tests/Unity`, `Tests/*/*.playmode.cs` | Unity Play Mode 검사(MCP로 실행) |
