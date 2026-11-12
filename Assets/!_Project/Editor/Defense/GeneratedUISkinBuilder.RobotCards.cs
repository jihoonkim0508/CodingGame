using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    public static partial class GeneratedUISkinBuilder
    {
        public static void ApplyRobotRosterCards()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before applying robot buttons.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/!_Project/Scenes/Defense.unity")
                throw new InvalidOperationException("Open the Defense scene before applying robot buttons.");
            if (scene.isDirty)
                throw new InvalidOperationException("Save the open Defense scene before applying robot buttons.");
            var canvas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(item => item.name == "PlayerCanvas");
            string backup = "Temp/RobotCardBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, backup + "/Defense.unity");
            var roster = canvas.transform.Find("Deck/Robots");
            Undo.RegisterFullObjectHierarchyUndo(roster.gameObject, "Apply robot roster cards");
            StyleRobotRosterCards(roster);
            RecordUIOverrides(roster);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Robot roster cards applied. Backup: " + backup);
        }

        static void StyleRobotRosterCards(Transform roster)
        {
            var surfaceSprite = Single("RobotCardBlue", new Vector4(54, 52, 54, 52));
            var iconSprite = Single("RobotCardIconWell", Vector4.zero);
            var stockSprite = Single("RobotCardStockBadge", Vector4.zero);
            var textColor = new Color(.96f, .99f, 1f);
            foreach (var button in roster.GetComponentsInChildren<Button>(true))
            {
                // The existing full-height button remains the tutorial and pointer target.
                var hit = button.GetComponent<Image>();
                var oldShape = hit.GetComponent<LearningCardShape>();
                if (oldShape) UnityEngine.Object.DestroyImmediate(oldShape);
                hit.sprite = null;
                hit.color = Color.clear;
                hit.raycastTarget = true;

                var legacySurface = button.transform.Find("CapsuleSurface");
                if (legacySurface) legacySurface.name = "CardSurface";
                var surface = Child(button.transform, "CardSurface");
                Stretch(surface, Vector2.zero, Vector2.one, new Vector2(0, 2), new Vector2(0, -2));
                var background = surface.GetComponent<Image>();
                if (!background) background = surface.gameObject.AddComponent<Image>();
                Skin(background, surfaceSprite, Color.white);
                background.pixelsPerUnitMultiplier = 4;
                background.raycastTarget = false;
                surface.SetAsFirstSibling();

                var outline = Child(surface, "SelectionOutline");
                Stretch(outline, Vector2.zero, Vector2.one, new Vector2(-2, -2), new Vector2(2, 2));
                var outlineImage = outline.GetComponent<Image>();
                if (!outlineImage) outlineImage = outline.gameObject.AddComponent<Image>();
                RoundedControl(outlineImage, Color.clear, 10);
                outlineImage.GetComponent<LearningCardShape>().Configure(10, 2, new Color(.42f, .92f, 1f), false, false);
                outlineImage.raycastTarget = false;

                var iconWell = RoleCardArt(button.transform, "IconWell", iconSprite, .018f, .422f, 28);
                iconWell.SetSiblingIndex(surface.GetSiblingIndex() + 1);
                var icon = button.transform.Find("Generated icon") as RectTransform;
                if (icon)
                {
                    // RoleEmblems uses equal atlas cells with different transparent margins.
                    var centerOffset = button.name switch
                    {
                        "Buffer" => new Vector2(.19f, -2.63f),
                        "Warrior" => new Vector2(-.47f, .19f),
                        "Tank" => new Vector2(0, -.19f),
                        "Bomber" => new Vector2(-.19f, -3f),
                        "Shooter" => new Vector2(.56f, -.28f),
                        "Utility" => new Vector2(.66f, -1.97f),
                        _ => Vector2.zero
                    };
                    Stretch(icon, new Vector2(.037f, .5f), new Vector2(.403f, .5f),
                        centerOffset + new Vector2(0, -24), centerOffset + new Vector2(0, 24));
                    icon.SetAsLastSibling();
                    icon.GetComponent<Image>().raycastTarget = false;
                }
                var oldSymbol = button.transform.Find("Symbol");
                if (oldSymbol) oldSymbol.gameObject.SetActive(false);

                var role = button.transform.Find("RoleName").GetComponent<TMP_Text>();
                Stretch(role.rectTransform, new Vector2(.43f, .5f), new Vector2(.70f, .5f), new Vector2(0, -17), new Vector2(0, 17));
                role.color = textColor;
                role.fontSize = 19;
                role.enableAutoSizing = true;
                role.fontSizeMin = 10;
                role.fontSizeMax = 19;
                role.fontStyle = FontStyles.Bold;
                role.alignment = TextAlignmentOptions.Midline;
                role.margin = Vector4.zero;
                role.textWrappingMode = TextWrappingModes.NoWrap;
                role.raycastTarget = false;

                RoleCardArt(button.transform, "StockBox", stockSprite, .71f, .98f, 17);

                var count = button.transform.Find("Label").GetComponent<TMP_Text>();
                Stretch(count.rectTransform, new Vector2(.735f, .5f), new Vector2(.955f, .5f), new Vector2(0, -15), new Vector2(0, 15));
                count.text = "0";
                count.color = textColor;
                count.fontSize = 18;
                count.enableAutoSizing = true;
                count.fontSizeMin = 6;
                count.fontSizeMax = 18;
                count.fontStyle = FontStyles.Bold;
                count.alignment = TextAlignmentOptions.Midline;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.margin = Vector4.zero;
                count.raycastTarget = false;
                count.transform.SetAsLastSibling();

                var group = button.GetComponent<CanvasGroup>();
                if (!group) group = button.gameObject.AddComponent<CanvasGroup>();
                var badge = button.GetComponent<LearningRobotBadge>();
                if (!badge) badge = button.gameObject.AddComponent<LearningRobotBadge>();
                badge.SetupCard(background, outline.gameObject, group);
                badge.Refresh(0, false, true);
                button.targetGraphic = background;
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = colors.selectedColor = colors.disabledColor = Color.white;
                colors.highlightedColor = new Color(.88f, .97f, 1f);
                colors.pressedColor = new Color(.74f, .9f, 1f);
                colors.colorMultiplier = 1;
                colors.fadeDuration = .10f;
                button.colors = colors;
            }
        }

        static RectTransform RoleCardArt(Transform parent, string name, Sprite sprite, float left, float right, float halfHeight)
        {
            var rect = Child(parent, name);
            Stretch(rect, new Vector2(left, .5f), new Vector2(right, .5f), new Vector2(0, -halfHeight), new Vector2(0, halfHeight));
            var image = rect.GetComponent<Image>();
            if (!image) image = rect.gameObject.AddComponent<Image>();
            Skin(image, sprite, Color.white);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }
    }
}
