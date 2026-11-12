using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CodingGame.Defense.Editor
{
    // Puts the toy-workshop look (UITheme) on the battle HUD, the code editor and 디버's dialogue box: sticker sprites and
    // candy colours by object path, texts recoloured to read on the paper they sit on, Jalnan on headings and buttons.
    // Rerunnable; also imports Resources/UI's sprites (9-sliced) and makes the Jalnan TMP font. Batchmode works:
    //   Unity.exe -batchmode -projectPath . -executeMethod CodingGame.Defense.Editor.UIRestyle.Apply -quit
    public static class UIRestyle
    {
        const string Folder = "Assets/!_Project/Resources/UI";
        const string Scene = "Assets/!_Project/Scenes/Defense.unity";
        const string CodeEditor = "Assets/!_Project/Defense/Prefabs/RobotCodeEditor.prefab", Overlay = "Assets/!_Project/Defense/Tutorial/TutorialOverlay.prefab";
        // 9-slice borders (left, bottom, right, top) of what Editor/UI~/ui_sprites.py draws.
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4> {
            ["ui_card"] = new Vector4(24, 30, 24, 24), ["ui_card_flat"] = new Vector4(20, 20, 20, 20), ["ui_button"] = new Vector4(22, 34, 22, 22),
            ["ui_pill"] = new Vector4(27, 31, 27, 27), ["ui_chip"] = new Vector4(23, 23, 23, 23), ["ui_tab"] = new Vector4(13, 16, 13, 13),
            ["ui_bar_bottom"] = new Vector4(2, 2, 2, 15), ["ui_bar_top"] = new Vector4(2, 15, 2, 2), ["ui_circle"] = Vector4.zero, ["ui_grid"] = Vector4.zero, ["ui_arrow"] = Vector4.zero, ["ui_fill"] = new Vector4(18, 18, 18, 18) };

        sealed class Rule
        {
            public Regex Path; public string Sprite; public Color Fill; public Color? Text, Icon; public bool Display, Hide, Still, Tiled;
        }
        static Rule R(string path, string sprite, Color fill, Color? text = null, bool display = false, Color? icon = null, bool still = false, bool tiled = false)
            => new Rule { Path = new Regex("(^|/)" + path + "$"), Sprite = sprite, Fill = fill, Text = text, Display = display, Icon = icon, Still = still, Tiled = tiled };
        static Rule Hidden(string path) => new Rule { Path = new Regex("(^|/)" + path + "$"), Hide = true };
        static readonly Color Ink = UITheme.Ink, White = Color.white;

        static readonly Rule[] Rules = {
            // Battle HUD
            R("PlayerCanvas/Header", "ui_bar_top", UITheme.Paper, Ink),
            R("PlayerCanvas/Header/Info", "ui_button", UITheme.Paper, Ink),
            R("PlayerCanvas/Deck", "ui_bar_bottom", UITheme.Paper, Ink),
            R("PlayerCanvas/Deck/Start", "ui_button", UITheme.Primary, Ink, true),
            // Robot cards are paper: the robot's picture tells the kinds apart, and 전투 시작 stays the one yellow button.
            R("PlayerCanvas/Deck/Robots/[^/]+", "ui_button", UITheme.Paper, Ink, true),
            R("PlayerCanvas/RobotActions/Develop", "ui_button", UITheme.Primary, Ink, true),
            R("PlayerCanvas/RobotActions/Recall", "ui_button", UITheme.Coral, White, true, White),
            R("PlayerCanvas/RobotActions/Upgrade", "ui_button", UITheme.Paper, Ink, true),
            R("PlayerCanvas/CombatControls/(Pause|Speed)", "ui_button", UITheme.Paper, Ink),
            R("PlayerCanvas/Guide", "ui_pill", UITheme.Paper, Ink),
            R("PlayerCanvas/PlacementToast", "ui_card", UITheme.Coral, White),
            R("PlayerCanvas/Reward/Card", "ui_card", UITheme.Paper, Ink),
            R("PlayerCanvas/Reward/Card/Claim", "ui_button", UITheme.Primary, Ink, true),
            R("PlayerCanvas/Reward/Card/RewardScroll/Scrollbar", "ui_card_flat", UITheme.PaperDeep),
            R("PlayerCanvas/Reward/Card/RewardScroll/Scrollbar/.*Handle", "ui_card_flat", UITheme.Muted),
            R("PlayerCanvas/Tooltip", "ui_card", UITheme.Paper, Ink),
            R("PlayerCanvas/UpcomingCards/[^/]+", "ui_card_flat", UITheme.Paper, Ink),
            // Code editor (prefab, and its instance in the scene)
            R("RobotCodeEditor/Background", null, UITheme.World),
            R("RobotCodeEditor/Editor", "ui_card", UITheme.Paper, Ink),
            R("RobotCodeEditor/Editor/Workspace/Viewport", "ui_grid", UITheme.GridPaper, Ink, tiled: true),
            Hidden("RobotCodeEditor/Editor/Workspace/Viewport/Program"),
            R("RobotCodeEditor/Editor/TitleBar/FunctionName", "ui_card_flat", White, Ink),
            R("RobotCodeEditor/Editor/PreviewPanel", "ui_card", UITheme.Paper, Ink),
            R("RobotCodeEditor/Library", "ui_card", UITheme.Paper, Ink),
            R("RobotCodeEditor/Library/Categories/Category\\d", "ui_tab", UITheme.PaperDeep, Ink, still: true),
            R("RobotCodeEditor/RobotStats", "ui_card", UITheme.Paper, Ink),
            R("RobotCodeEditor/RobotStats/Recall", "ui_button", UITheme.Coral, White, true, White),
            R("RobotCodeEditor/Automatic", "ui_button", UITheme.Paper, Ink, icon: UITheme.Coral),
            R("RobotCodeEditor/CloseEditor", "ui_button", UITheme.Primary, Ink, true),
            R("RobotCodeEditor/(LeftToggle|RightToggle|PreviewButton|Info)", "ui_button", UITheme.Paper, Ink),
            R("RobotCodeEditor/Python", "ui_card_flat", UITheme.Night),
            // Block help window (the editor's i button)
            R("BlockHelpCanvas/BlockHelpWindow", null, new Color(UITheme.Night.r, UITheme.Night.g, UITheme.Night.b, .8f)),
            R("BlockHelpCanvas/BlockHelpWindow/Card", "ui_card", UITheme.Paper, Ink),
            R("BlockHelpCanvas/BlockHelpWindow/Card/BlockList", "ui_card_flat", UITheme.PaperDeep, Ink),
            R("BlockHelpCanvas/BlockHelpWindow/Card/BlockList/Scrollbar", "ui_card_flat", UITheme.PaperDeep),
            R("BlockHelpCanvas/BlockHelpWindow/Card/BlockList/Scrollbar/.*Handle", "ui_card_flat", UITheme.Muted),
            R("BlockHelpCanvas/BlockHelpWindow/Card/BlockList/Viewport/Content/[^/]+", "ui_pill", UITheme.PaperDeep, Ink, still: true),
            R("BlockHelpCanvas/BlockHelpWindow/Card/Close", "ui_button", UITheme.Paper, Ink, true),
            R("BlockHelpCanvas/BlockHelpWindow/Card/Detail/ExampleBox", "ui_card_flat", UITheme.Night),
            // 디버's dialogue box
            R("TutorialOverlay/DialoguePanel", "ui_card", UITheme.Paper, Ink),
            Hidden("TutorialOverlay/DialoguePanel/Accent"),
            R("TutorialOverlay/DialoguePanel/NextText", "ui_button", UITheme.Primary, Ink, still: true),
            R("TutorialOverlay/DialoguePanel/PreviousText", "ui_button", UITheme.Paper, Ink),
        };
        // Headings in Jalnan, whatever they sit on.
        static readonly Regex DisplayText = new Regex("(^|/)(PlayerCanvas/Header/Brand|PlayerCanvas/Reward/Card/Title|RobotCodeEditor/Title|RobotCodeEditor/Subtitle|RobotCodeEditor/Library/Title|TutorialOverlay/DialoguePanel/SpeakerName|BlockHelpWindow/Card/Title|BlockHelpWindow/Card/Detail/Name)$");
        static readonly Regex InkText = new Regex("(^|/)PlayerCanvas/Header/Brand$");
        // Body text in Pretendard Bold.
        static readonly Regex BoldText = new Regex("(^|/)(TutorialOverlay/DialoguePanel/Dialogue|PlayerCanvas/Deck/(Notice|DeploymentCount))$");
        // Children of a card that sit outside it, on the editor's sky background.
        static readonly Regex OnNight = new Regex("(^|/)RobotCodeEditor/(Library/Title|Subtitle|Status)$");

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 종료 후 적용하세요.");
            ImportSprites();
            var display = DisplayFont(); var bold = BoldFont();
            int changed = 0;
            foreach (var path in new[] { CodeEditor, Overlay })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                changed += Restyle(new[] { root }, display, bold);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            changed += Restyle(scene.GetRootGameObjects(), display, bold);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIRestyle] {changed} images restyled");
        }

        static int Restyle(IEnumerable<GameObject> roots, TMP_FontAsset display, TMP_FontAsset bold)
        {
            var styled = new Dictionary<Transform, Rule>();
            foreach (var root in roots)
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    string path = PathOf(image.transform);
                    var rule = Rules.FirstOrDefault(r => r.Path.IsMatch(path));
                    if (rule == null) continue;
                    Undo.RecordObject(image, "UI restyle");
                    if (rule.Hide) { image.color = Color.clear; EditorUtility.SetDirty(image); continue; }
                    var sprite = rule.Sprite == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{rule.Sprite}.png");
                    UITheme.Style(image, sprite, rule.Fill);
                    if (rule.Tiled) { image.type = Image.Type.Tiled; image.pixelsPerUnitMultiplier = 1; }
                    styled[image.transform] = rule;
                    EditorUtility.SetDirty(image);
                    var button = image.GetComponent<Button>();
                    if (button)
                    {
                        Undo.RecordObject(button, "UI restyle");
                        button.transition = Selectable.Transition.ColorTint; button.targetGraphic = image;
                        button.colors = new ColorBlock { normalColor = White, highlightedColor = new Color(.95f, .95f, .95f), pressedColor = new Color(.82f, .82f, .82f),
                            selectedColor = White, disabledColor = new Color(.8f, .8f, .8f, .55f), colorMultiplier = 1, fadeDuration = .08f };
                        if (!rule.Still && !button.GetComponent<UIPressScale>()) button.gameObject.AddComponent<UIPressScale>();
                        EditorUtility.SetDirty(button);
                    }
                    foreach (Transform child in image.transform)
                        if (child.name == "Icon" && child.TryGetComponent<Image>(out var icon))
                        { Undo.RecordObject(icon, "UI restyle"); icon.color = rule.Icon ?? Ink; EditorUtility.SetDirty(icon); }
                }
            foreach (var root in roots)
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    // Palette blocks keep their own colours; only their stock count (×2 beside the block) sits on the paper.
                    if (text.name != "Stock" && text.GetComponentInParent<BlockCoding.CommandBlockView>(true)) continue;
                    string path = PathOf(text.transform);
                    var owner = Nearest(text.transform, styled, out var ownerImage);
                    var rule = owner;
                    bool heading = DisplayText.IsMatch(path);
                    if (rule?.Text == null && !heading && !OnNight.IsMatch(path)) continue;
                    Undo.RecordObject(text, "UI restyle");
                    if (OnNight.IsMatch(path)) text.color = new Color(1, 1, 1, text.color.a);
                    else if (rule?.Text != null)
                    {
                        // A button's label takes the button's text colour; text on a card keeps its role (plain, dim, accent).
                        bool label = ownerImage.GetComponent<Button>() || InkText.IsMatch(path) || rule.Text != Ink;
                        var color = label ? rule.Text.Value : OnPaper(text.color);
                        text.color = new Color(color.r, color.g, color.b, text.color.a);
                    }
                    if ((heading || rule != null && rule.Display) && display) { text.font = display; text.fontStyle &= ~FontStyles.Bold; }
                    else if (BoldText.IsMatch(path) && bold) { text.font = bold; text.fontStyle &= ~FontStyles.Bold; }
                    EditorUtility.SetDirty(text);
                }
            return styled.Count;
        }

        // Light text on the old dark panels, as it should read on paper: plain text ink, dim text muted, colours deepened.
        static Color OnPaper(Color old)
        {
            Color.RGBToHSV(old, out float h, out float s, out float v);
            if (v < .62f) return old; // already a colour for paper (a rerun)
            if (s >= .45f) return h > .45f && h < .72f ? UITheme.Hex(0x1971C2) : h > .22f ? UITheme.Hex(0x0C8A73) : h > .07f ? UITheme.Hex(0xC77700) : UITheme.Hex(0xE03131);
            return v >= .85f ? Ink : UITheme.Muted;
        }

        static Rule Nearest(Transform t, Dictionary<Transform, Rule> styled, out Transform owner)
        {
            for (owner = t.parent; owner; owner = owner.parent) if (styled.TryGetValue(owner, out var rule)) return rule;
            return null;
        }
        static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

        static void ImportSprites()
        {
            foreach (var (name, border) in Borders.Select(p => (p.Key, p.Value)))
            {
                string path = $"{Folder}/{name}.png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) { Debug.LogWarning("[UIRestyle] missing " + path); continue; }
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteBorder == border && !importer.mipmapEnabled) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = name == "ui_grid" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        // Jalnan 2 (in the project already) as a dynamic TMP font with Pretendard behind it for anything it lacks.
        static TMP_FontAsset DisplayFont() => DynamicFont("Jalnan2TTF.ttf", "Jalnan2 SDF");
        // Pretendard's Bold weight (PretendardBold.ttf, cut from PretendardVariable.ttf at wght 700 with fontTools'
        // instancer), for 디버's lines and the deck hints, which read thin in the regular weight. TMP's own bold style
        // would widen the spacing between Korean letters instead.
        static TMP_FontAsset BoldFont() => DynamicFont("PretendardBold.ttf", "Pretendard Bold SDF");
        static TMP_FontAsset DynamicFont(string file, string name)
        {
            string path = $"{Folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset) return asset;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/" + file);
            if (!font) { Debug.LogWarning($"[UIRestyle] {file} not found; its texts keep their font"); return null; }
            asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = name + " Atlas"; AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset);
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/PretendardVariable SDF.asset");
            if (body) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { body };
            // Glyphs it picks up while playing in the Editor are dropped on build instead of piling up in the asset.
            var serialized = new SerializedObject(asset);
            var clear = serialized.FindProperty("m_ClearDynamicDataOnBuild");
            if (clear != null) { clear.boolValue = true; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
