using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] TutorialInputBridge bridge;
        [SerializeField] TutorialDialogueView view;
        [SerializeField] TutorialSequence[] sequences = Array.Empty<TutorialSequence>();
        [SerializeField] StageOneTutorial stageOne;
        readonly HashSet<string> launched = new HashSet<string>();
        readonly HashSet<TutorialSequence> rejected = new HashSet<TutorialSequence>();
        readonly HashSet<string> duplicateEntries = new HashSet<string>();
        readonly HashSet<string> visitedText = new HashSet<string>();
        readonly HashSet<string> completedStep = new HashSet<string>();
        TutorialSequence active;
        DefenseSimulation session;
        int viewedStep, viewedText, progressStep;
        bool initialized;
        public bool StageOneEnabled => stageOne;
        bool StageOneActive => stageOne && stageOne.Active;
        public bool IsActive => StageOneActive || active && view && view.IsVisible;
        public bool BlocksGameplayInput => IsActive && InputLimited;
        public bool BlocksPreview => BlocksGameplayInput && !AllowPreview;
        // Tutorial off, idle, or with input limiting disabled in the developer options.
        bool Unrestricted => !initialized || !IsActive || !InputLimited;
        bool InputLimited => !battle || battle.Simulation == null || battle.Simulation.Options.tutorialInput;
        public bool PauseBattle => StageOneActive ? stageOne.Pause : IsActive && bridge && bridge.PauseBattle;
        public bool AllowPreview => StageOneActive ? stageOne.Preview : IsActive && bridge && bridge.AllowPreview;

        void Start()
        {
            if (!battle || !bridge || !view) { DisableWithError("Battle, Bridge, View 참조를 연결하세요."); return; }
            try
            {
                bridge.Initialize(this); bridge.Completed += CompleteAction;
                view.Initialize(PreviousText, NextText);
                if (stageOne) stageOne.Initialize();
                session = battle.Simulation;
                initialized = true;
            }
            catch (Exception error) { DisableWithError(error.Message); }
        }

        void DisableWithError(string message)
        {
            Debug.LogError(name + ": 튜토리얼 비활성화 — " + message, this);
            if (bridge) bridge.SetStep(null);
            if (view) view.Hide();
            enabled = false;
        }

        void Update()
        {
            if (!initialized) return;
            bridge.SetInputLimited(InputLimited);
            if (battle.Simulation != null && !battle.Simulation.Options.tutorial) { Finish(); return; }
            if (stageOne && stageOne.OwnsSession) return;
            if (session != battle.Simulation)
            {
                Finish(); session = battle.Simulation; launched.Clear(); rejected.Clear(); duplicateEntries.Clear(); visitedText.Clear(); completedStep.Clear();
            }
            if (active)
            {
                var current = progressStep < active.steps.Length ? active.steps[progressStep] : null;
                if (current != null && !(current.kind == TutorialStepKind.ActionGuide && completedStep.Contains(current.stepId)) && !bridge.TargetsAvailable(current))
                {
                    Debug.LogError($"튜토리얼 중단 [{active.sequenceId}/{current.stepId}]: 대상이 사라졌습니다.", active);
                    Finish();
                }
                return;
            }
            if (session == null) return;
            var phase = EntryPhase(session.Phase);
            if (phase == null) return;
            var matches = sequences.Where(s => s && s.enabled && s.stage == battle.Stage && s.wave == session.WaveIndex + 1 && s.entryPhase == phase).ToArray();
            if (matches.Length > 1)
            {
                string key = battle.Stage + "-" + (session.WaveIndex + 1) + "-" + phase;
                if (duplicateEntries.Add(key)) Debug.LogError("튜토리얼 시퀀스 중복: " + key, this);
                return;
            }
            if (matches.Length == 0 || rejected.Contains(matches[0]) || launched.Contains(matches[0].sequenceId)) return;
            if (!Validate(matches[0])) { rejected.Add(matches[0]); return; }
            Begin(matches[0]);
        }

        static TutorialEntryPhase? EntryPhase(BattlePhase phase)
        {
            if (phase == BattlePhase.Ready) return TutorialEntryPhase.Preparation;
            if (phase == BattlePhase.Running || phase == BattlePhase.Paused) return TutorialEntryPhase.Battle;
            if (phase == BattlePhase.Reward) return TutorialEntryPhase.Reward;
            return null;
        }

        bool Validate(TutorialSequence sequence, bool standalone = false)
        {
            if (string.IsNullOrWhiteSpace(sequence.sequenceId) || sequence.stage < 1 || sequence.wave < 1 || sequence.steps == null || sequence.steps.Length == 0 || sequence.typeSpeed < 1f || sequence.typeSpeed > 20f)
                return Invalid(sequence, "시퀀스 ID, 대사, 단계 또는 타이핑 속도를 확인하세요.");
            if (!standalone && sequences.Count(item => item && item.sequenceId == sequence.sequenceId) != 1)
                return Invalid(sequence, "sequenceId가 중복됐습니다.");
            var stepIds = new HashSet<string>();
            foreach (var step in sequence.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.stepId) || !stepIds.Add(step.stepId) || step.texts == null || step.texts.Length == 0 ||
                    step.texts.Any(t => t == null || string.IsNullOrWhiteSpace(t.textId))) return Invalid(sequence, "단계/대사 ID와 문장을 확인하세요.");
                if (step.texts.Select(t => t.textId).Distinct().Count() != step.texts.Length)
                    return Invalid(sequence, "한 단계 안에서 textId가 중복됐습니다.");
                if (step.kind != TutorialStepKind.ActionGuide) continue;
                if (step.completionMode == TutorialCompletionMode.Unconfigured || step.targetKeys == null || step.targetKeys.Length == 0 ||
                    step.targetKeys.Any(k => string.IsNullOrWhiteSpace(k) || !bridge.HasTarget(k))) return Invalid(sequence, "행동유도 조건과 대상 키를 Bridge에 연결하세요.");
                if (step.completionMode == TutorialCompletionMode.ExternalSignal && string.IsNullOrWhiteSpace(step.completionSignal))
                    return Invalid(sequence, "완료 신호를 지정하세요.");
                if (step.completionMode == TutorialCompletionMode.DragDrop &&
                    (string.IsNullOrWhiteSpace(step.sourceKey) || string.IsNullOrWhiteSpace(step.destinationKey) ||
                     !bridge.HasTarget(step.sourceKey) || !bridge.HasTarget(step.destinationKey) ||
                     !step.targetKeys.Contains(step.sourceKey) || !step.targetKeys.Contains(step.destinationKey)))
                    return Invalid(sequence, "드래그 출발·도착 대상을 연결하세요.");
            }
            return true;
        }

        bool Invalid(TutorialSequence sequence,string message)
        { Debug.LogError($"튜토리얼 설정 오류 [{sequence.sequenceId}]: {message}", sequence); return false; }

        void Begin(TutorialSequence sequence)
        {
            active = sequence; launched.Add(sequence.sequenceId); progressStep = 0; viewedStep = viewedText = 0;
            bridge.SetStep(active.steps[0]); ShowText(true);
        }

        string TextKey(TutorialStep step, TutorialText text) => active.sequenceId + "/" + step.stepId + "/" + text.textId;
        void ShowText(bool firstVisit)
        {
            var step = active.steps[viewedStep]; var text = step.texts[viewedText];
            string key = TextKey(step,text); bool animate = firstVisit && !visitedText.Contains(key);
            visitedText.Add(key);
            bridge.SetStep(active.steps[progressStep], viewedStep == progressStep);
            view.Show(active, step.dialoguePosition, text.text, animate);
            bool previous = viewedStep > 0 || viewedText > 0;
            bool next = viewedText + 1 < step.texts.Length || viewedStep < progressStep || step.kind != TutorialStepKind.ActionGuide || completedStep.Contains(step.stepId);
            view.SetNavigation(previous,next);
        }

        void PreviousText()
        {
            if (StageOneActive) { stageOne.Previous(); return; }
            if (!IsActive) return;
            if (view.IsTyping) view.FinishTyping();
            if (viewedText > 0) viewedText--;
            else if (viewedStep > 0) { viewedStep--; viewedText = active.steps[viewedStep].texts.Length - 1; }
            else return;
            ShowText(false);
        }

        void NextText()
        {
            if (StageOneActive) { stageOne.Next(); return; }
            if (!IsActive) return;
            if (view.IsTyping) { view.FinishTyping(); return; }
            var step = active.steps[viewedStep];
            if (viewedText + 1 < step.texts.Length) { viewedText++; ShowText(true); return; }
            if (viewedStep < progressStep) { viewedStep++; viewedText = 0; ShowText(!visitedText.Contains(TextKey(active.steps[viewedStep],active.steps[viewedStep].texts[0]))); return; }
            if (viewedStep != progressStep || step.kind == TutorialStepKind.ActionGuide && !completedStep.Contains(step.stepId)) return;
            progressStep++;
            if (progressStep >= active.steps.Length) { Finish(); return; }
            viewedStep = progressStep; viewedText = 0; bridge.SetStep(active.steps[progressStep]); ShowText(true);
        }

        void CompleteAction(TutorialStep step)
        {
            if (!active || progressStep >= active.steps.Length || active.steps[progressStep] != step || !completedStep.Add(step.stepId)) return;
            progressStep++;
            if (progressStep >= active.steps.Length) { Finish(); return; }
            viewedStep = progressStep; viewedText = 0;
            ShowText(true);
        }

        public bool CanPerformAction(string key) => Unrestricted || (StageOneActive ? stageOne.Allows(key) : bridge.CanPerformAction(key, null));
        public bool AllowsWorldPoint(Vector3 point) => Unrestricted || bridge.AllowsWorldPoint(point);
        public bool AllowsRobotChoice(int index) => Unrestricted || bridge.AllowsRobotChoice(index);
        public void ReportSuccess(string key,string signal,GameObject target=null) { if (initialized) bridge.ReportSuccess(key,signal,target); }
        public void ReportWorldSuccess(Vector3 point,string signal) { if (initialized) bridge.ReportWorldSuccess(point,signal); }
        public void ReportDrop(RectTransform source, RectTransform destination, string signal) { if (initialized) bridge.ReportDrop(source,destination,signal); }
        public void RegisterWorldTarget(string key, GameObject owner, Bounds bounds)
        { if (initialized && bridge.HasRuntimeTarget(key)) bridge.RegisterWorldTarget(key,owner,bounds); }
        public void UnregisterRuntimeTarget(string key, GameObject owner) { if (initialized) bridge.UnregisterRuntimeTarget(key,owner); }

        void Finish()
        {
            active = null; progressStep = viewedStep = viewedText = 0;
            if (bridge) bridge.SetStep(null);
            if (view) view.Hide();
        }

        #if UNITY_EDITOR
        TutorialSequence editorSequence;
        public TutorialStep EditorStep => active && viewedStep < active.steps.Length ? active.steps[viewedStep] : null;
        public TutorialInputBridge EditorBridge => bridge;
        public TutorialDialogueView EditorView => view;
        public StageOneTutorial EditorStageOne => stageOne;
        public void EditorStop() { Finish(); if(editorSequence) Destroy(editorSequence); editorSequence=null; }
        public void EditorPrevious() => PreviousText();
        public void EditorNext() => NextText();
        public void EditorFinishTyping() => view.FinishTyping();
        public void EditorRun(TutorialSequence sequence,int step)
        {
            if (!initialized || !sequence || step < 0 || step >= sequence.steps.Length) throw new InvalidOperationException("시퀀스·step 확인 필요");
            if (!Validate(sequence,true)) throw new InvalidOperationException("시퀀스 설정 오류: Unity Console 확인");
            if (!bridge.TargetsAvailable(sequence.steps[step])) throw new InvalidOperationException("현재 step의 선행 대상이 없습니다.");
            EditorStop(); editorSequence=Instantiate(sequence); active=editorSequence; session=battle.Simulation;
            progressStep=viewedStep=step; viewedText=0; visitedText.Clear(); completedStep.Clear(); ShowText(true);
        }
#endif
        void OnDestroy() { if (bridge) bridge.Completed -= CompleteAction;
#if UNITY_EDITOR
            if(editorSequence)Destroy(editorSequence);
#endif
        }
    }
}
