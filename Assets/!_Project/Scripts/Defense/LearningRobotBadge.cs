using UnityEngine;
using UnityEngine.UI;
namespace CodingGame.Defense
{
    public sealed class LearningRobotBadge : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] GameObject selectionOutline;
        [SerializeField] CanvasGroup contents;
        int oldCount = -1;
        bool selected, available;

        public void Setup(Image image)
        {
            background = image;
            oldCount = -1;
        }

        public void SetupCard(Image image, GameObject outline, CanvasGroup group)
        {
            Setup(image);
            selectionOutline = outline;
            contents = group;
        }

        void OnEnable() => oldCount = -1;

        public void Refresh(int count, bool on, bool interactable = true)
        {
            on &= interactable;
            if (oldCount == count && selected == on && available == interactable) return;
            oldCount = count;
            selected = on;
            available = interactable;
            if (background)
                background.color = on
                    ? (selectionOutline ? new Color(.70f, 1f, .90f) : new Color(.65f, .9f, 1f))
                    : Color.white;
            if (selectionOutline) selectionOutline.SetActive(on);
            if (contents) contents.alpha = interactable ? 1f : .48f;
        }
    }
}
