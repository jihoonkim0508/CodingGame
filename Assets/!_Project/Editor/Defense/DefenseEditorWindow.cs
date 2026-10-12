using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // Thin UI over DevTest: the same actions are available to AI test scripts without this window.
    public sealed class DefenseEditorWindow : EditorWindow
    {
        const string Root = "Assets/!_Project/Editor/Defense/";
        static readonly string[] Tabs = { "플레이", "밸런스", "테스트 옵션" };
        [SerializeField] DefenseCampaignDefinition campaign;
        [SerializeField] int testStage = 1, testWave = 1, coins = 100, campaignStages = 5;
        [SerializeField] bool previousStageRewards = true, tutorial;
        [SerializeField] int jumpWave = 1, jumpSection;
        [SerializeField] DefenseTestOptions options = new DefenseTestOptions();
        DefenseCampaignDefinition playContent;
        readonly List<Object> copies = new List<Object>();
        ScrollView content;
        Label status, feedback, live;
        string tab = Tabs[0];
        bool hadBattle;
        double nextRefresh;

        [MenuItem("Tools/Develop/개발 에디터")]
        public static void Open() => GetWindow<DefenseEditorWindow>("개발 에디터");
        [MenuItem("Tools/Develop/Defense 테스트 Play")]
        static void PlayDefenseMenu() => DevTest.PlayDefense();

        void OnEnable()
        {
            EditorApplication.update += UpdateEditor;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        void OnDisable()
        {
            EditorApplication.update -= UpdateEditor;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (!EditorApplication.isPlaying) DisposeCopies();
        }
        public void CreateGUI()
        {
            if (!campaign) campaign = AssetDatabase.FindAssets("t:DefenseCampaignDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            minSize = new Vector2(520, 420);
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "DefenseEditorWindow.uxml").CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "DefenseEditorWindow.uss"));
            status = rootVisualElement.Q<Label>("sessionStatus");
            feedback = rootVisualElement.Q<Label>("feedback");
            content = rootVisualElement.Q<ScrollView>("content");
            var toolbar = rootVisualElement.Q("toolbar");
            tab = SessionState.GetString("CodingGame.EditorTab", tab);
            foreach (var name in Tabs)
            {
                var button = new Button(() => { tab = name; SessionState.SetString("CodingGame.EditorTab", tab); CreateGUI(); }) { text = name };
                if (name == tab) button.AddToClassList("selected");
                toolbar.Add(button);
            }
            Build();
        }

        void Act(Action action)
        {
            try { feedback.text = ""; action(); if (string.IsNullOrEmpty(feedback.text)) feedback.text = "완료"; }
            catch (Exception e) { feedback.text = e.Message; }
        }
        void Heading(string text) { var label = new Label(text); label.AddToClassList("section"); content.Add(label); }
        void Note(string text) { var label = new Label(text); label.AddToClassList("readout"); content.Add(label); }
        void Buttons(params (string name, Action action)[] actions)
        {
            var row = new VisualElement(); row.AddToClassList("row");
            foreach (var (name, action) in actions) row.Add(new Button(() => Act(action)) { text = name });
            content.Add(row);
        }
        void Integer(string label, int value, Action<int> change) { var f = new IntegerField(label) { value = value }; f.RegisterValueChangedCallback(e => change(e.newValue)); content.Add(f); }
        void Check(string label, bool value, Action<bool> change) { var t = new Toggle(label) { value = value }; t.RegisterValueChangedCallback(e => change(e.newValue)); content.Add(t); }
        void Property(Object target, params string[] names)
        {
            if (!target) return;
            var so = new SerializedObject(target); var parent = new VisualElement();
            foreach (var name in names) { var p = so.FindProperty(name); if (p != null) parent.Add(new PropertyField(p)); }
            parent.Bind(so); content.Add(parent);
        }

        void Build()
        {
            if (content == null) return;
            content.Clear(); live = null;
            switch (tab)
            {
                case "밸런스": Balance(); break;
                case "테스트 옵션": Options(); break;
                default: Play(); break;
            }
            Refresh();
        }

        DefenseTestOptions StartOptions()
        {
            var o = options.Copy(); o.tutorial = o.tutorialInput = tutorial;
            return o;
        }
        void Play()
        {
            Heading("시작 조건");
            Integer("스테이지", testStage, v => testStage = v);
            Integer("웨이브", testWave, v => testWave = v);
            Check("이전 스테이지까지의 보상 지급", previousStageRewards, v => previousStageRewards = v);
            Check("튜토리얼 실행", tutorial, v => tutorial = v);
            Buttons((EditorApplication.isPlaying ? "이 조건으로 재시작" : "▶ Defense 테스트 Play",
                    () => DevTest.PlayDefense(testStage, testWave, previousStageRewards, StartOptions(), EditorApplication.isPlaying ? Editable : null)));
            Jumps();
            if (!EditorApplication.isPlaying) { Note("일반 Play 버튼은 항상 Title 씬에서 시작합니다. Defense 테스트 Play는 메뉴 Tools > Develop에도 있습니다."); return; }
            Buttons(("■ Play 종료", DevTest.Stop));
            if (!DevTest.Battle) { Note("현재 씬에 전투가 없습니다. 위 버튼으로 Defense 테스트를 시작하면 이 씬에서 바로 전환합니다."); return; }

            Heading("전투");
            Buttons(("전투 시작", DevTest.StartWave), ("일시정지 / 재개", () => DevTest.Battle.TogglePause()), ("한 틱", () => DevTest.Battle.EditorStep()),
                ("보상 / 다음 단계", DevTest.Continue));
            var speed = new VisualElement(); speed.AddToClassList("row"); speed.Add(new Label("게임 속도"));
            foreach (float s in new[] { .5f, 1, 2, 4, 8 }) speed.Add(new Button(() => DevTest.Speed(s)) { text = "x" + s });
            content.Add(speed);

            Heading("자동 진행");
            Buttons(("자동 배치", DevTest.AutoDeploy), ("웨이브 끝까지 진행", () => DevTest.RunWave()));
            Integer("캠페인 자동 진행 마지막 스테이지", campaignStages, v => campaignStages = v);
            Buttons(("1-1부터 캠페인 자동 진행", () => feedback.text = DevTest.RunCampaign(campaignStages)),
                ("리포트 저장", () => feedback.text = DevTest.SaveReport("수동 저장") + " 저장 완료"));

            Heading("치트");
            Integer("코인", coins, v => coins = v);
            Buttons(("코인 설정", () => DevTest.Coins(coins)), ("전체 지급 (로봇 +10, 블록 +50)", DevTest.GrantAll),
                ("기지 무적 ON/OFF", () => { options.baseDamage = !options.baseDamage; DevTest.SetOptions(options); feedback.text = "기지 피해 " + (options.baseDamage ? "ON" : "OFF"); }),
                ("게임 내 개발 패널", () => DevTest.Battle.ToggleDeveloper()));

            if (DevTest.Battle.Tutorial)
            {
                Heading("튜토리얼");
                Buttons(("이전 대사", () => DevTest.Battle.Tutorial.EditorPrevious()), ("다음 대사", () => DevTest.Battle.Tutorial.EditorNext()),
                    ("튜토리얼 종료", () => DevTest.Battle.Tutorial.EditorStop()));
            }

            Heading("현재 상태");
            live = new Label(); live.AddToClassList("readout"); content.Add(live);
        }

        void Jumps()
        {
            Heading("바로가기");
            Buttons(("Title", DevTest.PlayTitle), ("Stage 맵", DevTest.PlayStageMap), ("저장 데이터 초기화", () =>
            {
                if (!EditorUtility.DisplayDialog("저장 데이터 초기화", "클리어한 스테이지 등 저장된 정보를 모두 지우고 처음 설치한 상태로 되돌립니다.", "초기화", "취소")) return;
                DevTest.ResetSaveData(); feedback.text = "저장 데이터를 초기화했습니다.";
            }));
            var row = new VisualElement(); row.AddToClassList("row");
            var waves = new PopupField<int>("Stage 1 튜토리얼", Enumerable.Range(1, 5).ToList(), Mathf.Clamp(jumpWave, 1, 5), w => "1-" + w, w => "1-" + w);
            var titles = DevTest.TutorialSections(jumpWave).Select((t, i) => $"{i + 1}. {t}").ToList();
            if (titles.Count == 0) titles.Add("(구간 없음)");
            var sections = new PopupField<string>(titles, Mathf.Clamp(jumpSection, 0, titles.Count - 1));
            waves.RegisterValueChangedCallback(e => { jumpWave = e.newValue; jumpSection = 0; Build(); });
            sections.RegisterValueChangedCallback(e => jumpSection = titles.IndexOf(e.newValue));
            row.Add(waves); row.Add(sections);
            row.Add(new Button(() => Act(() => DevTest.JumpTutorial(jumpWave, jumpSection))) { text = "이 구간으로" });
            content.Add(row);
        }

        // Play Mode edits go to copies so a test never changes the asset until "원본에 저장".
        DefenseCampaignDefinition Editable
        {
            get
            {
                if (!EditorApplication.isPlaying || !campaign) return campaign;
                if (!playContent)
                {
                    campaign.ValidateContent();
                    playContent = Copy(campaign);
                    playContent.robots = campaign.robots.Select(Copy).ToArray();
                    playContent.enemies = campaign.enemies.Select(Copy).ToArray();
                    RemapEnemies(playContent, campaign.enemies, playContent.enemies);
                }
                return playContent;
            }
        }
        T Copy<T>(T source) where T : Object { var c = Instantiate(source); c.name = source.name; copies.Add(c); return c; }
        // Points every spawn group at the matching enemy in a parallel definition list.
        static void RemapEnemies(DefenseCampaignDefinition content, EnemyDefinition[] from, EnemyDefinition[] to)
        {
            foreach (var stage in content.stages) foreach (var wave in stage.waves) foreach (var group in wave.groups)
                group.enemy = to[Array.IndexOf(from, group.enemy)];
        }
        void Balance()
        {
            var fieldRow = new ObjectField("캠페인 asset") { objectType = typeof(DefenseCampaignDefinition), value = campaign, allowSceneObjects = false };
            fieldRow.RegisterValueChangedCallback(e => { DisposeCopies(); campaign = e.newValue as DefenseCampaignDefinition; Build(); });
            content.Add(fieldRow);
            var c = Editable; if (!c) { Note("캠페인 asset을 선택하세요."); return; }
            if (EditorApplication.isPlaying)
            {
                Note("Play 중에는 복사본을 편집합니다. '실행 중 적용'은 현재 전투에만, '원본에 저장'은 asset에 저장합니다. 저장하지 않은 값은 Play 종료 시 사라집니다.");
                Buttons(("실행 중 적용", ApplyBalance), ("원본에 저장", SaveToAsset), ("편집값 되돌리기", () => { DisposeCopies(); Build(); }));
            }
            else Buttons(("저장", SaveToAsset), ("설정 검사", () => { c.ValidateContent(); feedback.text = "설정 검사 통과"; }));

            Heading("공통");
            Property(c, "baseHealth", "robotLimit", "rules", "permissions", "progression");
            Heading("로봇");
            foreach (var r in c.robots) StatsFoldout(r, r.displayName);
            Heading("적");
            foreach (var e in c.enemies) StatsFoldout(e, e.displayName);
            Heading("스테이지·웨이브");
            if (EditorApplication.isPlaying) Property(c, "stages"); else Property(c, "stages", "robots", "enemies");
            var plan = new Foldout { text = "최종 스폰 계획 (성장 규칙 적용 후 HP·속도)", value = false };
            for (int s = 0; s < c.stages.Length; s++)
                for (int w = 0; w < c.stages[s].waves.Length; w++)
                    foreach (var g in c.stages[s].waves[w].groups)
                    {
                        if (!g.enemy) continue;
                        var v = c.rules.Scale(g.enemy.stats, s + 1, w);
                        plan.Add(new Label($"{s + 1}-{w + 1} · 경로 {g.route} · {g.enemy.displayName} ×{g.count} · HP {v.health:0.#} · 속도 {v.speed:0.##} · {g.delay}s/{g.interval}s"));
                    }
            content.Add(plan);
        }
        void StatsFoldout(Object asset, string title)
        {
            var fold = new Foldout { text = title, value = false }; var so = new SerializedObject(asset);
            fold.Add(new PropertyField(so.FindProperty("stats"))); fold.Add(new PropertyField(so.FindProperty("description")));
            fold.Bind(so); content.Add(fold);
        }
        void ApplyBalance()
        {
            var c = Editable; c.ValidateContent();
            var battle = DevTest.Battle; if (!battle || battle.Simulation == null) throw new InvalidOperationException("실행 중인 전투가 없습니다.");
            battle.EditorApplyBalance(c);
        }
        void SaveToAsset()
        {
            if (!campaign) throw new InvalidOperationException("캠페인 asset을 선택하세요.");
            var targets = new Object[] { campaign }.Concat(campaign.robots).Concat(campaign.enemies).ToArray();
            if (EditorApplication.isPlaying && playContent)
            {
                playContent.ValidateContent();
                if (playContent.robots.Length != campaign.robots.Length || playContent.enemies.Length != campaign.enemies.Length)
                    throw new Exception("로봇·적 목록 변경은 Edit Mode에서 하세요.");
                Undo.RecordObjects(targets, "개발 에디터 저장");
                var value = Instantiate(playContent); value.name = campaign.name;
                value.robots = campaign.robots; value.enemies = campaign.enemies;
                RemapEnemies(value, playContent.enemies, campaign.enemies);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), campaign);
                DestroyImmediate(value);
                for (int i = 0; i < campaign.robots.Length; i++) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(playContent.robots[i]), campaign.robots[i]);
                for (int i = 0; i < campaign.enemies.Length; i++) EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(playContent.enemies[i]), campaign.enemies[i]);
            }
            campaign.ValidateContent();
            foreach (var item in targets) { EditorUtility.SetDirty(item); AssetDatabase.SaveAssetIfDirty(item); }
            feedback.text = "원본 asset 저장 완료";
        }

        void Options()
        {
            Note("계산 옵션은 다음 틱, 표시 옵션은 즉시 적용됩니다. 다음 테스트 Play에도 그대로 쓰입니다.");
            var labels = new Dictionary<string, string> {
                ["tutorial"] = "튜토리얼 실행", ["tutorialInput"] = "튜토리얼 입력 제한", ["robotCode"] = "로봇 코드 실행",
                ["enemyMovement"] = "적 이동", ["enemyAttack"] = "적 공격", ["enemySpawn"] = "적 스폰", ["baseDamage"] = "기지 피해", ["robotDamage"] = "로봇 피해",
                ["attackDamage"] = "적에게 공격 피해", ["waveTimeout"] = "웨이브 시간 제한", ["attackLimit"] = "공격 횟수 제한", ["deploymentLimit"] = "설치 개수 제한",
                ["inventoryLimit"] = "인벤토리 제한", ["unlockAll"] = "전체 해금", ["supply"] = "준비 보급", ["rewards"] = "종료 보상", ["drops"] = "처치 드롭",
                ["ranges"] = "사거리 표시", ["hitRanges"] = "피격 범위", ["launchEffects"] = "발사 이펙트", ["hitEffects"] = "피격 이펙트", ["slashEffects"] = "슬래시 이펙트",
                ["projectiles"] = "투사체 표시", ["preview"] = "미리보기 실행", ["detailedLog"] = "상세 이벤트 기록 (리포트용)" };
            foreach (var field in typeof(DefenseTestOptions).GetFields())
            {
                var f = field;
                Check(labels.TryGetValue(f.Name, out var label) ? label : f.Name, (bool)f.GetValue(options), v =>
                {
                    f.SetValue(options, v);
                    if (f.Name == "tutorial") tutorial = v;
                    if (EditorApplication.isPlaying) Act(() => DevTest.SetOptions(options));
                });
            }
            Buttons(("정상값 복원", () => { options = new DefenseTestOptions(); tutorial = true; if (EditorApplication.isPlaying) DevTest.SetOptions(options); Build(); }));
        }

        void UpdateEditor()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .25;
            bool hasBattle = DevTest.Battle;
            if (hasBattle != hadBattle) { hadBattle = hasBattle; Build(); }
            Refresh();
        }
        void Refresh()
        {
            if (status == null) return;
            var sim = DevTest.Sim;
            status.text = $"{(EditorApplication.isPlaying ? "Play" : "Edit")} · {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} · 속도 x{Time.timeScale:0.#}";
            if (sim == null) { if (live != null) live.text = ""; return; }
            status.text += $" · {sim.Setup.Stage}-{sim.WaveIndex + 1} {sim.Phase} · 기지 {sim.BaseHealth} · 코인 {sim.Coins} · 처치 {sim.Kills} / 누수 {sim.Leaks} · 남은 적 {sim.RemainingEnemies}";
            if (live == null) return;
            var battle = DevTest.Battle;
            live.text = $"튜토리얼 step: {(battle.Tutorial ? battle.Tutorial.EditorStep?.stepId : null) ?? "-"}\n" +
                $"로봇 재고: {string.Join(", ", sim.RobotInventory.Select(p => $"{p.Key} {p.Value}"))}\n" +
                $"블록 재고: {string.Join(", ", sim.Inventory.Select(p => $"{p.Key} {p.Value}"))}\n" +
                string.Join("\n", sim.Robots.Select(r => $"#{r.Id} {r.Spec.role} Lv{r.Level} HP {r.Health?.ToString("0") ?? "-"} 공격 {r.AttacksThisWave} {r.LastResult}{(r.Program?.Fault != null ? " 오류: " + r.Program.Fault : "")}"));
        }
        void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) DisposeCopies();
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) { hadBattle = false; CreateGUI(); }
        }
        void DisposeCopies() { foreach (var c in copies) if (c) DestroyImmediate(c); copies.Clear(); playContent = null; }
    }
}
