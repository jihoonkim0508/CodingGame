# 스테이지 선택 섬

- 작업 씬: `Assets/!_Project/Scenes/Stage.unity`.
- 선택 순서: 개발자의 작업실 → 데이터 분기 구역 → 자동화 생산 공장. 선택 가능한 섬은 세 개입니다.
- 모델·머터리얼·메시·개별 프리팹: 기존 요청한 저장 위치의 하위 폴더인 `Resources/Models/Stage01_Prop/StageSelection`.
- 전체 배치 프리팹: `Resources/ArtPrefabs/StageSelectionWorld.prefab`.
- 모델은 실제 3D 메시입니다. 작업실의 책상·모니터·키보드, 데이터 허브와 IF/ELIF/ELSE 단말, 공장의 컨베이어·로봇 팔을 테마별로 배치했습니다.
- 기존 단순 모델은 `StageSelection_OriginalLayout` 아래에 비활성 상태로 보존합니다.
- `Tools > Art > Stage Selection > Build Three Theme Islands`로 생성 모델과 Stage 씬의 배치를 다시 만들 수 있습니다. 직접 편집한 생성 모델은 재생성 시 갱신되므로, 변형본이 필요하면 별도 프리팹으로 저장하세요.
- 입장·해금·진행 저장은 기존 StageMap 시스템을 사용합니다. 테마별 색상은 유지하고 상태 표시 링과 모델의 밝기로 잠금·입장 가능·클리어를 구분합니다.
- 섬의 미세한 부유와 허브 회전은 화면 연출입니다. 클릭 판정과 선택 지점은 움직이지 않습니다.
- 새 코드는 `Resources/ArtScripts/StageSelection`에 있으며, 기존 StageMap 코드에는 선택 대상 탐색·테마 상태 표시·카메라 구도·이름 표시를 연결했습니다.
- 자동 검증용 Play probe는 `Verification~`에 보관되어 게임이나 일반 에디터 컴파일에 포함되지 않습니다. 검증 사본은 별도 회사/제품 이름으로 PlayerPrefs를 분리합니다.
