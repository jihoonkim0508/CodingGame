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
        }
        [Serializable] public sealed class Wave { public SpawnEntry[] groups = Array.Empty<SpawnEntry>(); }

        [SerializeField] DefenseCampaignDefinition campaign;
        [Header("Authored scene references")]
        [SerializeField] Camera battleCamera;
        [SerializeField] EventSystem eventSystem;
        [SerializeField] DefenseCodeEditor codeEditor;
        [SerializeField] BoxCollider deploymentArea;
        [SerializeField] Collider[] blockedAreas = Array.Empty<Collider>();
        [SerializeField] Transform actorsRoot;
        [SerializeField] DefenseRoute[] routes = Array.Empty<DefenseRoute>();
        [Tooltip("Moved onto the painted map's base cell when the routes come from a map.")]
        [SerializeField] Transform core;
        DefenseCoreGlitch coreGlitch;
        static readonly Color RangeColor = new Color(.35f, .9f, 1); // attack ranges: a MOBA-like cyan rim; the tutorial highlight pulses red (DefenseRangeRing)
        Bounds[] floorParts;
        [SerializeField] RobotDefinition[] robots = Array.Empty<RobotDefinition>();
        [SerializeField] EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        [SerializeField] DefenseRangeRing rangeRing;
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField] Transform placementMarker;
        [SerializeField] Transform blockDropPrefab;
        [SerializeField] Transform projectilePrefab;
        [SerializeField] DefenseHitVisuals hitEffects = new DefenseHitVisuals();
        [SerializeField] GameObject developerRoot;
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
        readonly Dictionary<int, DefenseActorView> enemyViews = new Dictionary<int, DefenseActorView>();
        readonly List<Transform> dropViews = new List<Transform>();
        readonly DefenseProjectileVisuals projectileViews = new DefenseProjectileVisuals();
        public bool DeveloperMode { get; private set; }
        public bool ShowHitRanges { get; private set; }
        public bool HelpOpen { get; internal set; }
        int palette = -1, selected;
        bool draggingRobot;
        public DefenseSimulation Simulation { get; private set; }
        public int SelectedId => selected;
        public bool IsPlacing => palette >= 0;
        public float BattleSpeed { get; private set; } = 1;
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
        public string RobotName(int id)
        { var bot = Simulation.FindRobot(id); return bot == null ? "로봇 없음" : robots[bot.DefinitionIndex].displayName; }

        void Start()
        {
            if (!campaign) throw new InvalidOperationException("DefenseBattle에 캠페인 asset을 연결하세요.");
            campaign.ValidateContent(); robots = campaign.robots; enemies = campaign.enemies;
            ValidateReferences();
            developerRoot.SetActive(false);
            startButton.onClick.AddListener(StartBattle);
            pauseButton.onClick.AddListener(TogglePause);
            speedButton.onClick.AddListener(ToggleSpeed);
            resetButton.onClick.AddListener(Restart);
            hitRangeButton.onClick.AddListener(ToggleHitRanges);
            hitRangeLabel.text = "근접 피격범위: OFF";
            removeButton.onClick.AddListener(RemoveSelected);
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => inventoryUI.SelectRobotItem(index)); }
            if (FromStageMap) stage = StageMap.StageProgress.SelectedStage;
            if (core) core.TryGetComponent(out coreGlitch);
            Restart();
        }
        void ValidateReferences()
        {
            if (!hitEffects.IsConfigured) throw new InvalidOperationException("피격 이펙트 프리팹을 Inspector에 연결하세요.");
            if (!selectionCamera || !inventoryUI || !attackPreview) throw new InvalidOperationException("선택 카메라·인벤토리·미리보기 참조를 연결하세요.");
            if (!hitRangeButton || !hitRangeLabel || !projectilePrefab || !blockDropPrefab || !developerRoot || !playerHUD || !battleCamera || !eventSystem || !codeEditor || !deploymentArea || !actorsRoot || !rangeRing || !effectRing || !placementMarker ||
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
                Waves=content.ReadWaves(selectedStage),
                CampaignWaves=Enumerable.Range(1,content.stages.Length).Select(content.ReadWaves).ToArray(),
                CampaignSeconds=content.stages.Select(s=>s.waveSeconds).ToArray()
            };
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
            selected = 0; BattleSpeed = 1; HidePlacement();
            rangeRing.Hide(); effectRing.Hide(); resultPanel.SetActive(false);
            Tell("로봇을 선택하고 허용 영역을 클릭하세요. 탱커만 경로 위에 설치합니다.");
            RefreshHUD();
        }
        public void StartBattle()
        {
            if (!TutorialAllows("Battle.Start")) return;
            string error = Simulation.StartError();
            if (error != null) { Tell(error); return; }
            HidePlacement();
            Trace("wave-start " + string.Join(", ", Simulation.UpcomingWave.Select(g => $"{enemies[g.DefinitionIndex].name} x{g.Count} HP={g.Enemy.health:0.0} speed={g.Enemy.speed:0.00}")));
            Simulation.Start(); Tell("전투 중 · 웨이브 종료 후 지정 보상을 수령하세요."); RefreshHUD();
            ReportTutorialAction("Battle.Start", "TargetClicked", "BattleStarted");
        }
        public void ClaimRewards()
        {
            if (!TutorialAllows("Battle.ClaimRewards") && !(Simulation.Phase == BattlePhase.Reward && TutorialAllows("Battle.ContinueCampaign"))) return;
            if (Simulation.Phase == BattlePhase.Reward) Trace($"wave-end kills={Simulation.Kills} leaks={Simulation.Leaks} survivors={Simulation.LastSurvivors} rewards={Simulation.PendingDrops.Count}");
            if (!Simulation.ClaimRewards()) return;
            selected = 0; rangeRing.Hide(); ClearCombatVisuals();
            ClearDrops(); Tell(Simulation.Phase == BattlePhase.Victory ? Simulation.CanAdvanceStage ?
                "스테이지 완료 · 다음 스테이지로 진행하세요." : "모든 스테이지를 완료했습니다." :
                "보상을 수령했습니다. 로봇과 코드를 정비하고 다음 전투를 시작하세요."); RefreshHUD();
            ReportTutorialAction("Battle.ClaimRewards", "TargetClicked", "RewardsClaimed");
            ReportTutorialAction("Battle.ContinueCampaign", "TargetClicked", "RewardsClaimed");
        }
        void ClearDrops() { foreach (var drop in dropViews) if (drop) Destroy(drop.gameObject); dropViews.Clear(); }
        public void ContinueCampaign()
        {
            if (Simulation.Phase == BattlePhase.Reward) { ClaimRewards(); return; }
            if (Simulation.Phase == BattlePhase.Defeat) { Restart(); return; }
            if (!TutorialAllows("Battle.ContinueCampaign")) return;
            if (FromStageMap)
            {
                StageMap.StageProgress.MarkCleared(stage);
                GameFlow.LoadScene(GameFlow.StageScene);
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
            ReportTutorialAction("Editor.ApplyProgram", "TargetClicked", "ProgramApplied");
        }
        public bool TutorialAllows(string key) => !tutorial || tutorial.CanPerformAction(key);
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
        public void TogglePause()
        {
            if (!TutorialAllows("Battle.TogglePause")) return;
            Simulation.TogglePause(); RefreshHUD(); ReportTutorialAction("Battle.TogglePause", "TargetClicked");
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
            if (!TutorialAllows("Battle.ToggleSpeed")) return;
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) return;
            BattleSpeed = BattleSpeed >= 3 ? 1 : BattleSpeed + 1; RefreshHUD();
            ReportTutorialAction("Battle.ToggleSpeed", "TargetClicked");
        }
        public void ChooseRobot(int index)
        {
            string tutorialKey = "Battle.Robot." + index;
            if (!TutorialAllows(tutorialKey)) return;
            if (!Simulation.CanPrepare || index < 0 || index >= robots.Length) return;
            string error = Simulation.DeploymentError(robots[index].stats.role);
            if (error != null) { palette = -1; RejectPlacement(index, error); return; }
            palette = index; selected = 0;
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
            if (index >= 0 && hits.Count == 0 && PointerPosition(screen, out var point)) PlaceRobot(index, point);
            HidePlacement(); rangeRing.Hide();
        }
        public bool PlaceRobot(int index, Vector3 position)
        {
            if (index < 0 || index >= robots.Length ||
                tutorial && (!tutorial.AllowsWorldPoint(position) || !tutorial.AllowsRobotChoice(index))) return false;
            var bot = Simulation.Place(robots[index].stats, index, new Point(position.x, position.z), out var error);
            if (bot == null) { RejectPlacement(index, error); return false; }
            var view = Instantiate(robots[index].prefab, DefenseViews.World(bot.Position), Quaternion.identity, actorsRoot);
            robotViews.Add(bot.Id, view);
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
            string unit = TutorialKeys.Unit(Simulation.Robots.OrderBy(r => r.Id).ToList().FindIndex(r => r.Id == id));
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
        // A robot's clickable body as a tutorial target.
        static Bounds RobotBounds(RobotState robot)
        {
            float radius = robot.Spec.radius;
            return new Bounds(DefenseViews.World(robot.Position, .65f), new Vector3(radius * 2.5f, 1.5f, radius * 2.5f));
        }
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
            if (error != null) { Tell(error); return; }
            if (Simulation.UpgradeRobot(selected))
            {
                Tell(RobotName(selected) + " · 레벨업 완료");
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
            SetPlacementTint(palette >= 0);
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            bool tutorialActive = TutorialBlocksInput;
            // Keep F1 and offer F2 when a recording overlay intercepts the help key.
            if (!tutorialActive && keyboard != null && (keyboard.f1Key.wasPressedThisFrame || keyboard.f2Key.wasPressedThisFrame)) ToggleDeveloper();
            if (!tutorialActive && keyboard != null && keyboard.tabKey.wasPressedThisFrame && !codeEditor.IsOpen)
            {
                if (DeveloperMode) ToggleDeveloper();
                CancelPlacement(); inventoryUI.Toggle();
            }
            if (!tutorialActive && keyboard != null && !DeveloperMode && !codeEditor.IsOpen && !inventoryUI.IsOpen)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) { if (Simulation.Phase == BattlePhase.Ready) StartBattle(); else TogglePause(); }
                if (keyboard.fKey.wasPressedThisFrame) ToggleSpeed();
                if (keyboard.escapeKey.wasPressedThisFrame) CancelPlacement();
            }
            if (draggingRobot && mouse != null && mouse.leftButton.wasReleasedThisFrame) EndRobotDrag(mouse.position.ReadValue());
            if (mouse != null && !codeEditor.IsOpen && !inventoryUI.IsOpen && !DeveloperMode)
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
            Simulation.Advance(tutorial && tutorial.PauseBattle ? 0 : UnityEngine.Time.deltaTime * BattleSpeed);
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
                if (evt.Kind == "drop") dropViews.Add(Instantiate(blockDropPrefab, DefenseViews.World(evt.Position, .35f), Quaternion.Euler(0, 45, 0), actorsRoot));
            }
            Simulation.Events.Clear();
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) ClearCombatVisuals();
            var current = Simulation.FindRobot(selected);
            // A tutorial highlight (Robot.Range.N) shows that robot's range glowing, selected or not.
            var lit = tutorial ? Simulation.Robots.OrderBy(r => r.Id).Where((r, i) => tutorial.Highlighted(TutorialKeys.Range(i))).FirstOrDefault() : null;
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
        const float RobotClickHeight = 1.8f;
        // While a robot is being placed the map shows where it may go: floor white, road red. Restored when placing ends.
        bool placementTint;
        (Color upper, Color inner, Color border, bool floors, bool roads) mapColors;
        void SetPlacementTint(bool on)
        {
            var map = routes.Length > 0 ? routes[0].Map : null;
            if (on == placementTint || !map) return;
            placementTint = on;
            if (on)
            {
                mapColors = (map.upperColor, map.roadInnerColor, map.roadBorderColor, map.useFloorColors, map.useRoadColors);
                map.upperColor = Color.white; map.roadInnerColor = new Color(.95f, .27f, .27f); map.roadBorderColor = new Color(.7f, .12f, .12f);
                map.useFloorColors = map.useRoadColors = true;
            }
            else
            {
                map.upperColor = mapColors.upper; map.roadInnerColor = mapColors.inner; map.roadBorderColor = mapColors.border;
                map.useFloorColors = mapColors.floors; map.useRoadColors = mapColors.roads;
            }
            map.ApplyFloorColors();
        }
        void OnDisable() => SetPlacementTint(false);
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
            if (!tutorial) return;
            var bots = Simulation.Robots.OrderBy(r => r.Id).ToList();
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
            tutorial.RegisterWorldTarget(TutorialKeys.Floor, gameObject, floorParts, box: true);
        }
        void SyncViews(float elapsed)
        {
            if (coreGlitch) coreGlitch.Show(Simulation.BaseHealth, Simulation.Phase == BattlePhase.Defeat);
            if (Simulation.Options.projectiles) projectileViews.Sync(Simulation, projectilePrefab, actorsRoot, Vector3.zero);
            else projectileViews.Clear();
            foreach (var id in robotViews.Keys.Where(id => !Simulation.Robots.Any(r => r.Id == id)).ToArray())
            { DefenseViews.Discard(robotViews[id]); robotViews.Remove(id); }
            foreach (var id in enemyViews.Keys.Where(id => !Simulation.Enemies.Any(e => e.Id == id)).ToArray())
            { DefenseViews.Discard(enemyViews[id]); enemyViews.Remove(id); }
            // Animations follow the battle: still while paused (or held by the tutorial), faster at 2x/3x, normal outside a wave.
            bool held = Simulation.Phase == BattlePhase.Paused || Simulation.Phase == BattlePhase.Running && tutorial && tutorial.PauseBattle;
            float pace = held ? 0 : Simulation.Phase == BattlePhase.Running ? BattleSpeed : 1;
            foreach (var bot in Simulation.Robots)
            {
                var view = robotViews[bot.Id];
                view.SyncRobot(bot, DefenseViews.World(bot.Position), battleCamera.transform.rotation); view.TickVisual(elapsed); view.SetPace(pace);
            }
            foreach (var enemy in Simulation.Enemies)
            {
                if (!enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = Instantiate(enemies[enemy.DefinitionIndex].prefab, DefenseViews.World(enemy.Position), Quaternion.identity, actorsRoot);
                    enemyViews.Add(enemy.Id, view);
                }
                view.SyncEnemy(enemy, Simulation.Time, DefenseViews.World(enemy.Position), battleCamera.transform.rotation);
                view.TickVisual(elapsed); view.SetPace(pace);
            }
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
            string phase = Simulation.Phase == BattlePhase.Ready ? "준비" : Simulation.Phase == BattlePhase.Running ? "전투 중" :
                Simulation.Phase == BattlePhase.Paused ? "일시정지" : Simulation.Phase == BattlePhase.Reward ? "보상 수령 대기 · F1/F2 사용자 화면" : Simulation.Phase == BattlePhase.Victory ? "방어 성공" : "방어 실패";
            header.text = $"WAVE {Simulation.WaveIndex + 1:00} / {Simulation.Setup.Waves.Length:00}    |    CORE {Simulation.BaseHealth:00}    |    KILL {Simulation.Kills:00}  LEAK {Simulation.Leaks:00}";
            stateText.text = $"{phase}   ·   {Simulation.Time:0.0}s   ·   적 {Simulation.Enemies.Count}   ·   로봇 {Simulation.Robots.Count}/{Simulation.Setup.RobotLimit}";
            pauseLabel.text = Simulation.Phase == BattlePhase.Paused ? "재개" : "일시정지";
            speedLabel.text = BattleSpeed + "x";
            startButton.interactable = Simulation.Phase == BattlePhase.Ready;
            pauseButton.interactable = Simulation.Phase == BattlePhase.Running || Simulation.Phase == BattlePhase.Paused;
            speedButton.interactable = !ended;
            for (int i = 0; i < routes.Length; i++) routes[i].ShowVisual(Simulation.RouteInUse(i));
            for (int i = 0; i < robotButtons.Length; i++)
                developerRobotLabels[i].text = robots[i].displayName + "   ×" + Simulation.RobotAvailable(robots[i].stats.role);
            var bot = Simulation.FindRobot(selected);
            removeButton.interactable = bot != null && Simulation.CanPrepare;
            if (bot != null)
            {
                string health = bot.Health.HasValue ? $"HP {bot.Health:0}/{bot.MaxHealth:0} · 저지 {Simulation.Enemies.Count(e => e.BlockedBy == bot.Id)}/{bot.Spec.blockCapacity}" : "체력 없음 · 적 피해 대상 제외";
                string control = bot.Program == null || bot.Program.IsEmpty ? "빈 로봇 · 코드 필요" : bot.Program.Fault == null ? "코드: " + bot.Program.Name : "코드 중지: " + bot.Program.Fault;
                if (bot.Program != null && !bot.Program.IsEmpty) control += $" · 함수 {bot.Program.CompletedRuns}/{Simulation.FunctionRepeatLimit}회";
                string attacks = $"공격 {bot.AttacksThisWave}회";
                selectionText.text = $"{robots[bot.DefinitionIndex].displayName}  #{bot.Id}\n\n{health}\n사거리 {bot.Range:0.00}    피해 {Simulation.ActionDamage(bot, bot.Action):0.0}\n공격 간격 {Simulation.ActionInterval(bot, bot.Action):0.00}s\n\n{ActionName(bot.Action)}\n{CompatibilityText(bot)}\n{control}\n실행 {bot.Executions}회 · {attacks} · 버프 {bot.Buffs.Count}개";
            }
            else if (palette >= 0) selectionText.text = robots[palette].displayName + "\n\n자유 배치 중\n초록: 설치 가능\n빨강: 설치 불가\n\n탱커: 경로 안\n일반 로봇: 경로 밖\n\n클릭 설치 / 우클릭 취소";
            else selectionText.text = "로봇 클릭: 코드 편집\nTAB 인벤토리\nSPACE 시작 / 정지";
            resultPanel.SetActive(ended);
            if (ended)
            {
                HidePlacement();
                resultText.text = (Simulation.Phase == BattlePhase.Victory ? "DEFENSE COMPLETE\n방어 성공" : "CORE LOST\n방어 실패") + $"\n\n처치 {Simulation.Kills} · 돌파 {Simulation.Leaks}\n상단 초기화로 다시 시작";
            }
        }
        void RejectPlacement(int index, string error)
        {
            Tell($"{robots[index].displayName} · {error}");
            playerHUD.ShowPlacementError(error);
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string text) => Debug.Log($"[Defense] S{stage} W{Simulation.WaveIndex + 1} {Simulation.Phase} robots={Simulation.Robots.Count}/{Simulation.Setup.RobotLimit} core={Simulation.BaseHealth} | {text}", this);
        void Tell(string text) { StatusMessage = text; message.text = text; Trace(text); }
        static string ActionName(RobotAction action) => DefenseProgression.Label(DefenseProgression.ActionBlock(action));
    }
}
