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
            bool ready = sim.CanPrepare, reward = sim.Phase == BattlePhase.Reward;
            bool ended = sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat;
            bool fighting = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            string state = ended ? "작전 종료" : fighting ? "전투 중" : "정비 중";
            counters.text = $"{battle.Stage}-{sim.WaveIndex + 1:00}     {state}     WAVE {sim.WaveIndex + 1}/{sim.Setup.Waves.Length}     적 {sim.RemainingEnemies}     {Math.Ceiling(sim.WaveRemaining):00}s     코인 {sim.Coins:N0}";
            combatControls.SetActive(fighting);
            RefreshEnemyCards(sim);
            guide.text = ready ? $"{sim.Lesson?.topic} · 로봇 배치 → 코드 작성 → 전투 시작" :
                reward ? "WAVE CLEAR  ·  블록을 수령하고 다음 전투를 준비하세요" : ended ? "작전 종료" : "DEFENDING  ·  블록 드랍 " + sim.PendingDrops.Count + "개  /  전투 종료 후 수령";
            notice.text = battle.StatusMessage;
            inventory.text = $"설치 {sim.Robots.Count} / {sim.Setup.RobotLimit}대  ·  버프형 최대 1대";
            inventory.color = sim.Robots.Count >= sim.Setup.RobotLimit ? new Color(1, .45f, .4f) : new Color(.75f, .9f, .95f);
            startButton.interactable = ready;
            pauseButton.interactable = speedButton.interactable = fighting;
            pauseLabel.text = sim.Phase == BattlePhase.Paused ? "계속" : "일시정지"; speedLabel.text = battle.BattleSpeed + "x";
            RefreshRobotButtons(battle, robotButtons, robotLabels, "\nx");
            RefreshRobotActions(sim);
            rewardPanel.SetActive(reward || ended);
            if (reward || ended) RefreshRewardPanel(sim, reward);
            shownRewardPhase = sim.Phase; shownRewardWave = sim.WaveIndex; shownRewardStage = battle.Stage;
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
        void RefreshRobotActions(DefenseSimulation sim)
        {
            var selected = sim.FindRobot(battle.SelectedId);
            robotActions.gameObject.SetActive(selected != null && battle.CanShowRobotActions);
            if (!robotActions.gameObject.activeSelf) return;
            var screen = battleCamera.WorldToScreenPoint(DefenseViews.World(selected.Position, .6f));
            var canvas = (RectTransform)canvasRoot.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var point);
            point.x = Mathf.Clamp(point.x, canvas.rect.xMin + 160, canvas.rect.xMax - 160);
            point.y = Mathf.Clamp(point.y, canvas.rect.yMin + 230, canvas.rect.yMax - 170);
            robotActions.anchoredPosition = point;
            upgradeRobotButton.interactable = sim.UpgradeError(selected.Id) == null;
            recallRobotButton.interactable = sim.RecallUnlocked;
            upgradeRobotLabel.text = !sim.UpgradeUnlocked ? "레벨업\n1-2 해금" : selected.IsMaxLevel ? "최고 레벨" : $"레벨업\n{sim.UpgradeCost} 코인";
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
                copy.anchorMin = copy.anchorMax = copy.pivot = new Vector2(0, 1); copy.sizeDelta = new Vector2(w, h); copy.anchoredPosition = new Vector2(x, -y);
                var count = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                count.rectTransform.SetParent(rewardBlocks, false); count.rectTransform.anchorMin = count.rectTransform.anchorMax = count.rectTransform.pivot = new Vector2(0, 1);
                count.rectTransform.anchoredPosition = new Vector2(x + w + 6, -y); count.rectTransform.sizeDelta = new Vector2(48, h);
                count.font = rewardItems.font; count.fontSize = 26; count.alignment = TextAlignmentOptions.MidlineLeft; count.text = "x" + group.Count(); count.raycastTarget = false;
                x += w + 60; rowHeight = Mathf.Max(rowHeight, h);
            }
            rewardItems.margin = new Vector4(0, y + rowHeight + 16, 0, 0);
        }
        void RefreshRewardPanel(DefenseSimulation sim, bool reward)
        {
            claimButton.interactable = sim.Phase != BattlePhase.Victory || sim.CanAdvanceStage || battle.FromStageMap;
            claimLabel.text = reward ? (sim.WaveIndex + 1 == sim.Setup.Waves.Length ? "보상 수령 · 스테이지 완료" : "보상 수령 · 다음 준비") :
                sim.Phase != BattlePhase.Victory ? "이 스테이지 재도전" : battle.FromStageMap ? "스테이지 맵으로" : sim.CanAdvanceStage ? $"스테이지 {battle.Stage + 1}로 진행" : "3스테이지 완료";
            if (shownRewardPhase != sim.Phase || shownRewardWave != sim.WaveIndex || shownRewardStage != battle.Stage)
            {
                rewardTitle.text = reward ? $"WAVE {sim.WaveIndex + 1}  /  " + (sim.LastWaveTimedOut ? "TIME UP" : "CLEAR") : sim.Phase == BattlePhase.Victory ? $"STAGE {battle.Stage} COMPLETE" : "CORE LOST";
                rewardItems.text = reward ? (sim.PendingDrops.Count == 0 ? "지급할 블록이 없습니다." : "") :
                    (sim.Phase == BattlePhase.Victory ? battle.FromStageMap ? "5개 웨이브 방어 성공 · 스테이지 맵에서 다음 스테이지를 선택하세요." : sim.CanAdvanceStage ? "5개 웨이브 방어 성공 · 보유 블록과 로봇을 다음 스테이지로 가져갑니다." : "1~3스테이지, 총 15개 웨이브를 완료했습니다." : "방어 실패 · 배치와 코드를 바꿔 도전하세요") + $"\n\n처치 {sim.Kills}   /   돌파 {sim.Leaks}";
                if (reward) rewardItems.text += $"\n\n잔여 적 {sim.LastSurvivors}" + (sim.WaveIndex + 1 < sim.Setup.Waves.Length ? " · 다음 웨이브에 추가" : " · 최종 기록");
                ShowRewardBlocks(reward ? sim.PendingDrops : null);
                Canvas.ForceUpdateCanvases(); rewardScroll.verticalNormalizedPosition = 1;
            }
        }
    }
}
