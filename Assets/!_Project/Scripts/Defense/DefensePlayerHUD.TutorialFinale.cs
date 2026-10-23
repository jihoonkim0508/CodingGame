using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed partial class DefensePlayerHUD
    {
        RectTransform finaleFailure;
        Button retryFinale, quitFinale;
        public bool FinaleFailureVisible => finaleFailure && finaleFailure.gameObject.activeInHierarchy;
        void RefreshFinaleFailure(bool visible)
        {
            if (!finaleFailure && !visible) return;
            if (!finaleFailure)
            {
                finaleFailure = new GameObject("1S-5W failure choices", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                finaleFailure.SetParent(canvasRoot.transform, false);
                finaleFailure.anchorMin = Vector2.zero; finaleFailure.anchorMax = Vector2.one; finaleFailure.sizeDelta = Vector2.zero;
                finaleFailure.GetComponent<Image>().color = new Color(0, 0, 0, .72f);
                var heading = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                heading.transform.SetParent(finaleFailure, false);
                heading.font = rewardTitle.font; heading.text = "코어 방어 실패!"; heading.fontSize = 52; heading.color = new Color(1, .4f, .4f);
                heading.alignment = TextAlignmentOptions.Center; heading.raycastTarget = false;
                heading.rectTransform.anchorMin = heading.rectTransform.anchorMax = new Vector2(.5f, .5f);
                heading.rectTransform.anchoredPosition = new Vector2(0, 100); heading.rectTransform.sizeDelta = new Vector2(850, 100);
                Button Choice(string name, string label, float x, UnityEngine.Events.UnityAction action)
                {
                    var button = Instantiate(claimButton, finaleFailure); button.name = name;
                    button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(action);
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(x, -20); rect.sizeDelta = new Vector2(370, 85);
                    button.GetComponentInChildren<TMP_Text>(true).text = label; button.gameObject.SetActive(true);
                    return button;
                }
                retryFinale = Choice("Retry 1S-5W", "1S-5W 다시 시작", -200, battle.RetryTutorialFinale);
                quitFinale = Choice("Exit to desktop", "바탕화면으로 나가기", 200, battle.QuitTutorialFinale);
            }
            finaleFailure.gameObject.SetActive(visible);
            if (!visible) return;
            if (banner) banner.transform.parent.gameObject.SetActive(false);
            battle.Tutorial.RegisterUI(TutorialKeys.RetryFinale, (RectTransform)retryFinale.transform);
            battle.Tutorial.RegisterUI(TutorialKeys.Quit, (RectTransform)quitFinale.transform);
            retryFinale.interactable = battle.TutorialAllows(TutorialKeys.RetryFinale);
            quitFinale.interactable = battle.TutorialAllows(TutorialKeys.Quit);
        }
    }
}
