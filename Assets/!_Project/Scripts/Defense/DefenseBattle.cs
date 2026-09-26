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
    public sealed class DefenseBattle : MonoBehaviour
    {
        [Serializable] public sealed class SpawnEntry
        {
            public EnemyDefinition enemy;
            public int route, count = 5;
            public float delay, interval = 1.5f;
        }
        [Serializable] public sealed class Wave { public SpawnEntry[] groups = Array.Empty<SpawnEntry>(); }

        [Header("Authored scene references")]
        [SerializeField] Camera battleCamera;
        [SerializeField] EventSystem eventSystem;
        [SerializeField] DefenseCodeEditor codeEditor;
        [SerializeField] BoxCollider deploymentArea;
        [SerializeField] Collider[] blockedAreas = Array.Empty<Collider>();
        [SerializeField] Transform actorsRoot;
        [SerializeField] DefenseRoute[] routes = Array.Empty<DefenseRoute>();
        [SerializeField] RobotDefinition[] robots = Array.Empty<RobotDefinition>();
        [SerializeField] EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        [SerializeField] DefenseRangeRing rangeRing;
        [SerializeField] DefenseRangeRing effectRing;
        [SerializeField] Transform placementMarker;
        [SerializeField] Transform blockDropPrefab;
        [SerializeField] Transform projectilePrefab;
        [SerializeField] DefenseProgression progression = new DefenseProgression();
        [SerializeField] GameObject developerRoot;
        [SerializeField] DefensePlayerHUD playerHUD;
        [SerializeField] DefenseSelectionCamera selectionCamera;
        [SerializeField] DefenseInventoryUI inventoryUI;
        [SerializeField] DefenseAttackPreview attackPreview;
        [SerializeField, Min(.1f)] float waveSeconds = 35;
        [SerializeField, Min(1)] int stage = 1;
        public int Stage => stage;
        public RobotDefinition RobotDefinition(int index) => robots[index];
        public EnemyDefinition PreviewEnemy => enemies[0];
        public string EnemyName(int index) => enemies[index].displayName;
        public string EnemyDescription(int index) => enemies[index].description;
        [Header("Test content — provisional values")]
        [SerializeField] Wave[] waves = Array.Empty<Wave>();
        [SerializeField] int baseHealth = 12, robotLimit = 18;
        [SerializeField] ActionPermission[] approvedCrossRoleActions = Array.Empty<ActionPermission>();
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
        int palette = -1, selected;
        bool draggingRobot;
        float speed = 1, effectTime;
        string shownMessage;
        public DefenseSimulation Simulation { get; private set; }
        public int SelectedId => selected;
        public float BattleSpeed => speed;
        public string StatusMessage => shownMessage;
        public event Action<int> RobotSelected;
        public string DefinitionName(int index) => robots[index].displayName;
        public RobotRole DefinitionRole(int index) => robots[index].stats.role;
        public void BeginCodeView(RobotState robot)
        {
            inventoryUI.Close(); playerHUD.SetVisible(false); developerRoot.SetActive(false);
            selectionCamera.Focus(World(robot.Position));
            attackPreview.Open(robot);
        }
        public void EndCodeView()
        {
            selectionCamera.Restore(); playerHUD.SetVisible(!DeveloperMode); developerRoot.SetActive(DeveloperMode);
            attackPreview.Close();
        }
        public string RobotName(int id)
        { var bot = Simulation.Robots.Find(r => r.Id == id); return bot == null ? "로봇 없음" : robots[bot.DefinitionIndex].displayName; }

        void Start()
        {
            ValidateReferences();
            developerRoot.SetActive(false);
            startButton.onClick.AddListener(StartBattle);
            pauseButton.onClick.AddListener(TogglePause);
            speedButton.onClick.AddListener(ToggleSpeed);
            resetButton.onClick.AddListener(Restart);
            removeButton.onClick.AddListener(RemoveSelected);
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => inventoryUI.SelectRobotItem(index)); }
            Restart();
        }
        void ValidateReferences()
        {
            if (!selectionCamera || !inventoryUI || !attackPreview) throw new InvalidOperationException("선택 카메라·인벤토리·미리보기 참조를 연결하세요.");
            if (!projectilePrefab || !blockDropPrefab || !developerRoot || !playerHUD || !battleCamera || !eventSystem || !codeEditor || !deploymentArea || !actorsRoot || !rangeRing || !effectRing || !placementMarker ||
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
        BattleSetup MakeSetup()
        {
            var bounds = deploymentArea.bounds;
            return new BattleSetup { PlayerFlow = true, WaveSeconds = waveSeconds, Progression = progression, DropSeed = Environment.TickCount,
                Min = new Point(bounds.min.x, bounds.min.z), Max = new Point(bounds.max.x, bounds.max.z),
                Routes = routes.Select(r => r.Read()).ToArray(), BaseHealth = baseHealth, RobotLimit = robotLimit,
                ActionProfiles = robots.GroupBy(r => r.stats.role).Select(g => g.First().stats.Copy()).ToArray(),
                Obstacles = blockedAreas.Select(area => new PlacementObstacle {
                    Min = new Point(area.bounds.min.x, area.bounds.min.z), Max = new Point(area.bounds.max.x, area.bounds.max.z) }).ToArray(),
                Permissions = approvedCrossRoleActions.Select(p => new ActionPermission { role = p.role, action = p.action, strength = p.strength }).ToArray(),
                Waves = waves.Select(w => w.groups.Select(g => {
                    if (!g.enemy || Array.IndexOf(enemies, g.enemy) < 0) throw new InvalidOperationException("스폰 적을 등록된 Enemy 목록에 연결하세요.");
                    return new SpawnGroup { Enemy = g.enemy.stats.Copy(), DefinitionIndex = Array.IndexOf(enemies, g.enemy),
                        RouteIndex = g.route, Count = g.count, Delay = g.delay, Interval = g.interval };
                }).ToArray()).ToArray() };
        }
        public void Restart()
        {
            inventoryUI.Close();
            codeEditor.Close(false);
            foreach (var view in robotViews.Values) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            foreach (var view in enemyViews.Values) { view.gameObject.SetActive(false); Destroy(view.gameObject); }
            robotViews.Clear(); enemyViews.Clear();
            ClearDrops();
            projectileViews.Clear();
            Simulation = new DefenseSimulation(MakeSetup());
            selected = 0; palette = -1; speed = 1; effectTime = 0;
            placementMarker.gameObject.SetActive(false); rangeRing.Hide(); effectRing.Hide(); resultPanel.SetActive(false);
            Tell("로봇을 선택하고 허용 영역을 클릭하세요. 탱커만 경로 위에 설치합니다.");
            RefreshHUD();
        }
        public void StartBattle()
        {
            string error = Simulation.StartError();
            if (error != null) { Tell(error); return; }
            palette = -1; placementMarker.gameObject.SetActive(false);
            Simulation.Start(); Tell("전투 중 · 처치한 적이 블록을 드랍합니다. 웨이브 종료 후 수령하세요."); RefreshHUD();
        }
        public void ClaimRewards()
        {
            if (!Simulation.ClaimRewards()) return;
            selected = 0; rangeRing.Hide(); ClearCombatVisuals();
            ClearDrops(); Tell("보상을 수령했습니다. 로봇과 코드를 정비하고 다음 전투를 시작하세요."); RefreshHUD();
        }
        void ClearDrops() { foreach (var drop in dropViews) if (drop) Destroy(drop.gameObject); dropViews.Clear(); }
        public void NotifyCodeApplied(int id, bool empty)
            => Tell(RobotName(id) + (empty ? " · 코드 없음" : " · 적용 완료"));
        public void ToggleDeveloper()
        {
            inventoryUI.Close(); codeEditor.Close(); CancelPlacement(); DeveloperMode = !DeveloperMode;
            developerRoot.SetActive(DeveloperMode); playerHUD.SetVisible(!DeveloperMode);
        }
        public void TogglePause() { Simulation.TogglePause(); RefreshHUD(); }
        public void ToggleSpeed()
        {
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) return;
            speed = speed == 1 ? 2 : 1; RefreshHUD();
        }
        public void ChooseRobot(int index)
        {
            if (!Simulation.CanPrepare || index < 0 || index >= robots.Length) return;
            if (Simulation.RobotAvailable(robots[index].stats.role) == 0) { Tell("보유 로봇이 없습니다."); return; }
            palette = index; selected = 0;
            Tell(robots[index].displayName + " 배치 · 허용 위치 클릭 / 우클릭 또는 ESC 취소");
            RefreshHUD();
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
            palette = -1; placementMarker.gameObject.SetActive(false); rangeRing.Hide();
        }
        public bool PlaceRobot(int index, Vector3 position)
        {
            if (index < 0 || index >= robots.Length) return false;
            var bot = Simulation.Place(robots[index].stats, index, new Point(position.x, position.z), out var error);
            if (bot == null) { Tell(error); return false; }
            var view = Instantiate(robots[index].prefab, World(bot.Position), Quaternion.identity, actorsRoot);
            view.Id = bot.Id; robotViews.Add(bot.Id, view);
            selected = bot.Id; palette = -1; placementMarker.gameObject.SetActive(false);
            Tell(robots[index].displayName + " 배치 완료 · 클릭하여 코드 작성");
            SyncViews(0); RefreshHUD(); return true;
        }
        public void SelectRobot(int id) { selected = id; palette = -1; placementMarker.gameObject.SetActive(false); RefreshHUD(); RobotSelected?.Invoke(id); }
        public void RemoveSelected()
        {
            if (!Simulation.CanPrepare) return;
            codeEditor.Close();
            Simulation.RemoveRobot(selected); selected = 0; rangeRing.Hide();
            Tell("로봇과 사용 블록을 인벤토리로 회수했습니다."); SyncViews(0); RefreshHUD();
        }
        void Update()
        {
            if (Simulation == null) return;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            // Keep F1 and offer F2 when a recording overlay intercepts the help key.
            if (keyboard != null && (keyboard.f1Key.wasPressedThisFrame || keyboard.f2Key.wasPressedThisFrame)) ToggleDeveloper();
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && !codeEditor.IsOpen)
            {
                if (DeveloperMode) ToggleDeveloper();
                CancelPlacement(); inventoryUI.Toggle();
            }
            if (keyboard != null && !DeveloperMode && !codeEditor.IsOpen && !inventoryUI.IsOpen)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) { if (Simulation.Phase == BattlePhase.Ready) StartBattle(); else TogglePause(); }
                if (keyboard.fKey.wasPressedThisFrame) ToggleSpeed();
                if (keyboard.escapeKey.wasPressedThisFrame) CancelPlacement();
            }
            if (draggingRobot && mouse != null && mouse.leftButton.wasReleasedThisFrame) EndRobotDrag(mouse.position.ReadValue());
            if (mouse != null && !codeEditor.IsOpen && !inventoryUI.IsOpen && !DeveloperMode)
            {
                if (mouse.rightButton.wasPressedThisFrame) CancelPlacement();
                if (!eventSystem.IsPointerOverGameObject() && PointerPosition(mouse.position.ReadValue(), out var point))
                {
                    if (palette >= 0)
                    {
                        var def = robots[palette]; string error = Simulation.PlacementError(def.stats, new Point(point.x, point.z));
                        placementMarker.gameObject.SetActive(true); placementMarker.position = point + Vector3.up * .08f;
                        rangeRing.Show(point, def.stats.range, error == null ? new Color(.25f,1,.7f) : new Color(1,.3f,.32f));
                        if (mouse.leftButton.wasPressedThisFrame) PlaceRobot(palette, point);
                    }
                    else if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var bot = Simulation.Robots.OrderBy(r => Point.DistanceSquared(r.Position, new Point(point.x, point.z))).FirstOrDefault();
                        if (bot != null && Point.Distance(bot.Position, new Point(point.x, point.z)) <= bot.Spec.radius + .5f) SelectRobot(bot.Id);
                        else { selected = 0; rangeRing.Hide(); }
                    }
                }
                else if (palette >= 0) { placementMarker.gameObject.SetActive(false); rangeRing.Hide(); }
            }
            double previous = Simulation.Time;
            Simulation.Advance(UnityEngine.Time.deltaTime * speed);
            float elapsed = (float)(Simulation.Time - previous);
            SyncViews(elapsed);
            foreach (var evt in Simulation.Events)
            {
                if (evt.Kind == "drop") dropViews.Add(Instantiate(blockDropPrefab, World(evt.Position) + Vector3.up * .35f, Quaternion.Euler(0, 45, 0), actorsRoot));
                if (robotViews.TryGetValue(evt.Source, out var source))
                {
                    if (evt.Kind == "launch") source.AimAt(World(evt.Position));
                    else if (evt.Kind == "hit" || evt.Kind == "buff" || evt.Kind == "stun") source.FireAt(World(evt.Position));
                }
                if (evt.Radius > 0) { effectRing.Show(World(evt.Position), evt.Radius, evt.Kind == "buff" ? new Color(.4f,1,.5f) : new Color(1,.65f,.25f)); effectTime = .25f; }
            }
            Simulation.Events.Clear(); effectTime -= elapsed;
            if (effectTime <= 0) effectRing.Hide();
            if (Simulation.Phase != BattlePhase.Running && Simulation.Phase != BattlePhase.Paused) ClearCombatVisuals();
            var current = Simulation.Robots.Find(r => r.Id == selected);
            if (current != null && palette < 0 && codeEditor.IsOpen) rangeRing.Show(World(current.Position), current.Range, robots[current.DefinitionIndex].color);
            else if (palette < 0) rangeRing.Hide();
            if (current == null) selected = 0;
            RefreshHUD();
        }
        void CancelPlacement()
        { draggingRobot = false; palette = -1; placementMarker.gameObject.SetActive(false); rangeRing.Hide(); Tell("배치 취소 · 설치한 로봇을 클릭하면 상세 정보를 볼 수 있습니다."); }
        void ClearCombatVisuals()
        {
            effectTime = 0; effectRing.Hide();
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
        static Vector3 World(Point point) => new Vector3(point.X, 0, point.Y);
        void SyncViews(float elapsed)
        {
            projectileViews.Sync(Simulation, projectilePrefab, actorsRoot, Vector3.zero);
            foreach (var id in robotViews.Keys.Where(id => !Simulation.Robots.Any(r => r.Id == id)).ToArray())
            { robotViews[id].gameObject.SetActive(false); Destroy(robotViews[id].gameObject); robotViews.Remove(id); }
            foreach (var id in enemyViews.Keys.Where(id => !Simulation.Enemies.Any(e => e.Id == id)).ToArray())
            { enemyViews[id].gameObject.SetActive(false); Destroy(enemyViews[id].gameObject); enemyViews.Remove(id); }
            foreach (var bot in Simulation.Robots)
            {
                var view = robotViews[bot.Id];
                view.Sync(World(bot.Position), bot.Health, bot.MaxHealth, false, false, bot.Buffs.Count > 0, battleCamera.transform.rotation); view.TickVisual(elapsed);
            }
            foreach (var enemy in Simulation.Enemies)
            {
                if (!enemyViews.TryGetValue(enemy.Id, out var view))
                {
                    view = Instantiate(enemies[enemy.DefinitionIndex].prefab, World(enemy.Position), Quaternion.identity, actorsRoot);
                    view.Id = enemy.Id; enemyViews.Add(enemy.Id, view);
                }
                view.Sync(World(enemy.Position), enemy.Health, enemy.Spec.health, enemy.Stunned(Simulation.Time), enemy.Speed(Simulation.Time) < enemy.Spec.speed, false, battleCamera.transform.rotation);
                view.TickVisual(elapsed);
            }
        }
        string CompatibilityText(RobotState bot)
        {
            switch (Simulation.Compatibility(bot, bot.Action))
            {
                case ActionCompatibility.Native: return "전문 행동 · 정상 실행";
                case ActionCompatibility.Reduced: return bot.Action != RobotAction.Block && bot.Action != RobotAction.Buff ? "비전문 행동 · 효율 10%" : "승인된 비전문 행동 · 낮은 효율";
                case ActionCompatibility.Ineffective: return "실행 무효 · 이 로봇에서는 " + Rules.ActionName(bot.Action) + "이 동작하지 않습니다.";
                default: return "호환 미정 · 효과를 실행하지 않습니다. 기획 확정 후 데이터에 등록하세요.";
            }
        }
        void RefreshHUD()
        {
            if (Simulation == null) return;
            bool ended = Simulation.Phase == BattlePhase.Victory || Simulation.Phase == BattlePhase.Defeat;
            if (ended) speed = 1;
            string phase = Simulation.Phase == BattlePhase.Ready ? "준비" : Simulation.Phase == BattlePhase.Running ? "전투 중" :
                Simulation.Phase == BattlePhase.Paused ? "일시정지" : Simulation.Phase == BattlePhase.Reward ? "보상 수령 대기 · F1/F2 사용자 화면" : Simulation.Phase == BattlePhase.Victory ? "방어 성공" : "방어 실패";
            header.text = $"WAVE {Simulation.WaveIndex + 1:00} / {Simulation.Setup.Waves.Length:00}    |    CORE {Simulation.BaseHealth:00}    |    KILL {Simulation.Kills:00}  LEAK {Simulation.Leaks:00}";
            stateText.text = $"{phase}   ·   {Simulation.Time:0.0}s   ·   적 {Simulation.Enemies.Count}   ·   로봇 {Simulation.Robots.Count}/{robotLimit}";
            pauseLabel.text = Simulation.Phase == BattlePhase.Paused ? "재개" : "일시정지";
            speedLabel.text = speed == 1 ? "1x" : "2x";
            startButton.interactable = Simulation.Phase == BattlePhase.Ready;
            pauseButton.interactable = Simulation.Phase == BattlePhase.Running || Simulation.Phase == BattlePhase.Paused;
            speedButton.interactable = !ended;
            for (int i = 0; i < robotButtons.Length; i++)
            {
                robotButtons[i].interactable = true;
                developerRobotLabels[i].text = robots[i].displayName + "   ×" + Simulation.RobotAvailable(robots[i].stats.role);
            }
            var bot = Simulation.Robots.Find(r => r.Id == selected);
            removeButton.interactable = bot != null && Simulation.CanPrepare;
            if (bot != null)
            {
                string health = bot.Health.HasValue ? $"HP {bot.Health:0}/{bot.MaxHealth:0} · 저지 {Simulation.Enemies.Count(e => e.BlockedBy == bot.Id)}/{bot.Spec.blockCapacity}" : "체력 없음 · 적 피해 대상 제외";
                string control = bot.Program == null || bot.Program.IsEmpty ? "빈 로봇 · 코드 필요" : bot.Program.Fault == null ? "코드: " + bot.Program.Name : "코드 중지: " + bot.Program.Fault;
                selectionText.text = $"{robots[bot.DefinitionIndex].displayName}  #{bot.Id}\n\n{health}\n사거리 {bot.Range:0.00}    피해 {Simulation.ActionDamage(bot, bot.Action):0.0}\n공격 간격 {Simulation.ActionInterval(bot, bot.Action):0.00}s\n\n{Rules.ActionName(bot.Action)}\n{CompatibilityText(bot)}\n{control}\n실행 {bot.Executions}회 · 버프 {bot.Buffs.Count}개";
            }
            else if (palette >= 0) selectionText.text = robots[palette].displayName + "\n\n자유 배치 중\n초록: 설치 가능\n빨강: 설치 불가\n\n탱커: 경로 안\n일반 로봇: 경로 밖\n\n클릭 설치 / 우클릭 취소";
            else selectionText.text = "로봇 클릭: 코드 편집\nTAB 인벤토리\nSPACE 시작 / 정지";
            resultPanel.SetActive(ended);
            if (ended)
            {
                palette = -1; placementMarker.gameObject.SetActive(false);
                resultText.text = (Simulation.Phase == BattlePhase.Victory ? "DEFENSE COMPLETE\n방어 성공" : "CORE LOST\n방어 실패") + $"\n\n처치 {Simulation.Kills} · 돌파 {Simulation.Leaks}\n상단 초기화로 다시 시작";
            }
        }
        void Tell(string text) { shownMessage = text; message.text = text; }
    }
}
