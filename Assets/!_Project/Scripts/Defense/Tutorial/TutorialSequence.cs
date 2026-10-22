using System;
using UnityEngine;

namespace CodingGame.Defense
{
    public enum TutorialEntryPhase { Preparation, Battle, Reward }
    public enum TutorialStepKind { Dialogue, ActionGuide }
    public enum TutorialDialoguePosition { [InspectorName("아래")] Bottom, [InspectorName("위")] Top }
    public enum TutorialCompletionMode { Unconfigured, TargetClick, DragDrop, ExternalSignal }

    [Serializable]
    public sealed class TutorialText
    {
        public string textId;
        [Tooltip("이 대화창의 발화자. '시스템'이면 경고 배너로 표시합니다.")] public string speaker;
        [TextArea(2, 8)] public string text;
        [Tooltip("이 대화창이 떠 있는 동안 테두리를 그릴 대상 키(대본의 '집중'). 클릭은 허용하지 않습니다.")]
        public string[] focusKeys = Array.Empty<string>();
        [Tooltip("이 대화창이 떠 있는 동안 빛나게 할 대상(하이라이트). 화면을 어둡게 하지 않습니다. 지금은 Robot.Range.N(사거리 원)만.")]
        public string[] highlightKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string stepId;
        public TutorialStepKind kind;
        [Tooltip("이 단계의 대화창 위치. 클릭할 대상의 반대쪽을 선택하세요.")]
        public TutorialDialoguePosition dialoguePosition = TutorialDialoguePosition.Bottom;
        public TutorialText[] texts = Array.Empty<TutorialText>();
        public string[] targetKeys = Array.Empty<string>();
        [Tooltip("Darken everything outside the targets.")]
        public bool dim = true;
        [Tooltip("행동 단계라도 화면을 어둡게 하지 않습니다(대상 테두리·화살표만). 예: 코드를 다 만든 뒤 '닫기'를 누르는 단계.")]
        public bool undimmed;
        [Tooltip("Block clicks outside the targets (the dialogue stays usable).")]
        public bool blockInput = true;
        [Tooltip("Draw outlines around highlighted targets.")]
        public bool outline = true;
        public TutorialCompletionMode completionMode;
        public string completionSignal;
        public string sourceKey, destinationKey;
        public bool pauseBattle = true, allowPreview;
        [Min(0)] public float highlightPadding = 10;
        [Tooltip("이 신호가 올 때까지 단계를 숨기고 기다립니다(예: FirstKill, WaveCleared, EditorClosed). 비우면 바로 표시.")]
        public string trigger;
        [Tooltip("이 신호가 이번 웨이브에 이미 왔으면 단계를 건너뜁니다(예: Leak이면 '다 막았어' 대사를 건너뜀).")]
        public string skipIf;
        [Tooltip("ProgramMatches 판정용 정답 Python(함수 머리줄 제외). 줄 단위, 들여쓰기 무시.")]
        [TextArea(2, 6)] public string expectedCode;
        [Tooltip("화살표를 띄울 대상 키. 'a|b'면 앞에서부터 화면에 있는 것. 비우면 화살표 없음.")]
        public string pointerKey;
        [Tooltip("편집기 힌트 버튼에 보여 줄 코드. 비우면 힌트 버튼을 숨깁니다.")]
        [TextArea(2, 6)] public string hintCode;
        [Tooltip("이 시간(초) 동안 완료하지 못하면 화살표를 띄웁니다. 0이면 바로.")]
        [Min(0)] public float pointerDelay;
        [Tooltip("단계 진입 시 코드 편집기를 닫습니다.")] public bool closeEditor;
        [Tooltip("단계 진입 시 robotIndex 로봇의 코드를 비웁니다(블록은 인벤토리로).")] public bool clearProgram;
        [Tooltip("단계 진입 시 편집기를 닫고 robotIndex 로봇의 편집기를 엽니다.")] public bool openEditor;
        [Tooltip("판정·코드 삭제·편집기 대상. 0 = 교육 로봇(처음 배치한 전사), 1 = 두 번째 전사.")]
        [Min(0)] public int robotIndex;
        [Tooltip("흑백 화면 가운데에 대화창을 크게 띄웁니다. 이어지는 단계도 켜 두면 가운데에 남고, 꺼진 단계로 넘어갈 때 원래 자리로 줄어들며 이동합니다.")] public bool introEntrance;
        [Tooltip("시스템 배너에 경고 카드 세 장을 함께 띄웁니다(1-1 A01).")] public bool alertCards;
        [Tooltip("끌어다 놓기 시연: '팔레트 키>놓을 키', 'a>b|c>d'면 앞에서부터 둘 다 화면에 있는 것. 반투명 블록이 반복해서 움직입니다.")]
        public string dragDemo;
        [Tooltip("0보다 크면 다음 버튼 없이, 글자가 다 나오고 이 시간(초) 뒤 저절로 다음 단계로 넘어갑니다.")] [Min(0)] public float autoAdvance;
        [Tooltip("행동을 기다리는 단계에서 다음 버튼을 누르면 강조(어둡게·테두리)를 걷습니다. 예: 직접 코드를 짜는 단계.")] public bool dismissable;
        [Tooltip("0보다 크면 행동이 일찍 와도 글자가 다 나오고 이 시간(초)이 지난 뒤에 다음 단계로 넘어갑니다(1-1 D06: 미리보기가 바로 적중해도 대사를 읽게).")]
        [Min(0)] public float holdSeconds;
    }

    [CreateAssetMenu(menuName = "CodingGame/Tutorial/Sequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public string sequenceId;
        public bool enabled;
        [Min(1)] public int stage = 1, wave = 1;
        public TutorialEntryPhase entryPhase;
        public Sprite speakerPortrait;
        [Range(1f, 40f), Tooltip("초당 글자 수")]
        public float typeSpeed = 3f;
        public TutorialStep[] steps = Array.Empty<TutorialStep>();
    }
}
