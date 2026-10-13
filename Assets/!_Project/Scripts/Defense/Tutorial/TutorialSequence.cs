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
        [Tooltip("Targets drawn with an outline. Empty: every target key. Other target keys stay clickable without an outline.")]
        public string[] highlightKeys = Array.Empty<string>();
        [Tooltip("Darken everything outside the targets.")]
        public bool dim = true;
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
        [Tooltip("ProgramMatches 판정용 정답 Python(함수 머리줄 제외). 줄 단위, 들여쓰기 무시.")]
        [TextArea(2, 6)] public string expectedCode;
        [Tooltip("화살표를 띄울 대상 키. 비우면 화살표 없음.")]
        public string pointerKey;
        [Tooltip("편집기 힌트 버튼에 보여 줄 코드. 비우면 힌트 버튼을 숨깁니다.")]
        [TextArea(2, 6)] public string hintCode;
        [Tooltip("이 시간(초) 동안 완료하지 못하면 화살표를 띄웁니다. 0이면 바로.")]
        [Min(0)] public float pointerDelay;
        [Tooltip("단계 진입 시 코드 편집기를 닫습니다.")] public bool closeEditor;
        [Tooltip("단계 진입 시 교육 로봇의 코드를 비웁니다(블록은 인벤토리로).")] public bool clearProgram;
    }

    [CreateAssetMenu(menuName = "CodingGame/Tutorial/Sequence")]
    public sealed class TutorialSequence : ScriptableObject
    {
        public string sequenceId;
        public bool enabled;
        [Min(1)] public int stage = 1, wave = 1;
        public TutorialEntryPhase entryPhase;
        public string speakerName = "도치";
        public Sprite speakerPortrait;
        [Range(1f, 20f), Tooltip("초당 글자 수")]
        public float typeSpeed = 3f;
        public TutorialStep[] steps = Array.Empty<TutorialStep>();
    }
}
