using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CodingGame.Defense
{
    // Runs the TutorialSequence for the current stage/wave: shows each step's dialogue, limits input through the bridge,
    // and advances on the next button or on signals from the game (ReportSuccess) and the simulation (TutorialWatcher).
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] TutorialInputBridge bridge;
        [SerializeField] TutorialDialogueView view;
        // Optional: without them arrows are skipped and system lines use the dialogue box.
        [SerializeField] TutorialPointer pointer;
        [SerializeField] TutorialSystemAlert alert;
        [SerializeField] TutorialSequence[] sequences = Array.Empty<TutorialSequence>();
        readonly TutorialWatcher watcher = new TutorialWatcher();
        readonly HashSet<string> launched = new HashSet<string>();
        readonly HashSet<TutorialSequence> rejected = new HashSet<TutorialSequence>();
        readonly HashSet<string> duplicateEntries = new HashSet<string>();
        readonly HashSet<string> visitedText = new HashSet<string>();
        readonly HashSet<string> completedStep = new HashSet<string>();
        // Signals seen during the current wave; Poll and ReportSuccess say each one once, trigger steps may come later.
        readonly HashSet<string> signals = new HashSet<string>();
        int signalWave = -1;
        TutorialSequence active;
        DefenseSimulation session;
        int viewedStep, viewedText, progressStep;
        float stepEntered, targetsLostAt = -1;
        string pointedAt;
        // A line that moves on by itself: set once its text has finished typing (planner: no skipping the dot operator lines).
        float autoNextAt = -1;
        // holdSeconds: when the current line may move on (MaxValue while it still types), and an action that came too early.
        float heldUntil;
        TutorialStep held;
        // Free coding (1-2 C06, 1-4 D04): the player pressed next, so the guide is gone until the step is done.
        bool guideDismissed;
        string[] highlight = Array.Empty<string>(), focused = Array.Empty<string>();
        readonly List<Vector2> pathPoints = new List<Vector2>();
        bool targetsSeen;
        string shownText;
        bool initialized, bannerUp;
        // Waiting for a trigger hides the dialogue and leaves the game free; a system banner still holds input.
        public bool IsActive => active && view && (view.IsVisible || bannerUp || guideDismissed);
        public bool BlocksGameplayInput => IsActive && InputLimited;
        public bool BlocksPreview => BlocksGameplayInput && !AllowPreview;
        // Tutorial off, idle, or with input limiting disabled in the developer options.
        bool Unrestricted => !initialized || !IsActive || !InputLimited;
        bool InputLimited => !battle || battle.Simulation == null || battle.Simulation.Options.tutorialInput;
        public bool PauseBattle => IsActive && bridge && bridge.PauseBattle;
        public bool AllowPreview => IsActive && bridge && bridge.AllowPreview;
        public IReadOnlyList<TutorialSequence> Sequences => sequences;
        // The dialogue on screen asks the game to make this target glow (e.g. Robot.Range.0).
        public bool Highlighted(string key) => initialized && IsActive && view.IsVisible && Array.IndexOf(highlight, key) >= 0;
        TutorialStep Current => active && progressStep < active.steps.Length ? active.steps[progressStep] : null;
        DefenseCodeEditor Editor => battle.CodeEditor;

        void Start()
        {
            if (!battle || !bridge || !view) { DisableWithError("Battle, Bridge, View 참조를 연결하세요."); return; }
            try
            {
                bridge.Initialize(this); bridge.Completed += CompleteAction;
                bridge.SetEditorAreas((key, corners) => Editor && Editor.TryGetTutorialCorners(key, corners));
                view.Initialize(PreviousText, NextText);
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
            if (session != battle.Simulation)
            {
                Finish(); session = battle.Simulation; launched.Clear(); rejected.Clear(); duplicateEntries.Clear(); visitedText.Clear(); completedStep.Clear();
            }
            if (session == null) return;
            if (session.WaveIndex != signalWave) { signals.Clear(); signalWave = session.WaveIndex; }
            var step = Current;
            foreach (var signal in watcher.Poll(session, step != null && step.completionSignal == TutorialSignals.ProgramMatches ? step.expectedCode : null, step?.robotIndex ?? 0))
                Signal(signal);
            if (active) { Tick(); return; }
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

        void Tick()
        {
            var step = Current;
            if (step == null) return;
            if (step.completionSignal == TutorialSignals.ConditionBuilt && Editor)
            {
                var taught = active.steps.Take(progressStep).LastOrDefault(s => s.completionSignal == TutorialSignals.ProgramMatches)?.expectedCode;
                var program = TutorialWatcher.Robot(session, step.robotIndex)?.Program;
                if (Editor.LooseExpressions().Any(text => TutorialWatcher.ConditionMatches(text, program, step.expectedCode, taught))) Signal(TutorialSignals.ConditionBuilt);
            }
            if (step.completionSignal == TutorialSignals.SlotFilled && Editor && Editor.SlotFilled(step.expectedCode)) Signal(TutorialSignals.SlotFilled);
            if (Current != step) return;
            // Battle lines still unread when the wave ends (fast at 3x) would show over the reward panel: skip to the wave-cleared line.
            if (signals.Contains(TutorialSignals.WaveCleared) && step.kind != TutorialStepKind.ActionGuide && step.trigger != TutorialSignals.WaveCleared &&
                progressStep < Array.FindIndex(active.steps, s => s.trigger == TutorialSignals.WaveCleared)) { Enter(progressStep + 1); return; }
            if (!string.IsNullOrWhiteSpace(step.skipIf) && signals.Contains(step.skipIf) && Triggered(step) && viewedStep == progressStep) { Enter(progressStep + 1); return; }
            if (!Triggered(step))
            {
                // No leak before the wave ended: a "show on leak" line never comes, so move on (WaveCleared steps still show).
                if (signals.Contains(TutorialSignals.WaveCleared)) { Enter(progressStep + 1); return; }
                Hide(); return;
            }
            if (!view.IsVisible && !bannerUp && !guideDismissed) { ShowText(true); return; }
            // Opening the hint is the player's own help: the highlight and the arrow step aside (planner).
            if (Editor && Editor.HintOpen) bridge.Dismiss();
            if (autoNextAt == float.MaxValue && !view.IsTyping) autoNextAt = Time.unscaledTime + step.autoAdvance;
            if (heldUntil == float.MaxValue && !view.IsTyping) heldUntil = Time.unscaledTime + step.holdSeconds;
            if (held == step && Time.unscaledTime >= heldUntil) { held = null; CompleteAction(step); return; }
            if (autoNextAt > 0 && autoNextAt != float.MaxValue && Time.unscaledTime >= autoNextAt) { autoNextAt = -1; Enter(progressStep + 1); return; }
            // The banner faded: same as pressing next (ShowText resets bannerUp for the following line).
            if (bannerUp && alert && !alert.IsShowing) { NextText(); return; }
            UpdatePointer(step);
            AvoidTargets(step);
            // Reading an earlier line turns the step's actions off, so its targets (the robot menu) hide; that is not a loss.
            if (step.kind != TutorialStepKind.ActionGuide || completedStep.Contains(step.stepId) || viewedStep != progressStep)
            { targetsLostAt = -1; return; }
            // Targets such as the robot menu appear a frame after the step starts; only a target that was there and stays gone ends the tutorial.
            if (bridge.TargetsAvailable(step)) { targetsSeen = true; targetsLostAt = -1; }
            else if (targetsSeen && targetsLostAt < 0) targetsLostAt = Time.unscaledTime;
            else if (targetsSeen && Time.unscaledTime - targetsLostAt > 1)
            {
                Debug.LogError($"튜토리얼 중단 [{active.sequenceId}/{step.stepId}]: 대상이 사라졌습니다.", active);
                Finish();
            }
        }

        static bool Same(string a, string b) => Regex.Replace(a ?? "", @"\s", "") == Regex.Replace(b ?? "", @"\s", "");
        bool Triggered(TutorialStep step) => string.IsNullOrWhiteSpace(step.trigger) || signals.Contains(step.trigger);

        // A signal from the game or the watcher: remember it for trigger steps and complete an action step waiting on it.
        void Signal(string signal)
        {
            signals.Add(signal);
            var step = Current;
            if (step != null && step.kind == TutorialStepKind.ActionGuide && step.completionMode == TutorialCompletionMode.ExternalSignal &&
                step.completionSignal == signal && viewedStep == progressStep && Triggered(step)) CompleteAction(step);
        }

        void UpdatePointer(TutorialStep step)
        {
            if (!pointer) return;
            Rect area = default; string chosen = null;
            bool show = viewedStep == progressStep && !completedStep.Contains(step.stepId) && !string.IsNullOrWhiteSpace(step.pointerKey) && !(Editor && Editor.HintOpen) &&
                Time.unscaledTime - stepEntered >= step.pointerDelay && bridge.TryGetArea(step.pointerKey, out area, out chosen);
            // Slots under or inside a code line are pointed at from below; from above the arrow would cover the line above.
            if (show) pointer.Show(area, chosen.Contains(".After.") || chosen.Contains(".Arg.")); else pointer.HideArrow();
            UpdateDragDemo(step);
            UpdateRoutePath();
            // Once the arrow moves to an empty argument slot, open the values tab, where the variables to drop in are.
            if (show && chosen != pointedAt && chosen.Contains(".Arg.") && Editor) Editor.RevealPalette(TutorialKeys.PalettePrefix + "Variable");
            if (show) pointedAt = chosen;
        }

        // A bottom dialogue that covers what the step points at or waits on moves to the top, unless the top covers one too.
        // Checked every frame: targets such as the robot menu or the reward panel appear after the line does.
        void AvoidTargets(TutorialStep step)
        {
            if (!view.IsVisible || view.AtTop || view.IsCentered || viewedStep != progressStep || completedStep.Contains(step.stepId)) return;
            if (!CoversTarget(step)) return;
            var from = view.Panel.localPosition;
            view.Place(TutorialDialoguePosition.Top);
            if (CoversTarget(step)) view.Place(TutorialDialoguePosition.Bottom); else view.SlideFrom(from);
        }
        bool CoversTarget(TutorialStep step)
        {
            var panel = bridge.ToLocal(view.Panel);
            foreach (var key in new[] { step.pointerKey }.Concat(step.targetKeys ?? Array.Empty<string>()))
                if (bridge.TryGetArea(key, out var area) && area.Overlaps(panel)) return true;
            return false;
        }

        // While the road is focused, arrows run along it from the entrance to the core, once it is drawn.
        void UpdateRoutePath()
        {
            pathPoints.Clear();
            if (view.IsVisible && Array.IndexOf(focused, TutorialKeys.Route) >= 0 && bridge.RoadDrawn)
                foreach (var world in battle.TutorialRoutePath())
                    if (bridge.ToLocal(world, out var local)) pathPoints.Add(local);
            if (pathPoints.Count > 1) pointer.ShowPath(pathPoints); else pointer.HidePath();
        }

        // The first "palette>slot" pair with both ends on screen plays as a drag demo until the step is done.
        void UpdateDragDemo(TutorialStep step)
        {
            if (viewedStep == progressStep && !completedStep.Contains(step.stepId) && !string.IsNullOrWhiteSpace(step.dragDemo) && Editor)
                foreach (var pair in step.dragDemo.Split('|'))
                {
                    var ends = pair.Split('>');
                    if (ends.Length == 2 && Editor.TutorialPaletteItem(ends[0].Trim()) is RectTransform item &&
                        bridge.TryGetArea(ends[0].Trim(), out var from) && bridge.TryGetArea(ends[1].Trim(), out var to))
                    { pointer.ShowDrag(item, from, to); return; }
                }
            pointer.HideDrag();
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
            if (string.IsNullOrWhiteSpace(sequence.sequenceId) || sequence.stage < 1 || sequence.wave < 1 || sequence.steps == null || sequence.steps.Length == 0 || sequence.typeSpeed < 1f || sequence.typeSpeed > 40f)
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
                if (step.texts.SelectMany(t => t.focusKeys ?? Array.Empty<string>()).Any(k => !bridge.HasTarget(k)))
                    return Invalid(sequence, $"{step.stepId}: 집중 대상 키를 Bridge에 연결하세요.");
                if (step.kind != TutorialStepKind.ActionGuide) continue;
                if (step.completionMode == TutorialCompletionMode.Unconfigured || step.targetKeys == null || step.targetKeys.Length == 0 ||
                    step.targetKeys.Any(k => string.IsNullOrWhiteSpace(k) || !bridge.HasTarget(k))) return Invalid(sequence, $"{step.stepId}: 행동유도 조건과 대상 키를 Bridge에 연결하세요.");
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
            // Step ids repeat across waves (A05a in 1-2 and 1-3), so completion is per sequence run.
            active = sequence; launched.Add(sequence.sequenceId); shownText = null; completedStep.Clear();
            Enter(0);
        }

        // Makes step index the progress step: applies its entry effects, then shows it unless it waits for a trigger.
        void Enter(int index)
        {
            if (index >= active.steps.Length) { Finish(); return; }
            progressStep = viewedStep = index; viewedText = 0; stepEntered = Time.unscaledTime; guideDismissed = false;
            targetsSeen = false; targetsLostAt = -1;
            if (pointer) pointer.Hide();
            var step = active.steps[index];
            held = null; heldUntil = step.holdSeconds > 0 ? float.MaxValue : 0;
            ApplyEntry(step);
            // ShowText / Hide set the bridge step.
            if (Triggered(step)) ShowText(true); else Hide();
        }

        void ApplyEntry(TutorialStep step)
        {
            if (!Editor) return;
            var robot = TutorialWatcher.Robot(session, step.robotIndex);
            if (step.closeEditor) Editor.Close(false);
            if (step.clearProgram && robot != null) Editor.ClearRobotProgram(robot.Id);
            if (step.openEditor && robot != null) { Editor.Close(false); Editor.Open(robot.Id); }
            Editor.SetHint(step.hintCode);
        }

        string TextKey(TutorialStep step, TutorialText text) => active.sequenceId + "/" + step.stepId + "/" + text.textId;
        // Backticked code names in the script show in the code colour; the concepts being taught stand out in bold yellow.
        static string Code(string text) => Regex.Replace(Regex.Replace(text ?? "", "`([^`]+)`", "<color=#6FFFD2>$1</color>"),
            "(변수|조건문|조건식|반복문|점\\(\\.\\))", "<b><color=#FFD84D>$1</color></b>");

        void ShowText(bool firstVisit)
        {
            var step = active.steps[viewedStep]; var text = step.texts[viewedText];
            string key = TextKey(step,text);
            // The second half of a two-step action repeats the same line; do not type it again.
            bool animate = firstVisit && !visitedText.Contains(key) && text.text != shownText;
            visitedText.Add(key); shownText = text.text;
            bridge.SetStep(active.steps[progressStep], viewedStep == progressStep);
            var focus = text.focusKeys ?? Array.Empty<string>();
            // Open the palette tab holding what this line shows or the arrow points at (1-3 C03's comparison sits in another tab).
            if (Editor) foreach (var target in focus.Concat((step.pointerKey ?? "").Split('|').Select(k => k.Trim()))) Editor.RevealPalette(target);
            pointedAt = null;
            bridge.SetFocus(focus);
            highlight = text.highlightKeys ?? Array.Empty<string>(); focused = focus;
            bool system = IsAlert(text);
            if (system)
            {
                view.Hide(); alert.Show(text.text, step.alertCards); bannerUp = true;
                return;
            }
            bannerUp = false;
            view.Show(active, step.dialoguePosition, Code(text.text), animate, text.speaker);
            view.SetDebugId(active.sequenceId + " " + step.stepId);
            // Lines that only explain (no action, nothing to look at) sit in the middle while preparing; battle and
            // reward screens, the code editor and a selected robot's range circle (1-1 B05 "이 원") keep the bottom.
            bool explain = step.kind != TutorialStepKind.ActionGuide && focus.Length == 0 && highlight.Length == 0 &&
                string.IsNullOrEmpty(step.trigger) && session != null && session.Phase == BattlePhase.Ready && !(Editor && Editor.IsOpen) &&
                session.FindRobot(battle.SelectedId) == null;
            view.SetCentered(step.introEntrance || explain, step.introEntrance);
            autoNextAt = step.autoAdvance > 0 && viewedStep == progressStep ? float.MaxValue : -1;
            bool previous = (viewedStep > 0 || viewedText > 0) && !Waiting;
            bool next = viewedText + 1 < step.texts.Length || viewedStep < progressStep || step.kind != TutorialStepKind.ActionGuide || completedStep.Contains(step.stepId) ||
                step.dismissable;
            if (autoNextAt > 0) next = false;
            view.SetNavigation(previous,next);
        }

        // Waiting for a trigger: no dialogue, no spotlight, no click blocking until the signal comes.
        void Hide()
        {
            if (view.IsVisible) view.Hide();
            bannerUp = false;
            bridge.SetStep(null);
            if (pointer) pointer.Hide();
        }

        // The player owes the current step an action: going back would hide the targets it asks for (planner request, 1-2 A05).
        bool Waiting => active && viewedStep == progressStep && progressStep < active.steps.Length &&
            active.steps[progressStep].kind == TutorialStepKind.ActionGuide && !completedStep.Contains(active.steps[progressStep].stepId);
        bool IsAlert(TutorialText text) => alert && text.speaker == TutorialSignals.SystemSpeaker;
        void PreviousText()
        {
            if (!IsActive || Waiting) return;
            if (view.IsTyping) view.FinishTyping();
            // Warning banners play once: going back steps over them to the line before (planner, 1-1 E01).
            int step = viewedStep, text = viewedText;
            do
            {
                if (text > 0) text--;
                else if (step > 0) { step--; text = active.steps[step].texts.Length - 1; }
                else return;
            } while (IsAlert(active.steps[step].texts[text]));
            viewedStep = step; viewedText = text;
            ShowText(false);
        }

        void NextText()
        {
            if (!IsActive || bannerUp && alert && alert.IsShowing) return;
            var step = active.steps[viewedStep];
            if (autoNextAt > 0) return;
            if (view.IsTyping) { view.FinishTyping(); return; }
            if (Waiting && step.dismissable) { bridge.Dismiss(); view.Hide(); guideDismissed = true; return; }
            if (viewedText + 1 < step.texts.Length) { viewedText++; ShowText(true); return; }
            if (viewedStep < progressStep)
            {
                do { viewedStep++; viewedText = 0; } while (viewedStep < progressStep && IsAlert(active.steps[viewedStep].texts[0]));
                ShowText(!visitedText.Contains(TextKey(active.steps[viewedStep],active.steps[viewedStep].texts[0]))); return;
            }
            if (viewedStep != progressStep || step.kind == TutorialStepKind.ActionGuide && !completedStep.Contains(step.stepId)) return;
            Enter(progressStep + 1);
        }

        void CompleteAction(TutorialStep step)
        {
            if (!active || progressStep >= active.steps.Length || active.steps[progressStep] != step) return;
            if (step.holdSeconds > 0 && Time.unscaledTime < heldUntil) { held = step; return; }
            if (!completedStep.Add(step.stepId)) return;
            Enter(progressStep + 1);
        }

        public bool CanPerformAction(string key) => Unrestricted || bridge.CanPerformAction(key, null);
        public bool AllowsWorldPoint(Vector3 point) => Unrestricted || bridge.AllowsWorldPoint(point);
        public bool AllowsRobotChoice(int index) => Unrestricted || bridge.AllowsRobotChoice(index);
        public void ReportSuccess(string key, string signal, GameObject target = null)
        {
            if (!initialized) return;
            // Game signals count for trigger steps even when the bridge ignores the click itself.
            if (signal != TutorialSignals.TargetClicked) signals.Add(signal);
            bridge.ReportSuccess(key, signal, target);
        }
        public void ReportWorldSuccess(Vector3 point,string signal) { if (initialized) bridge.ReportWorldSuccess(point,signal); }
        public void ReportDrop(RectTransform source, RectTransform destination, string signal) { if (initialized) bridge.ReportDrop(source,destination,signal); }
        public void RegisterWorldTarget(string key, GameObject owner, Bounds bounds, bool round = false) => RegisterWorldTarget(key, owner, new[] { bounds }, round);
        public void RegisterWorldTarget(string key, GameObject owner, Bounds[] parts, bool round = false, bool box = false)
        { if (initialized && bridge.HasRuntimeTarget(key)) bridge.RegisterWorldTarget(key,owner,parts,round,box); }
        public void UnregisterRuntimeTarget(string key, GameObject owner) { if (initialized) bridge.UnregisterRuntimeTarget(key,owner); }

        void Finish()
        {
            active = null; progressStep = viewedStep = viewedText = 0; bannerUp = false; shownText = null;
            if (bridge) bridge.SetStep(null);
            if (view) view.Hide();
            if (alert) alert.Hide();
            if (pointer) pointer.Hide();
            if (initialized && Editor) Editor.SetHint(null);
        }

        #if UNITY_EDITOR
        TutorialSequence editorSequence;
        // False until Update has picked up a new battle session (it resets the tutorial then, undoing an earlier jump).
        public bool EditorSynced => battle && session == battle.Simulation;
        public TutorialStep EditorStep => active && viewedStep < active.steps.Length ? active.steps[viewedStep] : null;
        public TutorialInputBridge EditorBridge => bridge;
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
            launched.Add(sequence.sequenceId); visitedText.Clear(); completedStep.Clear(); shownText=null;
            Enter(step);
        }
#endif
        void OnDestroy() { if (bridge) bridge.Completed -= CompleteAction;
#if UNITY_EDITOR
            if(editorSequence)Destroy(editorSequence);
#endif
        }
    }
}
