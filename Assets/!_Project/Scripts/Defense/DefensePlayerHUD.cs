using System;
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
        void Start()
        {
            if (!battle || !combatControls || enemyCards.Length != 3 || enemyCounts.Length != 3 || enemyCards.Any(x => !x) || enemyCounts.Any(x => !x) ||
                !upcoming || !canvasRoot || !rewardPanel || !counters || !guide || !inventory || !notice || !pauseLabel || !speedLabel ||
                !rewardTitle || !rewardItems || !claimLabel || !startButton || !pauseButton || !speedButton || !claimButton ||
                robotLabels.Length != 6 || robotLabels.Any(t => !t) || robotButtons.Length != 6 || robotButtons.Any(b => !b)) throw new InvalidOperationException("DefensePlayerHUD Inspector 참조를 연결하세요.");
            startButton.onClick.AddListener(battle.StartBattle);
            pauseButton.onClick.AddListener(battle.TogglePause); speedButton.onClick.AddListener(battle.ToggleSpeed);
            claimButton.onClick.AddListener(() => { if (battle.Simulation.Phase == BattlePhase.Reward) battle.ClaimRewards(); else battle.Restart(); });
            for (int i = 0; i < robotButtons.Length; i++) { int index = i; robotButtons[i].onClick.AddListener(() => battle.ChooseRobot(index)); }
        }
        public void SetVisible(bool visible) => canvasRoot.SetActive(visible);
        void LateUpdate()
        {
            var sim = battle.Simulation;
            if (sim == null) return;
            bool ready = sim.CanPrepare, reward = sim.Phase == BattlePhase.Reward;
            bool ended = sim.Phase == BattlePhase.Victory || sim.Phase == BattlePhase.Defeat;
            bool fighting = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            string state = ended ? "작전 종료" : fighting ? "전투 중" : "정비 중";
            counters.text = $"STAGE {battle.Stage}     {state}     WAVE {sim.WaveIndex + 1}/{sim.Setup.Waves.Length}     적 {sim.RemainingEnemies}     {Math.Ceiling(sim.WaveRemaining):00}s     코인 {sim.Coins:N0}";
            combatControls.SetActive(fighting);
            upcoming.text = "다음 웨이브";
            var next = sim.UpcomingWave;
            for (int i = 0; i < enemyCards.Length; i++)
            {
                int count = next.Where(g => g.DefinitionIndex == i).Sum(g => g.Count);
                enemyCards[i].SetActive(count > 0); enemyCounts[i].text = "× " + count;
            }
            guide.text = ready ? "01  로봇 배치     →     02  클릭하여 코드 작성     →     03  전투 시작" :
                reward ? "WAVE CLEAR  ·  블록을 수령하고 다음 전투를 준비하세요" : ended ? "작전 종료" : "DEFENDING  ·  블록 드랍 " + sim.PendingDrops.Count + "개  /  전투 종료 후 수령";
            notice.text = battle.StatusMessage;
            inventory.text = "";
            startButton.interactable = ready;
            pauseButton.interactable = sim.Phase == BattlePhase.Running || sim.Phase == BattlePhase.Paused;
            speedButton.interactable = pauseButton.interactable;
            pauseLabel.text = sim.Phase == BattlePhase.Paused ? "계속" : "일시정지"; speedLabel.text = battle.BattleSpeed + "x";
            for (int i = 0; i < robotButtons.Length; i++)
            {
                int count = sim.RobotAvailable(battle.DefinitionRole(i));
                robotButtons[i].interactable = ready && count > 0;
                robotLabels[i].text = battle.DefinitionName(i) + "\n× " + count;
            }
            rewardPanel.SetActive(reward || ended);
            if (reward || ended)
            {
                rewardTitle.text = reward ? $"WAVE {sim.WaveIndex + 1}  /  " + (sim.LastWaveTimedOut ? "TIME UP" : "CLEAR") : sim.Phase == BattlePhase.Victory ? "MISSION COMPLETE" : "CORE LOST";
                rewardItems.text = reward ? (sim.PendingDrops.Count == 0 ? "드랍한 블록이 없습니다." : string.Join("\n", sim.PendingDrops.GroupBy(k => k).OrderBy(g => g.Key).Select(g => DefenseProgression.Label(g.Key) + "   × " + g.Count()))) :
                    (sim.Phase == BattlePhase.Victory ? "5개 웨이브 방어 성공" : "방어 실패 · 배치와 코드를 바꿔 도전하세요") + $"\n\n처치 {sim.Kills}   /   돌파 {sim.Leaks}";
                claimLabel.text = reward ? (sim.WaveIndex + 1 == sim.Setup.Waves.Length ? "보상 수령 · 작전 완료" : "보상 수령 · 다음 준비") : "다시 시작";
                if (reward) rewardItems.text += $"\n\n잔여 적 {sim.LastSurvivors}" + (sim.WaveIndex + 1 < sim.Setup.Waves.Length ? " · 다음 웨이브에 추가" : " · 최종 기록");
            }
        }
    }
}
