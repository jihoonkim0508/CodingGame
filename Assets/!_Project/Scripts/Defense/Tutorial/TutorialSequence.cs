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
        [Tooltip("Darken everything outside the targets and block clicks there.")]
        public bool dim = true;
        public TutorialCompletionMode completionMode;
        public string completionSignal;
        public string sourceKey, destinationKey;
        public bool pauseBattle = true, allowPreview;
        [Min(0)] public float highlightPadding = 10;
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
