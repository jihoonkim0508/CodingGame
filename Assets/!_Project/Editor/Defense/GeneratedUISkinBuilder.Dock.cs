using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        static void TriangleDialogueNavigation(Transform dialog)
        {
            foreach (string name in new[] { "PreviousText", "NextText" })
            {
                bool left = name == "PreviousText";
                var button = dialog.Find(name).GetComponent<Button>();
                SetRect((RectTransform)button.transform, new Vector2(1, 0), new Vector2(left ? -104 : -54, 58), new Vector2(44, 44), new Vector2(1, 0));
                var hit = button.GetComponent<Image>(); Plain(hit, Color.clear); hit.raycastTarget = true;
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) { text.text = ""; text.gameObject.SetActive(false); }
                var rect = Child(button.transform, "NavigationTriangle");
                SetRect(rect, new Vector2(.5f, .5f), Vector2.zero, new Vector2(26, 30), new Vector2(.5f, .5f));
                if (!rect.GetComponent<CanvasRenderer>()) rect.gameObject.AddComponent<CanvasRenderer>();
                var arrow = rect.GetComponent<DialogueNavArrow>(); if (!arrow) arrow = rect.gameObject.AddComponent<DialogueNavArrow>();
                arrow.Configure(left); arrow.color = new Color(.72f, .9f, 1);
                button.targetGraphic = arrow; button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors; colors.normalColor = arrow.color; colors.highlightedColor = new Color(1, .88f, .35f); colors.pressedColor = Color.white; colors.disabledColor = new Color(.4f, .52f, .6f, .35f); button.colors = colors;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        static void RefinePlacementDock(Transform canvas)
        {
            var deck = canvas.Find("Deck"); if (!deck) return;
            Specific(canvas, "Deck", "RobotFormationDockV2", Color.white, 3);
            var roster = deck.Find("Robots") as RectTransform;
            var layout = roster.GetComponent<HorizontalLayoutGroup>(); if (layout) layout.enabled = false;
            var fitter = roster.GetComponent<ContentSizeFitter>(); if (fitter) fitter.enabled = false;
            var buttons = roster.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                Skin(button.GetComponent<Image>(),distinct["RobotRoleTileClean"],Color.white);button.GetComponent<Image>().pixelsPerUnitMultiplier=12;
                var icon = button.transform.Find("Generated icon") as RectTransform;
                if (icon) SetRect(icon, new Vector2(0, 1), new Vector2(25, -31), new Vector2(29, 29), new Vector2(.5f, .5f));
                var count = button.transform.Find("Label").GetComponent<TMP_Text>();
                var stock=Child(button.transform,"StockBox");SetRect(stock,Vector2.one,new Vector2(-8,-31),new Vector2(46,30),new Vector2(1,.5f));
                var stockImage=stock.GetComponent<Image>();if(!stockImage)stockImage=stock.gameObject.AddComponent<Image>();RoundedControl(stockImage,new Color(.12f,.34f,.56f),5);stockImage.GetComponent<LearningCardShape>().Configure(5,1,new Color(.48f,.74f,.94f),false);stockImage.raycastTarget=false;stock.SetSiblingIndex(count.transform.GetSiblingIndex());
                SetRect(count.rectTransform, Vector2.one, new Vector2(-11, -31), new Vector2(40, 28), new Vector2(1,.5f)); count.fontSize = 22;count.enableAutoSizing=true;count.fontSizeMin=16;count.fontSizeMax=22;count.alignment=TextAlignmentOptions.Midline;count.margin=Vector4.zero;
                var role = button.transform.Find("RoleName").GetComponent<TMP_Text>(); Stretch(role.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(6, 5), new Vector2(-6, 26)); role.fontSize = 16; role.enableAutoSizing = true; role.fontSizeMin = 12; role.fontSizeMax = 16;
            }
            var title = TextLabel(deck, "DockTitle", "로봇 편성", 17, new Color(.68f, .92f, 1));
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(24, -9), new Vector2(180, 23), new Vector2(0, 1)); title.alignment = TextAlignmentOptions.MidlineLeft; title.margin = Vector4.zero;title.color=new Color(.06f,.22f,.31f);
            var notice = deck.Find("Notice").GetComponent<TMP_Text>(); Stretch(notice.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(24, -64), new Vector2(-24, -37)); notice.alignment = TextAlignmentOptions.MidlineLeft; notice.margin = Vector4.zero; notice.color = new Color(.8f, .89f, .94f);
            var inventory = deck.Find("DeploymentCount").GetComponent<TMP_Text>(); SetRect(inventory.rectTransform, Vector2.one, new Vector2(-24, -9), new Vector2(280, 25), Vector2.one); inventory.fontSize = 14; inventory.alignment = TextAlignmentOptions.MidlineRight; inventory.margin = Vector4.zero;
            var divider = Child(deck, "ActionDivider"); var image = divider.GetComponent<Image>(); if (!image) image = divider.gameObject.AddComponent<Image>(); Plain(image, new Color(.35f, .68f, .79f, .4f)); image.raycastTarget = false;
            var start = deck.Find("Start") as RectTransform;Skin(start.GetComponent<Image>(),distinct["FormationStartWarm"],Color.white);start.GetComponent<Image>().pixelsPerUnitMultiplier=6;
            var label = start.GetComponentInChildren<TMP_Text>(true); label.fontSize = 23; label.enableAutoSizing = true; label.fontSizeMin = 18; label.fontSizeMax = 23; label.alignment = TextAlignmentOptions.Center; label.margin = Vector4.zero;
            var dock = deck.GetComponent<DefensePlacementDock>(); if (!dock) dock = deck.gameObject.AddComponent<DefensePlacementDock>(); dock.Configure(roster, start, divider, buttons, notice);
        }
    }
}
