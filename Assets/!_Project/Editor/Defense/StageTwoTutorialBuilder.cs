using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    public static class StageTwoTutorialBuilder
    {
        public const string Folder = "Assets/!_Project/Defense/Tutorial/Stage2";
        const string CampaignPath = "Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset";
        const string Drop = TutorialKeys.DropZone, Palette = TutorialKeys.Palette, Preview = TutorialKeys.Preview;
        static Dictionary<string, TutorialText> lines;
        static TutorialStep Line(string id, params string[] focus)
        {
            var source = lines[id];
            var step = new TutorialStep { stepId = id.Split('.')[1], dim = false, allowPreview = true,
                texts = new[] { new TutorialText { textId = id, speaker = source.speaker, text = source.text, focusKeys = focus } } };
            if (id == "2-1.A01" || id == "2-3.A01")
            { step.systemTone = id == "2-1.A01" ? TutorialNoticeTone.Calm : TutorialNoticeTone.Positive; step.systemTitle = id == "2-1.A01" ? "새로운 구역으로" : "새로운 로봇을 만났어요!"; }
            if (id == "2-5.A01")
            { step.texts[0].speaker = TutorialSignals.SystemSpeaker; step.systemTitle = "보스 접근 경고"; }
            return step;
        }
        static TutorialStep Action(TutorialStep step, string signal, params string[] targets)
        {
            step.kind = TutorialStepKind.ActionGuide; step.completionMode = TutorialCompletionMode.ExternalSignal;
            step.completionSignal = signal; step.targetKeys = targets; step.pointerKey = targets.FirstOrDefault(); return step;
        }
        static TutorialStep Click(string id, string key)
        {
            var step = Line(id, key); step.kind = TutorialStepKind.ActionGuide;
            step.completionMode = TutorialCompletionMode.TargetClick; step.targetKeys = new[] { key }; step.pointerKey = key; return step;
        }
        static TutorialStep Edit(string id, string signal, string expected, params string[] focus)
        {
            var step = Action(Line(id, focus), signal, Drop, Palette); step.expectedCode = expected;
            step.pointerKey = focus.FirstOrDefault();
            var from = focus.FirstOrDefault(key => key.StartsWith(TutorialKeys.PalettePrefix, StringComparison.Ordinal));
            var to = focus.FirstOrDefault(key => key.StartsWith(TutorialKeys.CodePrefix, StringComparison.Ordinal));
            if (from != null && to != null) step.dragDemo = from + ">" + to;
            return step;
        }
        static TutorialStep Observe(string id, int iteration, bool restart = false)
        {
            var step = Line(id, Preview); step.completionSignal = TutorialDirector.StageTwoObservation;
            step.observePreview = restart; step.previewIteration = iteration; step.holdSeconds = 1; return step;
        }
        static TutorialStep Start(string id)
        {
            var step = Action(Line(id, TutorialKeys.Start), TutorialSignals.BattleStarted, TutorialKeys.Start);
            step.dialoguePosition = TutorialDialoguePosition.Top; return step;
        }
        static TutorialStep EnemyWarning() => new TutorialStep {
            stepId = "EnemyWarning", dim = false, pauseBattle = true, systemTitle = "적 접근 경고",
            texts = new[] { new TutorialText { textId = "EnemyWarning", speaker = TutorialSignals.SystemSpeaker,
                text = "오류 코드가 접근 중입니다. 준비를 마치고 전투를 시작하세요!" } }
        };
        static TutorialStep Claim(string wave)
        {
            var step = new TutorialStep { stepId = "Claim", dim = false, dialoguePosition = TutorialDialoguePosition.Top,
                texts = new[] { new TutorialText { textId = wave + ".Claim", speaker = "안내자", text = "결과를 확인했으면 <b>‘보상 수령’</b>을 눌러 다음으로 가자." } } };
            step.kind = TutorialStepKind.ActionGuide; step.completionMode = TutorialCompletionMode.TargetClick;
            step.targetKeys = new[] { "Battle.ClaimRewards" }; step.pointerKey = "Battle.ClaimRewards"; return step;
        }
        static TutorialStep Wait(string id, string hints = null)
        {
            var step = Line(id); step.stepId = "WaitResult"; step.trigger = TutorialSignals.WaveCleared;
            step.pauseBattle = false; step.hintCode = hints; return step;
        }
        static List<TutorialStep> One(int warriorButton)
        {
            var pick = Click("2-1.A03", TutorialKeys.RobotButton(warriorButton)); pick.stepId = "A03-pick";
            var place = Action(Line("2-1.A03", TutorialKeys.Floor, TutorialKeys.Route), TutorialSignals.RobotPlaced, TutorialKeys.Deployment); place.stepId = "A03-place";
            var select = Click("2-1.B01", TutorialDirector.TeachingUnit); select.stepId = "B01-select";
            var open = Action(Line("2-1.B01", TutorialKeys.Develop), TutorialSignals.EditorOpened, TutorialKeys.Develop); open.stepId = "B01-open";
            var baseline = Line("2-1.B02", "Editor.Code.Line.1"); baseline.stageTwoExample = 1;
            var startObservation = Observe("2-1.B03", 0, true);
            var count = Edit("2-1.C03", TutorialDirector.StageTwoProgram, "3", "Editor.Code.Name.1.0");
            var result = Line("2-1.D02", TutorialKeys.RewardPanel); result.trigger = TutorialSignals.WaveCleared; result.pauseBattle = false;
            var retry = Line("2-5.C01"); retry.stepId = "RestartIntro"; retry.stageTwoRestartOnly = true;
            return new List<TutorialStep> {
                retry,
                Line("2-1.A01"), Line("2-1.A02"), pick, place, select, open, baseline, startObservation,
                Line("2-1.B04", Preview),
                Edit("2-1.C01", TutorialSignals.CodeLinePlaced, "1:For", "Editor.Palette.For", "Editor.Code.Start"),
                Edit("2-1.C02", TutorialDirector.StageTwoProgram, "", "Editor.Code.Body.1", "Editor.Code.Line.2", "Editor.Code.Line.3"), count,
                Observe("2-1.C04", 1, true), Observe("2-1.C05", 2), Observe("2-1.C06", 3),
                Observe("2-1.C07", 0), Line("2-1.C08", Preview), Line("2-1.C09", Preview),
                Action(Line("2-1.D01", TutorialKeys.Close), TutorialSignals.EditorClosed, TutorialKeys.Close),
                EnemyWarning(), Start("2-1.D01"), result, Claim("2-1")
            }.Select(s => { if (s.completionSignal == TutorialSignals.BattleStarted) s.stepId = "D01-start"; return s; }).ToList();
        }
        static List<TutorialStep> Two() => new List<TutorialStep> {
            Line("2-2.A01"), Line("2-2.A02"), Line("2-2.B01"), Line("2-2.B02"), Line("2-2.D01"),
            EnemyWarning(), Wait("2-2.D02", StageTwoLearning.HintCode(RobotRole.Warrior)), Line("2-2.D03"), Claim("2-2") };
        static List<TutorialStep> Three(int shooterButton)
        {
            var select = Click("2-3.B01", TutorialDirector.TeachingUnit); select.stepId = "B01-select";
            var open = Action(Line("2-3.B01", TutorialKeys.Develop), TutorialSignals.EditorOpened, TutorialKeys.Develop); open.stepId = "B01-open";
            var baseline = Line("2-3.B02", "Editor.Code.Body.1"); baseline.stageTwoExample = 2;
            var slot = Edit("2-3.B03", TutorialSignals.SlotFilled, "Editor.Code.BodyArg.1.1.0:Variable", "Editor.Code.BodyEnd.1", "Editor.Palette.Shot");
            slot.texts[0].text = "<b>for 안의 enemy = get_nearest_enemy()</b> 바로 아래, 표시된 빈 칸에 <b>attack</b> 블록을 넣어 줘.";
            slot.highlightPadding = 3;
            // First place the attack statement, then connect its target in B04.
            slot.completionSignal = TutorialSignals.StageTwoAttackPlaced;
            slot.expectedCode = "";
            slot.autoSelectSlot = "Editor.Code.BodyEnd.1";
            var target = Edit("2-3.B04", TutorialDirector.StageTwoProgram, "", "Editor.Code.BodyArg.1.1.0", "Editor.Palette.Variable"); target.autoSelectSlot = "Editor.Code.BodyArg.1.1.0";
            var shot = Action(Line("2-3.B05", Preview), TutorialDirector.StageTwoShot, Preview); shot.restartPreview = true; shot.holdSeconds = 1;
            var hit = Action(Line("2-3.B06", Preview), TutorialDirector.StageTwoImpact, Preview); hit.holdSeconds = 1;
            var result = Wait("2-3.B07"); result.texts[0].text = "원거리 공격의 결과를 확인했어. 다음에는 직접 배치하고 반복 코드를 만들어 보자.";
            return new List<TutorialStep> { Line("2-3.A01"), Click("2-3.A02", TutorialKeys.RobotButton(shooterButton)),
                Action(Line("2-3.A03", TutorialKeys.Route, TutorialKeys.Floor), TutorialSignals.RobotPlaced, TutorialKeys.Deployment),
                select, open, baseline, slot, target, shot, hit, Line("2-3.B07", Preview), Line("2-3.C01", Preview), Line("2-3.C02", Preview),
                Action(new TutorialStep { stepId = "Close", allowPreview = true, dim = false, texts = new[] { new TutorialText { textId = "2-3.Close", speaker = "안내자", text = "개발창을 닫고 실제 전투에서도 확인해 보자." } } }, TutorialSignals.EditorClosed, TutorialKeys.Close),
                EnemyWarning(), Start("2-1.D01"), result, Claim("2-3") };
        }
        static List<TutorialStep> Four() => new List<TutorialStep> { Line("2-4.A01"), Line("2-4.A02"), Line("2-4.B01"), Line("2-4.B02"), Line("2-4.D01"), EnemyWarning(), Wait("2-4.D02", StageTwoLearning.HintCode(RobotRole.Shooter)), Claim("2-4") };
        static List<TutorialStep> Five() => new List<TutorialStep> { Line("2-5.A01"), Line("2-5.A02"), Line("2-5.A03"), Line("2-5.A04"), Line("2-5.A05"), Wait("2-5.A06", StageTwoLearning.HintCode(RobotRole.Warrior)), Line("2-5.A07"), Claim("2-5") };

        public static TutorialSequence[] Build(DefenseCampaignDefinition campaign)
        {
            lines = new Dictionary<string, TutorialText>();
            foreach (string raw in File.ReadAllLines("Docs/flow_stage2/tutorial_stage2_대본.md"))
            {
                var match = Regex.Match(raw, @"^\[(2-\d\.[A-Z]\d\d)\] (?:\(UI로 표시\) )?(안내자|시스템|힌트 \d+) : (.*)$");
                if (match.Success) lines.Add(match.Groups[1].Value, new TutorialText { speaker = match.Groups[2].Value, text = match.Groups[3].Value });
            }
            // Apply the document's current-runtime corrections, retaining the original in the source document.
            lines["2-3.C01"].text = "원거리 로봇은 같은 적이라도 거리에 따라 주는 피해가 달라져.";
            lines["2-3.C02"].text = "<b>같은 로봇이라면, 사거리 안에서 적이 가까울수록 피해가 커지고 멀수록 작아져.</b>";
            lines["2-3.B07"].text = "원거리 로봇은 사거리 안의 적에게 탄을 쏠 수 있어. 배치할 때 사거리와 길이 겹치는지 확인하자.";
            lines["2-4.B01"].text = "공격 범위가 적이 지나는 길에 닿는지 확인해. 같은 원거리 로봇은 사거리 안에서 적이 가까울수록 피해가 커진다는 점도 생각해 보자.";
            lines["2-4.C04"].text = "같은 원거리 로봇도 적까지의 거리에 따라 피해가 달라져요. 배치와 공격 결과를 함께 살펴보세요.";
            Directory.CreateDirectory(Folder);
            int warrior = Array.FindIndex(campaign.robots, r => r.stats.role == RobotRole.Warrior), shooter = Array.FindIndex(campaign.robots, r => r.stats.role == RobotRole.Shooter);
            var groups = new[] { One(warrior), Two(), Three(shooter), Four(), Five() };
            var result = new TutorialSequence[5];
            for (int i = 0; i < groups.Length; i++)
            {
                string id = "2-" + (i + 1), path = Folder + "/Tutorial_" + id + ".asset";
                var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>(path);
                if (!sequence) { sequence = ScriptableObject.CreateInstance<TutorialSequence>(); AssetDatabase.CreateAsset(sequence, path); }
                sequence.sequenceId = id; sequence.enabled = true; sequence.stage = 2; sequence.wave = i + 1;
                sequence.entryPhase = TutorialEntryPhase.Preparation; sequence.typeSpeed = 30; sequence.steps = groups[i].ToArray();
                EditorUtility.SetDirty(sequence); result[i] = sequence;
            }
            for (int wave = 1; wave <= 5; wave++)
            {
                int index = Array.FindIndex(campaign.progression.lessons, l => l.stage == 2 && l.wave == wave);
                if (index < 0) throw new InvalidOperationException("Stage 2 lesson missing: " + wave);
                campaign.progression.lessons[index] = DefenseCurriculum.StageTwoLesson(wave);
            }
            var basic = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/!_Project/Defense/Tutorial/Stage1/TutorialError.asset");
            var stage = campaign.stages[1]; stage.waveSeconds = 120; stage.waves = new DefenseBattle.Wave[5];
            for (int i = 0; i < 5; i++) stage.waves[i] = new DefenseBattle.Wave { groups = new[] { new DefenseBattle.SpawnEntry {
                enemy = basic, route = 0, count = i == 4 ? 1 : i == 0 ? 2 : 3, interval = 1.5f,
                healthOverride = i == 4 ? 110 : 12, speedOverride = i == 4 ? .55f : .8f,
                leakDamageOverride = i == 4 ? 12 : 1, boss = i == 4 } } };
            EditorUtility.SetDirty(campaign); return result;
        }

        [MenuItem("Tools/Develop/Stage 2 튜토리얼 asset 다시 만들기")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 적용하세요.");
            EditorSceneManager.OpenScene("Assets/!_Project/Scenes/Defense.unity", OpenSceneMode.Single);
            var campaign = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>(CampaignPath);
            var created = Build(campaign);
            var director = UnityEngine.Object.FindObjectsByType<TutorialDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(director); var list = so.FindProperty("sequences");
            var kept = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as TutorialSequence).Where(s => s && s.stage != 2).ToArray();
            list.arraySize = kept.Length + created.Length;
            for (int i = 0; i < list.arraySize; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = i < kept.Length ? kept[i] : created[i - kept.Length];
            so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(director.gameObject.scene);
            Debug.Log("Stage 2 applied: " + string.Join(", ", created.Select(s => s.sequenceId + " (" + s.steps.Length + ")")));
        }
    }
}
