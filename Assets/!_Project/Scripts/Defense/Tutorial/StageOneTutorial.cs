using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;
using UnityEngine;

namespace CodingGame.Defense
{
    // Teaches Stage 1 through the real game: the player places the teaching robot, edits its program in the block
    // editor (with the live attack preview) and clears real waves. Scene references are authored; dialogue comes from JSON.
    public sealed class StageOneTutorial : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] TutorialDialogueView dialogue;
        [SerializeField] TutorialInputBridge bridge;
        [SerializeField] DefenseCodeEditor codeEditor;
        [SerializeField] TextAsset[] stages;
        [SerializeField] GameObject boot;
        [SerializeField] CanvasGroup bootFade;
        [SerializeField] AudioSource warningSource;
        [SerializeField] RobotRole teachingRole = RobotRole.Warrior;
        StageOneContent[] content;
        TutorialSequence presentation;
        TutorialStep step;
        DefenseSimulation session;
        StageOneSection current;
        int wave, section, line, boundary, kills, leaks, leaksAtStart;
        bool waiting, completed, initialized, bossHint, missedShown;
        DefenseProgram observedProgram;
        bool editorWasOpen;
        readonly HashSet<string> visitedText = new HashSet<string>();
        float bootUntil;
        TutorialDialoguePosition shownBattlePosition;
        public bool OwnsSession => initialized && battle.Simulation != null && battle.Simulation.Setup.StageOneLearning && battle.Stage == 1 && battle.Simulation.Options.tutorial;
        public bool Active => OwnsSession && current != null;
        public bool Pause => Active && !(waiting && IsBattleSection);
        public bool Preview => Active && !IsBattleSection;
        int BattleSection => wave < 5 ? 4 : 6;
        bool IsBattleSection => section == BattleSection;
        bool IsCodeSection => wave == 1 && section == 3 || wave == 2 && (section == 2 || section == 3) ||
            wave == 3 && section == 3 || wave == 4 && (section == 2 || section == 3) || wave == 5 && (section == 4 || section == 5);
        bool IsPlacement => wave == 1 && section == 1;
        // 1-1 opens with "select the robot below": only that button works and only choosing it moves the dialogue on.
        bool SelectGate => IsPlacement && line == 0 && !completed && !battle.IsPlacing && session.Robots.Count == 0;
        // Lessons between placement and the wave happen inside the code editor, next to the attack preview.
        bool InEditor => Robot != null && section > (wave == 1 ? 1 : 0) && section < BattleSection;
        RobotState Robot => session?.Robots.FirstOrDefault(r => r.Spec.role == teachingRole) ?? session?.Robots.FirstOrDefault();
        int RobotIndex => Enumerable.Range(0, 6).First(i => battle.DefinitionRole(i) == teachingRole);
        string RobotKey => "Battle.Robot." + RobotIndex;
        BlockKind AttackKind => DefenseProgression.ActionBlock(Rules.NativeAction(teachingRole));

        public void Initialize()
        {
            if (!battle || !dialogue || !bridge || !codeEditor || !boot || !bootFade || !warningSource || !warningSource.clip || stages == null || stages.Length != 5)
                throw new InvalidOperationException("StageOneTutorial의 JSON·UI·전투 참조를 연결하세요.");
            content = stages.Select((asset, index) => StageOneContent.Read(asset, index + 1)).ToArray();
            presentation = ScriptableObject.CreateInstance<TutorialSequence>();
            presentation.stage = 1; presentation.typeSpeed = 20; presentation.speakerName = "안내자";
            initialized = true; boot.SetActive(false);
        }
        void Update()
        {
            if (!OwnsSession) { if (current != null) End(); return; }
            var sim = battle.Simulation;
            if (session != sim) { session = sim; wave = 0; current = null; visitedText.Clear(); }
            if (wave != sim.WaveIndex + 1) { wave = sim.WaveIndex + 1; section = 0; EnterSection(); }
            if (!Active) return;
            if (editorWasOpen != codeEditor.IsOpen) { editorWasOpen = codeEditor.IsOpen; Show(false); }
            // Starting or ending a wave moves the dialogue off or back onto the map and clears the guide boxes.
            if (IsBattleSection && dialogue.IsVisible && shownBattlePosition != BattlePosition) { shownBattlePosition = BattlePosition; Show(false); }
            if (boot.activeSelf)
            {
                bootFade.alpha = Mathf.Clamp01((bootUntil - Time.unscaledTime) / .6f);
                if (Time.unscaledTime >= bootUntil) boot.SetActive(false);
            }
            if (waiting && IsBattleSection)
            {
                if (sim.Leaks > leaks) { leaks = sim.Leaks; if (wave == 1) EventDialogue(5); else Say(current.interaction?.retry_hint); }
                if (sim.Kills > kills) { kills = sim.Kills; if (wave == 1 && kills == 1) EventDialogue(4); }
                if (wave == 5 && !bossHint && sim.Phase == BattlePhase.Running && sim.Leaks > leaksAtStart) { bossHint = true; EventDialogue(0); }
                if ((sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory) && sim.LastSurvivors == 0 && sim.Leaks == leaksAtStart) Complete();
                else if ((sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Defeat) && !missedShown)
                { missedShown = true; Say("오류 코드를 놓쳤어. 다시 시작해 배치와 코드를 조절해봐."); }
            }
            else if (waiting && IsPlacement && sim.Robots.Count > 0) Complete();
            else if (IsPlacement && line == 0 && !completed && (battle.IsPlacing || sim.Robots.Count > 0)) { line = 1; Show(); }
            else if (waiting && IsCodeSection) CheckProgram();
            UpdateHighlight();
        }
        void CheckProgram()
        {
            var program = Robot?.Program;
            if (program == null || program == observedProgram) return;
            observedProgram = program;
            try { StageOneCode.Validate(TeachingSource(program), wave, section, AttackKind); Complete(); }
            // The editor applies every edit, so half-built programs are normal: hint in its status line, not the dialogue.
            catch (FormatException error) { if (codeEditor.IsOpen) codeEditor.Panel.ShowMessage(error.Message + " · " + current.interaction?.retry_hint); }
        }
        // Generated Python back to the teaching notation: drop the header and implicit enemy lines, name the attack Attack().
        static string TeachingSource(DefenseProgram program) =>
            Regex.Replace(string.Join("\n", program.Source.Replace("\r", "").Split('\n').Skip(1)
                .Where(s => s.Trim() != "enemy = get_nearest_enemy()" && !s.Trim().StartsWith("enemyDistance = enemy.get_distance("))
                .Select(s => s.Length >= 4 ? s.Substring(4) : s)), @"\b(attack|slash|block|boom|slow)\(enemy\)", "Attack()");

        void EnterSection()
        {
            current = content[wave - 1].sections[section]; line = 0; waiting = completed = bossHint = missedShown = false;
            // The line index where each section stops and waits for the player.
            boundary = (wave, section) switch { (1, 1) => 4, (1, 3) => 2, (2, 2) => 4, (2, 3) => 3, (3, 3) => 2, (4, 3) => 2, (5, 4) => 1, _ => current.dialogues.Length };
            if (IsBattleSection) boundary = wave switch { 1 => 4, 2 => 5, 4 => 1, _ => 0 };
            kills = session.Kills; leaks = leaksAtStart = session.Leaks;
            if (wave == 5 && section == 0) warningSource.Play();
            if (wave == 1 && section == 0) { bootUntil = Time.unscaledTime + 1.5f; bootFade.alpha = 1; boot.SetActive(true); }
            if (IsBattleSection && codeEditor.IsOpen) codeEditor.Close(false);
            if (InEditor)
            {
                if (!codeEditor.IsOpen) codeEditor.Open(Robot.Id);
                string starter = Starter();
                if (starter != null) LoadProgram(starter);
            }
            if (boundary == 0) Wait(); else Show();
        }
        // Real blocks for the section, built from the program the player already has (keeps their damage/range).
        string Starter()
        {
            var blocks = Robot?.Program?.CopyBlocks() ?? new List<CodeBlock>();
            string damage = StageOneCode.Setting(blocks, "damage") ?? "10";
            if (wave == 1) return blocks.Count == 0 ? "damage = 10\nAttack()" : null;
            if (wave == 2) return section >= 1 && StageOneCode.Setting(blocks, "range") == null ? $"damage = {damage}\nrange = 5\nAttack()" : null;
            if (wave <= 4) return !blocks.Any(b => b.Kind == BlockKind.If) ? $"damage = {damage}\nrange = 16\nif enemyDistance <= 5:\n    Attack()" : null;
            return section >= 3 && StageOneCode.Setting(blocks, "attackDistance") == null
                ? $"damage = {damage}\nrange = 16\nattackDistance = 10\nif enemyDistance <= attackDistance:\n    Attack()" : null;
        }
        void LoadProgram(string source)
        {
            codeEditor.Panel.LoadProgram("warrior", StageOneCode.Parse(source, true, AttackKind));
            codeEditor.Panel.Apply();
            observedProgram = Robot?.Program;
        }
        static string Code(string text) => Regex.Replace(text ?? "", "`([^`]+)`", "<color=#6FFFD2>$1</color>");
        bool Running => session != null && (session.Phase == BattlePhase.Running || session.Phase == BattlePhase.Paused);
        // A running wave needs the whole map, so dialogue drops to the bottom where the deck is idle anyway.
        TutorialDialoguePosition BattlePosition => Running ? TutorialDialoguePosition.Bottom : TutorialDialoguePosition.Top;
        TutorialDialoguePosition Position => IsBattleSection ? BattlePosition : IsPlacement && !codeEditor.IsOpen ? TutorialDialoguePosition.Top : TutorialDialoguePosition.Bottom;
        bool FirstVisit(int index, string text) => visitedText.Add(wave + "/" + section + "/" + index + "/" + text);
        void Show(bool allowAnimation = true)
        {
            var shown = current.dialogues[Math.Min(line, current.dialogues.Length - 1)];
            presentation.speakerName = shown.speaker;
            string text = wave == 5 && IsBattleSection && waiting && !bossHint ? current.interaction.prompt : shown.text;
            dialogue.BottomLift = codeEditor.IsOpen ? 44 : 0;
            dialogue.Show(presentation, Position, Code(text), allowAnimation && FirstVisit(line, text));
            // The line that asks for the action waits for it; Next stays locked until the player acts.
            if (NeedsAction && !waiting && !completed && line == boundary - 1) { Wait(); return; }
            dialogue.SetNavigation(line > 0 && !waiting, !SelectGate && (!waiting || completed));
            SetInput();
        }
        void Say(string text)
        {
            if (!string.IsNullOrEmpty(text)) Announce(text, true);
        }
        // A one-off line outside the section flow: no navigation until the flow resumes.
        void Announce(string text, bool animate) { dialogue.Show(presentation, Position, Code(text), animate); dialogue.SetNavigation(false, false); }
        public void Previous()
        { if (!Active || waiting) return; if (line > 0) { line--; Show(false); } }
        public void Next()
        {
            if (!Active || boot.activeSelf) return;
            if (dialogue.IsTyping) { dialogue.FinishTyping(); return; }
            if (waiting && !completed || SelectGate) return;
            if (line + 1 < current.dialogues.Length && (!NeedsAction || completed || line + 1 < boundary)) { line++; Show(); return; }
            if (NeedsAction && !completed) { Wait(); return; }
            section++;
            if (section >= content[wave - 1].sections.Length) { End(); return; }
            EnterSection();
        }
        bool NeedsAction => IsPlacement || IsCodeSection || IsBattleSection;
        void Wait()
        {
            waiting = true;
            if (IsCodeSection) observedProgram = Robot?.Program; // the player has to change the code themselves
            if (boundary == 0) Show(); else SetInput();
            dialogue.SetNavigation(false, false);
        }
        // Input stays locked to the allowed targets for the whole tutorial; outlines only mark the next action,
        // and a running wave keeps its targets clickable without boxes over the map.
        void SetInput()
        {
            bool acting = waiting && !completed || SelectGate;
            string[] keys = !acting ? Array.Empty<string>() : SelectGate ? new[] { RobotKey } : IsPlacement ? new[] { RobotKey, "Battle.DeploymentArea" } :
                IsCodeSection ? new[] { "Editor.ProgramDropZone", "Editor.BlockPalette" } :
                new[] { "Battle.Start", "Battle.Restart", RobotKey, "Battle.DeploymentArea", "Robot.Selected", "Battle.Develop", "Editor.ProgramDropZone", "Editor.BlockPalette", "Editor.Close" };
            step = new TutorialStep { kind = TutorialStepKind.ActionGuide, targetKeys = keys.Where(k => bridge.HasTarget(k)).ToArray(),
                completionMode = TutorialCompletionMode.ExternalSignal, pauseBattle = Pause, allowPreview = Preview,
                dim = acting && IsPlacement, blockInput = true, outline = acting && !Running };
            UpdateHighlight();
            bridge.SetStep(step, true);
        }
        // Outline only the next thing to press.
        void UpdateHighlight()
        {
            if (step == null) return;
            string key = IsPlacement ? battle.IsPlacing ? "Battle.DeploymentArea" : RobotKey :
                IsCodeSection ? "Editor.ProgramDropZone" : codeEditor.IsOpen ? "Editor.Close" : "Battle.Start";
            if (step.highlightKeys.Length != 1 || step.highlightKeys[0] != key) step.highlightKeys = new[] { key };
        }
        public bool Allows(string key) => !Active || SelectGate && key == RobotKey || waiting && !completed && (IsPlacement ? key == RobotKey || key == "Battle.DeploymentArea" :
            IsCodeSection ? key.StartsWith("Editor.") && key != "Editor.Close" :
            IsBattleSection && (key == "Battle.Start" || key == "Battle.Restart" || key == RobotKey || key == "Battle.DeploymentArea" || key == "Robot.Selected" || key == "Battle.Develop" || key.StartsWith("Editor.")));
        void Complete()
        {
            if (completed) return;
            if (wave == 5 && IsBattleSection) { section = 7; EnterSection(); return; }
            completed = true; waiting = false;
            if (wave == 1 && IsBattleSection) line = 6;
            else if (boundary < current.dialogues.Length) line = boundary;
            Show();
            if (IsCodeSection && codeEditor.IsOpen) codeEditor.Panel.ShowMessage(current.interaction?.success_feedback ?? "완료");
        }
        void EventDialogue(int index)
        { string text = current.dialogues[index].text; Announce(text, FirstVisit(index, text)); }
        void End()
        { dialogue.BottomLift = 0; current = null; step = null; boot.SetActive(false); bridge.SetStep(null); dialogue.Hide(); }
        void OnDestroy() { if (presentation) Destroy(presentation); }
#if UNITY_EDITOR
        public RobotRole EditorTeachingRole => teachingRole;
        public int EditorWave => Active ? wave : 0;
        public int EditorSection => Active ? section : -1;
        // Lessons after 1-1 placement need the teaching robot on the map; the caller places it first.
        public bool EditorNeedsRobot(int target) => target > (wave == 1 ? 1 : 0) && Robot == null;
        public void EditorJump(int target)
        {
            if (!Active) throw new InvalidOperationException("Stage 1 튜토리얼이 실행 중이 아닙니다.");
            if (target < 0 || target >= content[wave - 1].sections.Length) throw new ArgumentOutOfRangeException(nameof(target), "구간 번호 범위 오류");
            if (EditorNeedsRobot(target)) throw new InvalidOperationException("이 구간은 교육용 로봇 배치가 필요합니다.");
            section = target; EnterSection();
        }
#endif
    }
}
