using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Scene references are authored; only teaching data and block trees are built at runtime.
    public sealed class StageOneTutorial : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] TutorialDialogueView dialogue;
        [SerializeField] TutorialInputBridge bridge;
        [SerializeField] DefenseCodeEditor codeEditor;
        [SerializeField] TextAsset[] stages;
        [SerializeField] GameObject lessonPanel;
        [SerializeField] TMP_Text heading, examples, feedback, observation;
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] Button submit;
        [SerializeField] GameObject demonstration, boot;
        [SerializeField] CanvasGroup bootFade;
        [SerializeField] RectTransform rangeCircle, enemyMarker;
        [SerializeField] Image enemyGraphic;
        [SerializeField] TMP_Text variableBox, comparison, hitResult;
        [SerializeField] AudioSource warningSource;
        StageOneContent[] content;
        TutorialSequence presentation;
        DefenseSimulation session;
        StageOneSection current;
        int wave, section, line, boundary, kills, leaks;
        bool waiting, completed, initialized;
        string notice;
        DefenseProgram observedProgram;
        bool editorWasOpen;
        bool bossHint;
        readonly HashSet<string> visitedText = new HashSet<string>();
        float diagramRange, diagramDistance;
        float bootUntil;
        public bool OwnsSession => initialized && battle.Simulation != null && battle.Simulation.Setup.StageOneLearning && battle.Stage == 1 && battle.Simulation.Options.tutorial;
        public bool Active => OwnsSession && current != null;
        public bool Pause => Active && !(waiting && IsBattleSection);
        public bool Preview => Active && !IsBattleSection;
        bool IsBattleSection => wave < 5 ? section == 4 : section == 6;
        bool IsCodeSection => wave == 1 && section == 3 || wave == 2 && (section == 2 || section == 3) ||
            wave == 3 && section == 3 || wave == 4 && (section == 2 || section == 3) || wave == 5 && (section == 4 || section == 5);
        bool IsPlacement => wave == 1 && section == 1;
        public void Initialize()
        {
            if (!battle || !dialogue || !bridge || !codeEditor || !lessonPanel || !heading || !examples || !feedback || !observation || !codeInput || !submit ||
                !demonstration || !boot || !bootFade || !rangeCircle || !enemyMarker || !enemyGraphic || !variableBox || !comparison || !hitResult ||
                !warningSource || !warningSource.clip || stages == null || stages.Length != 5)
                throw new InvalidOperationException("StageOneTutorial의 JSON·UI·전투 참조를 연결하세요.");
            content = stages.Select((asset, index) => StageOneContent.Read(asset, index + 1)).ToArray();
            presentation = ScriptableObject.CreateInstance<TutorialSequence>();
            presentation.stage = 1; presentation.typeSpeed = 20; presentation.speakerName = "안내자";
            submit.onClick.AddListener(Submit);
            initialized = true; lessonPanel.SetActive(false); demonstration.SetActive(false); boot.SetActive(false);
        }
        void Update()
        {
            if (!OwnsSession) { if (current != null) End(); return; }
            var sim = battle.Simulation;
            if (session != sim) { session=sim; wave=0; current=null; visitedText.Clear(); }
            if (wave != sim.WaveIndex + 1) { wave=sim.WaveIndex+1; section=0; EnterSection(); }
            if (!Active) return;
            if(demonstration.activeSelf)
            {
                rangeCircle.sizeDelta=Vector2.Lerp(rangeCircle.sizeDelta,Vector2.one*diagramRange,Time.unscaledDeltaTime*8);
                enemyMarker.anchoredPosition=Vector2.Lerp(enemyMarker.anchoredPosition,new Vector2(diagramDistance,0),Time.unscaledDeltaTime*8);
            }
            if(editorWasOpen!=codeEditor.IsOpen)
            {
                editorWasOpen=codeEditor.IsOpen;
                lessonPanel.SetActive(!editorWasOpen && !IsPlacement);
                demonstration.SetActive(!editorWasOpen && !IsPlacement && !IsBattleSection);
                Show();
            }
            if(boot.activeSelf)
            {
                bootFade.alpha=Mathf.Clamp01((bootUntil-Time.unscaledTime)/.6f);
                if(Time.unscaledTime>=bootUntil) boot.SetActive(false);
            }
            if (wave == 1 && section == 0 && Time.unscaledTime < bootUntil) observation.text="SYSTEM START\n개발툴 오류 감지";
            else if (waiting && IsBattleSection)
            {
                if (sim.Leaks > leaks) { leaks=sim.Leaks; feedback.text=current.interaction?.retry_hint; if(wave==1) EventDialogue(5); }
                if (sim.Kills > kills) { kills=sim.Kills; feedback.text="오류 코드 처리 · " + kills; if(wave==1 && kills==1) EventDialogue(4); }
                if (sim.Phase == BattlePhase.Defeat) { notice=current.interaction?.retry_hint; feedback.text=notice; }
                if(wave==5 && !bossHint && sim.Phase==BattlePhase.Running && sim.Leaks>leaksAtStart)
                { bossHint=true; EventDialogue(0); }
                if ((sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory) && sim.LastSurvivors == 0 && sim.Leaks == leaksAtStart)
                    Complete();
                else if (sim.Phase == BattlePhase.Reward && !completed) feedback.text="오류 코드를 놓쳤어. 다시 시작해 배치와 코드를 조절해봐.";
            }
            else if (waiting && IsPlacement && sim.Robots.Count > 0) Complete();
            else if (waiting && IsCodeSection)
            {
                var program=sim.Robots.FirstOrDefault(r=>r.Id==battle.SelectedId)?.Program;
                if(program!=null && program!=observedProgram)
                {
                    observedProgram=program;
                    try
                    {
                        string source=string.Join("\n",program.Source.Replace("\r","").Split('\n').Skip(1)
                            .Where(s=>s.Trim()!="enemy = get_nearest_enemy()" && s.Trim()!="enemyDistance = enemy.get_distance(get_pos_x(), get_pos_y()) if enemy is not None else float('inf')")
                            .Select(s=>s.Length>=4?s.Substring(4):s)).Replace("attack(enemy)","Attack()");
                        StageOneCode.Validate(source,wave,section); Complete();
                    }
                    catch(FormatException error) { feedback.text=error.Message+"\n"+current.interaction?.retry_hint; }
                }
            }
            if (sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused)
            {
                var robot=sim.Robots.FirstOrDefault();
                var enemy=robot == null ? null : sim.Enemies.Where(e=>e.Active).OrderBy(e=>System.Numerics.Vector2.DistanceSquared(e.Position,robot.Position)).FirstOrDefault();
                if (enemy != null) observation.text=$"enemyDistance = {System.Numerics.Vector2.Distance(enemy.Position,robot.Position):0.0}\n피해 {robot.DamageSetting ?? robot.Spec.damage:0} · range = {robot.Range:0.0}\n개발툴 오류 {sim.Setup.BaseHealth-sim.BaseHealth}";
            }
        }
        int leaksAtStart;
        void EnterSection()
        {
            current=content[wave-1].sections[section]; line=0; waiting=completed=false; bossHint=false; notice=null;
            boundary = wave == 1 && section == 1 ? 4 : wave == 1 && section == 3 ? 2 : wave == 2 && section == 2 ? 4 :
                wave == 2 && section == 3 ? 3 : wave == 3 && section == 3 ? 2 : wave == 4 && section == 3 ? 2 :
                wave == 5 && section == 4 ? 1 : current.dialogues.Length;
            if (IsBattleSection && wave < 5) boundary = wave == 1 ? 4 : wave == 2 ? 5 : wave == 3 ? 0 : 1;
            if(wave==5 && IsBattleSection) boundary=0;
            kills=session.Kills; leaks=leaksAtStart=session.Leaks;
            presentation.wave=wave;
            heading.text=current.section_title;
            examples.text=string.Join("\n\n",current.code_examples.Select(e=>e.code));
            feedback.text=current.interaction?.prompt ?? "";
            observation.text=wave == 5 && section == 0 ? "WARNING\nUNKNOWN ERROR DETECTED" : "";
            if(wave==5 && section==0) warningSource.Play();
            if(wave==5 && section==7) observation.text="STAGE CLEAR\nStage 2 · 반복 해금";
            if(wave==1 && section==0) { bootUntil=Time.unscaledTime+1.5f; bootFade.alpha=1; boot.SetActive(true); }
            demonstration.SetActive(!IsPlacement && !IsBattleSection && !(wave==1 && section==0));
            if(demonstration.activeSelf) DrawDemonstration();
            lessonPanel.SetActive(true); codeInput.gameObject.SetActive(false); submit.gameObject.SetActive(false);
            // Comparison demonstrations retain the supplied numbers and use the same <= rule as combat.
            if(wave==3 && section==2) observation.text="15 <= 10  False\n9 <= 10  True";
            if(wave==5 && section==3) observation.text="14 <= 10  False\n8 <= 10  True";
            if(wave==4 && section==1) observation.text="거리 5: 늦게 공격\n거리 10: 일찍 공격";
            if(boundary==0) Wait(); else Show();
        }
        bool FirstVisit(int index,string text) => visitedText.Add(wave+"/"+section+"/"+index+"/"+text);
        void Show(bool allowAnimation = true)
        {
            presentation.speakerName=current.dialogues[Math.Min(line,current.dialogues.Length-1)].speaker;
            string text=wave==5 && IsBattleSection && waiting && !bossHint ? current.interaction.prompt : current.dialogues[Math.Min(line,current.dialogues.Length-1)].text;
            string sample=string.Join("\n\n",current.code_examples.Select(e=>e.code));
            foreach(Match token in Regex.Matches(text,"`([^`]+)`")) sample=sample.Replace(token.Groups[1].Value,"<color=#6FFFD2>"+token.Groups[1].Value+"</color>");
            examples.text=sample;
            bool firstVisit=FirstVisit(line,text);
            dialogue.Show(presentation, IsPlacement || IsBattleSection || codeEditor.IsOpen ? TutorialDialoguePosition.Top : TutorialDialoguePosition.Bottom, text,allowAnimation && firstVisit,null);
            dialogue.SetNavigation(line>0,!waiting || completed);
            if(demonstration.activeSelf) DrawDemonstration();
            SetInput();
        }
        public void Previous()
        { if(!Active || waiting) return; if(line>0) { line--; Show(false); } }
        public void Next()
        {
            if(!Active) return;
            if(boot.activeSelf) return;
            if(dialogue.IsTyping) { dialogue.FinishTyping(); return; }
            if(waiting && !completed) return;
            if(line+1 < current.dialogues.Length && (!NeedsAction || completed || line+1<boundary)) { line++; Show(); return; }
            if(NeedsAction && !completed) { Wait(); return; }
            section++;
            if(section >= content[wave-1].sections.Length) { End(); return; }
            EnterSection();
        }
        bool NeedsAction => IsPlacement || IsCodeSection || IsBattleSection;
        void Wait()
        {
            waiting=true; SetInput();
            if(IsPlacement) lessonPanel.SetActive(false);
            if(IsCodeSection)
            {
                observedProgram=session.Robots.FirstOrDefault(r=>r.Id==battle.SelectedId)?.Program;
                codeInput.gameObject.SetActive(true); submit.gameObject.SetActive(true);
                codeInput.SetTextWithoutNotify(wave==1 ? "damage = 10" : wave==2 && section==2 ? "range = 5" : wave==2 ? "damage = 15\nrange = 7" : wave==5 ? "attackDistance = 10\n\nif enemyDistance <= attackDistance:\n    Attack()" : "if enemyDistance <= 5:\n    Attack()");
            }
            if(boundary==0) Show();
            dialogue.SetNavigation(false,false);
        }
        void SetInput()
        {
            string[] keys = waiting && !completed ? IsPlacement ? new[]{"Battle.Robot.4","Battle.DeploymentArea"} : IsCodeSection ?
                new[]{"Lesson.Input","Lesson.Submit","Robot.Selected","Battle.Develop","Editor.ProgramDropZone","Editor.BlockPalette","Editor.Close"} :
                new[]{"Battle.Start","Battle.Restart","Battle.DeploymentArea","Battle.Robot.4","Robot.Selected","Battle.Develop","Editor.ProgramDropZone","Editor.BlockPalette","Editor.Close"} : Array.Empty<string>();
            keys=keys.Where(k=>bridge.HasTarget(k)).ToArray();
            bridge.SetStep(new TutorialStep { kind=TutorialStepKind.ActionGuide, targetKeys=keys, completionMode=TutorialCompletionMode.ExternalSignal, pauseBattle=Pause, allowPreview=Preview },true);
        }
        public bool Allows(string key) => !Active || waiting && !completed && (IsPlacement ? key=="Battle.Robot.4" || key=="Battle.DeploymentArea" :
            IsCodeSection ? key.StartsWith("Editor.") || key=="Battle.Develop" || key=="Robot.Selected" :
            IsBattleSection && (key=="Battle.Start" || key=="Battle.Restart" || key=="Battle.Robot.4" || key=="Battle.DeploymentArea" || key=="Robot.Selected" || key=="Battle.Develop" || key.StartsWith("Editor.")));
        public void Submit()
        {
            if(!Active || !waiting || !IsCodeSection) return;
            try
            {
                StageOneCode.Validate(codeInput.text,wave,section);
                var robot=session.Robots.FirstOrDefault(r=>r.Id==battle.SelectedId) ?? session.Robots.FirstOrDefault();
                if(robot==null) throw new FormatException("로봇을 먼저 배치하세요.");
                string source=codeInput.text;
                // Preserve previously learned stats when only the next variable is edited.
                if(!source.Contains("damage")) source="damage = "+(robot.DamageSetting ?? 20).ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+source;
                if(!source.Contains("range")) source="range = "+(wave>=3 ? 16 : robot.RangeSetting).ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+source;
                session.ApplyProgram(robot.Id,"stage1",StageOneCode.Parse(source,true));
                Complete();
            }
            catch(FormatException error) { feedback.text=error.Message+"\n"+current.interaction?.retry_hint; }
        }
        void Complete()
        {
            if(completed) return;
            if(wave==5 && IsBattleSection) { section=7; EnterSection(); return; }
            completed=true; waiting=false; notice=current.interaction?.success_feedback; feedback.text=notice;
            if(IsBattleSection) observation.text=wave==5?"STAGE CLEAR":"WAVE CLEAR";
            codeInput.gameObject.SetActive(false); submit.gameObject.SetActive(false);
            lessonPanel.SetActive(true);
            if(wave==1 && IsBattleSection) line=6;
            else if(boundary<current.dialogues.Length) line=boundary;
            Show();
        }
        void EventDialogue(int index)
        { string text=current.dialogues[index].text;dialogue.Show(presentation,TutorialDialoguePosition.Top,text,FirstVisit(index,text),null); dialogue.SetNavigation(false,false); }
        void End()
        { current=null; lessonPanel.SetActive(false); demonstration.SetActive(false); boot.SetActive(false); bridge.SetStep(null); dialogue.Hide(); }
        void DrawDemonstration()
        {
            float damage=wave==1 && section==2 ? 10 : 20;
            float range=wave==1 ? 5 : wave==2 ? section>=2 && (line>=4 || completed) ? 8 : 5 : 16;
            float threshold=wave==4 ? section==1 ? line<2 ? 5 : 10 : section==3 && !completed ? 2 : 8 :
                wave==5 && section>=4 && (line>0 || completed) ? 15 : 10;
            float distance=wave==1 ? 4 : wave==3 && section==2 ? line>=3 ? 9 : 15 : wave==5 && section==3 ? line==0 ? 14 : 8 :
                wave==2 && section==2 ? line<2 ? 6 : 4 : 8;
            if(wave==1 && section==3) damage=completed ? 20 : 10;
            var robot=session.Robots.FirstOrDefault();
            if(completed && robot!=null) { damage=robot.DamageSetting ?? damage; range=robot.RangeSetting; }
            diagramRange=(wave>=3?Mathf.Min(range,threshold):range)*20;
            diagramDistance=distance*10;
            variableBox.text=$"<color=#6FFFD2>damage</color>  [ {damage:0} ]\n<color=#6FFFD2>range</color>  [ {range:0} ]"+(wave==5?$"\n<color=#6FFFD2>attackDistance</color>  [ {threshold:0} ]":"");
            // Run the illustration through combat, so the displayed result cannot diverge from <= and range rules.
            var setup=new BattleSetup { StageOneLearning=true, FunctionRepeatLimit=10,
                Min=new System.Numerics.Vector2(-30,-30),Max=new System.Numerics.Vector2(30,30),
                ActionProfiles=session.Setup.ActionProfiles, Permissions=session.Setup.Permissions,
                Routes=new[]{new Route(new[]{new System.Numerics.Vector2(-20,0),new System.Numerics.Vector2(20,0)},1)},
                Waves=new[]{new[]{new SpawnGroup {Enemy=new EnemySpec(),Count=1,Delay=100,Interval=1}}} };
            var demo=new DefenseSimulation(setup);
            var spec=session.Setup.ActionProfiles.Single(s=>s.role==RobotRole.Shooter).Copy();
            spec.range=range;
            var bot=demo.Place(spec,4,new System.Numerics.Vector2(0,3),out _);
            string source=$"damage = {damage.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nrange = {range.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            if(wave>=3) source+=$"\nattackDistance = {threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nif enemyDistance <= attackDistance:\n    Attack()";
            demo.ApplyProgram(bot.Id,"illustration",StageOneCode.Parse(source,true));
            demo.Start();bot.NextAction=0;
            var target=new EnemyState {Id=999,Active=true,Spec=new EnemySpec {health=1000},Health=1000,Position=new System.Numerics.Vector2(distance,3)};
            demo.Enemies.Add(target); bot.Program.Tick(demo,bot);
            comparison.text=wave>=3?$"enemyDistance = {distance:0}\n{distance:0} <= {threshold:0}  {(distance<=threshold?"True":"False")}":$"enemyDistance = {distance:0}\nrange = {range:0}";
            hitResult.text=target.Health<1000?$"Attack() · {1000-target.Health:0} DAMAGE":"대기 · 조건 또는 사거리 밖";
            enemyGraphic.color=target.Health<1000?new Color(1,.6f,.25f):new Color(.9f,.25f,.3f);
        }
        void OnDestroy() { if(presentation) Destroy(presentation); }
    }
}
