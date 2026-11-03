using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    // Visual state only; the authored Buttons retain their existing battle callbacks.
    [DefaultExecutionOrder(110)]
    public sealed class DefenseCombatControls : MonoBehaviour
    {
        [SerializeField] DefenseBattle battle;
        [SerializeField] Image pauseBackground;
        [SerializeField] GameObject pauseIcon, resumeIcon;
        [SerializeField] TMP_Text speedValue;
        int shownPhase=-1;
        float shownSpeed=-1;
        public void Configure(DefenseBattle source,Image background,GameObject pause,GameObject resume,TMP_Text speed)
        {battle=source;pauseBackground=background;pauseIcon=pause;resumeIcon=resume;speedValue=speed;shownPhase=-1;shownSpeed=-1;}
        void OnEnable(){shownPhase=-1;shownSpeed=-1;}
        void LateUpdate()
        {
            if(!battle||battle.Simulation==null)return;
            int phase=(int)battle.Simulation.Phase;
            if(shownPhase!=phase)
            {
                shownPhase=phase;bool paused=battle.Simulation.Phase==BattlePhase.Paused;
                pauseIcon.SetActive(!paused);resumeIcon.SetActive(paused);
                pauseBackground.color=paused?new Color(.10f,.32f,.35f):new Color(.11f,.23f,.34f);
            }
            if(!Mathf.Approximately(shownSpeed,battle.BattleSpeed))
            {shownSpeed=battle.BattleSpeed;speedValue.color=shownSpeed>1?new Color(.6f,.95f,.87f):new Color(.91f,.97f,1);}
        }
    }
}
