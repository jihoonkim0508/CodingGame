using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed class DefensePlayerHUD : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] GameObject canvasRoot, rewardPanel;
        [SerializeField, Tooltip("튜토리얼 보상 집중(Battle.RewardPanel) 영역: 보상 카드 위쪽부터 '잔여 적' 줄 바로 아래까지")] RectTransform rewardFocus;
        [SerializeField, Tooltip("상단 바 왼쪽: 스테이지-웨이브와 섬 이름")] TMP_Text brand;
        [SerializeField] TMP_Text counters, guide, inventory, notice, pauseLabel, speedLabel, rewardTitle, rewardItems, claimLabel;
        [SerializeField] Button startButton, pauseButton, speedButton, claimButton;
        [SerializeField] GameObject combatControls;
        [SerializeField] GameObject[] enemyCards = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] enemyCounts = Array.Empty<TMP_Text>();
        [SerializeField] Button[] robotButtons = Array.Empty<Button>();
        [SerializeField] TMP_Text[] robotLabels = Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text upcoming;
        [SerializeField] ScrollRect rewardScroll;
        [SerializeField] CanvasGroup placementToast;
        [SerializeField] TMP_Text placementToastText;
        [SerializeField] Camera battleCamera;
        [SerializeField] RectTransform robotActions;
        [SerializeField] Button upgradeRobotButton, developRobotButton, recallRobotButton;
        [SerializeField] TMP_Text upgradeRobotLabel;
        RawImage[] enemyImages;
        DefenseHoverInfo[] enemyHovers;
        int[] cardDefinitions, cardCounts, shownCardCounts;
        Button[] upperRobotButtons;
        Coroutine placementFeedback;
        Vector2 toastPosition;
        RectTransform goalRect;
        Image goalBackground;
        Vector2 goalPosition;
        Vector3 goalScale;
        Color goalColor;
        float goalAnimationStart;
        BattlePhase shownRewardPhase;
        int shownRewardWave = -1, shownRewardStage = -1;
        void Start()
        {
            if (!battleCamera || !robotActions || !upgradeRobotButton || !developRobotButton || !recallRobotButton || !upgradeRobotLabel || !placementToast || !placementToastText || !battle || !rewardScroll || !combatControls || enemyCards.Length != 3 || enemyCounts.Length != 3 || enemyCards.Any(x => !x) || enemyCounts.Any(x => !x) ||
                !upcoming || !brand || !canvasRoot || !rewardPanel || !counters || !guide || !inventory || !notice || !pauseLabel || !speedLabel ||
                !rewardTitle || !rewardItems || !claimLabel || !startButton || !pauseButton || !speedButton || !claimButton ||
                robotLabels.Length != 6 || robotLabels.Any(t => !t) || robotButtons.Length != 6 || robotButtons.Any(b => !b)) throw new InvalidOperationException("DefensePlayerHUD Inspector 참조를 연결하세요.");
            startButton.onClick.AddListener(battle.StartBattle);
            pauseButton.onClick.AddListener(battle.TogglePause); speedButton.onClick.AddListener(battle.ToggleSpeed);
            claimButton.onClick.AddListener(battle.ContinueCampaign);
            upgradeRobotButton.onClick.AddListener(battle.UpgradeSelected);
            developRobotButton.onClick.AddListener(battle.DevelopSelected);
            recallRobotButton.onClick.AddListener(battle.RemoveSelected);
            toastPosition = ((RectTransform)placementToast.transform).anchoredPosition;
            // Clear of the deck's ink edge: top-aligned at the very top, the status line touched it.
            notice.alignment = TextAlignmentOptions.MidlineLeft; ((RectTransform)notice.transform).anchoredPosition += new Vector2(0, -8);
            placementToast.alpha = 0;
            upcoming.text = "다음 웨이브";
            // White on the blue backdrop: Jalnan with a thin ink outline (3.7:1 bare, below the 4.5:1 small text needs).
            if (UITheme.Display && UITheme.Label) { upcoming.font = UITheme.Display; upcoming.fontSharedMaterial = UITheme.Label; upcoming.color = Color.white; }
            goalRect = (RectTransform)guide.transform.parent;
            goalRect.anchorMin = goalRect.anchorMax = new Vector2(.5f, 1);
            goalRect.sizeDelta = new Vector2(Mathf.Min(800, ((RectTransform)canvasRoot.transform).rect.width - 48), 56);
            goalRect.anchoredPosition = new Vector2(0, -128);
            goalPosition = goalRect.anchoredPosition;
            goalScale = goalRect.localScale;
            guide.alignment = TextAlignmentOptions.Center;
            // Later stages' goal lines are longer than Stage 1's; shrink rather than spill out of the 56 px banner.
            guide.fontSizeMax = guide.fontSize; guide.fontSizeMin = 17; guide.enableAutoSizing = true;
            goalBackground = goalRect.GetComponent<Image>();
            if (goalBackground) { goalBackground.raycastTarget = false; goalColor = goalBackground.color; }

            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => battle.ChooseRobot(index)); }
            enemyImages = enemyCards.Select(c => c.GetComponentInChildren<RawImage>(true)).ToArray();
            enemyHovers = enemyCards.Select(c => c.GetComponent<DefenseHoverInfo>()).ToArray();
            cardDefinitions = new int[enemyCards.Length];
            cardCounts = new int[enemyCards.Length];
            shownCardCounts = Enumerable.Repeat(-1, enemyCards.Length).ToArray();
            upperRobotButtons = new[] { upgradeRobotButton, developRobotButton };
        }
        static string Label(string name) => $"<color={UITheme.MutedHex}><size=82%>{name}</size></color> ";
        public void SetVisible(bool visible) => canvasRoot.SetActive(visible);
        public void ShowPlacementError(string reason)
        {
            if (placementFeedback != null) StopCoroutine(placementFeedback);
            placementToastText.text = "설치 불가능!\n" + reason;
            placementFeedback = StartCoroutine(PlacementFeedback());
        }
        IEnumerator PlacementFeedback()
        {
            var rect = (RectTransform)placementToast.transform;
            for (float elapsed = 0; elapsed < 1.8f; elapsed += Time.unscaledDeltaTime)
            {
                rect.anchoredPosition = toastPosition + Vector2.right * (elapsed < .35f ? Mathf.Sin(elapsed * 90) * 9 * (1 - elapsed / .35f) : 0);
                placementToast.alpha = 1 - Mathf.Clamp01((elapsed - 1.1f) / .7f);
                yield return null;
            }
            rect.anchoredPosition = toastPosition; placementToast.alpha = 0; placementFeedback = null;
        }
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null) return;
            AnimateResult();
            FlashLeak(sim);
            AnimateFloaters();
            CallWave(sim);
            // While the tutorial's own 디버 is talking (1-1 reward lines), the result panel's 디버 steps aside.
            if (guideCorner) guideCorner.gameObject.SetActive(!(battle.Tutorial && battle.Tutorial.IsActive));
            bool ready = sim.CanPrepare, reward = sim.Phase == BattlePhase.Reward;
            bool ended = sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat;
            bool fighting = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            // Dim labels, bright values; the phase in its own colour so preparing and fighting read apart at a glance.
            string state = ended ? $"<color={UITheme.MutedHex}>작전 종료</color>" : fighting ? $"<color={UITheme.WarnHex}><b>전투 중</b></color>" : $"<color={UITheme.CalmHex}><b>정비 중</b></color>";
            string seconds = $"<b>{Math.Ceiling(sim.WaveRemaining):0}초</b>";
            if (sim.FinalRush && !reward && !ended) seconds = $"<color={UITheme.DangerHex}>{seconds} · 끝나면 남은 적 돌진</color>";
            counters.text = $"{state}      {Label("웨이브")}<b>{sim.WaveIndex + 1} / {sim.Setup.Waves.Length}</b>      {Label("남은 적")}<b>{(reward ? sim.LastSurvivors : sim.RemainingEnemies)}</b>" +
                $"      {Label("시간")}{seconds}      {Label("코인")}<b><color={UITheme.GoldHex}>{sim.Coins:N0}</color></b>";
            brand.text = $"<b>{battle.Stage}-{sim.WaveIndex + 1}</b>  <size=72%><color={UITheme.MutedHex}>{StageMap.StageProgress.Title(battle.Stage)}</color></size>";
            combatControls.SetActive(fighting);
            RefreshEnemyCards(sim, fighting);
            // Stage 1 goals come with its tutorial; later stages show the lesson's own goal line.
            string lessonGoal = battle.Stage > 1 ? sim.Lesson?.goal : null;
            bool freePlayGoal = (battle.Tutorial && battle.Tutorial.ShowFreePlayGoal || !string.IsNullOrEmpty(lessonGoal)) && !reward && !ended;
            if (freePlayGoal && !goalRect.gameObject.activeSelf) goalAnimationStart = Time.unscaledTime;
            guide.transform.parent.gameObject.SetActive(freePlayGoal);
            if (freePlayGoal)
            {
                guide.text = !string.IsNullOrEmpty(lessonGoal) ? "목표 · " + lessonGoal :
                    sim.WaveIndex == 1 ? "목표 · 새 전사를 배치하고 반복으로 여러 번 베기" :
                    sim.WaveIndex == 3 ? "목표 · 두 전사의 조건문으로 몰려오는 적 막기" :
                    "목표 · my_attack_range()와 조건문으로 잡몹과 보스 막기";
                AnimateGoal();
            }
            else
            {
                goalRect.anchoredPosition = goalPosition;
                goalRect.localScale = goalScale;
                if (goalBackground) goalBackground.color = goalColor;
            }
            notice.text = battle.StatusMessage;
            inventory.text = $"설치 {sim.Robots.Count} / {sim.Setup.RobotLimit}대  ·  버프형 최대 1대  ·  Esc 메뉴";
            inventory.color = sim.Robots.Count >= sim.Setup.RobotLimit ? UITheme.Hex(0xE03131) : UITheme.Muted;
            startButton.interactable = ready;
            pauseButton.interactable = fighting;
            speedButton.interactable = fighting && battle.SpeedUnlocked;
            pauseLabel.text = sim.Phase == BattlePhase.Paused ? "계속" : "일시정지";
            speedLabel.text = fighting && battle.Tutorial && Mathf.Approximately(battle.Tutorial.BattlePace, .2f)
                ? "0.2x" : battle.BattleSpeed + "x";
            RefreshRobotButtons(battle, robotButtons, robotLabels, "\nx");
            RefreshRobotIcons(sim);
            RefreshRobotActions(sim);
            RefreshRobotStatus(sim);
            rewardPanel.SetActive(reward || ended);
            if (reward || ended) RefreshRewardPanel(sim, reward);
            else resultShown = -100; // a restart right after the result clears its banner too, not only after BannerSeconds
            shownRewardPhase = sim.Phase; shownRewardWave = sim.WaveIndex; shownRewardStage = battle.Stage;
        }
        void AnimateGoal()
        {
            // Unscaled time keeps the gentle motion consistent while the player pauses or changes battle speed.
            float phase = (Time.unscaledTime - goalAnimationStart) * (2 * Mathf.PI / 3.6f);
            float wave = Mathf.Sin(phase);
            goalRect.anchoredPosition = goalPosition + Vector2.up * (2f * wave);
            goalRect.localScale = goalScale * (1f + .004f * (.5f + .5f * Mathf.Sin(phase + .5f)));
            if (!goalBackground) return;
            float glow = .5f + .5f * Mathf.Sin(phase - .7f);
            goalBackground.color = new Color(goalColor.r + .015f * glow,
                goalColor.g + .025f * glow, goalColor.b + .03f * glow, goalColor.a);
        }
        // Shared with the inventory window: unlocked robots show, and only stocked ones can be picked while preparing.
        // Each robot card shows the robot itself (RobotDefinition.icon, photographed from its prefab) left of its name,
        // faded while none are left to place.
        Image[] robotIcons;
        void RefreshRobotIcons(DefenseSimulation sim)
        {
            if (robotIcons == null)
            {
                robotIcons = new Image[robotButtons.Length];
                for (int i = 0; i < robotButtons.Length; i++)
                {
                    var sprite = battle.RobotDefinition(i).icon;
                    if (!sprite) continue;
                    var icon = robotIcons[i] = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    icon.rectTransform.SetParent(robotButtons[i].transform, false);
                    icon.rectTransform.anchorMin = Vector2.zero; icon.rectTransform.anchorMax = new Vector2(0, 1); icon.rectTransform.pivot = new Vector2(0, .5f);
                    icon.rectTransform.sizeDelta = new Vector2(58, -18); icon.rectTransform.anchoredPosition = new Vector2(14, 1); // inside the card's ink outline
                    icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
                    var label = robotLabels[i].rectTransform; label.offsetMin = new Vector2(label.offsetMin.x + 62, label.offsetMin.y);
                }
            }
            for (int i = 0; i < robotIcons.Length; i++)
                if (robotIcons[i]) robotIcons[i].color = new Color(1, 1, 1, sim.RobotAvailable(battle.DefinitionRole(i)) > 0 ? 1 : .4f);
        }
        internal static void RefreshRobotButtons(DefenseBattle battle, Button[] buttons, TMP_Text[] labels, string separator)
        {
            var sim = battle.Simulation;
            for (int i = 0; i < buttons.Length; i++)
            {
                var role = battle.DefinitionRole(i);
                int count = sim.RobotAvailable(role);
                buttons[i].gameObject.SetActive(sim.RobotUnlocked(role));
                buttons[i].interactable = sim.CanPrepare && count > 0;
                labels[i].text = battle.DefinitionName(i) + separator + count;
            }
        }
        void RefreshEnemyCards(DefenseSimulation sim, bool fighting)
        {
            // Cards follow the wave's own enemy types (in spawn order), so tutorial enemies get cards too. While fighting they
            // stay on the wave being fought and count down as its enemies die; between waves they show the next wave.
            var groups = fighting ? sim.CurrentWave : sim.UpcomingWave;
            int typeCount = 0;
            Array.Clear(cardCounts, 0, cardCounts.Length);
            for (int g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                int index = 0;
                while (index < typeCount && cardDefinitions[index] != group.DefinitionIndex) index++;
                if (index == typeCount)
                {
                    if (typeCount == cardDefinitions.Length) continue;
                    cardDefinitions[typeCount++] = group.DefinitionIndex;
                }
                cardCounts[index] += group.Count;
            }
            upcoming.gameObject.SetActive(typeCount > 0);
            upcoming.text = fighting ? "이번 웨이브" : "다음 웨이브";
            for (int i = 0; i < enemyCards.Length; i++)
            {
                enemyCards[i].SetActive(i < typeCount);
                if (i >= typeCount) continue;
                var definition = battle.EnemyAt(cardDefinitions[i]);
                if (definition.icon && enemyImages[i]) enemyImages[i].texture = definition.icon;
                if (enemyHovers[i]) enemyHovers[i].SetEnemy(definition);
                int count = fighting ? sim.RemainingOf(cardDefinitions[i]) : cardCounts[i];
                if (shownCardCounts[i] != count)
                {
                    shownCardCounts[i] = count;
                    enemyCounts[i].text = "× " + count;
                }
            }
        }
        const float ButtonGap = 6;
        void RefreshRobotActions(DefenseSimulation sim)
        {
            var selected = sim.FindRobot(battle.SelectedId);
            robotActions.gameObject.SetActive(selected != null && battle.CanShowRobotActions);
            if (!robotActions.gameObject.activeSelf) return;
            var canvas = (RectTransform)canvasRoot.transform;
            Vector2 Local(float height)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, battleCamera.WorldToScreenPoint(DefenseViews.World(selected.Position, height)), null, out var p);
                return p;
            }
            // The buttons go above the robot's head and below its feet, measured on screen (the robot models are 2.5 tall).
            Vector2 foot = Local(0), head = Local(DefenseBattle.RobotHeight), point = (foot + head) / 2;
            float above = head.y - point.y + ButtonGap, below = point.y - foot.y + ButtonGap;
            point.x = Mathf.Clamp(point.x, canvas.rect.xMin + 160, canvas.rect.xMax - 160);
            point.y = Mathf.Clamp(point.y, canvas.rect.yMin + below + 90, canvas.rect.yMax - above - 90);
            robotActions.anchoredPosition = point;
            foreach (var button in upperRobotButtons)
            { var r = (RectTransform)button.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, above + r.rect.height / 2); }
            var recall = (RectTransform)recallRobotButton.transform;
            recall.anchoredPosition = new Vector2(recall.anchoredPosition.x, -below - recall.rect.height / 2);
            upgradeRobotButton.interactable = sim.UpgradeError(selected.Id) == null;
            recallRobotButton.interactable = sim.RecallUnlocked;
            upgradeRobotLabel.text = !sim.UpgradeUnlocked ? "레벨업\n1-2 해금" : selected.IsMaxLevel ? "최고 레벨" : $"레벨업\n{sim.UpgradeCostFor(selected)} 코인";
        }
        // A tag over each robot's head (DefenseDebrief.LiveStatus). Labels sit in a layer behind the other HUD parts; the
        // selected robot's tag hides while its action menu is open over its head.
        // The wave's start: its number large over the map for a moment, with a horn, so each fight has a beginning.
        // Only Ready → Running counts; resuming from a pause does not call the wave again.
        TextMeshProUGUI waveCall;
        float waveCallAt = -100;
        BattlePhase lastPhase;
        const float WaveCallSeconds = 1.4f;
        void CallWave(DefenseSimulation sim)
        {
            if (sim.Phase == BattlePhase.Running && lastPhase == BattlePhase.Ready)
            {
                if (!waveCall)
                {
                    waveCall = new GameObject("Wave call", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    waveCall.rectTransform.SetParent(canvasRoot.transform, false);
                    waveCall.rectTransform.anchorMin = waveCall.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    waveCall.rectTransform.sizeDelta = new Vector2(1400, 260); waveCall.rectTransform.anchoredPosition = new Vector2(0, 110);
                    // Regular weight: TMP's bold adds spacing per letter, which at this size pulls the Korean line apart.
                    waveCall.font = UITheme.Display ? UITheme.Display : rewardItems.font; waveCall.fontSize = 110; waveCall.characterSpacing = -2;
                    waveCall.alignment = TextAlignmentOptions.Center; waveCall.raycastTarget = false;
                    if (UITheme.Banner) waveCall.fontSharedMaterial = UITheme.Banner;
                }
                bool last = sim.WaveIndex + 1 == sim.Setup.Waves.Length;
                waveCall.text = (last ? "마지막 웨이브" : $"WAVE {sim.WaveIndex + 1}") + $"\n<size=36%>오류 코드 {sim.RemainingEnemies}마리 접근!</size>";
                waveCall.color = last ? UITheme.Coral : UITheme.Sunny;
                waveCall.transform.SetAsLastSibling();
                waveCallAt = Time.unscaledTime; battle.PlaySound("wave-start");
            }
            lastPhase = sim.Phase;
            if (!waveCall) return;
            float t = Time.unscaledTime - waveCallAt;
            waveCall.gameObject.SetActive(t < WaveCallSeconds && !MenuOpen); // it would show through the Esc menu's shade
            if (t >= WaveCallSeconds) return;
            waveCall.rectTransform.localScale = Vector3.one * (t < .2f ? Mathf.Lerp(1.6f, 1, Mathf.SmoothStep(0, 1, t / .2f)) : 1 + .02f * (t - .2f));
            waveCall.alpha = t < .2f ? t / .2f : t > WaveCallSeconds - .45f ? (WaveCallSeconds - t) / .45f : 1;
        }

        // The damage a hit did, over the enemy, and "+n 코인" over a kill: rising and fading, so every attack the code makes
        // shows its result. A small pool is reused oldest first, so a burst of hits never piles up labels.
        sealed class Floater { public TextMeshProUGUI label; public Vector3 world; public float born, life, rise; }
        readonly System.Collections.Generic.List<Floater> floaters = new System.Collections.Generic.List<Floater>();
        RectTransform floaterLayer;
        const int MaxFloaters = 24;
        const float EnemyHeadHeight = 1.8f;
        public void ShowCombatText(CombatEvent evt)
        {
            var sim = battle.Simulation;
            if (sim == null || !sim.Options.hitEffects) return;
            if ((evt.Kind == "hit" || evt.Kind == "projectile-hit") && evt.AppliedAmount > 0)
                Float(evt.Position, Mathf.Max(1, Mathf.RoundToInt(evt.AppliedAmount)).ToString(), Color.white, 30, .6f, 46);
            else if (evt.Kind == "killed" && sim.Setup.PlayerFlow && sim.Setup.Progression.coinsPerKill > 0)
                Float(evt.Position, $"+{sim.Setup.Progression.coinsPerKill} 코인", new Color(1, .83f, .42f), 32, 1f, 74);
        }
        void Float(System.Numerics.Vector2 position, string text, Color color, float size, float life, float rise)
        {
            if (!floaterLayer)
            {
                floaterLayer = new GameObject("Combat text", typeof(RectTransform)).GetComponent<RectTransform>();
                floaterLayer.SetParent(canvasRoot.transform, false); floaterLayer.SetSiblingIndex(0);
                floaterLayer.anchorMin = Vector2.zero; floaterLayer.anchorMax = Vector2.one; floaterLayer.sizeDelta = Vector2.zero;
            }
            var floater = floaters.FirstOrDefault(f => !f.label.gameObject.activeSelf);
            if (floater == null && floaters.Count < MaxFloaters)
            {
                var label = new GameObject("Floater", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(floaterLayer, false); label.rectTransform.sizeDelta = new Vector2(200, 40);
                label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
                // Numbers over the world share Jalnan's thin-outline Label look (one material, not an instance per floater).
                if (UITheme.Display && UITheme.Label) { label.font = UITheme.Display; label.fontSharedMaterial = UITheme.Label; }
                else { label.font = rewardItems.font; label.outlineWidth = .32f; label.outlineColor = UITheme.Ink; }
                floaters.Add(floater = new Floater { label = label });
            }
            floater ??= floaters.OrderBy(f => f.born).First();
            floater.world = DefenseViews.World(position, EnemyHeadHeight);
            floater.born = Time.unscaledTime; floater.life = life; floater.rise = rise;
            floater.label.text = text; floater.label.fontSize = size; floater.label.color = color;
            floater.label.gameObject.SetActive(true);
        }
        void AnimateFloaters()
        {
            var canvas = (RectTransform)canvasRoot.transform;
            foreach (var f in floaters)
            {
                if (!f.label.gameObject.activeSelf) continue;
                float k = (Time.unscaledTime - f.born) / f.life;
                if (k >= 1) { f.label.gameObject.SetActive(false); continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, battleCamera.WorldToScreenPoint(f.world), null, out var at);
                // Pops up fast, then drifts; fades over the last half.
                f.label.rectTransform.anchoredPosition = at + Vector2.up * (f.rise * (1 - (1 - k) * (1 - k)));
                f.label.alpha = k < .5f ? 1 : 1 - (k - .5f) * 2;
                f.label.transform.localScale = Vector3.one * (k < .12f ? 1 + .35f * (1 - k / .12f) : 1);
            }
        }

        // When an enemy reaches the core the screen's edges glow red for a moment (the core bar shakes with it), so a leak
        // is felt even while the player is looking at the other end of the road.
        Image leakFlash;
        int shownCoreHealth = -1;
        float leakFlashAt = -100;
        const float LeakFlashSeconds = .7f;
        void FlashLeak(DefenseSimulation sim)
        {
            if (!leakFlash)
            {
                leakFlash = new GameObject("Leak flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rect = leakFlash.rectTransform; rect.SetParent(canvasRoot.transform, false); rect.SetSiblingIndex(1);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                leakFlash.sprite = EdgeGlow(); leakFlash.raycastTarget = false;
            }
            if (shownCoreHealth >= 0 && sim.BaseHealth < shownCoreHealth) leakFlashAt = Time.unscaledTime;
            shownCoreHealth = sim.BaseHealth;
            float k = 1 - Mathf.Clamp01((Time.unscaledTime - leakFlashAt) / LeakFlashSeconds);
            leakFlash.color = new Color(1, .16f, .12f, .6f * k * k);
            leakFlash.enabled = k > 0;
        }
        // Clear in the middle, opaque toward the edges.
        static Sprite EdgeGlow()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2 - 1, dy = (y + .5f) / size * 2 - 1;
                    float edge = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * .6f + Mathf.Sqrt(dx * dx + dy * dy) / 1.414f * .4f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, edge))));
                }
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        RectTransform statusLayer;
        readonly System.Collections.Generic.List<TextMeshProUGUI> statusLabels = new System.Collections.Generic.List<TextMeshProUGUI>();
        void RefreshRobotStatus(DefenseSimulation sim)
        {
            var canvas = (RectTransform)canvasRoot.transform;
            if (!statusLayer)
            {
                statusLayer = new GameObject("Robot status", typeof(RectTransform)).GetComponent<RectTransform>();
                statusLayer.SetParent(canvas, false); statusLayer.SetSiblingIndex(0);
                statusLayer.anchorMin = Vector2.zero; statusLayer.anchorMax = Vector2.one; statusLayer.sizeDelta = Vector2.zero; statusLayer.pivot = canvas.pivot;
            }
            int shown = 0;
            bool results = sim.Phase == BattlePhase.Reward || sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat;
            foreach (var bot in sim.Robots)
            {
                // With the result up, every robot wears its debrief name so the advice points at a robot on the map.
                string text = results ? DefenseDebrief.ResultTag(sim, bot, RobotLabel(sim, bot)) : DefenseDebrief.LiveStatus(sim, bot);
                if (text == null || robotActions.gameObject.activeSelf && bot.Id == battle.SelectedId) continue;
                if (shown == statusLabels.Count)
                {
                    // A dark plate sized to the text behind each tag.
                    var plate = new GameObject("Status tag", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
                    var rect = (RectTransform)plate.transform; rect.SetParent(statusLayer, false);
                    rect.anchorMin = rect.anchorMax = canvas.pivot; rect.pivot = new Vector2(.5f, 0);
                    var back = UITheme.Style(plate.GetComponent<Image>(), UITheme.Chip, UITheme.Paper); back.pixelsPerUnitMultiplier = 1.7f; back.raycastTarget = false;
                    var layout = plate.GetComponent<HorizontalLayoutGroup>(); layout.padding = new RectOffset(16, 16, 6, 8);
                    layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                    var fit = plate.GetComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    var made = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    made.rectTransform.SetParent(rect, false);
                    made.font = rewardItems.font; made.fontSize = 22; made.fontStyle = FontStyles.Bold; made.alignment = TextAlignmentOptions.Center; made.color = UITheme.Ink;
                    made.textWrappingMode = TextWrappingModes.NoWrap; made.raycastTarget = false;
                    statusLabels.Add(made);
                }
                var label = statusLabels[shown++];
                if (label.text != text) label.text = text;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, battleCamera.WorldToScreenPoint(DefenseViews.World(bot.Position, DefenseBattle.RobotHeight)), null, out var head);
                var tag = (RectTransform)label.transform.parent;
                tag.anchoredPosition = head + Vector2.up * 10;
                tag.gameObject.SetActive(true);
            }
            for (int i = shown; i < statusLabels.Count; i++) statusLabels[i].transform.parent.gameObject.SetActive(false);
        }
        // The wave's result in large letters over the screen; the result panel fades in under it, then the rewarded blocks
        // pop out in turn. Driven from LateUpdate by the time it started, so an interrupted frame never leaves the panel or a
        // block invisible; a later result (the stage, right after the last wave) replaces the banner.
        // The panel waits until the banner has nearly faded, so TIME UP/CLEAR never sits on top of the reward text.
        const float BannerSeconds = 1.4f, PanelFadeFrom = 1.3f, BlocksFrom = 1.6f, BlockStagger = .15f, BlockPop = .3f;
        TextMeshProUGUI banner;
        CanvasGroup bannerGroup;
        CanvasGroup resultGroup;
        float resultShown = -100;
        readonly System.Collections.Generic.List<RectTransform> popping = new System.Collections.Generic.List<RectTransform>();
        void ShowEndBanner(string text, bool lost)
        {
            if (!banner)
            {
                // A dark strip across the screen keeps the letters readable over the bright floor.
                var strip = new GameObject("Result banner", typeof(RectTransform), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
                strip.SetParent(canvasRoot.transform, false);
                strip.anchorMin = new Vector2(0, .5f); strip.anchorMax = new Vector2(1, .5f); strip.sizeDelta = new Vector2(0, 200);
                var shade = strip.GetComponent<Image>(); shade.color = new Color(UITheme.Night.r, UITheme.Night.g, UITheme.Night.b, .8f); shade.raycastTarget = false;
                bannerGroup = strip.GetComponent<CanvasGroup>(); bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
                banner = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                banner.rectTransform.SetParent(strip, false);
                banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(.5f, .5f); banner.rectTransform.sizeDelta = new Vector2(1600, 220);
                // The body font in regular weight: bold spreads Korean letters apart at this size (see the wave call).
                banner.font = UITheme.Display ? UITheme.Display : rewardItems.font; banner.fontSize = 112; banner.characterSpacing = -2; banner.alignment = TextAlignmentOptions.Center; banner.raycastTarget = false;
                if (UITheme.Banner) banner.fontSharedMaterial = UITheme.Banner;
            }
            banner.transform.parent.SetAsLastSibling();
            banner.color = lost ? UITheme.Coral : UITheme.Sunny; banner.text = text.Replace("  /  ", "  ");
            resultGroup = rewardPanel.GetComponent<CanvasGroup>();
            if (!resultGroup) resultGroup = rewardPanel.AddComponent<CanvasGroup>();
            resultShown = Time.unscaledTime;
        }
        void AnimateResult()
        {
            float t = Time.unscaledTime - resultShown;
            if (banner)
            {
                banner.transform.parent.gameObject.SetActive(t < BannerSeconds);
                float k = Mathf.Clamp01(t / .25f);
                banner.rectTransform.localScale = Vector3.one * (t < .25f ? Mathf.Lerp(1.8f, 1, Mathf.SmoothStep(0, 1, k)) : 1 + .03f * (t - .25f));
                bannerGroup.alpha = t < .25f ? k : t > BannerSeconds - .4f ? Mathf.Clamp01((BannerSeconds - t) / .4f) : 1;
            }
            if (resultGroup) resultGroup.alpha = Mathf.Clamp01((t - PanelFadeFrom) / .4f);
            // Stage clear: the stars land one by one, each earned one with a chime a step higher than the last.
            for (int i = 0; starRow && starRow.gameObject.activeInHierarchy && i < stars.Count; i++)
            {
                float p = Mathf.Clamp01((t - StarsFrom - i * StarStagger) / StarPop);
                stars[i].rectTransform.localScale = Vector3.one * (p < .55f ? Mathf.Lerp(0, 1.4f, p / .55f) : Mathf.Lerp(1.4f, 1, (p - .55f) / .45f));
                stars[i].rectTransform.localEulerAngles = new Vector3(0, 0, (1 - p) * 40);
                if (p > 0 && i >= starsRung && i < starsEarned) { starsRung = i + 1; battle.PlaySound("star" + (i + 1)); }
            }
            // Blocks come in pairs with their counts.
            for (int i = 0; i < popping.Count; i++)
            {
                if (!popping[i]) continue;
                float p = Mathf.Clamp01((t - BlocksFrom - i / 2 * BlockStagger) / BlockPop);
                popping[i].localScale = Vector3.one * (p < .6f ? Mathf.Lerp(0, 1.15f, p / .6f) : Mathf.Lerp(1.15f, 1, (p - .6f) / .4f));
            }
        }

        // The rewarded blocks as they look in the editor (planner: an image, not a name), in a row above the reward text.
        RectTransform rewardBlocks;
        void ShowRewardBlocks(System.Collections.Generic.IReadOnlyList<CodingGame.BlockCoding.BlockKind> drops)
        {
            if (rewardBlocks) Destroy(rewardBlocks.gameObject);
            rewardBlocks = null; rewardItems.margin = Vector4.zero;
            var panel = battle.CodeEditor ? battle.CodeEditor.Panel : null;
            if (drops == null || drops.Count == 0) return;
            rewardBlocks = new GameObject("Reward blocks", typeof(RectTransform)).GetComponent<RectTransform>();
            rewardBlocks.SetParent(rewardItems.transform, false);
            rewardBlocks.anchorMin = rewardBlocks.anchorMax = rewardBlocks.pivot = new Vector2(0, 1); rewardBlocks.anchoredPosition = Vector2.zero;
            float x = 0, y = 0, rowHeight = 0, width = ((RectTransform)rewardItems.transform).rect.width;
            popping.Clear();
            foreach (var group in drops.GroupBy(k => k).OrderBy(g => g.Key))
            {
                var view = panel == null ? null : group.Key == CodingGame.BlockCoding.BlockKind.Variable ? panel.VariableTemplate : panel.Palette.FirstOrDefault(p => p.view.BaseKind == group.Key).view;
                // A block with no palette item yet keeps its name, so no reward goes unseen.
                if (!view) { rewardItems.text = DefenseProgression.Label(group.Key) + "   x" + group.Count() + "\n" + rewardItems.text; continue; }
                var copy = DefenseViews.GraphicCopy(view.Rect, rewardBlocks);
                copy.gameObject.SetActive(true);
                // Same width rule as CommandBlockView.preferredWidth; the palette original is not laid out while its tab is closed.
                var header = copy.GetChild(0).Find("Header") as RectTransform;
                if (header) LayoutRebuilder.ForceRebuildLayoutImmediate(header);
                float w = header ? Mathf.Max(100, LayoutUtility.GetPreferredWidth(header) + 28) : 100, h = view.preferredHeight;
                if (x > 0 && x + w > width) { x = 0; y += rowHeight + 10; rowHeight = 0; }
                // Centred pivots, so the pop grows from the middle.
                copy.anchorMin = copy.anchorMax = new Vector2(0, 1); copy.pivot = new Vector2(.5f, .5f); copy.sizeDelta = new Vector2(w, h); copy.anchoredPosition = new Vector2(x + w * .5f, -y - h * .5f);
                var count = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                count.rectTransform.SetParent(rewardBlocks, false); count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(0, 1); count.rectTransform.pivot = new Vector2(.5f, .5f);
                count.rectTransform.anchoredPosition = new Vector2(x + w + 30, -y - h * .5f); count.rectTransform.sizeDelta = new Vector2(48, h);
                count.font = rewardItems.font; count.fontSize = 26; count.alignment = TextAlignmentOptions.MidlineLeft; count.text = "x" + group.Count(); count.raycastTarget = false;
                x += w + 60; rowHeight = Mathf.Max(rowHeight, h);
                popping.Add(copy); popping.Add(count.rectTransform);
            }
            rewardItems.margin = new Vector4(0, y + rowHeight + 16, 0, 0);
        }
        // The stage's stars, the core left and what the next star needs, so a clear invites a better run.
        // 디버 to the left of the result card (the title's parent; the reward panel itself is the full-screen shade), so it
        // fades in with the panel: face and one line for this result.
        RectTransform guideCorner;
        Image guidePortrait;
        TextMeshProUGUI guideLine;
        Sprite[] guideFaces;
        void ShowGuide(DefenseSimulation sim)
        {
            if (!guideCorner)
            {
                guideFaces = Enum.GetNames(typeof(GuideMood)).Select(m => Resources.Load<Sprite>("Guide/Guide_" + m.ToLowerInvariant())).ToArray();
                guideCorner = new GameObject("Guide", typeof(RectTransform)).GetComponent<RectTransform>();
                guideCorner.SetParent(rewardTitle.transform.parent, false);
                guideCorner.anchorMin = guideCorner.anchorMax = new Vector2(0, 1); guideCorner.pivot = new Vector2(1, 1);
                guideCorner.sizeDelta = new Vector2(300, 330); guideCorner.anchoredPosition = new Vector2(-28, -30);
                guidePortrait = new GameObject("Face", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                guidePortrait.rectTransform.SetParent(guideCorner, false); guidePortrait.preserveAspect = true; guidePortrait.raycastTarget = false;
                guidePortrait.rectTransform.anchorMin = guidePortrait.rectTransform.anchorMax = new Vector2(.5f, 1); guidePortrait.rectTransform.pivot = new Vector2(.5f, 1);
                guidePortrait.rectTransform.sizeDelta = new Vector2(150, 150); guidePortrait.rectTransform.anchoredPosition = Vector2.zero;
                var bubble = new GameObject("Line", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bubble.rectTransform.SetParent(guideCorner, false); bubble.raycastTarget = false;
                bubble.rectTransform.anchorMin = new Vector2(0, 1); bubble.rectTransform.anchorMax = new Vector2(1, 1); bubble.rectTransform.pivot = new Vector2(.5f, 1);
                bubble.rectTransform.sizeDelta = new Vector2(0, 150); bubble.rectTransform.anchoredPosition = new Vector2(0, -162);
                UITheme.Style(bubble, UITheme.Card, UITheme.Paper);
                guideLine = Text(bubble.transform, 24); guideLine.alignment = TextAlignmentOptions.Center; guideLine.color = UITheme.Ink;
                guideLine.rectTransform.anchorMin = Vector2.zero; guideLine.rectTransform.anchorMax = Vector2.one;
                guideLine.rectTransform.sizeDelta = new Vector2(-28, -20); guideLine.enableAutoSizing = true; guideLine.fontSizeMin = 17; guideLine.fontSizeMax = 24;
            }
            var (mood, line) = DefenseDebrief.GuideSays(sim);
            guidePortrait.sprite = guideFaces[(int)mood]; guidePortrait.enabled = guidePortrait.sprite;
            guideLine.text = $"<color={UITheme.CalmHex}><b>디버</b></color>\n{line}";
        }

        // Under the big stars: a new record or the best one, and what the next star asks for.
        string StarLine(DefenseSimulation sim)
        {
            int earned = sim.StageStars, max = sim.Setup.BaseHealth;
            int best = battle.FromStageMap ? StageMap.StageProgress.BestStars(battle.Stage) : 0;
            string line = earned > best && best > 0 ? $"<color={UITheme.GoodHex}><b>새 기록!</b></color>   " : best > earned ? $"<color={UITheme.MutedHex}>최고 기록 {new string('★', best)}</color>   " : "";
            line += $"코어 {sim.BaseHealth} / {max} · " + (earned < 3 ? $"코어를 {DefenseSimulation.CoreForStars(earned + 1, max)} 이상 남기면 ★{earned + 1}" : "최고 등급!");
            return line;
        }
        // Three large stars in the reward text's top margin (stage clear has no reward blocks there), gold when earned.
        readonly System.Collections.Generic.List<TextMeshProUGUI> stars = new System.Collections.Generic.List<TextMeshProUGUI>();
        RectTransform starRow;
        int starsEarned, starsRung;
        const float StarsFrom = 1.7f, StarStagger = .4f, StarPop = .35f, StarRowHeight = 110;
        void ShowStars(DefenseSimulation sim)
        {
            bool show = sim.Phase == BattlePhase.Victory;
            if (!starRow && show)
            {
                starRow = new GameObject("Stars", typeof(RectTransform)).GetComponent<RectTransform>();
                starRow.SetParent(rewardItems.transform, false);
                starRow.anchorMin = new Vector2(0, 1); starRow.anchorMax = Vector2.one; starRow.pivot = new Vector2(.5f, 1);
                starRow.sizeDelta = new Vector2(0, StarRowHeight); starRow.anchoredPosition = Vector2.zero;
                for (int i = 0; i < 3; i++)
                {
                    var star = new GameObject("Star", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    star.rectTransform.SetParent(starRow, false);
                    star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    star.rectTransform.sizeDelta = new Vector2(120, 120); star.rectTransform.anchoredPosition = new Vector2((i - 1) * 124, 0);
                    star.font = rewardItems.font; star.fontSize = 104; star.alignment = TextAlignmentOptions.Center; star.raycastTarget = false;
                    star.text = "★";
                    if (UITheme.Display && UITheme.Banner) { star.font = UITheme.Display; star.fontSharedMaterial = UITheme.Banner; }
                    else { star.outlineWidth = .2f; star.outlineColor = UITheme.Ink; }
                    stars.Add(star);
                }
            }
            if (starRow) starRow.gameObject.SetActive(show);
            if (!show) return;
            starsEarned = sim.StageStars; starsRung = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                stars[i].color = i < starsEarned ? UITheme.Sunny : UITheme.PaperDeep;
                stars[i].rectTransform.localScale = Vector3.zero;
            }
            var margin = rewardItems.margin; margin.y = StarRowHeight + 8; rewardItems.margin = margin;
        }
        // Esc: a menu over the battle to resume, switch sound, restart the stage or leave for the stage map. A running wave
        // pauses while it is open and resumes when it closes.
        GameObject menu;
        TMP_Text soundLabel;
        bool pausedByMenu;
        public bool MenuOpen => menu && menu.activeSelf;
        public void ToggleMenu()
        {
            if (!menu) BuildMenu();
            bool open = !menu.activeSelf;
            menu.SetActive(open); menu.transform.SetAsLastSibling();
            var sim = battle.Simulation;
            if (open && sim.Phase == BattlePhase.Running) { battle.TogglePause(); pausedByMenu = sim.Phase == BattlePhase.Paused; }
            else if (!open && pausedByMenu) { if (sim.Phase == BattlePhase.Paused) battle.TogglePause(); pausedByMenu = false; }
            soundLabel.text = DefenseSounds.Muted ? "소리 켜기" : "소리 끄기";
            voiceLabel.text = VoiceLabel;
        }
        TMP_Text voiceLabel;
        static string VoiceLabel => TutorialDialogueView.VoiceOn ? "디버 목소리 끄기" : "디버 목소리 켜기";
        void BuildMenu()
        {
            var shade = new GameObject("Menu", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            shade.SetParent(canvasRoot.transform, false); shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.sizeDelta = Vector2.zero;
            shade.GetComponent<Image>().color = new Color(UITheme.Night.r, UITheme.Night.g, UITheme.Night.b, .75f);
            menu = shade.gameObject;
            // A paper card holding the title and buttons.
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            card.SetParent(shade, false); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(500, 640); card.anchoredPosition = new Vector2(0, 0);
            UITheme.Style(card.GetComponent<Image>(), UITheme.Card, UITheme.Paper);
            TMP_Text Text(string text, float y, float size)
            {
                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.rectTransform.SetParent(card, false); label.rectTransform.sizeDelta = new Vector2(440, 60); label.rectTransform.anchoredPosition = new Vector2(0, y);
                label.font = rewardTitle.font; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.text = text;
                label.color = UITheme.Ink; label.textWrappingMode = TextWrappingModes.Normal;
                return label;
            }
            TMP_Text Add(string text, float y, Color fill, Action click)
            {
                var button = Instantiate(claimButton, card);
                var r = (RectTransform)button.transform;
                r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(380, 66); r.anchoredPosition = new Vector2(0, y);
                button.interactable = true; button.onClick.AddListener(() => click());
                UITheme.Style((Image)button.targetGraphic, UITheme.Button, fill);
                var label = button.GetComponentInChildren<TMP_Text>(); label.text = text; label.color = UITheme.Ink;
                return label;
            }
            var title = Text("일시정지", 250, 46);
            if (UITheme.Display) title.font = UITheme.Display;
            Add("계속하기", 160, UITheme.Primary, ToggleMenu);
            soundLabel = Add("소리 끄기", 80, UITheme.PaperDeep, () => { DefenseSounds.Muted = !DefenseSounds.Muted; soundLabel.text = DefenseSounds.Muted ? "소리 켜기" : "소리 끄기"; });
            voiceLabel = Add(VoiceLabel, 0, UITheme.PaperDeep, () => { TutorialDialogueView.VoiceOn = !TutorialDialogueView.VoiceOn; voiceLabel.text = VoiceLabel; });
            Add("이 스테이지 처음부터", -80, UITheme.PaperDeep, () => { ToggleMenu(); battle.Restart(); });
            Add(battle.FromStageMap ? "스테이지 맵으로" : "타이틀로", -160, UITheme.Coral, () => GameFlow.LoadScene(battle.FromStageMap ? GameFlow.StageScene : GameFlow.TitleScene));
            Text("스테이지 도중에 나가면 이 스테이지 진행은 저장되지 않아요.", -250, 19).color = UITheme.Muted;
            menu.SetActive(false); // built closed, so the first Esc opens it
        }
        // On a defeat that can go back one wave, the result button retries the wave and a copy beside it restarts the stage
        // (a wave that began with little core health may be better started over).
        Button restartStageButton;
        Vector2 claimAnchorMax;
        void SplitDefeatButtons(bool split)
        {
            var claim = (RectTransform)claimButton.transform;
            if (split && !restartStageButton)
            {
                claimAnchorMax = claim.anchorMax;
                restartStageButton = Instantiate(claimButton, claim.parent);
                restartStageButton.name = "RestartStage";
                restartStageButton.onClick.AddListener(battle.Restart);
                ((Image)restartStageButton.targetGraphic).color = UITheme.Paper; // the retry beside it stays the one yellow button
                var label = restartStageButton.GetComponentInChildren<TMP_Text>();
                label.text = "스테이지 처음부터"; label.fontSize = claimLabel.fontSize * .85f;
                var r = (RectTransform)restartStageButton.transform;
                r.anchorMin = new Vector2(.64f, claim.anchorMin.y); r.anchorMax = claimAnchorMax;
            }
            if (!restartStageButton) return;
            restartStageButton.gameObject.SetActive(split);
            claim.anchorMax = new Vector2(split ? .62f : claimAnchorMax.x, claimAnchorMax.y);
        }
        // Robots of one kind are numbered in placement order; the same name heads the debrief line and the robot's tag.
        string RobotLabel(DefenseSimulation sim, RobotState bot)
        {
            var same = sim.Robots.Where(r => r.DefinitionIndex == bot.DefinitionIndex).ToList();
            return battle.DefinitionName(bot.DefinitionIndex) + (same.Count > 1 ? " " + (same.IndexOf(bot) + 1) : "");
        }
        // The debrief under the reward text: the headline, then a card per robot with a stripe in its state's colour
        // (green MVP, yellow advice, blue fine), its numbers, a bar for its share of the wave's damage and its own advice.
        // The cards sit in the reward text's bottom margin so the tutorial's reward focus above them is unchanged.
        sealed class DebriefCard { public RectTransform root; public Image stripe, share; public TextMeshProUGUI title, advice; }
        readonly System.Collections.Generic.List<DebriefCard> debriefCards = new System.Collections.Generic.List<DebriefCard>();
        RectTransform debriefRoot;
        TextMeshProUGUI debriefHeadline;
        static readonly Color MvpColor = UITheme.Hex(0x40C057), AdviceColor = UITheme.Hex(0xFD7E14), FineColor = UITheme.Hex(0x4DABF7);
        void ShowDebrief(DefenseSimulation sim)
        {
            var (headline, lines) = DefenseDebrief.Summary(sim, bot => RobotLabel(sim, bot));
            if (!debriefRoot)
            {
                debriefRoot = new GameObject("Debrief", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                debriefRoot.SetParent(rewardItems.transform, false);
                debriefRoot.anchorMin = Vector2.zero; debriefRoot.anchorMax = new Vector2(1, 0); debriefRoot.pivot = new Vector2(.5f, 0);
                debriefRoot.sizeDelta = Vector2.zero; // a new RectTransform is 100 wide: stretched, that would overhang both sides
                var column = debriefRoot.GetComponent<VerticalLayoutGroup>(); column.spacing = 10;
                column.childControlWidth = column.childControlHeight = true; column.childForceExpandWidth = true; column.childForceExpandHeight = false;
                debriefRoot.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                debriefHeadline = Text(debriefRoot, rewardItems.fontSize * .85f);
            }
            debriefHeadline.text = headline;
            float most = Mathf.Max(1, lines.Length == 0 ? 0 : lines.Max(l => l.Bot.DamageThisWave));
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var card = i < debriefCards.Count ? debriefCards[i] : MakeDebriefCard();
                card.root.gameObject.SetActive(true);
                card.stripe.color = line.Mvp ? MvpColor : line.Issue != DefenseDebrief.Issue.None ? AdviceColor : FineColor;
                card.title.text = $"<b>{line.Name}</b>" + (line.Mvp ? $"  <color={UITheme.GoodHex}><size=80%>MVP</size></color>" : "") +
                    $"\n<color={UITheme.MutedHex}><size=88%>{line.Stats}</size></color>";
                card.share.rectTransform.anchorMax = new Vector2(line.Bot.DamageThisWave / most, 1);
                card.share.color = card.stripe.color;
                card.share.transform.parent.gameObject.SetActive(line.Bot.Spec.role != RobotRole.Buffer);
                card.advice.text = line.Advice ?? "";
                card.advice.gameObject.SetActive(line.Advice != null);
            }
            for (int i = lines.Length; i < debriefCards.Count; i++) debriefCards[i].root.gameObject.SetActive(false);
            debriefRoot.gameObject.SetActive(lines.Length > 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(debriefRoot);
            float height = lines.Length == 0 ? 0 : debriefRoot.rect.height;
            debriefRoot.anchoredPosition = Vector2.zero;
            var margin = rewardItems.margin; margin.w = height > 0 ? height + 28 : 0; rewardItems.margin = margin;
        }
        TextMeshProUGUI Text(Transform parent, float size)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            text.font = rewardItems.font; text.fontSize = size; text.color = rewardItems.color;
            text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
            return text;
        }
        DebriefCard MakeDebriefCard()
        {
            var root = new GameObject("Robot card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            root.SetParent(debriefRoot, false);
            var back = UITheme.Style(root.GetComponent<Image>(), UITheme.FlatCard, UITheme.PaperDeep); back.pixelsPerUnitMultiplier = 1.4f; back.raycastTarget = false;
            var column = root.GetComponent<VerticalLayoutGroup>(); column.padding = new RectOffset(22, 14, 10, 12); column.spacing = 6;
            column.childControlWidth = column.childControlHeight = true; column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            var stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
            stripe.rectTransform.SetParent(root, false); stripe.GetComponent<LayoutElement>().ignoreLayout = true; stripe.raycastTarget = false;
            stripe.rectTransform.anchorMin = Vector2.zero; stripe.rectTransform.anchorMax = new Vector2(0, 1); stripe.rectTransform.pivot = new Vector2(0, .5f);
            stripe.rectTransform.sizeDelta = new Vector2(8, -16); stripe.rectTransform.anchoredPosition = new Vector2(8, 0); stripe.rectTransform.anchoredPosition = Vector2.zero;
            var title = Text(root, rewardItems.fontSize * .85f);
            // The share bar: a dark track with a fill whose width is this robot's damage over the top damage dealer's.
            var track = new GameObject("Share", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<Image>();
            track.rectTransform.SetParent(root, false); track.color = UITheme.Hex(0xDCCFB6); track.raycastTarget = false;
            track.GetComponent<LayoutElement>().preferredHeight = 6;
            var share = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            share.rectTransform.SetParent(track.rectTransform, false); share.raycastTarget = false;
            share.rectTransform.anchorMin = Vector2.zero; share.rectTransform.anchorMax = Vector2.one; share.rectTransform.sizeDelta = Vector2.zero;
            var advice = Text(root, rewardItems.fontSize * .76f); advice.color = UITheme.Hex(0x7A4E0C);
            var card = new DebriefCard { root = root, stripe = stripe, share = share, title = title, advice = advice };
            debriefCards.Add(card);
            return card;
        }
        // The tutorial's reward focus (1-1 E06a) ends just under the reward text, so the dialogue box below does not cover it.
        const float FocusMargin = 24;
        void FitRewardFocus()
        {
            if (!rewardFocus) return;
            rewardItems.ForceMeshUpdate();
            var card = (RectTransform)rewardFocus.parent;
            var textBottom = rewardItems.rectTransform.TransformPoint(new Vector3(0, rewardItems.textBounds.min.y));
            float bottom = card.InverseTransformPoint(textBottom).y - FocusMargin;
            rewardFocus.anchorMin = Vector2.zero; rewardFocus.anchorMax = Vector2.one;
            rewardFocus.offsetMax = Vector2.zero; rewardFocus.offsetMin = new Vector2(0, Mathf.Max(0, bottom - card.rect.yMin));
        }
        void RefreshRewardPanel(DefenseSimulation sim, bool reward)
        {
            claimButton.interactable = sim.Phase != BattlePhase.Victory || sim.CanAdvanceStage || battle.FromStageMap;
            bool retry = battle.CanRetryWave;
            SplitDefeatButtons(retry);
            claimLabel.text = reward ? (sim.WaveIndex + 1 == sim.Setup.Waves.Length ? "보상 수령 · 스테이지 완료" : "보상 수령 · 다음 준비") :
                retry ? $"WAVE {sim.Checkpoint.Wave + 1} 다시 도전\n<size=70%>배치·코드 그대로</size>" : sim.Phase != BattlePhase.Victory ? "이 스테이지 재도전" : battle.FromStageMap ? "스테이지 맵으로" : sim.CanAdvanceStage ? $"스테이지 {battle.Stage + 1}로 진행" : "3스테이지 완료";
            if (shownRewardPhase != sim.Phase || shownRewardWave != sim.WaveIndex || shownRewardStage != battle.Stage)
            {
                // Korean first; the banner keeps the English call as a small line above it.
                bool victory = sim.Phase == BattlePhase.Victory;
                string english = reward ? $"WAVE {sim.WaveIndex + 1} " + (sim.LastWaveTimedOut ? "TIME UP" : "CLEAR") :
                    victory ? $"STAGE {battle.Stage} COMPLETE" : sim.ObjectiveFailed ? "MINIONS ESCAPED" : "CORE LOST";
                rewardTitle.text = reward ? (sim.LastWaveTimedOut ? $"웨이브 {sim.WaveIndex + 1} 시간 종료" : $"웨이브 {sim.WaveIndex + 1} 방어 성공!") :
                    victory ? $"스테이지 {battle.Stage} 클리어!" : sim.ObjectiveFailed ? "보스 전에 잡몹을 놓쳤어요" : "코어가 무너졌어요";
                if (shownRewardPhase != sim.Phase) ShowEndBanner($"<size=40%>{english}</size>\n{rewardTitle.text}", sim.Phase == BattlePhase.Defeat);
                rewardItems.text = reward ? (sim.PendingDrops.Count == 0 ? "지급할 블록이 없습니다." : "") :
                    (sim.Phase == BattlePhase.Victory ? battle.FromStageMap ? "5개 웨이브 방어 성공 · 스테이지 맵에서 다음 스테이지를 선택하세요." : sim.CanAdvanceStage ? "5개 웨이브 방어 성공 · 보유 블록과 로봇을 다음 스테이지로 가져갑니다." : "1~3스테이지, 총 15개 웨이브를 완료했습니다." : "방어 실패 · 배치와 코드를 바꿔 도전하세요") + $"\n\n처치 {sim.Kills}   /   돌파 {sim.Leaks}";
                if (sim.LastRush > 0) rewardItems.text += $"\n\n시간 종료 · 남은 적 {sim.LastRush}마리가 코어로 돌진 (코어 -{sim.LastRushDamage})";
                else if (reward) rewardItems.text += $"\n\n잔여 적 {sim.LastSurvivors}" + (sim.WaveIndex + 1 < sim.Setup.Waves.Length ? " · 다음 웨이브에 추가" : " · 최종 기록");
                if (sim.Phase == BattlePhase.Victory) rewardItems.text = StarLine(sim) + "\n\n" + rewardItems.text;
                ShowRewardBlocks(reward ? sim.PendingDrops : null);
                ShowStars(sim);
                ShowDebrief(sim);
                ShowGuide(sim);
                Canvas.ForceUpdateCanvases(); rewardScroll.verticalNormalizedPosition = 1;
                FitRewardFocus();
            }
        }
    }
}
