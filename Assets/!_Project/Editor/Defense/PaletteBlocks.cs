using System;
using System.Linq;
using CodingGame.BlockCoding;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    // Adds the palette blocks the Defense scene's code editor lacks, as copies of blocks already there (the scene's editor
    // instance holds the palette): the calculation `( ) + ( )` in 값 from the comparison (its picker gets + - * / at run time),
    // and the enemy kind `Enemy. [Tank]` in 값 from the comparison's picker alone (Normal / Fast / Tank at run time).
    // Blocks already in the palette are left alone. is_dead() has no block of its own: it is typed after `enemy.`.
    // Also the for-each `for [e] in [get_enemies()]:`, get_enemies() and break_shield( ) with their block-help entries.
    public static class PaletteBlocks
    {
        const string Scene = "Assets/!_Project/Scenes/Defense.unity";

        public static void Add()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var panel = Object.FindObjectsByType<CommandCodingPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var so = new SerializedObject(panel);
            var palette = so.FindProperty("palette");
            CommandBlockView View(int i) => (CommandBlockView)palette.GetArrayElementAtIndex(i).FindPropertyRelative("view").objectReferenceValue;
            CommandBlockView Find(BlockKind kind) => Enumerable.Range(0, palette.arraySize).Select(View).FirstOrDefault(v => v && v.BaseKind == kind);
            string log = "";
            void Append(BlockKind kind, BlockKind from, int category, Action<CommandBlockView> shape)
            {
                if (Find(kind)) { log += kind + " kept; "; return; }
                var source = Find(from) ?? throw new InvalidOperationException("팔레트에 " + from + " 블록이 없습니다.");
                var copy = Object.Instantiate(source.gameObject, source.transform.parent);
                copy.name = kind.ToString();
                copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                var view = copy.GetComponent<CommandBlockView>();
                var fields = new SerializedObject(view);
                fields.FindProperty("kind").intValue = (int)kind;
                fields.ApplyModifiedPropertiesWithoutUndo();
                shape(view);
                palette.arraySize++;
                var item = palette.GetArrayElementAtIndex(palette.arraySize - 1);
                item.FindPropertyRelative("view").objectReferenceValue = view;
                item.FindPropertyRelative("category").intValue = category;
                log += kind + " added; ";
            }
            Append(BlockKind.Arithmetic, BlockKind.Comparison, 2, view => { });
            // `Enemy. [Tank]`: the comparison's picker (kinds filled at run time) without its two holes, after get_nearest_enemy().
            Append(BlockKind.EnemyTypeName, BlockKind.Comparison, 2, view =>
            {
                var fields = new SerializedObject(view);
                fields.FindProperty("arguments").arraySize = 0;
                fields.ApplyModifiedPropertiesWithoutUndo();
                var header = view.transform.Find("Header");
                foreach (var hole in new[] { "Slot0", "Slot1" }) Object.DestroyImmediate(header.Find(hole).gameObject);
                var picker = header.Find("Operator"); picker.name = "Kind";
                picker.Find("Caption").GetComponent<TMP_Text>().text = "Tank";
                var label = Object.Instantiate(Find(BlockKind.NearestEnemy).transform.Find("Header").GetComponentInChildren<TMP_Text>(true).gameObject, header);
                label.name = "Label"; label.transform.SetSiblingIndex(0);
                label.GetComponent<TMP_Text>().text = "Enemy.";
                view.transform.SetSiblingIndex(Find(BlockKind.NearestEnemy).transform.GetSiblingIndex() + 1);
            });
            // get_enemies() from get_nearest_enemy(), break_shield( ) from shoot( ), and `for [e] in [ ]:` from the for with the
            // variable's name field of `name = ` (the list slot gets get_enemies() when the loop is placed: FillNewLoop).
            void Relabel(CommandBlockView view, string from, string to)
            {
                foreach (var label in view.transform.Find("Header").GetComponentsInChildren<TMP_Text>(true))
                    if (label.text == from) label.text = to;
            }
            Append(BlockKind.GetEnemies, BlockKind.NearestEnemy, 2, view => Relabel(view, "get_nearest_enemy()", "get_enemies()"));
            Append(BlockKind.BreakShield, BlockKind.Shot, 1, view => Relabel(view, "shoot(", "break_shield("));
            Append(BlockKind.ForEach, BlockKind.For, 0, view =>
            {
                var header = view.transform.Find("Header");
                Relabel(view, "for i in range(", "for"); Relabel(view, "):", ":");
                var field = Object.Instantiate(Find(BlockKind.DeclareVariable).transform.Find("Header/VariableName").gameObject, header);
                field.name = "VariableName"; field.transform.SetSiblingIndex(1);
                var input = field.GetComponent<TMP_InputField>(); input.text = "e";
                var inLabel = Object.Instantiate(header.GetChild(0).gameObject, header);
                inLabel.name = "InLabel"; inLabel.transform.SetSiblingIndex(2); inLabel.GetComponent<TMP_Text>().text = "in";
                header.Find("Slot0/Hint").GetComponent<TMP_Text>().text = "list";
                var fields = new SerializedObject(view);
                fields.FindProperty("input").objectReferenceValue = input;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            // my_hp() from my_attack_range(); `[ ] and [ ]` from the comparison: its picker gives way to an `and` label and both
            // holes take conditions (the reflector lesson's `e.type == Enemy.Reflector and e.reflecting`).
            Append(BlockKind.MyHp, BlockKind.MyAttackRange, 2, view => Relabel(view, "my_attack_range()", "my_hp()"));
            int logic = Enumerable.Range(0, palette.arraySize).First(i => View(i) && View(i).BaseKind == BlockKind.Comparison);
            Append(BlockKind.And, BlockKind.Comparison, palette.GetArrayElementAtIndex(logic).FindPropertyRelative("category").intValue, view =>
            {
                var header = view.transform.Find("Header");
                var picker = header.Find("Operator");
                var label = Object.Instantiate(Find(BlockKind.NearestEnemy).transform.Find("Header").GetComponentInChildren<TMP_Text>(true).gameObject, header);
                label.name = "AndLabel"; label.transform.SetSiblingIndex(picker.GetSiblingIndex());
                label.GetComponent<TMP_Text>().text = "and";
                Object.DestroyImmediate(picker.gameObject);
                var fields = new SerializedObject(view);
                fields.FindProperty("dropdown").objectReferenceValue = null;
                fields.ApplyModifiedPropertiesWithoutUndo();
                foreach (var zone in view.GetComponentsInChildren<CommandDropZone>(true))
                {
                    var slot = new SerializedObject(zone);
                    slot.FindProperty("slot").intValue = (int)BlockSlotKind.Condition;
                    slot.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var hint in new[] { "Slot0/Hint", "Slot1/Hint" }) if (header.Find(hint)?.GetComponent<TMP_Text>() is TMP_Text text) text.text = "조건";
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            // Block help (도감) entries for the new blocks, as copies of an existing entry's button.
            var help = Object.FindObjectsByType<DefenseBlockHelp>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var helpSo = new SerializedObject(help);
            var entries = helpSo.FindProperty("entries");
            void Help(BlockKind kind, BlockKind after, string title, string description, string example)
            {
                int Index(BlockKind k)
                {
                    for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue == (int)k) return i;
                    return -1;
                }
                if (Index(kind) >= 0) { log += "help " + kind + " kept; "; return; }
                int at = Index(after); if (at < 0) at = entries.arraySize - 1;
                var source = entries.GetArrayElementAtIndex(at);
                var button = (UnityEngine.UI.Button)source.FindPropertyRelative("button").objectReferenceValue;
                var label = (TMP_Text)source.FindPropertyRelative("label").objectReferenceValue;
                string path = AnimationUtility.CalculateTransformPath(label.transform, button.transform);
                var copy = Object.Instantiate(button.gameObject, button.transform.parent);
                copy.name = kind.ToString(); copy.transform.SetSiblingIndex(button.transform.GetSiblingIndex() + 1);
                entries.InsertArrayElementAtIndex(at);
                var entry = entries.GetArrayElementAtIndex(at + 1);
                entry.FindPropertyRelative("kind").intValue = (int)kind;
                entry.FindPropertyRelative("button").objectReferenceValue = copy.GetComponent<UnityEngine.UI.Button>();
                entry.FindPropertyRelative("label").objectReferenceValue = (path.Length == 0 ? copy.transform : copy.transform.Find(path)).GetComponent<TMP_Text>();
                entry.FindPropertyRelative("title").stringValue = title;
                entry.FindPropertyRelative("description").stringValue = description;
                entry.FindPropertyRelative("example").stringValue = example;
                log += "help " + kind + " added; ";
            }
            Help(BlockKind.ForEach, BlockKind.While, "for e in get_enemies() · 적마다 반복",
                "지금 길 위에 있는 적을 하나씩 e에 담아 안쪽 명령을 반복합니다. 코어에 가장 가까운(앞선) 적부터 차례로 옵니다.\n\n" +
                "원하는 적을 찾으면 break로 반복을 끝내세요. 이름 칸을 눌러 e를 다른 이름으로 바꿀 수 있습니다.\n입력: get_enemies() · 안쪽: 실행할 명령",
                "target = get_nearest_enemy()\nfor e in get_enemies():\n    if e.type == Enemy.Healer:\n        target = e\n        break\nshoot(target)");
            Help(BlockKind.GetEnemies, BlockKind.NearestEnemy, "get_enemies() · 적 목록",
                "길 위의 모든 적 목록입니다. 코어에 가까운 적이 앞에 옵니다. for e in 블록의 빈칸에 넣어 씁니다.", "for e in get_enemies():\n    shoot(e)");
            Help(BlockKind.Shield, BlockKind.IsDead, "enemy.shield · 보호막",
                "적의 보호막 양입니다(없으면 0). 보호막은 체력보다 먼저 피해를 받습니다. 보호막병이 주변 적에게 씌웁니다.\n변수 뒤에 점을 찍고 shield를 고르세요.",
                "if e.shield > 0:\n    break_shield(e)\nelse:\n    shoot(e)");
            Help(BlockKind.BreakShield, BlockKind.Shot, "break_shield(enemy) · 보호막 깨기",
                "원거리형 전용 두 번째 기술. 사거리 안 적의 보호막을 바로 없애고 체력에 약간의 피해를 줍니다. shoot()과 따로 재사용 대기(3초)를 가집니다.",
                "e = get_nearest_enemy()\nif e.shield > 0:\n    break_shield(e)\nelse:\n    shoot(e)");
            Help(BlockKind.Front, BlockKind.Shield, "enemy.front() · 큐의 맨 앞",
                "큐(Queue)는 먼저 들어온 것이 먼저 나갑니다(FIFO). 큐 지네는 맨 앞 마디만 피해를 받고, 그 마디가 쓰러지면 다음 마디가 맨 앞이 됩니다.\n" +
                "e.front()는 e가 속한 지네의 맨 앞 마디를 돌려줍니다(지네가 아니면 e 자신). 변수 뒤에 점을 찍고 front()를 고르세요.",
                "for e in get_enemies():\n    if e.type == Enemy.Queue:\n        shoot(e.front())");
            Help(BlockKind.Top, BlockKind.Front, "enemy.top() · 스택의 맨 위",
                "스택(Stack)은 나중에 들어온 것이 먼저 나갑니다(LIFO). 스택 지네는 몇 초마다 꼬리에 마디를 쌓고, 맨 위(가장 나중에 쌓인 꼬리) 마디만 피해를 받습니다.\n" +
                "e.top()은 e가 속한 지네의 맨 위 마디를 돌려줍니다(지네가 아니면 e 자신). 늦게 잡을수록 마디가 늘어납니다(최대 8). e.size로 마디 수를 읽을 수 있습니다.",
                "for e in get_enemies():\n    if e.type == Enemy.Stack:\n        shoot(e.top())\n    elif e.type == Enemy.Queue:\n        shoot(e.front())");
            Help(BlockKind.Reflecting, BlockKind.Top, "enemy.reflecting · 반사 중",
                "반사병이 반사 상태면 True입니다. 반사병은 몇 초마다(보통 3초 → 반사 2초) 거울처럼 빛나며, 그동안 맞은 공격을 쏜 로봇에게 그대로 돌려보냅니다(반사병은 피해 없음).\n" +
                "변수 뒤에 점을 찍고 reflecting을 고르세요. 다른 적은 늘 False입니다.",
                "for e in get_enemies():\n    if e.type == Enemy.Reflector and e.reflecting:\n        continue\n    shoot(e)\n    break");
            Help(BlockKind.And, BlockKind.Reflecting, "a and b · 그리고",
                "두 조건이 모두 True일 때만 True입니다. 왼쪽이 False면 오른쪽은 보지 않습니다. 양쪽 빈칸에 논리 블록(비교, is_dead(), reflecting ...)을 넣으세요.",
                "if e.type == Enemy.Reflector and e.reflecting:\n    continue");
            Help(BlockKind.MyHp, BlockKind.MyAttackRange, "my_hp() · 내 체력",
                "이 로봇의 지금 체력입니다. 반사병에게 공격이 튕겨 오면 줄고, 0이 되면 로봇이 이번 웨이브에서 쓰러집니다(재도전하면 돌아옴).",
                "if my_hp() < 30:\n    wait(1)");
            // Who does what best (2026-11-25): attack actions belong to 공격류 (근접·원거리), buff and slow to 지원류; the other
            // class's work weakly. Appended to the entries once.
            var rules = AssetDatabase.LoadAssetAtPath<DefenseCampaignDefinition>("Assets/!_Project/Defense/Tutorial/Stage1/StageOneCampaign.asset").rules;
            string penalty = $" (효과 {rules.reducedStrength:0%}, 재사용 대기 {rules.reducedIntervalMultiplier:0.#}배)";
            foreach (var (kind, note) in new[] {
                (BlockKind.Buff, "공격류 로봇이 지원 기술을 쓰면 효과가 약해요" + penalty), (BlockKind.Slow, "공격류 로봇이 지원 기술을 쓰면 효과가 약해요" + penalty),
                (BlockKind.Shot, "지원 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty), (BlockKind.Slash, "지원 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty),
                (BlockKind.Block, "지원 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty), (BlockKind.Boom, "지원 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty),
                (BlockKind.BreakShield, "지원 로봇이 공격 기술을 쓰면 효과가 약해요" + penalty) })
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("kind").intValue != (int)kind) continue;
                    var text = entry.FindPropertyRelative("description");
                    text.stringValue = text.stringValue.Replace("원거리형 전용 두 번째 기술", "공격류(근접·원거리) 기술").Replace("원거리형", "원거리 로봇").Replace("전사형", "근접 로봇")
                        .Replace("탱커형", "근접 로봇").Replace("버프형", "지원 로봇").Replace("유틸형", "지원 로봇").Replace("폭탄형", "공격류 로봇");
                    if (!text.stringValue.Contains(note.Substring(0, 12))) text.stringValue += "\n" + note;
                    log += "help " + kind + " class note; ";
                }
            helpSo.ApplyModifiedPropertiesWithoutUndo();
            // The inventory lists one row per item kind (DefenseInventoryUI checks the count): a row for each new kind, as a copy of
            // the last row (the label and count's common parent).
            var inventory = Object.FindObjectsByType<DefenseInventoryUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var inventorySo = new SerializedObject(inventory);
            var slots = inventorySo.FindProperty("blockSlots");
            foreach (var kind in DefenseProgression.ItemKinds)
            {
                if (Enumerable.Range(0, slots.arraySize).Any(i => slots.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue == (int)kind)) continue;
                var last = slots.GetArrayElementAtIndex(slots.arraySize - 1);
                var label = (TMP_Text)last.FindPropertyRelative("label").objectReferenceValue; var count = (TMP_Text)last.FindPropertyRelative("count").objectReferenceValue;
                var row = label.transform.parent;
                if (count.transform.parent != row) throw new InvalidOperationException("인벤토리 줄 구조가 예상과 다릅니다.");
                var copy = Object.Instantiate(row.gameObject, row.parent); copy.name = kind.ToString();
                copy.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
                slots.arraySize++;
                var slot = slots.GetArrayElementAtIndex(slots.arraySize - 1);
                slot.FindPropertyRelative("kind").intValue = (int)kind;
                slot.FindPropertyRelative("label").objectReferenceValue = copy.transform.GetChild(label.transform.GetSiblingIndex()).GetComponent<TMP_Text>();
                slot.FindPropertyRelative("count").objectReferenceValue = copy.transform.GetChild(count.transform.GetSiblingIndex()).GetComponent<TMP_Text>();
                log += "inventory " + kind + " added; ";
            }
            inventorySo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[PaletteBlocks] " + log + "scene saved " + EditorSceneManager.SaveScene(scene));
        }
    }
}
