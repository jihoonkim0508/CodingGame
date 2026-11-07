using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    [DefaultExecutionOrder(200)]
    public sealed class DefensePlacementDock : MonoBehaviour
    {
        [SerializeField] RectTransform roster, action, divider;
        [SerializeField] Button[] robots;
        [SerializeField] TMP_Text notice;
        // This branch's robot cards (photo, name and count) rather than PR #19's 126 px emblem cards (9차 99).
        const float CardWidth = 188;
        int previousMask = -1;
        float previousWidth = -1;

        public void Configure(RectTransform list, RectTransform start, RectTransform rule, Button[] buttons, TMP_Text message)
        {
            roster = list; action = start; divider = rule; robots = buttons; notice = message;
            previousMask = -1;
        }
        void OnEnable() { previousMask = -1; }
        // The editor lays the dock out once (13차) so the scene shows it as the battle does; at run time it follows which robots
        // are on the deck.
        public void Layout() { previousMask = -1; LateUpdate(); }
        void LateUpdate()
        {
            if (!roster || !action || robots == null || !(transform.parent is RectTransform parent)) return;
            int mask = 0, count = 0;
            for (int i = 0; i < robots.Length; i++) if (robots[i] && robots[i].gameObject.activeSelf) { mask |= 1 << i; count++; }
            float available = Mathf.Max(360, parent.rect.width - 48);
            if (previousMask == mask && Mathf.Approximately(previousWidth, available)) return;
            previousMask = mask; previousWidth = available;
            var root = (RectTransform)transform;
            float width = Mathf.Min(available, Mathf.Max(760, count * (CardWidth + 10) - 10 + 292));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, 0);
            root.anchoredPosition = new Vector2(0, 16); root.sizeDelta = new Vector2(width, 164);
            float actionWidth = Mathf.Min(220, width * .29f), rosterWidth = width - actionWidth - 72;
            Place(roster, 24, 14, rosterWidth, 78);
            Place(action, width - 24 - actionWidth, 14, actionWidth, 78);
            Place(divider, width - 37 - actionWidth, 20, 1, 66);
            float cardWidth = count > 0 ? Mathf.Min(CardWidth, (rosterWidth - (count - 1) * 10) / count) : CardWidth;
            float x = Mathf.Max(0, (rosterWidth - (count * cardWidth + Mathf.Max(0, count - 1) * 10)) / 2);
            foreach (var button in robots) if (button && button.gameObject.activeSelf)
            {
                Place((RectTransform)button.transform, x, 0, cardWidth, 78);
                x += cardWidth + 10;
            }
            if (notice) { notice.enableAutoSizing = true; notice.fontSizeMin = 12; notice.fontSizeMax = 15; }
        }
        static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        }
    }
}
