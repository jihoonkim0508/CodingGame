# Stage 1 스테이지별 JSON 안내서

이 안내서는 스테이지별로 나눈 JSON 파일의 구조와 게임 적용 방법을 설명합니다. 파일 하나에는 스테이지 하나만 들어 있습니다.

## 파일 목록

| 스테이지 | 파일 |
|---|---|
| 1-1 변수 | stage1_1_variable.json |
| 1-2 여러 변수 | stage1_2_variables.json |
| 1-3 조건 | stage1_3_condition.json |
| 1-4 조건 활용 | stage1_4_condition_usage.json |
| 1-5 변수와 조건 종합 | stage1_5_variable_condition.json |

## 각 JSON의 최상위 구조

각 파일에는 다음 항목이 있습니다.

| 필드 | 의미 |
|---|---|
| format_version | JSON 자료 형식 버전 |
| package_id | 개별 스테이지 파일의 식별자 |
| package_type | 튜토리얼 스테이지 콘텐츠임을 표시 |
| locale | 콘텐츠 언어. 현재 한국어(ko-KR) |
| title | 해당 스테이지 제목 |
| implementation_contract | 콘텐츠를 화면과 게임 흐름에 연결하는 적용 기준 |
| stage_id | 스테이지 번호 |
| learning_goals | 스테이지 학습 목표 목록 |
| sections | 장면과 학습 내용을 담은 섹션 배열 |

이전 통합본의 stages 배열 대신, 한 파일의 최상위에 해당 스테이지의 stage_id, title, learning_goals, sections가 직접 들어 있습니다.

## 섹션 구조

sections 배열의 각 항목은 다음 필드로 구성됩니다.

| 필드 | 의미 |
|---|---|
| section_title | 장면 또는 학습 단위의 제목 |
| direction | 화면 연출, UI, 애니메이션 제작 지시 |
| dialogues | 안내자 등 화자의 대사 목록 |
| code_examples | 해당 장면에서 보여줄 코드 예시 목록 |
| interaction | 플레이어 입력이나 행동에 대한 안내. 필요 없으면 null |

섹션은 배열 순서대로 진행합니다.

### dialogues

각 대사에는 speaker와 text가 있습니다. speaker는 화자 이름이고 text는 플레이어에게 출력할 문장입니다. 대사는 배열 순서대로 보여줍니다. direction은 제작 지시이므로 대사로 출력하지 않습니다.

### code_examples

각 예시에는 label, language, code가 있습니다. label은 예시의 구분용 제목, language는 표시할 코드 언어, code는 화면에 보여줄 코드입니다. 코드 입력 기능에 실제로 실행시키려면 프로젝트의 코드 처리 기능과 연결해야 합니다.

### interaction

interaction 값이 객체이면 플레이어에게 수행 목표와 행동을 안내하고 입력을 기다립니다.

| 필드 | 의미 |
|---|---|
| prompt | 플레이어가 달성할 목표 |
| player_action | 플레이어가 할 행동 |
| expected_input | 정답 또는 예시 입력. null이면 지정된 입력 문자열이 없음 |
| success_feedback | 성공했을 때 표시할 안내 |
| retry_hint | 입력 실패 또는 전투 중 막혔을 때 표시할 힌트 |

expected_input은 항상 문자 단위로 비교해야 하는 정답 문자열은 아닙니다. “예:”로 시작하는 값은 예시 답안이며, 코드 입력은 게임의 문법 검사기에 연결해 의미가 같은 유효한 답을 허용할 수 있습니다.

interaction이 null이면 별도 플레이어 입력을 요구하지 않습니다. 대사와 연출이 끝난 후 실제 게임 흐름에 따라 다음 섹션으로 진행합니다.

## implementation_contract

각 파일에 공통으로 포함된 구현 기준입니다.

- stage_order: 전체 Stage 1 진행 순서입니다. 파일은 각각 분리되어 있어도 이 순서에 따라 연결합니다.
- section_order: 해당 파일의 sections 배열 순서를 따릅니다.
- direction: 장면 제작 지시로 사용하고 안내 대사로 출력하지 않습니다.
- dialogues: 화자와 대사를 순서대로 표시합니다.
- code_examples: 학습 화면에 제시할 코드입니다.
- interaction: 입력 요구 여부, 정답/예시, 성공 피드백과 재시도 힌트를 처리합니다.
- progression: 실제 웨이브 클리어 상태와 스테이지 진행을 연결합니다. 1-5 보스전을 완료하면 Stage 2를 해금합니다.
- code_binding: Attack() 표기를 프로젝트의 실제 공격 기능에 연결합니다.
- preserve_terms: damage, range, enemyDistance, attackDistance, if, <=, Attack()의 표기를 학습 순서에 맞게 유지합니다.

## 적용 순서

1. stage_order에 따라 스테이지 파일을 불러옵니다.
2. 해당 JSON의 sections를 순서대로 진행합니다.
3. direction은 화면 연출 지시로 사용하고 dialogues는 플레이어에게 순서대로 출력합니다.
4. code_examples가 있으면 해당 장면의 학습 코드로 보여줍니다.
5. interaction이 객체이면 플레이어 입력을 받고 성공 피드백이나 재시도 힌트를 표시합니다. null이면 별도 입력을 기다리지 않습니다.
6. 게임의 실제 웨이브 결과에 따라 스테이지 클리어 및 다음 스테이지 해금을 처리합니다.

## 프로젝트에 연결할 때 확인할 점

- Attack()은 공격 행동을 설명하는 튜토리얼 표기입니다. 프로젝트의 실제 공격 API에 연결합니다.
- 변수명과 비교 기호는 학습 콘텐츠 연결을 위해 그대로 유지합니다.
- direction 문장을 플레이어에게 그대로 읽어주지 않습니다.
- 웨이브 시작, 적 처치, 개발툴 게이지 변화, 스테이지 클리어는 프로젝트의 실제 게임 상태와 연결합니다.
- 특정 게임 엔진의 UI 컴포넌트나 이벤트 API는 정해져 있지 않으므로, 해당 연결은 프로젝트 구조에 맞춰 구현합니다.
