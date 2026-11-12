using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Point = System.Numerics.Vector2;

namespace CodingGame.Defense
{
    public sealed partial class DefenseBattle : MonoBehaviour
    {
        [Serializable] public sealed class SpawnEntry
        {
            public EnemyDefinition enemy;
            public int route, count = 5;
            public float delay, interval = 1.5f;
            public float healthOverride, speedOverride;
            public int leakDamageOverride;
            public bool boss;
        }
        [Serializable] public sealed class Wave { public SpawnEntry[] groups = Array.Empty<SpawnEntry>(); }

        [SerializeField] DefenseCampaignDefinition campaign;
        [Header("Authored scene references")]
        [SerializeField] Camera battleCamera;
        [SerializeField, Tooltip("Stage 1 전용 일자 맵과 그 카메라 자리(Tools/Develop/1스테이지 일자 맵 만들기). 비우면 모든 스테이지가 작업실 맵")]
        CodingGame.Art.MapPainter.ModularMapRoot stageOneMap;
        [SerializeField] CodingGame.Art.MapPainter.ModularMapRoot workshopMap;
        [SerializeField] Transform stageOneView;
        [SerializeField, Tooltip("로봇·적 체력바 UI 층(Tools/Optimize/Unit Health Bars)")] DefenseHealthBars healthBars;
        Pose workshopView; bool workshopViewKnown;
        [SerializeField] EventSystem eventSystem;
        [SerializeField] DefenseCodeEditor codeEditor;
        [SerializeField] BoxCollider deploymentArea;
        [SerializeField] Collider[] blockedAreas = Array.Empty<Collider>();
        [SerializeField] Transform actorsRoot;
        [SerializeField] DefenseRoute[] routes = Array.Empty<DefenseRoute>();
        [Tooltip("Moved onto the painted map's base cell when the routes come from a map.")]
        [SerializeField] Transform core;
        DefenseCoreGlitch coreGlitch;
        static readonly Color RangeColor = new Color(.35f, .9f, 1); // attack ranges: a MOBA-like cyan rim; the tutorial highlight pulses blue and fills (DefenseRangeRing)
        Bounds[] floorParts, firstSpotPart;
        [SerializeField] RobotDefinition[] robots = Array.Empty<RobotDefinition>();
        [SerializeField] EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        [SerializeField] DefenseRangeRing rangeRing;
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField] Transform placementMarker;
        [SerializeField] Transform blockDropPrefab;
        [SerializeField] Transform projectilePrefab;
        [SerializeField] DefenseHitVisuals hitEffects = new DefenseHitVisuals();
        [SerializeField] GameObject developerRoot;
        [SerializeField, Tooltip("상단 바의 'F1 개발자' 안내(PR #19 상단 바 이후 숨김).")] GameObject developerHint;
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] Button hitRangeButton;
        [SerializeField] TMP_Text hitRangeLabel;
        [SerializeField] DefensePlayerHUD playerHUD;
        [SerializeField] DefenseSelectionCamera selectionCamera;
        [SerializeField] DefenseInventoryUI inventoryUI;
        [SerializeField] DefenseAttackPreview attackPreview;
        [SerializeField, Min(1)] int stage = 1;
        public int Stage => stage;
        public bool FromStageMap => StageMap.StageProgress.SelectedStage > 0;
        public RobotDefinition RobotDefinition(int index) => robots[index];
        public IReadOnlyList<RobotDefinition> RobotDefinitions => robots;
        public EnemyDefinition PreviewEnemy => enemies[0];
        [Header("Authored HUD")]
        [SerializeField] TMP_Text header;
        [SerializeField] TMP_Text selectionText;
        [SerializeField] TMP_Text message;
        [SerializeField] TMP_Text stateText;
        [SerializeField] TMP_Text resultText;
        [SerializeField] GameObject resultPanel;
        [SerializeField] Button startButton, pauseButton, speedButton, resetButton, removeButton;
        [SerializeField] TMP_Text pauseLabel, speedLabel;
        [SerializeField] Button[] robotButtons = Array.Empty<Button>();
        [SerializeField] TMP_Text[] developerRobotLabels = Array.Empty<TMP_Text>();

        readonly Dictionary<int, DefenseActorView> robotViews = new Dictionary<int, DefenseActorView>();
        DefenseSounds sounds;
        public void PlaySound(string kind) => sounds.Play(kind);
        readonly Dictionary<int, DefenseActorView> enemyViews = new Dictionary<int, DefenseActorView>();
        readonly List<Transform> dropViews = new List<Transform>();
        readonly DefenseProjectileVisuals projectileViews = new DefenseProjectileVisuals();
        bool bossEntranceStarted, bossCameraRestored;
        readonly HashSet<int> liveIds = new HashSet<int>();
        readonly List<int> goneIds = new List<int>();
        [SerializeField, HideInInspector] DefenseBossWarning bossWarning; // baked in the scene (Tools/Optimize/128 Bake Battle UI)
        DefenseBossWarning BossWarning => bossWarning ? bossWarning : bossWarning = new GameObject("BossWarning").AddComponent<DefenseBossWarning>();
        float bossEntranceUntil, bossCameraRestoreAt;
        public bool DeveloperMode { get; private set; }
        // The developer panel (F1) and the balance window (F2) are for the team only: developer rights (GameFlow.Developer).
        public static bool DeveloperTools => GameFlow.Developer;
        public bool ShowHitRanges { get; private set; }
        public bool HelpOpen { get; internal set; }
        public bool HasDialogueBlockingWindow => HelpOpen || (inventoryUI && inventoryUI.IsOpen) || (codeEditor && codeEditor.HintOpen) || (playerHUD && playerHUD.MenuOpen);
        int palette = -1, selected;
        bool draggingRobot;
        public DefenseSimulation Simulation { get; private set; }
        public int SelectedId => selected;
        public bool IsPlacing => palette >= 0;
        public int PlacingDefinition => palette;
        public float BattleSpeed { get; private set; } = 1;
        public bool SpeedUnlocked => Simulation != null && (!Simulation.Setup.PlayerFlow || !Simulation.Options.tutorial ||
            Stage != 1 || Simulation.WaveIndex > 0 || tutorial && tutorial.SpeedLessonReached);
        public string StatusMessage { get; private set; }
        public TutorialDirector Tutorial => tutorial;
        public DefenseCodeEditor CodeEditor => codeEditor;
        int tutorialUnits;
        public bool TutorialBlocksInput => tutorial && tutorial.BlocksGameplayInput;
        public bool TutorialBlocksPreview => tutorial && tutorial.BlocksPreview;
        public bool CanShowRobotActions => Simulation != null && Simulation.CanPrepare && !DeveloperMode && !HelpOpen && !codeEditor.IsOpen && !inventoryUI.IsOpen &&
            (TutorialAllows("Battle.Develop") || TutorialAllows("Battle.Upgrade") || TutorialAllows("Battle.Recall"));
        public string DefinitionName(int index) => robots[index].displayName;
        public RobotRole DefinitionRole(int index) => robots[index].stats.role;
        public EnemyDefinition EnemyAt(int index) => enemies[index];
        public void BeginCodeView(RobotState robot)
        {
            inventoryUI.Close(); playerHUD.SetVisible(false); developerRoot.SetActive(false);
            if (!TutorialBlocksPreview) attackPreview.Open(robot);
            else attackPreview.Close();
        }
        public void EndCodeView()
        {
            selectionCamera.Restore(); playerHUD.SetVisible(!DeveloperMode); developerRoot.SetActive(DeveloperMode);
            attackPreview.Close();
        }
        public void RestartCodePreview() => attackPreview.Restart();
        public string RobotName(int id)
        { var bot = Simulation.FindRobot(id); return bot == null ? "로봇 없음" : robots[bot.DefinitionIndex].displayName; }

        void Start()
        {
            if (!campaign) throw new InvalidOperationException("DefenseBattle에 캠페인 asset을 연결하세요.");
            campaign.ValidateContent(); robots = campaign.robots; enemies = campaign.enemies;
            ValidateReferences();
            // Body texts are saved in Pretendard Bold (Tools/Optimize/126); no font pass at load.
            sounds = gameObject.AddComponent<DefenseSounds>();
            developerRoot.SetActive(false);
            developerHint.SetActive(false); // PR #19's header puts the coin stat in this corner; F1 still works in developer mode
            startButton.onClick.AddListener(StartBattle);
            pauseButton.onClick.AddListener(TogglePause);
            speedButton.onClick.AddListener(ToggleSpeed);
            resetButton.onClick.AddListener(Restart);
            hitRangeButton.onClick.AddListener(ToggleHitRanges);
            hitRangeLabel.text = "근접 피격범위: OFF";
            removeButton.onClick.AddListener(RemoveSelected);
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => inventoryUI.SelectRobotItem(index)); }
            if (FromStageMap) stage = StageMap.StageProgress.SelectedStage;
            devWindow = gameObject.AddComponent<DefenseDevWindow>();
            if (core) core.TryGetComponent(out coreGlitch);
            Restart();
        }
        void ValidateReferences()
        {
            if (!hitEffects.IsConfigured) throw new InvalidOperationException("피격 이펙트 프리팹을 Inspector에 연결하세요.");
            if (!selectionCamera || !inventoryUI || !attackPreview) throw new InvalidOperationException("선택 카메라·인벤토리·미리보기 참조를 연결하세요.");
            if (!hitRangeButton || !hitRangeLabel || !projectilePrefab || !blockDropPrefab || !developerRoot || !developerHint || !playerHUD || !battleCamera || !eventSystem || !codeEditor || !deploymentArea || !actorsRoot || !rangeRing || !effectRing || !placementMarker ||
                !header || !selectionText || !message || !stateText || !resultText || !resultPanel || !startButton || !pauseButton ||
                !speedButton || !resetButton || !removeButton || !pauseLabel || !speedLabel)
                throw new InvalidOperationException("DefenseBattle의 필수 Inspector 참조가 누락되었습니다.");
            if (robots.Length == 0 || robotButtons.Length != robots.Length || developerRobotLabels.Length != robots.Length || developerRobotLabels.Any(t => !t) || robots.Any(r => !r || !r.prefab) ||
                robotButtons.Any(b => !b) || blockedAreas.Any(area => !area) || enemies.Length == 0 || enemies.Any(e => !e || !e.prefab) || routes.Length == 0 || routes.Any(r => !r))
                throw new InvalidOperationException("로봇·적·경로·버튼 목록의 Inspector 참조를 확인하세요.");
            foreach (var r in robots) r.stats.Validate();
            var warrior = robots.FirstOrDefault(r => r.stats.role == RobotRole.Warrior);
            var bomber = robots.FirstOrDefault(r => r.stats.role == RobotRole.Bomber);
            if (warrior && bomber && Mathf.Abs(bomber.stats.range - warrior.stats.range * 1.5f) > .001f)
                throw new InvalidOperationException("시험 정의의 폭탄 사거리는 전사 기본 사거리의 1.5배여야 합니다.");
        }
        BattleSetup MakeSetup(DefenseCampaignDefinition content, int selectedStage)
        {
            content.ValidateContent();
            if (selectedStage < 1 || selectedStage > content.stages.Length) throw new ArgumentException("스테이지 범위 오류");
            UseStageMap(selectedStage);
            var bounds=deploymentArea.bounds;
            var map=routes[0].Map;
            if(map&&map.hasBase&&core)core.position=map.CellPosition(map.baseCell);
            return new BattleSetup {
                PlayerFlow=true, Stage=selectedStage, BaseHealth=content.baseHealth, RobotLimit=content.robotLimit,
                WaveSeconds=content.stages[selectedStage-1].waveSeconds,
                RulesConfig=JsonUtility.FromJson<DefenseBalanceRules>(JsonUtility.ToJson(content.rules)),
                Progression=content.progression.Copy(),
                Permissions=content.permissions.Select(p=>p.Copy()).ToArray(),
                ActionProfiles=content.robots.Select(r=>r.stats.Copy()).ToArray(),
                Min=new Point(bounds.min.x,bounds.min.z), Max=new Point(bounds.max.x,bounds.max.z),
                Routes=routes.Select(r=>r.Read()).ToArray(),
                Obstacles=blockedAreas.Select(a=>a.bounds).Concat(MapProps(map)).Select(b=>new PlacementObstacle {Min=new Point(b.min.x,b.min.z),Max=new Point(b.max.x,b.max.z)}).ToArray(),
                Floor=MapTiles(map).ToArray(),
                Waves=content.ReadWaves(selectedStage),
                CampaignWaves=Enumerable.Range(1,content.stages.Length).Select(content.ReadWaves).ToArray(),
                CampaignSeconds=content.stages.Select(s=>s.waveSeconds).ToArray()
            };
        }
        // Stage 1 plays on a short straight road and Stages 2·3 on the workshop (play feedback: the workshop was too big for the
        // first lessons). Only one map is active, so routes, tiles and props (DefenseRoute.Map) come from it; the camera and the
        // core's glitch follow.
        // The core model of the map in use (the core bar stands over it) and the camera that shows it.
        public Transform CoreModel { get; private set; }
        // The spawn gate's and the core's renderers: the road focus stops at their edge instead of lighting them.
        Renderer[][] routeEnds = System.Array.Empty<Renderer[]>();
        // A road part cut back along its length to the model's edge where they overlap on the ground.
        static Bounds OffModel(Bounds part, Renderer[] model)
        {
            Bounds? hull = null;
            foreach (var r in model) if (r && r.enabled && r.gameObject.activeInHierarchy) { if (hull is Bounds h) { h.Encapsulate(r.bounds); hull = h; } else hull = r.bounds; }
            if (!(hull is Bounds m) || m.max.x <= part.min.x || m.min.x >= part.max.x || m.max.z <= part.min.z || m.min.z >= part.max.z) return part;
            Vector3 min = part.min, max = part.max;
            if (part.size.x >= part.size.z) { if (m.center.x < part.center.x) min.x = Mathf.Min(max.x, m.max.x); else max.x = Mathf.Max(min.x, m.min.x); }
            else { if (m.center.z < part.center.z) min.z = Mathf.Min(max.z, m.max.z); else max.z = Mathf.Max(min.z, m.min.z); }
            part.SetMinMax(min, max);
            return part;
        }
        public Camera BattleCamera => battleCamera;
        void UseStageMap(int selectedStage)
        {
            if (!stageOneMap || !workshopMap || !stageOneView) return;
            if (!workshopViewKnown) { workshopView = new Pose(battleCamera.transform.position, battleCamera.transform.rotation); workshopViewKnown = true; }
            bool first = selectedStage == 1;
            stageOneMap.gameObject.SetActive(first); workshopMap.gameObject.SetActive(!first);
            var view = first ? new Pose(stageOneView.position, stageOneView.rotation) : workshopView;
            selectionCamera.SetHome(view.position, view.rotation);
            floorParts = null;
            CoreModel = (first ? stageOneMap : workshopMap).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CodeBase");
            var gate = (first ? stageOneMap : workshopMap).GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ErrorGate");
            routeEnds = new[] { gate, CoreModel }.Where(t => t).Select(t => t.GetComponentsInChildren<Renderer>(true)).ToArray();
            if (coreGlitch) coreGlitch.SetModel(CoreModel);
            StartCoroutine(CombineTiles(first ? stageOneMap : workshopMap));
        }
        // 163: the floor and road tiles (about 250 renderers) drawn as a few combined meshes once they stop moving. Not marked
        // static in the scene, since the map's build animation raises and scales each tile when the map first shows.
        System.Collections.IEnumerator CombineTiles(CodingGame.Art.MapPainter.ModularMapRoot map)
        {
            yield return null; // the build animation starts in the map's Start
            var build = map.GetComponent<CodingGame.Art.MapPainter.MapBuildSequence>();
            while (build && build.IsAnimating) yield return null;
            foreach (var group in new[] { map.floorGroup, map.roadGroup })
                if (group && !group.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.isPartOfStaticBatch)) StaticBatchingUtility.Combine(group.gameObject);
        }
        // The ground of the map, one square per floor or road cell (cells are centred on CellPosition).
        static IEnumerable<PlacementObstacle> MapTiles(CodingGame.Art.MapPainter.ModularMapRoot map)
        {
            if (!map) yield break;
            foreach (var cell in map.floorCells.Concat(map.roadCells))
            {
                Vector3 a = map.transform.TransformPoint(new Vector3((cell.x - .5f) * map.cellSize, 0, (cell.z - .5f) * map.cellSize));
                Vector3 b = map.transform.TransformPoint(new Vector3((cell.x + .5f) * map.cellSize, 0, (cell.z + .5f) * map.cellSize));
                yield return new PlacementObstacle { Min = new Point(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z)), Max = new Point(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)) };
            }
        }
        // Props the map carries besides its floor, road and backdrop (Stage01 Workshop's pots, books, base…): no robot on them
        // (planner). One footprint per prop, the box around its renderers.
        static IEnumerable<Bounds> MapProps(CodingGame.Art.MapPainter.ModularMapRoot map)
        {
            if (!map) yield break;
            foreach (Transform group in map.transform)
            {
                if (group == map.floorGroup || group == map.roadGroup || group.GetComponent<CodingGame.Art.MapPainter.BlueprintBackdrop>()) continue;
                foreach (Transform prop in group)
                {
                    var parts = prop.GetComponentsInChildren<Renderer>();
                    if (parts.Length == 0) continue;
                    var box = parts[0].bounds;
                    foreach (var part in parts) box.Encapsulate(part.bounds);
                    yield return box;
                }
            }
        }
        public void Restart()
        {
            if (!TutorialAllows("Battle.Restart") && !(Simulation != null && Simulation.Phase == BattlePhase.Defeat && TutorialAllows("Battle.ContinueCampaign"))) return;
            var setup = MakeSetup(campaign, stage);
            setup.PreviousStageRewards = FromStageMap;
#if UNITY_EDITOR
            setup.TestOptions=EditorOptions;
#endif
            ResetSession(setup);
        }
        // A lost wave starts over as it began (robots, code, levels, coins, core health), so a defeat costs one wave, not the
        // stage. The new session replays the wave's tutorial sequence, so only one that just talks before the battle allows
        // it (1-2, 1-4, 1-5); 1-1 and 1-3 would ask for placing and coding again.
        public bool CanRetryWave => Simulation != null && Simulation.Phase == BattlePhase.Defeat && Simulation.Checkpoint != null &&
            !(tutorial && tutorial.Sequences.Any(s => s && s.enabled && s.stage == stage && s.wave == Simulation.Checkpoint.Wave + 1 && !s.OnlyTalksBeforeBattle));
        public void RetryWave()
        {
            if (!CanRetryWave || !TutorialAllows("Battle.ContinueCampaign")) return;
            var checkpoint = Simulation.Checkpoint;
            var setup = MakeSetup(campaign, stage);
            setup.InitialWave = checkpoint.Wave; setup.PreviousStageRewards = true;
#if UNITY_EDITOR
            setup.TestOptions=EditorOptions;
#endif
            ResetSession(setup);
            try { Simulation.Restore(checkpoint); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                Trace("retry-failed " + error.Message);
                Restart(); Tell("웨이브를 되돌리지 못해 스테이지를 처음부터 시작합니다."); return;
            }
            foreach (var bot in Simulation.Robots) SpawnRobotView(bot);
            Tell($"WAVE {checkpoint.Wave + 1} 다시 도전 · 배치와 코드를 고친 뒤 전투를 시작하세요.");
            SyncViews(0); RefreshHUD();
        }
        void ResetSession(BattleSetup setup)
        {
            setup.Validate();
            inventoryUI.Close();
            codeEditor.Close(false);
            tutorial?.UnregisterRuntimeTarget(TutorialKeys.SelectedRobot, null);
            foreach (var view in robotViews.Values.Concat(enemyViews.Values)) DefenseViews.Discard(view);
            robotViews.Clear(); enemyViews.Clear();
            ClearDrops();
            projectileViews.Clear();
            hitEffects.Clear();
            Simulation = new DefenseSimulation(setup);
            bossEntranceStarted = bossCameraRestored = false;
            bossEntranceUntil = bossCameraRestoreAt = 0;
            selectionCamera.Restore();
            selected = 0; BattleSpeed = 1; HidePlacement();
            rangeRing.Hide(); effectRing.Hide(); resultPanel.SetActive(false);
            foreach (var pair in enemyStats) Simulation.SetEnemyStats(pair.Key, pair.Value);
            Tell("로봇을 선택하고 허용 영역을 클릭하세요. 탱커만 경로 위에 설치합니다.");
            RefreshHUD();
        }

        // F2 balance window (developer rights): fixed health, speed, attack damage and core damage per enemy definition,
        // applied to the battle under way and kept for every battle until the game quits; null gives the authored ones back.
        static readonly Dictionary<int, DefenseSimulation.EnemyStats> enemyStats = new Dictionary<int, DefenseSimulation.EnemyStats>();
        DefenseDevWindow devWindow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBalance() => enemyStats.Clear();
        public IReadOnlyList<EnemyDefinition> EnemyDefinitions => enemies;
        public bool EnemyStatsSet(int definition) => enemyStats.ContainsKey(definition);
        public EnemySpec EnemyNow(int definition) => Simulation.EnemyNow(definition, enemies[definition].stats);
        public void SetEnemyStats(int definition, DefenseSimulation.EnemyStats stats)
        {
            Simulation.SetEnemyStats(definition, stats);
            if (stats == null) enemyStats.Remove(definition); else enemyStats[definition] = stats;
            SyncViews(0); RefreshHUD();
            Trace(stats == null ? $"balance {enemies[definition].name} authored" :
                $"balance {enemies[definition].name} hp {stats.health} speed {stats.speed} damage {stats.damage} leak {stats.leakDamage}");
        }

        // F1 panel (developer rights): "1-3.C05" starts that stage and wave over and opens its tutorial at that line ("1-3"
        // at its start). Returns what went wrong, or null.
        public string DeveloperJump(string id)
        {
            if (!DeveloperTools || !tutorial) return "개발자 권한이 없습니다.";
            var parts = (id ?? "").Trim().Split('.');
            var sequence = tutorial.Sequences.FirstOrDefault(s => s && s.sequenceId == parts[0]);
            if (!sequence) return "없는 시퀀스: " + parts[0];
            int step = parts.Length > 1 ? Array.FindIndex(sequence.steps, s => s.stepId == parts[1]) : 0;
            if (step < 0) return "없는 대사 ID: " + id;
            SetDeveloperMode(false);
            stage = sequence.stage;
            var setup = MakeSetup(campaign, stage);
            setup.InitialWave = sequence.wave - 1; setup.PreviousStageRewards = true;
#if UNITY_EDITOR
            setup.TestOptions = EditorOptions;
#endif
            ResetSession(setup);
            tutorial.JumpWhenBegun(sequence.sequenceId, step);
            return null;
        }
        public void StartBattle()
        {
            if (!TutorialAllows("Battle.Start")) { if (tutorial) tutorial.NudgeBlocked(); return; }
            string error = Simulation.StartError();
            if (error != null) { Tell(error); return; }
            HidePlacement();
            Trace("wave-start " + string.Join(", ", Simulation.UpcomingWave.Select(g => $"{enemies[g.DefinitionIndex].name} x{g.Count} HP={g.Enemy.health:0.0} speed={g.Enemy.speed:0.00}")));
            Trace(InventoryLine());
            Simulation.Start();
            Tell(Simulation.FinalRush ? "마지막 웨이브 · 시간이 끝나면 남은 적이 모두 코어로 돌진합니다." : "전투 중 · 웨이브 종료 후 지정 보상을 수령하세요.");
            RefreshHUD();
            ReportTutorialAction("Battle.Start", "TargetClicked", "BattleStarted");
        }
        public void ClaimRewards()
        {
            if (!TutorialAllows("Battle.ClaimRewards") && !(Simulation.Phase == BattlePhase.Reward && TutorialAllows("Battle.ContinueCampaign"))) return;
            if (Simulation.Phase == BattlePhase.Reward) Trace($"wave-end kills={Simulation.Kills} leaks={Simulation.Leaks} survivors={Simulation.LastSurvivors} rewards={Simulation.PendingDrops.Count}");
            if (!Simulation.ClaimRewards()) return;
            UISound.Play("reward"); UISound.Play("coin", .7f);
            selected = 0; rangeRing.Hide(); ClearCombatVisuals();
            ClearDrops(); Tell(Simulation.Phase == BattlePhase.Victory ? Simulation.CanAdvanceStage ?
                "스테이지 완료 · 다음 스테이지로 진행하세요." : "모든 스테이지를 완료했습니다." :
                "보상을 수령했습니다. 로봇과 코드를 정비하고 다음 전투를 시작하세요."); RefreshHUD();
            Trace(InventoryLine());
            ReportTutorialAction("Battle.ClaimRewards", "TargetClicked", "RewardsClaimed");
            ReportTutorialAction("Battle.ContinueCampaign", "TargetClicked", "RewardsClaimed");
        }
        void ClearDrops() { foreach (var drop in dropViews) if (drop) Destroy(drop.gameObject); dropViews.Clear(); }
        public void ContinueCampaign()
        {
            if (Simulation.Phase == BattlePhase.Reward) { ClaimRewards(); return; }
            if (Simulation.Phase == BattlePhase.Defeat) { if (CanRetryWave) RetryWave(); else Restart(); return; }
            if (!TutorialAllows("Battle.ContinueCampaign")) return;
            if (FromStageMap)
            {
                StageMap.StageProgress.RecordStars(stage, Simulation.StageStars);
                StageMap.StageProgress.MarkCleared(stage);
                GameFlow.LoadScene(GameFlow.StageScene);
                return;
            }
            if (Simulation.CanAdvanceStage && stageOneMap && stage == 1)
            {
                // Stage 2 is on another map, so it starts as a new session with Stage 1's rewards (as from the stage map).
                codeEditor.Close(false); inventoryUI.Close();
                stage = 2; var next = MakeSetup(campaign, stage); next.PreviousStageRewards = true;
                ResetSession(next);
                ReportTutorialAction("Battle.ContinueCampaign", "TargetClicked", "StageAdvanced");
                return;
            }
            if (Simulation.CanAdvanceStage)
            {
                codeEditor.Close(false); inventoryUI.Close();
                Simulation.AdvanceStage(); stage = Simulation.Setup.Stage;
                selected = 0; ClearDrops(); ClearCombatVisuals();
                Tell($"STAGE {stage} · {Simulation.Lesson?.topic}"); RefreshHUD();
                ReportTutorialAction("Battle.ContinueCampaign", "TargetClicked", "StageAdvanced");
            }
        }
        public void NotifyCodeApplied(int id, bool empty)
        {
            Tell(RobotName(id) + (empty ? " · 코드 없음" : " · 적용 완료"));
            // Autosave applies on every edit; log the inventory only when the reservation actually changed.
            string inventory = InventoryLine();
            if (inventory != lastInventory) Trace(lastInventory = inventory);
            ReportTutorialAction("Editor.ApplyProgram", "TargetClicked", "ProgramApplied");
        }
        public bool TutorialAllows(string key) => !tutorial || tutorial.CanPerformAction(key);
        // The robot's Play button (play feedback 4): calls its function again once it has ended; a press while it runs does
        // nothing. The tutorial knows the button by the robot's place in id order, like Robot.Unit.
        public void PlayRobot(int id)
        {
            string key = TutorialKeys.Play(TutorialWatcher.RobotIndex(Simulation, id));
            if (!TutorialAllows(key) || !Simulation.Invoke(id)) return;
            UISound.Play("click"); ReportTutorialAction(key, TutorialSignals.TargetClicked);
        }
        public void ReportTutorialAction(string key, params string[] signals) { if (tutorial) foreach (var signal in signals) tutorial.ReportSuccess(key, signal); }
        public void ReportTutorialDrop(RectTransform source, RectTransform destination, string signal) => tutorial?.ReportDrop(source, destination, signal);
        public void ToggleDeveloper()
        {
            if (!TutorialAllows("Battle.ToggleDeveloper")) return;
            SetDeveloperMode(!DeveloperMode);
            ReportTutorialAction("Battle.ToggleDeveloper", "TargetClicked");
        }
        void SetDeveloperMode(bool on)
        {
            inventoryUI.Close(); codeEditor.Close(); CancelPlacement(); DeveloperMode = on;
            developerRoot.SetActive(on); playerHUD.SetVisible(!on);
        }
        // The Esc menu: it opens at any time, the tutorial included. Its pause is not the player's pause button, so the tutorial
        // neither gates nor counts it; the tutorial and the battle's sounds wait while it is open.
        public void ToggleMenu() => playerHUD.ToggleMenu();
        public bool InTutorial => tutorial && tutorial.IsActive;
        public bool CanRestart => TutorialAllows("Battle.Restart");
        // pausedWave: the menu paused a running wave (opening) / resumes it (closing); a wave the player paused stays paused.
        public void MenuOpened(bool open, bool pausedWave)
        {
            if (pausedWave && Simulation != null && Simulation.Phase == (open ? BattlePhase.Running : BattlePhase.Paused)) { Simulation.TogglePause(); RefreshHUD(); }
            if (tutorial) tutorial.SetPaused(open);
            AudioListener.pause = open;
        }
        public void TogglePause()
        {
            if (!TutorialAllows(TutorialKeys.Pause)) return;
            Simulation.TogglePause(); RefreshHUD(); ReportTutorialAction(TutorialKeys.Pause, TutorialSignals.TargetClicked);
        }
        public void ToggleHitRanges()
        {
            if (!TutorialAllows("Battle.ToggleHitRanges")) return;
            ShowHitRanges = !ShowHitRanges;
            hitRangeLabel.text = "근접 피격범위: " + (ShowHitRanges ? "ON" : "OFF");
            hitEffects.SetHitRangesVisible(ShowHitRanges);
            attackPreview.SetHitRangesVisible(ShowHitRanges);
            ReportTutorialAction("Battle.ToggleHitRanges", "TargetClicked");
        }
        public void ToggleSpeed()
        {
            if (!SpeedUnlocked || !TutorialAllows(TutorialKeys.Speed)) return;
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) return;
            // 1x, 2x, 3x; developer mode adds 5x (GameFlow.Developer).
            BattleSpeed = BattleSpeed < 3 ? BattleSpeed + 1 : BattleSpeed == 3 && GameFlow.Developer ? 5 : 1; RefreshHUD();
            ReportTutorialAction(TutorialKeys.Speed, TutorialSignals.TargetClicked);
        }
        public void ChooseRobot(int index)
        {
            string tutorialKey = "Battle.Robot." + index;
            if (!TutorialAllows(tutorialKey)) return;
            if (!Simulation.CanPrepare || index < 0 || index >= robots.Length) return;
            string error = Simulation.DeploymentError(robots[index].stats.role);
            if (error != null) { palette = -1; RejectPlacement(index, error); return; }
            palette = index; selected = 0;
            // The marker under the cursor is the robot's body as the placement rules see it (play feedback 3: a 0.9 disc under a
            // 1.5 body made it hard to judge what fits).
            float body = robots[index].stats.radius * 2;
            placementMarker.localScale = new Vector3(body, placementMarker.localScale.y, body);
            Tell(robots[index].displayName + " 배치 · 허용 위치 클릭 / 우클릭 또는 ESC 취소");
            RefreshHUD();
            ReportTutorialAction(tutorialKey, "TargetClicked");
        }
        public void BeginRobotDrag(int index)
        {
            if (!Simulation.CanPrepare || DeveloperMode) return;
            ChooseRobot(index);
            if (palette == index) { draggingRobot = true; inventoryUI.Close(); }
        }
        public void EndRobotDrag(Vector2 screen)
        {
            if (!draggingRobot) return;
            draggingRobot = false;
            int index = palette;
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screen }, hits);
            if (index >= 0 && hits.Count == 0 && PointerPosition(screen, out var point) && PlaceRobot(index, point)) return;
            // A short drag ending on the button is still a robot selection. Keep it armed for a map click.
            placementMarker.gameObject.SetActive(false); rangeRing.Hide();
        }
        DefenseActorView SpawnRobotView(RobotState bot)
        {
            var view = Instantiate(robots[bot.DefinitionIndex].prefab, DefenseViews.World(bot.Position), Quaternion.identity, actorsRoot);
            view.Face(battleCamera.transform.position); // face shown to the player when placed
            robotViews.Add(bot.Id, view);
            return view;
        }
        // 1-3: the tutorial stands the lesson's shooter by the road, far enough past the entrance (8) that the enemies walk into
        // its range (7) and close enough to the road that they pass within 3 of it (the lesson's condition); out on the open
        // floor that condition might never come true.
        public void PlaceLessonShooter()
        {
            int index = Array.FindIndex(robots, r => r.stats.role == RobotRole.Shooter);
            var sim = Simulation;
            if (index < 0 || sim == null || !sim.CanEdit || sim.Robots.Any(r => r.Spec.role == RobotRole.Shooter)) return;
            var groups = sim.Setup.Waves[sim.WaveIndex];
            var road = sim.Setup.Routes[groups.Length > 0 ? groups[0].RouteIndex : 0].Points;
            for (float along = 8; along < 20; along += .5f)
                for (float off = 2; off <= 2.81f; off += .2f)
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var (point, normal) = Along(road, along);
                        var spot = point + normal * off * side;
                        if (sim.PlacementError(robots[index].stats, spot) != null) continue;
                        SpawnRobotView(sim.Place(robots[index].stats, index, spot, out _));
                        sounds.Play("place"); SyncViews(0); RefreshHUD();
                        return;
                    }
            throw new InvalidOperationException("1-3 원거리 로봇을 세울 길 옆 자리가 없습니다.");
        }
        // The point at distance s along a polyline and the unit normal to its left there.
        static (Point point, Point normal) Along(Point[] points, float s)
        {
            for (int i = 1; i < points.Length; i++)
            {
                var segment = points[i] - points[i - 1]; float length = segment.Length();
                if (s <= length || i == points.Length - 1)
                {
                    var dir = length > 0 ? segment / length : new Point(1, 0);
                    return (points[i - 1] + dir * Math.Min(s, length), new Point(-dir.Y, dir.X));
                }
                s -= length;
            }
            return (points[0], new Point(0, 1));
        }
        // 1-1's first warrior stands at one spot by the road (the planner's pick): the lesson lights that cell, only a click
        // inside it places the robot, and the robot stands exactly on the spot.
        static readonly Point FirstWarriorSpot = new Point(2.582f, -.191f);
        public static Vector3 FirstWarriorPosition => DefenseViews.World(FirstWarriorSpot);
        // One map cell round the spot, lit on the ground in line with the map (a square in perspective, not a screen rect).
        Bounds FirstSpotCell
        {
            get
            {
                var map = routes[0].Map; float size = map ? map.cellSize * map.transform.lossyScale.x : 2;
                return new Bounds(FirstWarriorPosition, new Vector3(size, .1f, size));
            }
        }
        bool FirstLessonPlacement => stage == 1 && Simulation != null && Simulation.WaveIndex == 0 && Simulation.Robots.Count == 0 &&
            tutorial && tutorial.IsActive;
        public bool PlaceRobot(int index, Vector3 position)
        {
            if (index < 0 || index >= robots.Length ||
                tutorial && (!tutorial.AllowsWorldPoint(position) || !tutorial.AllowsRobotChoice(index))) return false;
            if (FirstLessonPlacement)
            {
                var cell = FirstSpotCell;
                if (position.x < cell.min.x || position.x > cell.max.x || position.z < cell.min.z || position.z > cell.max.z)
                { RejectPlacement(index, "디버가 밝혀 준 칸에 세워 주세요."); return false; }
                position = FirstWarriorPosition;
            }
            var bot = Simulation.Place(robots[index].stats, index, new Point(position.x, position.z), out var error);
            if (bot == null) { RejectPlacement(index, error); return false; }
            var view = SpawnRobotView(bot);
            sounds.Play("place");
            // The tutorial asks the player to click the new robot next, so its range and menu stay hidden until then.
            selected = TutorialBlocksInput ? 0 : bot.Id; HidePlacement();
            RegisterSelectedRobot(bot, view);
            Tell(robots[index].displayName + " 배치 완료 · 개발 버튼으로 코드 작성");
            SyncViews(0); RefreshHUD();
            tutorial?.ReportWorldSuccess(position, "TargetClicked");
            tutorial?.ReportWorldSuccess(position, "RobotPlaced");
            return true;
        }
        public void SelectRobot(int id)
        {
            if (!robotViews.TryGetValue(id, out var view) || !view) return;
            string unit = TutorialKeys.Unit(TutorialWatcher.RobotIndex(Simulation, id));
            string key = TutorialAllows(unit) ? unit : TutorialAllows(TutorialKeys.SelectedRobot) ? TutorialKeys.SelectedRobot : "Battle.SelectRobot";
            if (!TutorialAllows(key)) return;
            var robot = Simulation.FindRobot(id);
            if (robot == null) return;
            selected = id; HidePlacement();
            RegisterSelectedRobot(robot, view);
            RefreshHUD();
            tutorial?.ReportSuccess(key, TutorialSignals.TargetClicked, key == "Battle.SelectRobot" ? null : view.gameObject);
        }
        void RegisterSelectedRobot(RobotState robot, DefenseActorView view)
        {
            if (tutorial) tutorial.RegisterWorldTarget(TutorialKeys.SelectedRobot, view.gameObject, RobotBounds(robot));
        }
        // A robot's clickable body as a tutorial target, tall enough to take the status tag over its head too (play feedback 3:
        // the focus box ran along the '코드 없음' tag's top edge).
        static Bounds RobotBounds(RobotState robot)
        {
            float width = Mathf.Max(robot.Spec.radius * 2.5f, RobotWidth), height = RobotHeight + RobotTagHeight;
            return new Bounds(DefenseViews.World(robot.Position, height / 2), new Vector3(width, height, width));
        }
        const float RobotTagHeight = .35f;
        public void DevelopSelected()
        {
            if (!TutorialAllows("Battle.Develop")) return;
            codeEditor.Open(selected);
            if (codeEditor.IsOpen) ReportTutorialAction("Battle.Develop", "TargetClicked", "EditorOpened");
        }
        public void UpgradeSelected()
        {
            if (!TutorialAllows("Battle.Upgrade")) return;
            string error = Simulation.UpgradeError(selected);
            if (error != null) { Tell(error); UISound.Play("error"); return; }
            if (Simulation.UpgradeRobot(selected))
            {
                Tell(RobotName(selected) + " · 레벨업 완료"); UISound.Play("upgrade");
                ReportTutorialAction("Battle.Upgrade", "TargetClicked", "RobotUpgraded");
            }
        }
        public void RemoveSelected()
        {
            if (!TutorialAllows("Battle.Recall")) return;
            if (!Simulation.CanPrepare) return;
            if (!Simulation.RecallUnlocked) { Tell("회수는 1-3부터 열립니다."); return; }
            codeEditor.Close();
            if (robotViews.TryGetValue(selected, out var removed)) tutorial?.UnregisterRuntimeTarget(TutorialKeys.SelectedRobot, removed.gameObject);
            Simulation.RemoveRobot(selected); selected = 0; rangeRing.Hide();
            Tell("로봇과 사용 블록을 인벤토리로 회수했습니다."); SyncViews(0); RefreshHUD();
            ReportTutorialAction("Battle.Recall", "TargetClicked", "RobotRecalled");
        }
        void Update()
        {
            if (Simulation == null || HelpOpen) return;
            bool dialogueInput = tutorial && tutorial.DialogueInputCaptured;
            var mouse = dialogueInput ? null : Mouse.current; var keyboard = dialogueInput ? null : Keyboard.current;
            bool tutorialActive = TutorialBlocksInput;
            // F1 works in a tutorial too (its dialogue-id jump is for the tutorial); the tutorial does not gate it.
            if (DeveloperTools && keyboard != null && keyboard.f1Key.wasPressedThisFrame) SetDeveloperMode(!DeveloperMode);
            if (!tutorialActive && keyboard != null && keyboard.tabKey.wasPressedThisFrame && !codeEditor.IsOpen)
            {
                if (DeveloperMode) ToggleDeveloper();
                CancelPlacement(); inventoryUI.Toggle();
            }
            if (!tutorialActive && keyboard != null && !DeveloperMode && !codeEditor.IsOpen && !inventoryUI.IsOpen)
            {
                if (keyboard.spaceKey.wasPressedThisFrame && !playerHUD.MenuOpen) { if (Simulation.Phase == BattlePhase.Ready) StartBattle(); else TogglePause(); }
            }
            // Esc first drops a robot being placed (outside the tutorial); otherwise it opens or closes the menu, the tutorial
            // included (play feedback: there was no way out of a lesson). The code editor and inventory use Esc themselves.
            var esc = Keyboard.current;
            if (esc != null && esc.escapeKey.wasPressedThisFrame && !DeveloperMode && !codeEditor.IsOpen && !inventoryUI.IsOpen)
            { if (!tutorialActive && (palette >= 0 || draggingRobot)) CancelPlacement(); else playerHUD.ToggleMenu(); }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && SpeedUnlocked && !DeveloperMode &&
                !codeEditor.IsOpen && !inventoryUI.IsOpen) ToggleSpeed();
            if (draggingRobot && mouse != null && mouse.leftButton.wasReleasedThisFrame) EndRobotDrag(mouse.position.ReadValue());
            if (mouse != null && !codeEditor.IsOpen && !inventoryUI.IsOpen && !DeveloperMode && !(devWindow && devWindow.HasPointer(mouse.position.ReadValue())))
            {
                if (!tutorialActive && mouse.rightButton.wasPressedThisFrame) CancelPlacement();
                if (!eventSystem.IsPointerOverGameObject() && PointerPosition(mouse.position.ReadValue(), out var point))
                {
                    if (palette >= 0)
                    {
                        var def = robots[palette]; string error = Simulation.PlacementError(def.stats, new Point(point.x, point.z));
                        bool tutorialAllowed = !tutorial || tutorial.AllowsWorldPoint(point);
                        placementMarker.gameObject.SetActive(true); placementMarker.position = point + Vector3.up * .08f;
                        rangeRing.Show(point, def.stats.range, error == null ? RangeColor : new Color(.55f,.55f,.58f));
                        if (mouse.leftButton.wasPressedThisFrame && tutorialAllowed) PlaceRobot(palette, point);
                    }
                    else if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var bot = RobotUnder(mouse.position.ReadValue());
                        if (bot != null && (!tutorial || tutorial.AllowsWorldPoint(DefenseViews.World(bot.Position)))) SelectRobot(bot.Id);
                        else if (!tutorialActive) { selected = 0; rangeRing.Hide(); }
                    }
                }
                else if (palette >= 0) { placementMarker.gameObject.SetActive(false); rangeRing.Hide(); }
            }
            double previous = Simulation.Time;
