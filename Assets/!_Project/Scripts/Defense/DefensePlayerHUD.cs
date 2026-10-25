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
                !upcoming || !canvasRoot || !rewardPanel || !counters || !guide || !inventory || !notice || !pauseLabel || !speedLabel ||
                !rewardTitle || !rewardItems || !claimLabel || !startButton || !pauseButton || !speedButton || !claimButton ||
                robotLabels.Length != 6 || robotLabels.Any(t => !t) || robotButtons.Length != 6 || robotButtons.Any(b => !b)) throw new InvalidOperationException("DefensePlayerHUD Inspector 참조를 연결하세요.");
            startButton.onClick.AddListener(battle.StartBattle);
            pauseButton.onClick.AddListener(battle.TogglePause); speedButton.onClick.AddListener(battle.ToggleSpeed);
            claimButton.onClick.AddListener(battle.ContinueCampaign);
            upgradeRobotButton.onClick.AddListener(battle.UpgradeSelected);
            developRobotButton.onClick.AddListener(battle.DevelopSelected);
            recallRobotButton.onClick.AddListener(battle.RemoveSelected);
            toastPosition = ((RectTransform)placementToast.transform).anchoredPosition;
            placementToast.alpha = 0;
            upcoming.text = "다음 웨이브";
            goalRect = (RectTransform)guide.transform.parent;
            goalRect.anchorMin = goalRect.anchorMax = new Vector2(.5f, 1);
            goalRect.sizeDelta = new Vector2(Mathf.Min(800, ((RectTransform)canvasRoot.transform).rect.width - 48), 56);
            goalRect.anchoredPosition = new Vector2(0, -128);
            goalPosition = goalRect.anchoredPosition;
            goalScale = goalRect.localScale;
            guide.alignment = TextAlignmentOptions.Center;
            goalBackground = goalRect.GetComponent<Image>();
            if (goalBackground) { goalBackground.raycastTarget = false; goalColor = goalBackground.color; }
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => battle.ChooseRobot(index)); }
            enemyImages = enemyCards.Select(c => c.GetComponentInChildren<RawImage>(true)).ToArray();
            enemyHovers = enemyCards.Select(c => c.GetComponent<DefenseHoverInfo>()).ToArray();
        }
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
            bool ready = sim.CanPrepare, reward = sim.Phase == BattlePhase.Reward;
            bool ended = sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat;
            bool fighting = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            string state = ended ? "작전 종료" : fighting ? "전투 중" : "정비 중";
            counters.text = $"{battle.Stage}-{sim.WaveIndex + 1:00}     {state}     WAVE {sim.WaveIndex + 1}/{sim.Setup.Waves.Length}     적 {sim.RemainingEnemies}     {Math.Ceiling(sim.WaveRemaining):00}s     코인 {sim.Coins:N0}";
            combatControls.SetActive(fighting);
            RefreshEnemyCards(sim);
            bool freePlayGoal = battle.Tutorial && battle.Tutorial.ShowFreePlayGoal && !reward && !ended;
            if (freePlayGoal && !goalRect.gameObject.activeSelf) goalAnimationStart = Time.unscaledTime;
            guide.transform.parent.gameObject.SetActive(freePlayGoal);
            if (freePlayGoal)
            {
                guide.text = sim.WaveIndex == 1 ? "목표 · 새 전사를 배치하고 변수로 적을 찾아 공격하기" :
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
            inventory.text = $"설치 {sim.Robots.Count} / {sim.Setup.RobotLimit}대  ·  버프형 최대 1대";
            inventory.color = sim.Robots.Count >= sim.Setup.RobotLimit ? new Color(1, .45f, .4f) : new Color(.75f, .9f, .95f);
            startButton.interactable = ready;
            pauseButton.interactable = speedButton.interactable = fighting;
            pauseLabel.text = sim.Phase == BattlePhase.Paused ? "계속" : "일시정지";
            speedLabel.text = fighting && battle.Tutorial && Mathf.Approximately(battle.Tutorial.BattlePace, .2f)
                ? "0.2x" : battle.BattleSpeed + "x";
            RefreshRobotButtons(battle, robotButtons, robotLabels, "\nx");
            RefreshRobotActions(sim);
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
        void RefreshEnemyCards(DefenseSimulation sim)
        {
            // Cards follow the wave's own enemy types (in spawn order), so tutorial enemies get cards too.
            var types = sim.UpcomingWave.GroupBy(g => g.DefinitionIndex).Take(enemyCards.Length).ToArray();
            upcoming.gameObject.SetActive(types.Length > 0);
            for (int i = 0; i < enemyCards.Length; i++)
            {
                enemyCards[i].SetActive(i < types.Length);
                if (i >= types.Length) continue;
                var definition = battle.EnemyAt(types[i].Key);
                if (definition.icon && enemyImages[i]) enemyImages[i].texture = definition.icon;
                if (enemyHovers[i]) enemyHovers[i].SetEnemy(definition);
                enemyCounts[i].text = "× " + types[i].Sum(g => g.Count);
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
            foreach (var button in new[] { upgradeRobotButton, developRobotButton })
            { var r = (RectTransform)button.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, above + r.rect.height / 2); }
            var recall = (RectTransform)recallRobotButton.transform;
            recall.anchoredPosition = new Vector2(recall.anchoredPosition.x, -below - recall.rect.height / 2);
            upgradeRobotButton.interactable = sim.UpgradeError(selected.Id) == null;
            recallRobotButton.interactable = sim.RecallUnlocked;
            upgradeRobotLabel.text = !sim.UpgradeUnlocked ? "레벨업\n1-2 해금" : selected.IsMaxLevel ? "최고 레벨" : $"레벨업\n{sim.UpgradeCost} 코인";
        }
        // The wave's result in large letters over the screen; the result panel fades in under it, then the rewarded blocks
        // pop out in turn. Driven from LateUpdate by the time it started, so an interrupted frame never leaves the panel or a
        // block invisible; a later result (the stage, right after the last wave) replaces the banner.
        const float BannerSeconds = 1.4f, PanelFadeFrom = .8f, BlocksFrom = 1.1f, BlockStagger = .15f, BlockPop = .3f;
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
                var shade = strip.GetComponent<Image>(); shade.color = new Color(0, 0, 0, .6f); shade.raycastTarget = false;
                bannerGroup = strip.GetComponent<CanvasGroup>(); bannerGroup.blocksRaycasts = false; bannerGroup.interactable = false;
                banner = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                banner.rectTransform.SetParent(strip, false);
                banner.rectTransform.anchorMin = banner.rectTransform.anchorMax = new Vector2(.5f, .5f); banner.rectTransform.sizeDelta = new Vector2(1600, 220);
                banner.font = rewardTitle.font; banner.fontSize = 120; banner.fontStyle = FontStyles.Bold; banner.alignment = TextAlignmentOptions.Center; banner.raycastTarget = false;
            }
            banner.transform.parent.SetAsLastSibling();
            banner.color = lost ? new Color(1, .35f, .35f) : rewardTitle.color; banner.text = text.Replace("  /  ", "  ");
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
            claimLabel.text = reward ? (sim.WaveIndex + 1 == sim.Setup.Waves.Length ? "보상 수령 · 스테이지 완료" : "보상 수령 · 다음 준비") :
                sim.Phase != BattlePhase.Victory ? "이 스테이지 재도전" : battle.FromStageMap ? "스테이지 맵으로" : sim.CanAdvanceStage ? $"스테이지 {battle.Stage + 1}로 진행" : "3스테이지 완료";
            if (shownRewardPhase != sim.Phase || shownRewardWave != sim.WaveIndex || shownRewardStage != battle.Stage)
            {
                rewardTitle.text = reward ? $"WAVE {sim.WaveIndex + 1}  /  " + (sim.LastWaveTimedOut ? "TIME UP" : "CLEAR") :
                    sim.Phase == BattlePhase.Victory ? $"STAGE {battle.Stage} COMPLETE" : sim.ObjectiveFailed ? "MINIONS ESCAPED" : "CORE LOST";
                if (shownRewardPhase != sim.Phase) ShowEndBanner(rewardTitle.text, sim.Phase == BattlePhase.Defeat);
                rewardItems.text = reward ? (sim.PendingDrops.Count == 0 ? "지급할 블록이 없습니다." : "") :
                    (sim.Phase == BattlePhase.Victory ? battle.FromStageMap ? "5개 웨이브 방어 성공 · 스테이지 맵에서 다음 스테이지를 선택하세요." : sim.CanAdvanceStage ? "5개 웨이브 방어 성공 · 보유 블록과 로봇을 다음 스테이지로 가져갑니다." : "1~3스테이지, 총 15개 웨이브를 완료했습니다." : "방어 실패 · 배치와 코드를 바꿔 도전하세요") + $"\n\n처치 {sim.Kills}   /   돌파 {sim.Leaks}";
                if (reward) rewardItems.text += $"\n\n잔여 적 {sim.LastSurvivors}" + (sim.WaveIndex + 1 < sim.Setup.Waves.Length ? " · 다음 웨이브에 추가" : " · 최종 기록");
                ShowRewardBlocks(reward ? sim.PendingDrops : null);
                Canvas.ForceUpdateCanvases(); rewardScroll.verticalNormalizedPosition = 1;
                FitRewardFocus();
            }
        }
    }
}
