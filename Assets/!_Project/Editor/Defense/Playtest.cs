// Play-test runner for the tutorial and battle feedback (Docs/임시변경점.md §16): starts a fresh Defense play session, drives it
// with real pointer drags and tutorial navigation, and writes Temp/Playtest/report.md with a screenshot per scenario.
// Run from Tools/Develop. Each scenario checks one reported issue; a failure or time-out moves on to the next scenario.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    [InitializeOnLoad]
    public static class Playtest
    {
        const string Pending = "CodingGame.Playtest.Pending", Folder = "Temp/Playtest";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        sealed class Until { public Func<bool> done; public string what; public double deadline; }
        static readonly List<(string name, Func<IEnumerator> body)> Scenarios = new List<(string, Func<IEnumerator>)>
        {
            ("1-1 A01 경고 카드 박자", AlertBeat),
            ("1-1 E01 뒤로가기에 경고 재생 안 됨", BackSkipsAlert),
            ("1-1 설명 중 블록 미리 꺼내기 막힘", ExplanationBlocksDrag),
            ("1-1 C08 꺼내 둔 블록 연결로 진행", PreplacedBlocksProgress),
            ("1-3 B01 기초 코드 두 줄 지급", DistanceLines),
            ("1-1 D03 변수 배치와 D04 enemy 입력 두 단계", VariableThenName),
            ("1-1 D08 바로 적중해도 1.5초 보여 줌", PreviewHitHold),
            ("1-1 E08 보상 집중이 대화창에 안 가림", RewardFocusClear),
            ("1-2 B01 자유 플레이: 대사 숨고 조작 풀림, 힌트", OneTwoIntro),
            ("베기 이펙트 SwordSlash_3", Slash),
            ("코어 피격·사망 글리치", CoreGlitch),
            ("맵 소품 위 설치 막힘 · 기지 체력바 위치 · 사거리 색", PropsAndBase),
            ("스테이지 맵 클릭 후 전환 대기", StageClickDelay),
            ("1-1 A04b 기지 체력 집중·B04 사거리 강조 색", BaseFocusAndRangeHighlight),
            ("로봇·몬스터 애니메이션", Animations),
        };
        static int index = -1;
        static IEnumerator body;
        static readonly Stack<IEnumerator> nested = new Stack<IEnumerator>(); // helpers a scenario yields (Jump, Fresh…)
        static Until until;
        static double resumeAt, startedAt;
        static StringBuilder report;
        static List<string> checks;
        static bool failed;
        static int passed;

        static Playtest() => EditorApplication.update += Tick;
        public static bool Running => index >= 0;

        // A fresh play session so the first screen (A01) loads like a player's.
        [MenuItem("Tools/Develop/플레이테스트 실행 (기획 피드백 확인)")]
        public static void Run() => RunOne(0);
        // only: one scenario (1-based), e.g. while fixing it; 0 runs them all.
        public static void RunOne(int only)
        {
            if (Running) { Debug.LogWarning("플레이테스트가 이미 실행 중입니다."); return; }
            Only = only;
            SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
        // Kept in SessionState: entering Play Mode reloads the domain.
        static int Only { get => SessionState.GetInt(OnlyKey, 0); set => SessionState.SetInt(OnlyKey, value); }
        const string OnlyKey = "CodingGame.Playtest.Only";
        [MenuItem("Tools/Develop/플레이테스트 중단")]
        public static void Stop() { SessionState.SetBool(Pending, false); if (Running) Finish("중단"); }

        static void Tick()
        {
            if (SessionState.GetBool(Pending, false))
            {
                // The scene's own start, as from the stage map: a DevTest session request would rebuild the battle a frame
                // later, and that rebuild's stall is not one a player sees.
                if (!EditorApplication.isPlayingOrWillChangePlaymode) { DevTest.PlayScene("Assets/!_Project/Scenes/" + GameFlow.DefenseScene + ".unity"); return; }
                if (!EditorApplication.isPlaying || !Battle || Battle.Simulation == null || !Battle.Tutorial || !Director.EditorSynced) return;
                SessionState.SetBool(Pending, false);
                Begin();
            }
            if (!Running) return;
            if (!EditorApplication.isPlaying) { Finish("Play 종료됨"); return; }
            if (EditorApplication.isPaused) return;
            double now = EditorApplication.timeSinceStartup;
            if (until != null)
            {
                bool done;
                try { done = until.done(); } catch (Exception e) { Fail(e.Message); Next(); return; }
                if (!done)
                {
                    if (now > until.deadline) { Fail("시간 초과: " + until.what); Next(); }
                    return;
                }
                until = null;
            }
            if (now < resumeAt) return;
            try
            {
                var current = nested.Count > 0 ? nested.Peek() : body;
                if (!current.MoveNext())
                {
                    if (nested.Count > 0) { nested.Pop(); return; }
                    Next(); return;
                }
                switch (current.Current)
                {
                    case IEnumerator helper: nested.Push(helper); break;
                    case float seconds: resumeAt = now + seconds; break;
                    case Until wait: until = wait; break;
                }
            }
            catch (Exception e) { Fail(e.GetType().Name + ": " + e.Message); Next(); }
        }

        static void Begin()
        {
            Directory.CreateDirectory(Folder);
            foreach (var file in Directory.GetFiles(Folder)) File.Delete(file);
            report = new StringBuilder("# 플레이테스트 결과\n\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            passed = 0; index = -1; Time.timeScale = 1;
            // The whole run is recorded; each scenario notes where it starts in the video.
            if (PlayRecorder.FindFFmpeg() != null) PlayRecorder.Start(Path.Combine(Folder, "playtest.mp4"));
            else report.Append("\n녹화 안 함: FFmpeg 없음 (winget install Gyan.FFmpeg)\n");
            Next();
        }
        static void Next()
        {
            if (index >= 0) Close();
            until = null; resumeAt = 0; nested.Clear();
            if (Only > 0 && index < 0) index = Only - 2;
            if (++index >= Scenarios.Count || Only > 0 && index >= Only) { Finish("완료", false); return; }
            checks = new List<string>(); failed = false; startedAt = PlayRecorder.Elapsed;
            body = Scenarios[index].body();
        }
        static void Close()
        {
            if (!failed) passed++;
            report.Append($"\n## {index + 1}. {Scenarios[index].name} — {(failed ? "FAIL" : "PASS")}\n\n");
            if (PlayRecorder.Recording) report.Append($"영상 {TimeSpan.FromSeconds(startedAt):mm\\:ss}부터\n\n");
            foreach (var line in checks) report.Append("- ").Append(line).Append('\n');
            report.Append($"\n![{index + 1}]({index + 1}.png)\n");
        }
        // closeCurrent: stopped mid-scenario (Next has already closed the last one when the run completes).
        static void Finish(string why, bool closeCurrent = true)
        {
            if (closeCurrent && index >= 0 && index < Scenarios.Count && body != null) Close();
            int total = Only > 0 ? 1 : Scenarios.Count;
            if (PlayRecorder.Recording) report.Append("\n" + PlayRecorder.Stop() + "\n");
            report.Insert(report.ToString().IndexOf('\n') + 1, $"\n**{passed} / {total} 통과** ({why})\n");
            File.WriteAllText(Path.Combine(Folder, "report.md"), report.ToString());
            Debug.Log($"[Playtest] {passed}/{total} 통과 ({why}) → {Folder}/report.md");
            index = -1; Only = 0; body = null; until = null; Time.timeScale = 1;
        }

        static void Check(bool ok, string what) { checks.Add((ok ? "✅ " : "❌ ") + what); if (!ok) failed = true; }
        static void Fail(string why) { checks.Add("❌ " + why); failed = true; }
        static Until Wait(Func<bool> done, string what, float seconds = 30) =>
            new Until { done = done, what = what, deadline = EditorApplication.timeSinceStartup + seconds };
        // Captured at the end of the frame: yield a moment before changing what is on screen.
        static void Shot()
        {
            var path = Path.Combine(Folder, (index + 1) + ".png");
            if (!PlayRecorder.Snapshot(path)) ScreenCapture.CaptureScreenshot(path);
        }

        static DefenseBattle Battle => DevTest.Battle;
        static TutorialDirector Director => Battle.Tutorial;
        static string StepId => Director.EditorStep != null ? Director.EditorStep.stepId : "-";
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static int StepIndex(int wave, string id) => Array.FindIndex(DevTest.TutorialSections(wave), s => s.StartsWith(id + " "));
        // Jumps inside the running Stage 1 session; the editor-side jump applies on its next update.
        static IEnumerator Jump(int wave, string id)
        {
            yield return Wait(() => Director.EditorSynced, "튜토리얼이 새 세션을 받음", 10);
            DevTest.JumpTutorial(wave, StepIndex(wave, id));
            yield return Wait(() => StepId == id, "점프 " + wave + " " + id, 10);
            yield return .2f;
        }
        // A new 1-1 tutorial session in the running scene, so a scenario does not inherit the previous one's state.
        static IEnumerator Fresh()
        {
            DevTest.PlayDefense(1, 1, true, false);
            yield return Wait(() => DevTest.Ready && Director.EditorSynced && Battle.Simulation.Time == 0 && StepId == "A01", "새 1-1 튜토리얼", 10);
        }
        static IEnumerator OpenEditor()
        {
            if (!Battle.CodeEditor.IsOpen) Battle.CodeEditor.Open(Battle.Simulation.Robots.OrderBy(r => r.Id).First().Id);
            yield return .3f;
        }
        static CommandDropZone Program => Battle.CodeEditor.Panel.Program;
        static CommandBlockView Palette(BlockKind kind) => Battle.CodeEditor.Panel.Palette.First(p => p.view.BaseKind == kind).view;
        static void ClearProgram() => Battle.CodeEditor.Panel.LoadProgram(Battle.CodeEditor.Panel.FunctionName, new List<CodeBlock>());

        // ---- pointer input, through the same raycasts a mouse uses (the tutorial spotlight included) ----
        static Vector2 ScreenPoint(RectTransform rect, float u = .5f)
        {
            var camera = rect.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            var r = rect.rect;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, u), r.center.y)));
        }
        static string TopHit(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits.Count > 0 ? hits[0].gameObject.name : "-";
        }
        // Press, drag in five moves and drop; returns what was pressed.
        static string Drag(Vector2 from, Vector2 to)
        {
            var system = EventSystem.current;
            var e = new PointerEventData(system) { position = from, pressPosition = from, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); system.RaycastAll(e, hits);
            if (hits.Count == 0) return "-";
            var pressed = hits[0].gameObject; e.pointerPressRaycast = e.pointerCurrentRaycast = hits[0];
            var dragged = ExecuteEvents.GetEventHandler<IDragHandler>(pressed);
            if (!dragged) return pressed.name;
            ExecuteEvents.Execute(pressed, e, ExecuteEvents.pointerDownHandler);
            e.pointerDrag = dragged; e.dragging = true;
            ExecuteEvents.Execute(dragged, e, ExecuteEvents.beginDragHandler);
            for (int i = 1; i <= 5; i++)
            {
                e.delta = (to - from) / 5; e.position = Vector2.Lerp(from, to, i / 5f);
                ExecuteEvents.Execute(dragged, e, ExecuteEvents.dragHandler);
            }
            hits.Clear(); system.RaycastAll(e, hits);
            if (hits.Count > 0) { e.pointerCurrentRaycast = hits[0]; ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, e, ExecuteEvents.dropHandler); }
            ExecuteEvents.Execute(dragged, e, ExecuteEvents.endDragHandler);
            return pressed.name;
        }

        // ---- scenarios ----

        // The three cards and the bar should land one even beat apart from the first screen on (planner: "탁 탁 탁").
        static IEnumerator AlertBeat()
        {
            var alert = Object.FindFirstObjectByType<TutorialSystemAlert>(FindObjectsInactive.Include);
            // Scene start: the attack preview world's ring must not be showing.
            var previewRing = GameObject.Find("AttackPreviewWorld")?.transform.Find("Effect")?.GetComponent<LineRenderer>();
            Check(previewRing && !previewRing.enabled, "씬 시작 때 AttackPreviewWorld의 Effect(사거리 원) 안 보임");
            yield return Wait(() => alert.IsShowing, "A01 경고 표시", 10);
            var cards = Field<IList>(alert, "cards");
            var bar = (RectTransform)alert.GetType().GetField("bar", Private).GetValue(alert);
            var clock = new float[cards.Count + 1]; var real = new double[cards.Count + 1];
            for (int i = 0; i < clock.Length; i++) clock[i] = -1;
            bool missed = Field<float>(alert, "clock") > .1f;
            var session = Battle.Simulation;
            var stalls = new List<(float clock, string text)>(); int frame = -1; bool shot = false;
            while (alert.IsShowing && clock.Any(t => t < 0) && Battle.Simulation == session)
            {
                for (int i = 0; i <= cards.Count; i++)
                {
                    if (clock[i] >= 0) continue;
                    bool landed = i < cards.Count
                        ? ((CanvasGroup)cards[i].GetType().GetField("Item2").GetValue(cards[i])).alpha > 0
                        : bar.localScale.x > 0;
                    if (landed) { clock[i] = Field<float>(alert, "clock"); real[i] = EditorApplication.timeSinceStartup; }
                }
                if (Time.frameCount != frame && Time.unscaledDeltaTime > .15f) stalls.Add((Field<float>(alert, "clock"), $"{Time.unscaledDeltaTime:0.00}s (연출 {Field<float>(alert, "clock"):0.00}s)"));
                frame = Time.frameCount;
                if (clock[1] >= 0 && clock[2] < 0 && !shot) { shot = true; Shot(); }
                yield return null;
            }
            Check(Battle.Simulation == session, "측정 중 세션이 바뀌지 않음");
            // A stall before the first card only delays the start; one while cards are on screen breaks the beat.
            var visible = stalls.Where(x => x.clock >= clock[0] && clock[0] >= 0).Select(x => x.text).ToList();
            Check(visible.Count == 0, "카드가 보이는 동안 0.15초 넘게 멈춘 프레임 " + (visible.Count == 0 ? "없음" : string.Join(", ", visible)) +
                (stalls.Count > visible.Count ? $" (첫 카드 전 멈춤 {string.Join(", ", stalls.Where(x => !visible.Contains(x.text)).Select(x => x.text))}: 시작만 늦어짐)" : ""));
            Check(!missed, "첫 카드부터 관찰" + (missed ? " (경고가 이미 진행 중이었음)" : ""));
            Check(clock.All(t => t >= 0), "카드 3장과 배너가 모두 나옴");
            for (int i = 1; i < clock.Length; i++)
            {
                if (clock[i] < 0 || clock[i - 1] < 0) continue;
                float beat = clock[i] - clock[i - 1], seen = (float)(real[i] - real[i - 1]);
                // A landing is seen on the first frame after it, so each time is late by up to one frame (~0.1 s right after load).
                Check(Mathf.Abs(seen - .5f) < .12f, $"{i}→{i + 1}번째 간격: 연출 {beat:0.00}s, 실제 {seen:0.00}s (목표 0.5s)");
            }
        }

        static IEnumerator BackSkipsAlert()
        {
            var alert = Object.FindFirstObjectByType<TutorialSystemAlert>(FindObjectsInactive.Include);
            yield return Fresh();
            checks.Add($"ℹ 새 세션: {StepId}, 동기화 {Director.EditorSynced}, 로봇 {Battle.Simulation.Robots.Count}");
            yield return Jump(1, "E01");
            checks.Add($"ℹ 점프 후: {StepId}, 로봇 {Battle.Simulation.Robots.Count}");
            Director.EditorFinishTyping(); Director.EditorPrevious();
            yield return .3f;
            Check(StepId == "D09", "E01에서 뒤로가기 → " + StepId + " (D09 기대, D10 경고 건너뜀)");
            Check(!alert.IsShowing, "뒤로가기 후 경고 배너 안 나옴");
            Shot(); yield return .1f;
            Director.EditorFinishTyping(); Director.EditorNext();
            yield return .3f;
            Check(StepId == "E01" && !alert.IsShowing, "다시 앞으로 → " + StepId + ", 경고 " + (alert.IsShowing ? "나옴" : "안 나옴"));
        }

        static IEnumerator ExplanationBlocksDrag()
        {
            yield return Fresh();
            yield return Jump(1, "C09");
            yield return OpenEditor();
            ClearProgram();
            yield return .2f;
            foreach (var id in new[] { "C09", "C10" })
            {
                for (int tries = 0; StepId != id && tries < 8; tries++) { Director.EditorFinishTyping(); Director.EditorNext(); yield return .2f; }
                if (StepId != id) { Fail($"{id}까지 넘기지 못함 (현재 {StepId}, 로봇 {Battle.Simulation.Robots.Count})"); yield break; }
                int before = Program.AllBlocks().Count();
                var pressed = Drag(ScreenPoint(Palette(BlockKind.NearestEnemy).Rect), ScreenPoint(Program.Rect, .7f));
                yield return .2f;
                Check(Program.AllBlocks().Count() == before, $"{id}: 팔레트 드래그 막힘 (누른 곳: {pressed})");
            }
            Shot();
        }

        // The planner's screenshot: enemy = value in the start box, get_nearest_enemy() left loose, then connected by drag.
        static IEnumerator PreplacedBlocksProgress()
        {
            yield return OpenEditor();
            yield return Jump(1, "C08");
            ClearProgram();
            Program.PlaceFree(Palette(BlockKind.DeclareVariable), Vector2.zero);
            Program.PlaceFree(Palette(BlockKind.NearestEnemy), new Vector2(300, -260));
            yield return .3f;
            Check(Program.StartChain().Count == 1 && Program.LooseRoots().Count() == 1, "사진과 같은 상태: 시작 위치에 enemy = value, get_nearest_enemy() 따로");
            Check(StepId == "C08", "연결 전에는 C08에 머묾 (" + StepId + ")");
            var declare = Program.StartChain()[0];
            var pressed = Drag(ScreenPoint(Program.LooseRoots().First().Rect), ScreenPoint(declare.Arguments[0].Rect, .2f));
            yield return Wait(() => StepId != "C08", "C08 완료", 3);
            Check(StepId == "C09", $"드래그로 연결하면 다음 대사로 ({pressed} → {StepId})");
            Shot();
        }

        // 1-3 B01 hands the first robot the two given lines; B02 explains the second; C01 asks for the if under them.
        static IEnumerator DistanceLines()
        {
            DevTest.JumpTutorial(3, 0);
            yield return Wait(() => Battle.Simulation.WaveIndex == 2 && Director.EditorSynced && StepId == "A01", "1-3 시작", 15);
            yield return OpenEditor();
            yield return Jump(3, "B01");
            yield return .3f;
            string code = string.Join(" / ", Program.StartChain().Select(v => v.Kind + (v.DraftValue != null ? " " + v.DraftValue : "")));
            Check(Program.StartChain().Count == 2 && Program.StartChain()[1].DraftValue == "enemy_distance", "B01: 기초 코드 두 줄 (" + code + ")");
            foreach (var id in new[] { "B01", "B02" })
            {
                var step = Director.EditorStep;
                Check(step.stepId == id, id + ": \"" + step.texts[0].text + "\"");
                foreach (var key in step.texts[0].focusKeys ?? Array.Empty<string>())
                    Check(Director.EditorBridge.TryGetArea(key, out _), $"{id}: 강조 대상 {key} 화면에 있음");
                if (id == "B02") { Shot(); yield return .1f; }
                Director.EditorFinishTyping(); Director.EditorNext(); yield return .3f;
            }
            yield return NextUntil("C01");
            Check(Director.EditorStep.completionSignal == TutorialSignals.SlotFilled && Director.EditorStep.expectedCode == "Editor.Code.Line.3:If", "C01: 셋째 줄에 if 놓기 단계");
        }

        static void Load(string code) => Battle.CodeEditor.Panel.LoadProgram(Battle.CodeEditor.Panel.FunctionName, TutorialAnswers.Parse(code));
        static IEnumerator NextUntil(string id)
        {
            for (int tries = 0; StepId != id && tries < 12; tries++) { Director.EditorFinishTyping(); Director.EditorNext(); yield return .15f; }
            if (StepId != id) throw new InvalidOperationException($"{id}까지 넘기지 못함 (현재 {StepId})");
        }

        static IEnumerator VariableThenName()
        {
            yield return Fresh();
            yield return Jump(1, "C04");
            yield return OpenEditor();
            yield return Jump(1, "D03");
            Check(Director.EditorStep.completionSignal == TutorialSignals.SlotFilled, "D03: 변수 블록을 놓는 단계 — \"" + Director.EditorStep.texts[0].text + "\"");
            Load("enemy = get_nearest_enemy()\nslash(variable)"); // slash() with a variable block not named yet
            yield return Wait(() => StepId == "D04", "D03 → D04", 3);
            Check(StepId == "D04", "변수 블록을 놓으면 다음 단계: " + StepId + " — \"" + Director.EditorStep.texts[0].text + "\"");
            Shot(); yield return .1f;
            Load("enemy = get_nearest_enemy()\nslash(enemy)");
            yield return Wait(() => StepId == "D05", "D04 → D05", 3);
            Check(StepId == "D05", "enemy를 입력하면 다음 대사: " + StepId);
        }

        static IEnumerator PreviewHitHold()
        {
            yield return Jump(1, "C04");
            yield return OpenEditor();
            yield return Jump(1, "D08");
            double shown = EditorApplication.timeSinceStartup;
            Director.ReportSuccess(TutorialKeys.Preview, TutorialSignals.PreviewHit); // the preview hits at once
            yield return .8f;
            Check(StepId == "D08", "바로 적중해도 0.8초 뒤 아직 D08 (" + StepId + ")");
            yield return Wait(() => StepId != "D08", "D08 넘어감", 10);
            double held = EditorApplication.timeSinceStartup - shown;
            Check(StepId == "D09" && held >= 1.5, $"D09로 넘어감, D08이 {held:0.0}초 보임 (타이핑 + 1.5초)");
        }

        static IEnumerator RewardFocusClear()
        {
            if (Battle.CodeEditor.IsOpen) Battle.CodeEditor.Close();
            yield return Fresh(); // from the editor steps the start button is not back yet; a new session starts clean
            yield return Jump(1, "E03");
            Battle.StartBattle(); Time.timeScale = 3;
            // Lines on the way (E05 on a leak, E06, E07) wait for "next" like a player reading them.
            yield return Wait(() =>
            {
                if (StepId == "E08") return true;
                var step = Director.EditorStep;
                if (step != null && step.kind == TutorialStepKind.Dialogue) { Director.EditorFinishTyping(); Director.EditorNext(); }
                return false;
            }, "웨이브 끝나고 E08", 120);
            Time.timeScale = 1;
            yield return 2.5f; // the result banner plays, then the reward card fades in
            Check(Director.EditorBridge.TryGetArea(TutorialKeys.RewardPanel, out var focus), "보상 집중 영역 있음");
            var view = Object.FindFirstObjectByType<TutorialDialogueView>();
            var corners = new Vector3[4]; view.Panel.GetWorldCorners(corners);
            var camera = view.Panel.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            float dialogueTop = RectTransformUtility.WorldToScreenPoint(camera, corners[1]).y;
            var hud = Object.FindFirstObjectByType<DefensePlayerHUD>();
            var rect = Field<RectTransform>(hud, "rewardFocus"); rect.GetWorldCorners(corners);
            float focusBottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]).y;
            Check(focusBottom > dialogueTop, $"집중 영역 아래끝 {focusBottom:0}px가 대화창 위끝 {dialogueTop:0}px보다 위 (대화창 {(view.AtTop ? "위" : "아래")})");
            Shot(); yield return .1f;
        }

        // 1-2 is free play: after A01~A03 the guide hides and the game is the player's until the wave ends.
        static IEnumerator OneTwoIntro()
        {
            DevTest.JumpTutorial(2, 0);
            yield return Wait(() => Battle.Simulation.WaveIndex == 1 && Director.EditorSynced && StepId == "A01", "1-2 시작", 15);
            yield return NextUntil("B01");
            yield return .5f;
            var view = Object.FindFirstObjectByType<TutorialDialogueView>(FindObjectsInactive.Include);
            Check(!view.IsVisible && !Battle.TutorialBlocksInput, "B01: 대사 숨김, 조작 가능");
            Check(Battle.Simulation.RobotAvailable(RobotRole.Warrior) == 1, "새 전사 1대 지급 (" + Battle.Simulation.RobotAvailable(RobotRole.Warrior) + ")");
            Check(Field<UnityEngine.UI.Button>(Battle.CodeEditor, "hintButton").gameObject.activeSelf, "힌트 버튼 보임");
            Shot(); yield return .1f;
        }

        static IEnumerator PropsAndBase()
        {
            DevTest.PlayDefense(1, 1, false);
            yield return Wait(() => DevTest.Ready && !Battle.Tutorial.IsActive && Battle.Simulation.Robots.Count == 0 && Battle.Simulation.Time == 0, "새 1-1", 10);
            var sim = Battle.Simulation;
            var spec = Battle.RobotDefinition(Enumerable.Range(0, sim.Setup.ActionProfiles.Length).First(i => Battle.DefinitionRole(i) == RobotRole.Warrior)).stats;
            var map = Object.FindFirstObjectByType<CodingGame.Art.MapPainter.ModularMapRoot>();
            var props = map.transform.Cast<Transform>().Where(g => g.name.Contains("Art")).SelectMany(g => g.Cast<Transform>()).ToList();
            int blocked = 0, inside = 0;
            foreach (var prop in props)
            {
                var p = new System.Numerics.Vector2(prop.position.x, prop.position.z);
                if (p.X < sim.Setup.Min.X || p.X > sim.Setup.Max.X || p.Y < sim.Setup.Min.Y || p.Y > sim.Setup.Max.Y) continue;
                inside++;
                if ((sim.PlacementError(spec, p) ?? "").Contains("장애물")) blocked++;
            }
            Check(inside > 0 && blocked == inside, $"설치 영역 안 소품 {inside}개 중 {blocked}개 위에 설치 막힘");
            var core = Field<Transform>(Battle, "core");
            var model = Field<Transform>(core.GetComponent<DefenseCoreGlitch>(), "model");
            var bar = core.GetComponentInChildren<DefenseCoreHealthBar>();
            yield return .2f;
            var barCorners = new Vector3[4]; ((RectTransform)bar.transform).GetWorldCorners(barCorners);
            var cam = Field<Camera>(bar, "battleCamera");
            var points = barCorners.Select(c => cam.WorldToScreenPoint(c)).ToArray();
            bool onScreen = points.All(p => cam.pixelRect.Contains(p));
            Check(onScreen, $"기지 체력바가 화면 안 (x {points.Min(p => p.x):0}~{points.Max(p => p.x):0} / 화면 폭 {cam.pixelWidth})");
            DevTest.PlaceTeachingRobot(Battle, RobotRole.Warrior);
            Battle.SelectRobot(sim.Robots[0].Id);
            yield return .4f;
            var ring = Field<DefenseRangeRing>(Battle, "rangeRing");
            var line = Field<LineRenderer>(ring, "line");
            Check(line.enabled && line.startColor.b > .9f && line.startColor.r < .5f, "선택한 로봇 사거리(평소): 파란(청록) 테두리 " + line.startColor);
            Shot(); yield return .1f;
        }

        static IEnumerator StageClickDelay()
        {
            GameFlow.LoadScene(GameFlow.StageScene);
            yield return Wait(() => Object.FindFirstObjectByType<CodingGame.StageMap.StageMapController>() != null, "스테이지 맵", 15);
            yield return 1f;
            var controller = Object.FindFirstObjectByType<CodingGame.StageMap.StageMapController>();
            var node = Object.FindObjectsByType<CodingGame.StageMap.StageNode>(FindObjectsSortMode.None).FirstOrDefault(n => n.State == CodingGame.StageMap.StageState.Available);
            if (!node) { Fail("들어갈 수 있는 스테이지가 없음"); yield break; }
            double clicked = EditorApplication.timeSinceStartup;
            controller.GetType().GetMethod("Enter", Private).Invoke(controller, new object[] { node });
            // After the camera lands, during the fade: no second "여기를 클릭!".
            yield return CodingGame.StageMap.StageCameraController.Duration + .05f;
            Check(Field<bool>(controller, "entering"), "스테이지를 고른 뒤에는 '여기를 클릭!' 안내를 그리지 않음");
            Shot(); yield return .05f;
            yield return Wait(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameFlow.DefenseScene, "Defense 씬", 15);
            double took = EditorApplication.timeSinceStartup - clicked;
            Check(took >= CodingGame.StageMap.StageCameraController.Duration + .3f, $"클릭 후 {took:0.00}초 뒤 전환 (카메라 이동 {CodingGame.StageMap.StageCameraController.Duration}초 + 페이드)");
        }

        // Screen rectangle of a world box (8 corners) or of a world-space RectTransform.
        static Rect ScreenRect(Camera camera, IEnumerable<Vector3> points)
        {
            var s = points.Select(p => (Vector2)camera.WorldToScreenPoint(p)).ToArray();
            var min = s.Aggregate(Vector2.Min); var max = s.Aggregate(Vector2.Max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        static IEnumerable<Vector3> BoxCorners(Bounds b) => Enumerable.Range(0, 8).Select(i => b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));

        static IEnumerator BaseFocusAndRangeHighlight()
        {
            GameFlow.LoadScene(GameFlow.DefenseScene); // back from the stage map
            yield return Wait(() => Battle && Battle.Simulation != null && Director.EditorSynced, "Defense", 20);
            yield return Fresh();
            yield return Jump(1, "A04b");
            yield return 3f; // after the map's build-in animation
            var core = Field<Transform>(Battle, "core");
            var model = Field<Transform>(core.GetComponent<DefenseCoreGlitch>(), "model");
            var bar = core.GetComponentInChildren<DefenseCoreHealthBar>();
            var cam = Field<Camera>(bar, "battleCamera");
            var parts = model.GetComponentsInChildren<Renderer>(); var box = parts[0].bounds; foreach (var r in parts) box.Encapsulate(r.bounds);
            var modelRect = ScreenRect(cam, BoxCorners(box));
            var corners = new Vector3[4]; ((RectTransform)bar.transform).GetWorldCorners(corners);
            var barRect = ScreenRect(cam, corners);
            float pad = Director.EditorStep.highlightPadding + 4; // the focus box: padding plus its outline
            var focusRect = Rect.MinMaxRect(barRect.xMin - pad, barRect.yMin - pad, barRect.xMax + pad, barRect.yMax + pad);
            Check((bar.transform.localPosition - new Vector3(-1.573f, 5.912f, 0)).sqrMagnitude < 1e-4f, "기지 체력바가 씬 위치 그대로 " + bar.transform.localPosition.ToString("0.000"));
            // The position is the planner's; against the model's box (larger than the tower itself) this is only reported.
            checks.Add($"ℹ 기지 체력바 {barRect.yMin:0}~{barRect.yMax:0}px, 집중 테두리 아래끝 {focusRect.yMin:0}px, 기지 모델 상자 위끝 {modelRect.yMax:0}px — {StepId}");
            Check(focusRect.xMax <= cam.pixelRect.xMax && focusRect.yMax <= cam.pixelRect.yMax, "집중 테두리가 화면 안");
            Shot(); yield return .1f;
            yield return Jump(1, "B04"); // "이 원" — the range highlight
            yield return .4f;
            var ring = Field<DefenseRangeRing>(Battle, "rangeRing");
            var line = Field<LineRenderer>(ring, "line");
            Check(line.enabled && line.startColor.r > .9f && line.startColor.b < .7f, "튜토리얼 사거리 강조: 빨간 깜빡임 " + line.startColor);
        }

        static Animator AnimatorOf(object view) => ((Component)view).GetComponentInChildren<Animator>();
        static IEnumerator Animations()
        {
            shotTaken = false;
            DevTest.PlayDefense(1, 1, false);
            yield return Wait(() => DevTest.Ready && !Battle.Tutorial.IsActive && Battle.Simulation.Robots.Count == 0 && Battle.Simulation.Time == 0, "새 1-1", 10);
            var sim = Battle.Simulation;
            sim.GrantAll();
            DevTest.PlaceTeachingRobot(Battle, RobotRole.Warrior);
            DevTest.PlaceTeachingRobot(Battle, RobotRole.Shooter);
            foreach (var bot in sim.Robots)
                sim.ApplyProgram(bot.Id, "robot_" + bot.Id, bot.Spec.role == RobotRole.Warrior
                    ? TutorialAnswers.Parse("enemy = get_nearest_enemy()\nslash(enemy)")
                    : new[] { new CodeBlock(BlockKind.Shot, "0", new CodeBlock(BlockKind.NearestEnemy)) });
            yield return .5f;
            var robots = Field<IDictionary>(Battle, "robotViews");
            var camera = Field<Camera>(Battle, "battleCamera").transform.position;
            foreach (DictionaryEntry pair in robots)
            {
                var head = ((Component)pair.Value).transform.Find("Head"); var toCamera = camera - head.position; toCamera.y = 0;
                Check(Vector3.Dot(head.forward, toCamera.normalized) > .95f, $"{sim.Robots.First(r => r.Id == (int)pair.Key).Spec.role}: 설치하면 카메라 쪽을 봄");
            }
            var roleOf = sim.Robots.ToDictionary(r => r.Id, r => r.Spec.role);
            foreach (DictionaryEntry pair in robots)
            {
                var animator = AnimatorOf(pair.Value);
                Check(animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && animator.speed > .9f, $"{roleOf[(int)pair.Key]}: 준비 중 Idle 재생 ({(animator ? animator.runtimeAnimatorController.name : "Animator 없음")})");
            }
            DevTest.StartWave(); Time.timeScale = 2;
            var enemies = Field<IDictionary>(Battle, "enemyViews");
            yield return Wait(() => enemies.Count > 0, "첫 몬스터", 30);
            yield return .5f;
            var monster = enemies.Values.Cast<object>().First();
            var walk = AnimatorOf(monster);
            Check(walk && walk.GetCurrentAnimatorStateInfo(0).IsName("Walk") && walk.speed > .9f, "몬스터: Walk 재생, 속도 " + (walk ? walk.speed.ToString("0.0") : "-"));
            var monsterTransform = ((Component)monster).transform; var before = monsterTransform.position;
            yield return .3f;
            var moved = monsterTransform.position - before; moved.y = 0;
            Check(moved.sqrMagnitude > 1e-4f && Vector3.Dot(monsterTransform.forward, moved.normalized) > .9f, $"몬스터가 걷는 방향을 봄 (dot {(moved.sqrMagnitude > 0 ? Vector3.Dot(monsterTransform.forward, moved.normalized) : 0):0.00})");
            var attacked = new HashSet<RobotRole>(); var attackSpeeds = new List<string>();
            yield return Wait(() =>
            {
                foreach (DictionaryEntry pair in robots)
                {
                    var animator = AnimatorOf(pair.Value);
                    if (animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Attack") && attacked.Add(roleOf[(int)pair.Key]))
                    {
                        var bot = sim.Robots.First(r => r.Id == (int)pair.Key);
                        float lasts = animator.GetCurrentAnimatorStateInfo(0).length; // already at the sped-up rate
                        attackSpeeds.Add($"{bot.Spec.role}: 공격 간격 {bot.Interval:0.00}s, 공격 동작 {lasts:0.00}s (배속 {animator.speed:0.00})");
                        Check(lasts <= bot.Interval + .01f, $"{bot.Spec.role}: 공격 동작이 다음 공격 전에 끝남");
                    }
                }
                if (attacked.Count == 1 && !shotTaken) { shotTaken = true; Shot(); }
                return attacked.Count == 2;
            }, "두 로봇의 공격 동작", 60);
            Check(attacked.Contains(RobotRole.Warrior), "전사형(AttackRobot): 공격할 때 Attack 재생");
            Check(attacked.Contains(RobotRole.Shooter), "사격형(ShotRobot): 공격할 때 Attack 재생");
            foreach (var line in attackSpeeds) checks.Add("ℹ " + line);
            Battle.TogglePause();
            yield return .3f;
            var live = enemies.Values.Cast<object>().Select(AnimatorOf).FirstOrDefault(a => a);
            Check(live && live.speed == 0, "일시정지하면 몬스터 애니메이션도 멈춤");
            Battle.TogglePause(); Time.timeScale = 1;
        }
        static bool shotTaken;

        static IList Slashes() { var visuals = Field<object>(Battle, "hitEffects"); return Field<IList>(visuals, "slashes"); }
        static IEnumerator Slash()
        {
            DevTest.PlayDefense(1, 1, false);
            yield return Wait(() => DevTest.Ready && !Battle.Tutorial.IsActive && Battle.Simulation.Robots.Count == 0, "튜토리얼 없는 1-1", 10);
            DevTest.PlaceTeachingRobot(Battle, RobotRole.Warrior);
            var bot = Battle.Simulation.Robots[0];
            Battle.Simulation.ApplyProgram(bot.Id, "robot_" + bot.Id, TutorialAnswers.Parse("enemy = get_nearest_enemy()\nslash(enemy)"));
            DevTest.StartWave(); Time.timeScale = 3;
            // Real time from just before the first enemy comes in range, so the frames between effect and strike are seen.
            yield return Wait(() => Battle.Simulation.Enemies.Any(e => System.Numerics.Vector2.Distance(e.Position, bot.Position) < bot.Range + 1), "적 접근", 60);
            Time.timeScale = 1;
            yield return Wait(() => Slashes().Count > 0, "첫 베기", 10);
            var effect = (ParticleSystem)Slashes()[0].GetType().GetField("Item1").GetValue(Slashes()[0]);
            var enemy = Battle.Simulation.Enemies.Where(e => e.Active).OrderBy(e => System.Numerics.Vector2.Distance(e.Position, bot.Position)).FirstOrDefault();
            Check(enemy != null && enemy.Health >= enemy.Spec.health, $"베기 이펙트가 나올 때는 아직 피해 없음 (체력 {enemy?.Health:0}/{enemy?.Spec.health:0})");
            Check(effect.name.StartsWith("SwordSlashFlat"), "이펙트: " + effect.name + " (SwordSlash_3을 가로로 눕힌 변형)");
            // The arc (the "Slash" particle) should sweep around the robot's lower body.
            var arcSystem = effect.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "Slash");
            var views = Field<IDictionary>(Battle, "robotViews");
            var robotModel = ((Component)views.Values.Cast<object>().First()).transform.Find("Head/Model").GetComponentInChildren<Renderer>().bounds;
            float arcY = float.NaN;
            for (int i = 0; i < 20 && float.IsNaN(arcY); i++)
            {
                yield return null;
                var live = new ParticleSystem.Particle[arcSystem.particleCount]; arcSystem.GetParticles(live);
                if (live.Length > 0) arcY = arcSystem.transform.TransformPoint(live[0].position).y;
            }
            float middle = (robotModel.min.y + robotModel.max.y) / 2;
            Check(arcY >= robotModel.min.y && arcY <= middle, $"베기 높이 {arcY:0.00}: 로봇 하단부 ({robotModel.min.y:0.00}~{middle:0.00})");
            float expectedScale = bot.Spec.range / 2.3f, scale = Mathf.Abs(effect.transform.localScale.x); // mirrored: negative x
            Check(Mathf.Abs(scale - expectedScale) < expectedScale * .05f, $"크기 {scale:0.00} (사거리 {bot.Spec.range} / 2.3 ≈ {expectedScale:0.00}: 궤적이 사거리 끝까지)");
            // The arc's ring sits 1.3 x scale ahead of the effect's origin; it should be centred on the robot.
            var ring = effect.transform.position + Vector3.ProjectOnPlane(effect.transform.forward, Vector3.up).normalized * (1.3f * scale);
            float offCentre = new Vector2(ring.x - bot.Position.X, ring.z - bot.Position.Y).magnitude;
            Check(offCentre < .1f, $"베기 궤적의 중심이 로봇 위치 (어긋남 {offCentre:0.00})");
            if (enemy != null)
            {
                var toEnemy = new Vector3(enemy.Position.X - bot.Position.X, 0, enemy.Position.Y - bot.Position.Y).normalized;
                // The effect is turned 49° left so its arc's average over its life (49° right of its forward) is on the target.
                var forward = Quaternion.Euler(0, 49, 0) * Vector3.ProjectOnPlane(effect.transform.forward, Vector3.up).normalized;
                Check(Vector3.Dot(forward, toEnemy) > .95f, $"베기 궤적의 중심이 적 방향 (dot {Vector3.Dot(forward, toEnemy):0.00})");
            }
            yield return .25f;
            Check(enemy != null && enemy.Health < enemy.Spec.health, $"검이 정면을 지나면 피해 (체력 {enemy?.Health:0}/{enemy?.Spec.health:0})");
            Shot();
        }

        static string Look(Transform core)
        {
            // The visible base: the map's model when there is one (the core's own shapes are hidden then).
            var model = Field<Transform>(core.GetComponent<DefenseCoreGlitch>(), "model");
            var material = (model ? model : core).GetComponentsInChildren<Renderer>().First(r => r.enabled).sharedMaterial;
            if (!material.shader.name.Contains("SphereGlitch")) return "원래";
            return material.GetFloat("_UseAlphaClipping") > .5f ? "사망" : "피격";
        }
        static IEnumerator CoreGlitch()
        {
            var core = Field<Transform>(Battle, "core");
            Check(core.GetComponent<DefenseCoreGlitch>(), "코어에 DefenseCoreGlitch 연결");
            var model = Field<Transform>(core.GetComponent<DefenseCoreGlitch>(), "model");
            checks.Add("ℹ 글리치 대상: " + (model ? model.name + " (맵 기지 모델)" : "코어 기본 도형"));
            if (Battle.Simulation.Phase == BattlePhase.Running) yield return Wait(() => Battle.Simulation.Phase != BattlePhase.Running, "웨이브 끝", 90);
            if (Battle.Simulation.Phase == BattlePhase.Reward) Battle.ClaimRewards();
            foreach (var bot in Battle.Simulation.Robots) Battle.Simulation.ApplyProgram(bot.Id, "robot_" + bot.Id, new List<CodeBlock>());
            int health = Battle.Simulation.BaseHealth;
            DevTest.StartWave(); Time.timeScale = 3;
            yield return Wait(() => Battle.Simulation.BaseHealth < health, "적이 코어에 닿음", 90);
            yield return null;
            Check(Look(core) == "피격", "코어 피격 직후: " + Look(core));
            Time.timeScale = 1; Shot();
            yield return 1f;
            Check(Look(core) == "원래" || Battle.Simulation.BaseHealth < health - 1, "0.6초 뒤 원래 모습: " + Look(core));
            typeof(DefenseSimulation).GetProperty("BaseHealth").SetValue(Battle.Simulation, 1);
            Time.timeScale = 3;
            // Next waves until the core falls (this wave's enemies may all be through already).
            yield return Wait(() =>
            {
                if (Battle.Simulation.Phase == BattlePhase.Reward) { Battle.ClaimRewards(); Battle.StartBattle(); }
                return Battle.Simulation.Phase == BattlePhase.Defeat;
            }, "코어 파괴", 120);
            Time.timeScale = 1;
            yield return .3f;
            Check(Look(core) == "사망", "코어 파괴 후: " + Look(core));
            Shot();
        }
    }
}
