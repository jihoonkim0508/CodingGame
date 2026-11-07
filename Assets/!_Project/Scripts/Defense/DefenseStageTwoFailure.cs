using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed partial class DefensePlayerHUD
    {
        GameObject stageTwoFailure;
        void RefreshStageTwoFailure(DefenseSimulation sim)
        {
            bool failed = battle.Stage == 2 && sim.WaveIndex == 4 && sim.Phase == BattlePhase.Defeat;
            claimButton.gameObject.SetActive(!failed);
            if (!failed) { if (stageTwoFailure) stageTwoFailure.SetActive(false); return; }
            if (!stageTwoFailure)
            {
                var overlay = new GameObject("Stage 2 failure choices", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                overlay.SetParent(canvasRoot.transform, false); overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
                overlay.offsetMin = overlay.offsetMax = Vector2.zero; overlay.GetComponent<Image>().color = new Color(0, 0, 0, .88f);
                stageTwoFailure = overlay.gameObject;
                var text = FailureText(overlay, "실패 안내", new Vector2(0, 140), new Vector2(900, 190), 30);
                text.text = "<b><color=#FF7777>튜토리얼 2 도전 실패!</color></b>\n\n이번에는 보스를 막지 못했어. 반복문과 로봇 배치를 다시 살펴보고 튜토리얼 2를 처음부터 다시 해 보자.";
                FailureButton(overlay, "튜토리얼 2 다시 시작", new Vector2(-220, -80), () => battle.Restart());
                FailureButton(overlay, "바탕화면으로 나가기", new Vector2(220, -80), GameFlow.QuitGame);
            }
            stageTwoFailure.SetActive(true); stageTwoFailure.transform.SetAsLastSibling();
        }
        TMP_Text FailureText(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false); text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size;
            text.font = rewardTitle.font; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
        void FailureButton(RectTransform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var button = Instantiate(claimButton, parent); button.name = label; button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(action);
            var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(400, 70);
            button.GetComponentInChildren<TMP_Text>(true).text = label; button.gameObject.SetActive(true); button.interactable = true;
        }
    }
}