#if UNITY_EDITOR
            if (!EditorManualClock)
#endif
            if (bossEntranceStarted && !bossCameraRestored && Time.unscaledTime >= bossCameraRestoreAt)
            { selectionCamera.Restore(); bossCameraRestored = true; }
            if (!bossEntranceStarted || Time.unscaledTime >= bossEntranceUntil)
                Simulation.Advance(UnityEngine.Time.deltaTime * BattleSpeed * (tutorial ? tutorial.BattlePace : 1f));
            float elapsed = (float)(Simulation.Time - previous);
            SyncViews(elapsed);
            SyncTutorialTargets();
            hitEffects.Tick(Simulation.Phase == BattlePhase.Paused ? 0 :
                Simulation.Phase == BattlePhase.Running ? elapsed : UnityEngine.Time.deltaTime * BattleSpeed);
            foreach (var evt in Simulation.Events)
            {
                robotViews.TryGetValue(evt.Source, out var source);
                #if UNITY_EDITOR
                EditorObserve("Battle",Simulation,evt);
#endif
                hitEffects.Show(evt, actorsRoot, Vector3.zero, battleCamera.transform.rotation, source, effectRing);
                sounds.Play(evt.Kind);
                playerHUD.ShowCombatText(evt);
                if (evt.Kind == "drop") dropViews.Add(Instantiate(blockDropPrefab, DefenseViews.World(evt.Position, .35f), Quaternion.Euler(0, 45, 0), actorsRoot));
            }
            Simulation.Events.Clear();
            sounds.Follow(Simulation);
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) ClearCombatVisuals();
            var current = Simulation.FindRobot(selected);
            // A tutorial highlight (Robot.Range.N) shows that robot's range glowing, selected or not.
            var lit = tutorial ? TutorialWatcher.OrderedRobots(Simulation).Where((r, i) => tutorial.Highlighted(TutorialKeys.Range(i))).FirstOrDefault() : null;
            if (lit != null && palette < 0 && !codeEditor.IsOpen)
                rangeRing.Show(DefenseViews.World(lit.Position), lit.Range, RangeColor, .65f + .35f * Mathf.Sin(Time.unscaledTime * 5));
            else if (Simulation.Options.ranges && current != null && palette < 0 && !codeEditor.IsOpen && !inventoryUI.IsOpen && !DeveloperMode)
                rangeRing.Show(DefenseViews.World(current.Position), current.Range, RangeColor, 0);
            else if (palette < 0) rangeRing.Hide();
            if (current == null) selected = 0;
            RefreshHUD();
        }
        void CancelPlacement()
        { draggingRobot = false; HidePlacement(); rangeRing.Hide(); Tell("배치 취소 · 설치한 로봇을 클릭하면 상세 정보를 볼 수 있습니다."); }
        void HidePlacement() { palette = -1; placementMarker.gameObject.SetActive(false); }
        void ClearCombatVisuals()
        {
            effectRing.Hide();
            foreach (var view in robotViews.Values) view.ClearVisuals();
            foreach (var view in enemyViews.Values) view.ClearVisuals();
        }
        bool PointerPosition(Vector2 screen, out Vector3 point)
        {
            if (!battleCamera.pixelRect.Contains(screen)) { point = default; return false; }
            var ray = battleCamera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) { point = ray.GetPoint(distance); return true; }
            point = default; return false;
        }
        // Tutorial world targets: Robot.Unit.N / Robot.Range.N for each robot in id order, Battle.Route for the routes in use.
        // The robot whose body (a capsule from the ground to its head, seen on screen) is under the pointer, nearest first.
        RobotState RobotUnder(Vector2 screen)
        {
            RobotState best = null; float bestDistance = float.MaxValue;
            foreach (var bot in Simulation.Robots)
            {
                Vector2 foot = battleCamera.WorldToScreenPoint(DefenseViews.World(bot.Position));
                Vector2 head = battleCamera.WorldToScreenPoint(DefenseViews.World(bot.Position, RobotClickHeight));
                float radius = Vector2.Distance(foot, (Vector2)battleCamera.WorldToScreenPoint(DefenseViews.World(bot.Position + new Point(bot.Spec.radius + .5f, 0))));
                Vector2 axis = head - foot; float t = Mathf.Clamp01(Vector2.Dot(screen - foot, axis) / Mathf.Max(1, axis.sqrMagnitude));
                float distance = Vector2.Distance(screen, foot + axis * t);
                if (distance <= radius && distance < bestDistance) { best = bot; bestDistance = distance; }
            }
            return best;
        }
        // The robot models at scale 1.15 (CharacterAnimation): about 2.5-2.6 tall and 1.9 wide, standing on a .24 plate.
        public const float RobotHeight = 2.7f;
        const float RobotWidth = 2f, RobotClickHeight = RobotHeight;
        // The route's points with the last one pulled back one cell: the walk ends on the core, the painted road a cell before.
        static Point[] RoadPoints(Route path)
        {
            var points = path.Points.ToArray();
            if (points.Length < 2) return points;
            Point last = points[points.Length - 1], before = points[points.Length - 2];
            float cell = path.HalfWidth * 2, length = Point.Distance(before, last);
            if (length > cell) points[points.Length - 1] = last + (before - last) / length * cell;
            return points;
        }
        // The first route in use, entrance to the road's end, in world space (tutorial path arrows).
        public IEnumerable<Vector3> TutorialRoutePath()
        {
            if (Simulation == null) yield break;
            for (int i = 0; i < Simulation.Setup.Routes.Length; i++)
            {
                if (!Simulation.RouteInUse(i)) continue;
                foreach (var point in RoadPoints(Simulation.Setup.Routes[i])) yield return DefenseViews.World(point, .1f);
                yield break;
            }
        }
        void SyncTutorialTargets()
        {
            // Nothing points at robots, ranges, the road or the floor without a lesson (13차 135): this sorted the robots, built
            // road bounds and key strings every frame in every stage. A lesson that starts registers them on its first frame.
            if (!tutorial || !tutorial.HasSequence) return;
            var bots = TutorialWatcher.OrderedRobots(Simulation);
            for (int i = 0; i < bots.Count; i++)
            {
                if (!robotViews.TryGetValue(bots[i].Id, out var view) || !view) continue;
                float range = bots[i].Range;
                tutorial.RegisterWorldTarget(TutorialKeys.Unit(i), view.gameObject, RobotBounds(bots[i]));
                tutorial.RegisterWorldTarget(TutorialKeys.Range(i), view.gameObject,
                    new Bounds(DefenseViews.World(bots[i].Position), new Vector3(range * 2, .1f, range * 2)), true);
            }
            for (int i = bots.Count; i < tutorialUnits; i++)
            { tutorial.UnregisterRuntimeTarget(TutorialKeys.Unit(i), null); tutorial.UnregisterRuntimeTarget(TutorialKeys.Range(i), null); }
            tutorialUnits = bots.Count;
            var route = new List<Bounds>();
            // Clipped to the map, in case a route starts off it.
            var min = DefenseViews.World(Simulation.Setup.Min); var max = DefenseViews.World(Simulation.Setup.Max);
            for (int i = 0; i < Simulation.Setup.Routes.Length; i++)
            {
                if (!Simulation.RouteInUse(i)) continue;
                // The painted road is narrower than the walkable width; 1.25× the walkable width leaves a margin around it.
                var path = Simulation.Setup.Routes[i]; var width = Vector3.one * (path.HalfWidth * 2.5f);
                var points = RoadPoints(path);
                for (int p = 1; p < points.Length; p++)
                {
                    // One ground rect per straight segment; the spotlight shapes the road on the ground (TutorialInputBridge.AddRoad).
                    var b = new Bounds(DefenseViews.World(points[p - 1]), width);
                    b.Encapsulate(new Bounds(DefenseViews.World(points[p]), width));
                    b.SetMinMax(new Vector3(Mathf.Max(b.min.x, min.x), 0, Mathf.Max(b.min.z, min.z)), new Vector3(Mathf.Min(b.max.x, max.x), .1f, Mathf.Min(b.max.z, max.z)));
                    foreach (var end in routeEnds) b = OffModel(b, end);
                    if (b.size.x > 0 && b.size.z > 0) route.Add(b);
                }
            }
            if (route.Count > 0) tutorial.RegisterWorldTarget(TutorialKeys.Route, gameObject, route.ToArray());
            else tutorial.UnregisterRuntimeTarget(TutorialKeys.Route, null);
            // Battle.Floor: the painted floor cells, boxed as one (tighter on screen than the placement area's rect in perspective).
            if (floorParts == null)
            {
                var map = routes[0].Map;
                float size = map ? map.cellSize * map.transform.lossyScale.x : 0;
                floorParts = map ? map.floorCells.Select(c => new Bounds(map.CellPosition(c), new Vector3(size, .1f, size))).ToArray() : new[] { deploymentArea.bounds };
            }
            // 1-1's cell as two halves without box: drawn like the road, a quad on the ground in perspective.
            if (FirstLessonPlacement && firstSpotPart == null)
            {
                var cell = FirstSpotCell; var half = new Vector3(cell.size.x / 2, cell.size.y, cell.size.z);
                firstSpotPart = new[] { new Bounds(cell.center - Vector3.right * half.x / 2, half), new Bounds(cell.center + Vector3.right * half.x / 2, half) };
            }
            tutorial.RegisterWorldTarget(TutorialKeys.Floor, gameObject, FirstLessonPlacement ? firstSpotPart : floorParts, box: !FirstLessonPlacement);
        }
        void SyncViews(float elapsed)
        {
            if (coreGlitch) coreGlitch.Show(Simulation.BaseHealth, Simulation.Phase == BattlePhase.Defeat);
            if (Simulation.Options.projectiles) projectileViews.Sync(Simulation, projectilePrefab, actorsRoot, Vector3.zero);
            else projectileViews.Clear();
            // Views of robots and enemies that are gone, found with a set instead of a search per view (13차 134).
            liveIds.Clear(); foreach (var r in Simulation.Robots) liveIds.Add(r.Id);
            goneIds.Clear(); foreach (var id in robotViews.Keys) if (!liveIds.Contains(id)) goneIds.Add(id);
            foreach (var id in goneIds) { DefenseViews.Discard(robotViews[id]); robotViews.Remove(id); }
            liveIds.Clear(); foreach (var e in Simulation.Enemies) liveIds.Add(e.Id);
            goneIds.Clear(); foreach (var id in enemyViews.Keys) if (!liveIds.Contains(id)) goneIds.Add(id);
            foreach (var id in goneIds) { DefenseViews.Discard(enemyViews[id]); enemyViews.Remove(id); }
            // Animations follow the battle: still while paused (or held by the tutorial), faster at 2x/3x, normal outside a wave.
            float pace = Simulation.Phase == BattlePhase.Paused ? 0 : Simulation.Phase == BattlePhase.Running
                ? BattleSpeed * (tutorial ? tutorial.BattlePace : 1f) : 1;
            foreach (var bot in Simulation.Robots)
            {
                // A robot placed straight into the simulation (dev tools, tests) has no view; skip it instead of throwing every frame.
                if (!robotViews.TryGetValue(bot.Id, out var view)) continue;
                view.SyncRobot(bot, DefenseViews.World(bot.Position), battleCamera.transform.rotation); view.TickVisual(elapsed); view.SetPace(pace);
                if (healthBars && bot.Health.HasValue)
                    healthBars.Show(DefenseHealthBars.RobotKey(bot.Id), battleCamera, view.HealthAnchor, view.HealthWidth, bot.Health.Value, bot.MaxHealth);
            }
            foreach (var enemy in Simulation.Enemies)
            {
                if (!enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = Instantiate(enemies[enemy.DefinitionIndex].prefab, DefenseViews.World(enemy.Position), Quaternion.identity, actorsRoot);
                    if (enemy.Boss)
                    {
                        view.gameObject.AddComponent<DefenseBossAura>();
                        if (!bossEntranceStarted && Simulation.Setup.PlayerFlow && Simulation.Setup.Stage == 1 && Simulation.WaveIndex == 4)
                        {
                            bossEntranceStarted = true; bossCameraRestored = false;
                            bossCameraRestoreAt = Time.unscaledTime + 2.1f;
                            bossEntranceUntil = Time.unscaledTime + 2.9f;
                            selectionCamera.FocusBoss(DefenseViews.World(enemy.Position, 1f));
                            Tell("보스 출현! · 작은 오류 코드를 모두 막아 낸 뒤 강력한 오류 코드가 등장했습니다.");
                            BossWarning.Play(DefenseBossWarning.Style);
                        }
                    }
                    enemyViews.Add(enemy.Id, view);
                }
                view.SyncEnemy(enemy, Simulation.Time, DefenseViews.World(enemy.Position), battleCamera.transform.rotation);
                view.TickVisual(elapsed); view.SetPace(pace);
                if (healthBars) healthBars.Show(enemy.Id, battleCamera, view.HealthAnchor, view.HealthWidth, enemy.Health, enemy.Spec.health);
            }
            if (healthBars) healthBars.Flush();
        }
        string CompatibilityText(RobotState bot)
        {
            switch (Simulation.Compatibility(bot, bot.Action))
            {
                case ActionCompatibility.Native: return "전문 행동 · 정상 실행";
                case ActionCompatibility.Reduced: return !Rules.NeedsPermission(bot.Action) ? "비전문 행동 · 효율 10%" : "승인된 비전문 행동 · 낮은 효율";
                case ActionCompatibility.Ineffective: return "실행 무효 · 이 로봇에서는 " + ActionName(bot.Action) + "이 동작하지 않습니다.";
                default: return "호환 미정 · 효과를 실행하지 않습니다. 기획 확정 후 데이터에 등록하세요.";
            }
        }
        void RefreshHUD()
        {
            if (Simulation == null) return;
            bool ended = Simulation.Phase == BattlePhase.Victory || Simulation.Phase == BattlePhase.Defeat;
            if (ended) BattleSpeed = 1;
            for (int i = 0; i < routes.Length; i++) routes[i].ShowVisual(Simulation.RouteInUse(i));
            // The developer panel (F1) is hidden in play; its dozen formatted texts were built every frame anyway (13차 136).
            if (!DeveloperMode) { resultPanel.SetActive(ended); if (ended) HidePlacement(); return; }
            string phase = Simulation.Phase == BattlePhase.Ready ? "준비" : Simulation.Phase == BattlePhase.Running ? "전투 중" :
                Simulation.Phase == BattlePhase.Paused ? "일시정지" : Simulation.Phase == BattlePhase.Reward ? "보상 수령 대기 · F1 사용자 화면" : Simulation.Phase == BattlePhase.Victory ? "방어 성공" : "방어 실패";
            header.text = $"WAVE {Simulation.WaveIndex + 1:00} / {Simulation.Setup.Waves.Length:00}    |    CORE {Simulation.BaseHealth:00}    |    KILL {Simulation.Kills:00}  LEAK {Simulation.Leaks:00}";
            stateText.text = $"{phase}   ·   {Simulation.Time:0.0}s   ·   적 {Simulation.Enemies.Count}   ·   로봇 {Simulation.Robots.Count}/{Simulation.Setup.RobotLimit}";
            pauseLabel.text = Simulation.Phase == BattlePhase.Paused ? "재개" : "일시정지";
            speedLabel.text = BattleSpeed + "x";
            startButton.interactable = Simulation.Phase == BattlePhase.Ready;
            pauseButton.interactable = Simulation.Phase == BattlePhase.Running || Simulation.Phase == BattlePhase.Paused;
            speedButton.interactable = SpeedUnlocked &&
                (Simulation.Phase == BattlePhase.Running || Simulation.Phase == BattlePhase.Paused);
            for (int i = 0; i < robotButtons.Length; i++)
                developerRobotLabels[i].text = robots[i].displayName + "   ×" + Simulation.RobotAvailable(robots[i].stats.role);
            var bot = Simulation.FindRobot(selected);
            removeButton.interactable = bot != null && Simulation.CanPrepare;
            if (bot != null)
            {
                string health = bot.Health.HasValue ? $"HP {bot.Health:0}/{bot.MaxHealth:0} · 저지 {Simulation.Enemies.Count(e => e.BlockedBy == bot.Id)}/{bot.Spec.blockCapacity}" : "체력 없음 · 적 피해 대상 제외";
                string control = bot.Program == null || bot.Program.IsEmpty ? "빈 로봇 · 코드 필요" : bot.Program.Fault == null ? "코드: " + bot.Program.Name : "코드 중지: " + bot.Program.Fault;
                if (bot.Program != null && !bot.Program.IsEmpty) control += bot.Program.Running ? " · 실행 중" : $" · 실행 {bot.Program.CompletedRuns}번 끝";
                string attacks = $"공격 {bot.AttacksThisWave}회";
                selectionText.text = $"{robots[bot.DefinitionIndex].displayName}  #{bot.Id}\n\n{health}\n사거리 {bot.Range:0.00}    피해 {Simulation.ActionDamage(bot, bot.Action):0.0}\n공격 간격 {Simulation.ActionInterval(bot, bot.Action):0.00}s\n\n{ActionName(bot.Action)}\n{CompatibilityText(bot)}\n{control}\n실행 {bot.Executions}회 · {attacks} · 버프 {bot.Buffs.Count}개";
            }
            else if (palette >= 0) selectionText.text = robots[palette].displayName + "\n\n자유 배치 중\n초록: 설치 가능\n빨강: 설치 불가\n\n탱커: 경로 안\n일반 로봇: 경로 밖\n\n클릭 설치 / 우클릭 취소";
            else selectionText.text = "로봇 클릭: 코드 편집\nTAB 인벤토리\nSPACE 시작 / 정지";
            resultPanel.SetActive(ended);
            if (ended)
            {
                HidePlacement();
                resultText.text = (Simulation.Phase == BattlePhase.Victory ? "DEFENSE COMPLETE\n방어 성공" :
                    Simulation.ObjectiveFailed ? "MINIONS ESCAPED\n보스 등장 전 작은 오류 코드 방어 실패" : "CORE LOST\n방어 실패") +
                    $"\n\n처치 {Simulation.Kills} · 돌파 {Simulation.Leaks}\n상단 초기화로 다시 시작";
            }
        }
        void RejectPlacement(int index, string error)
        {
            Tell($"{robots[index].displayName} · {error}");
            playerHUD.ShowPlacementError(error);
            sounds.Play("deny");
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string text) => Debug.Log($"[Defense] S{stage} W{Simulation.WaveIndex + 1} {Simulation.Phase} robots={Simulation.Robots.Count}/{Simulation.Setup.RobotLimit} core={Simulation.BaseHealth} | {text}", this);
        void Tell(string text) { StatusMessage = text; message.text = text; Trace(text); }
        // One shared block inventory: "Slash 2 (free 1 · robot3 1)" = owned, left for a new robot, held by each robot.
        string lastInventory;
        string InventoryLine() => "inventory " + string.Join(", ", Simulation.Inventory.OrderBy(p => p.Key).Select(p =>
            $"{p.Key} {p.Value} (free {Simulation.Available(p.Key)}" + string.Concat(Simulation.Robots.Select(r => (r, n: Simulation.Reserved(r, p.Key)))
                .Where(x => x.n > 0).Select(x => $" · robot{x.r.Id} {x.n}")) + ")"));
        static string ActionName(RobotAction action) => DefenseProgression.Label(DefenseProgression.ActionBlock(action));
    }
}
