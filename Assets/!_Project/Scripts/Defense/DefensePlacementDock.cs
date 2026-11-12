using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    [DefaultExecutionOrder(200)]
    public sealed class DefensePlacementDock : MonoBehaviour
    {
        const float PreferredCardWidth = 152;
        const float CardGap = 12;

        [SerializeField] RectTransform roster, action, divider;
        [SerializeField] Button[] robots;
        [SerializeField] TMP_Text notice;
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
        // Lifted off the screen's bottom by the next-wave tab's gap from the left edge (UpcomingTab x 24); the map's backdrop
        // shows under it (play feedback 2026-11-12).
        const float BottomGap = 24;
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
            float gapWidth = Mathf.Max(0, count - 1) * CardGap;
            float width = Mathf.Min(available, Mathf.Max(760, count * PreferredCardWidth + gapWidth + 292));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, 0);
            root.anchoredPosition = new Vector2(0, BottomGap); root.sizeDelta = new Vector2(width, 164);
            float actionWidth = Mathf.Min(220, width * .29f), rosterWidth = width - actionWidth - 72;
            Place(roster, 24, 14, rosterWidth, 78);
            Place(action, width - 24 - actionWidth, 14, actionWidth, 78);
            Place(divider, width - 37 - actionWidth, 20, 1, 66);
            float cardWidth = count > 0 ? Mathf.Min(PreferredCardWidth, (rosterWidth - gapWidth) / count) : PreferredCardWidth;
            float x = Mathf.Max(0, (rosterWidth - (count * cardWidth + gapWidth)) / 2);
            foreach (var button in robots) if (button && button.gameObject.activeSelf)
            {
                Place((RectTransform)button.transform, x, 0, cardWidth, 78);
                x += cardWidth + CardGap;
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
