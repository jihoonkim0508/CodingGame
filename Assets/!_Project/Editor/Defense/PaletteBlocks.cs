using System;
using System.Collections.Generic;
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
    // Also the for-each `for [e] in [get_enemies()]:`, get_enemies() and break_shield( ) with their block-help entries, and for
    // the tree legion `while [ ]:` (a copy of the if) and `[ ] or [ ]` (the comparison with an `or` label for its picker).
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
            int Category(BlockKind kind) => Enumerable.Range(0, palette.arraySize).Where(i => View(i) && View(i).BaseKind == kind)
                .Select(i => palette.GetArrayElementAtIndex(i).FindPropertyRelative("category").intValue).First();
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
            // `while [condition]:` in 제어 next to the if, the header's `if` reading `while`.
            Append(BlockKind.WhileCondition, BlockKind.If, Category(BlockKind.If), view =>
            {
                foreach (var label in view.transform.Find("Header").GetComponentsInChildren<TMP_Text>(true))
                    if (label.text.StartsWith("if", StringComparison.Ordinal)) label.text = "while" + label.text.Substring(2);
            });
            // `[ ] or [ ]` in 논리 next to the comparison: its two holes take conditions, an `or` label where its picker was.
            Append(BlockKind.Or, BlockKind.Comparison, Category(BlockKind.Comparison), view =>
            {
                var header = view.transform.Find("Header");
                var picker = header.Find("Operator");
                var label = Object.Instantiate(Find(BlockKind.NearestEnemy).transform.Find("Header").GetComponentInChildren<TMP_Text>(true).gameObject, header);
                label.name = "Label"; label.transform.SetSiblingIndex(picker.GetSiblingIndex());
                label.GetComponent<TMP_Text>().text = "or";
                Object.DestroyImmediate(picker.gameObject);
                foreach (var hole in new[] { "Slot0", "Slot1" })
                {
                    var zone = new SerializedObject(header.Find(hole).GetComponent<CommandDropZone>());
                    zone.FindProperty("slot").intValue = (int)BlockSlotKind.Condition;
                    zone.ApplyModifiedPropertiesWithoutUndo();
                }
                var fields = new SerializedObject(view);
                fields.FindProperty("dropdown").objectReferenceValue = null;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            Team();
            so.ApplyModifiedPropertiesWithoutUndo();
            // The team's blocks (stage 4), shaped from the blocks above: a label where a picker was, holes copied or taken away.
            void Team()
            {
                TMP_Text Label(Transform header, string text, int at)
                {
                    var label = Object.Instantiate(Find(BlockKind.NearestEnemy).transform.Find("Header").GetComponentInChildren<TMP_Text>(true).gameObject, header);
                    label.name = "Label"; label.transform.SetSiblingIndex(Mathf.Clamp(at, 0, header.childCount - 1));
                    var t = label.GetComponent<TMP_Text>(); t.text = text; return t;
                }
                void Holes(CommandBlockView view, params Transform[] holes)
                {
                    var fields = new SerializedObject(view); var list = fields.FindProperty("arguments"); list.arraySize = holes.Length;
                    for (int i = 0; i < holes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = holes[i].GetComponent<CommandDropZone>();
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                void NoPicker(CommandBlockView view, Transform picker)
                {
                    if (picker) Object.DestroyImmediate(picker.gameObject);
                    var fields = new SerializedObject(view); fields.FindProperty("dropdown").objectReferenceValue = null; fields.ApplyModifiedPropertiesWithoutUndo();
                }
                void Hint(Transform hole, string text) { if (hole && hole.Find("Hint")?.GetComponent<TMP_Text>() is TMP_Text t) t.text = text; }
                int values = Category(BlockKind.NearestEnemy), commands = Category(BlockKind.Shot), logic = Category(BlockKind.Comparison);
                Append(BlockKind.EmptyList, BlockKind.NearestEnemy, values, view => Relabel(view, "get_nearest_enemy()", "[]"));
                Append(BlockKind.GetRobots, BlockKind.GetEnemies, values, view => Relabel(view, "get_enemies()", "get_robots()"));
                Append(BlockKind.RobotRoleName, BlockKind.EnemyTypeName, values, view =>
                {
                    Relabel(view, "Enemy.", "Robot.");
                    if (view.transform.Find("Header/Kind/Caption")?.GetComponent<TMP_Text>() is TMP_Text caption) caption.text = "Melee";
                });
                // len([ ]): the comparison without its picker and second hole.
                Append(BlockKind.Len, BlockKind.Comparison, values, view =>
                {
                    var header = view.transform.Find("Header"); var picker = header.Find("Operator");
                    Object.DestroyImmediate(header.Find("Slot1").gameObject);
                    Label(header, ")", picker.GetSiblingIndex()); NoPicker(view, picker);
                    Label(header, "len(", 0);
                    Holes(view, header.Find("Slot0")); Hint(header.Find("Slot0"), "리스트");
                });
                // [ ][ [ ] ]: a dict (or list) and its key.
                Append(BlockKind.Index, BlockKind.Comparison, values, view =>
                {
                    var header = view.transform.Find("Header"); var picker = header.Find("Operator");
                    Label(header, "[", picker.GetSiblingIndex()); NoPicker(view, picker);
                    Label(header, "]", header.childCount);
                    Hint(header.Find("Slot0"), "사전"); Hint(header.Find("Slot1"), "키");
                });
                // [ ] in [ ]: the comparison's picker lists in / not in at run time (CommandBlockView.Choices).
                Append(BlockKind.In, BlockKind.Comparison, logic, view =>
                {
                    var header = view.transform.Find("Header");
                    Hint(header.Find("Slot0"), "값"); Hint(header.Find("Slot1"), "리스트");
                    if (header.Find("Operator/Label")?.GetComponent<TMP_Text>() is TMP_Text caption) caption.text = "in";
                });
                // not [ ]: the and block without its first hole, its label in front.
                Append(BlockKind.Not, BlockKind.And, Category(BlockKind.And), view =>
                {
                    var header = view.transform.Find("Header");
                    Object.DestroyImmediate(header.Find("Slot0").gameObject);
                    var label = header.Find("AndLabel"); label.GetComponent<TMP_Text>().text = "not"; label.SetSiblingIndex(0);
                    Holes(view, header.Find("Slot1"));
                });
                // {[ ]: [ ], [ ]: [ ], [ ]: [ ]}: three key/value pairs (PythonTreeCompiler.DictPairs).
                Append(BlockKind.DictLiteral, BlockKind.Comparison, values, view =>
                {
                    var header = view.transform.Find("Header"); var picker = header.Find("Operator");
                    Label(header, ":", picker.GetSiblingIndex()); NoPicker(view, picker);
                    Transform key = header.Find("Slot0"), value = header.Find("Slot1");
                    var holes = new List<Transform> { key, value };
                    for (int pair = 1; pair < PythonTreeCompiler.DictPairs; pair++)
                    {
                        Label(header, ",", header.childCount);
                        var k = Object.Instantiate(key.gameObject, header).transform; k.name = "Slot" + (2 * pair); k.SetAsLastSibling();
                        Label(header, ":", header.childCount);
                        var v = Object.Instantiate(value.gameObject, header).transform; v.name = "Slot" + (2 * pair + 1); v.SetAsLastSibling();
                        holes.Add(k); holes.Add(v);
                    }
                    Label(header, "{", 0); Label(header, "}", header.childCount);
                    for (int i = 0; i < holes.Count; i++) Hint(holes[i], i % 2 == 0 ? "키" : "값");
                    Holes(view, holes.ToArray());
                });
                // [ ].append([ ]): shoot( )'s line with a hole for the list in front.
                Append(BlockKind.Append, BlockKind.Shot, commands, view =>
                {
                    var header = view.transform.Find("Header"); var item = view.Arguments[0].transform;
                    var list = Object.Instantiate(item.gameObject, header).transform; list.name = "Receiver"; list.SetSiblingIndex(0);
                    Relabel(view, "shoot(", ".append(");
                    Hint(list, "리스트"); Hint(item, "값");
                    Holes(view, list, item);
                });
                Append(BlockKind.BuffRobot, BlockKind.Shot, commands, view => { Relabel(view, "shoot(", "buff("); Hint(view.Arguments[0].transform, "로봇"); });
            }
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
            Help(BlockKind.Left, BlockKind.Top, "enemy.left · 왼쪽 자식",
                "군단 트리는 대장(뿌리) 아래로 가지가 갈라지는 이진 트리입니다. 자식이 살아 있는 마디는 피해를 받지 않고, 잎(자식이 없는 마디)부터 쓰러뜨려야 합니다.\n" +
                "e.left는 e의 왼쪽 자식 마디입니다. 없거나 쓰러졌으면 None이라 조건에서 거짓이 됩니다. get_enemies()에는 대장만 나오니 left·right로 내려가세요.",
                "n = get_nearest_enemy()\nwhile n.left or n.right:\n    if n.left:\n        n = n.left\n    else:\n        n = n.right\nshoot(n)");
            Help(BlockKind.Right, BlockKind.Left, "enemy.right · 오른쪽 자식",
                "e.right는 e의 오른쪽 자식 마디입니다(없거나 쓰러졌으면 None). 왼쪽이 없을 때 오른쪽으로 내려가면 언젠가 잎에 닿습니다.",
                "n = get_nearest_enemy()\nif n.right:\n    shoot(n.right)");
            Help(BlockKind.Or, BlockKind.Comparison, "a or b · 또는",
                "두 조건 중 하나라도 참이면 참입니다. Enemy 값(e.left처럼)은 있으면 참, None이면 거짓입니다.",
                "e = get_nearest_enemy()\nif e.left or e.right:\n    shoot(e.left)\nelse:\n    shoot(e)");
            Help(BlockKind.WhileCondition, BlockKind.While, "while 조건: · 조건 반복",
                "조건이 참인 동안 안쪽 명령을 반복합니다. 매번 반복하기 전에 조건을 다시 확인하고, 거짓이 되면 반복을 끝내고 아래로 넘어갑니다.\n" +
                "while True:와 달리 스스로 멈춥니다. 트리의 잎까지 내려갈 때 씁니다.",
                "n = get_nearest_enemy()\nwhile n.left or n.right:\n    if n.left:\n        n = n.left\n    else:\n        n = n.right\nshoot(n)");
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
            // The team (stage 4): lists, the dict, robots as objects and weak_point(), its 도감 saying it is the tree walk of
            // stage 3 put into one call, given after the player did that walk by hand.
            Help(BlockKind.EmptyList, BlockKind.GetEnemies, "[] · 빈 리스트",
                "값을 차례로 담는 리스트를 새로 만듭니다. 팀 보드에 jobs = []처럼 두면 모든 로봇이 같은 리스트 하나를 함께 씁니다: 한 로봇이 넣은 것을 다른 로봇이 꺼내요.\n" +
                "팀 보드는 웨이브가 시작할 때마다 한 번 실행되어, 매 웨이브 새 빈 리스트로 시작합니다.", "# 팀 보드\njobs = []");
            Help(BlockKind.Append, BlockKind.EmptyList, "list.append(v) · 리스트에 넣기",
                "리스트의 맨 뒤에 값을 넣습니다. 적, 로봇, 숫자를 넣을 수 있어요. 쓰러진 적도 누군가 꺼낼 때까지 리스트에 남아 있으니(팀 보드 카드에 ✕) 꺼낸 뒤 is_dead()로 확인하세요.",
                "for e in get_enemies():\n    if e.type == Enemy.Healer and e not in jobs:\n        jobs.append(e)");
            Help(BlockKind.Pop, BlockKind.Append, "list.pop(0) · 맨 앞 꺼내기",
                "리스트의 맨 앞(가장 먼저 넣은) 값을 꺼내 돌려줍니다. 꺼낸 값은 리스트에서 빠지니 다른 로봇이 같은 일을 다시 꺼내지 않아요(큐, 먼저 넣은 것이 먼저).\n" +
                "빈 리스트에서 꺼내면 오류가 나니 if len(jobs) > 0:으로 먼저 확인하세요. 변수 뒤에 점을 찍고 pop(0)을 고르세요.",
                "if len(jobs) > 0:\n    target = jobs.pop(0)\n    while not target.is_dead():\n        shoot(target.weak_point())");
            Help(BlockKind.Len, BlockKind.Pop, "len(x) · 개수", "리스트(또는 사전)에 든 값의 개수입니다. 빈 리스트는 0이에요.", "if len(jobs) > 0:\n    target = jobs.pop(0)");
            Help(BlockKind.In, BlockKind.Or, "v in list · 들어 있나",
                "값이 리스트에 들어 있으면 True입니다. 가운데 칸에서 not in을 고르면 반대: 들어 있지 않으면 True. 같은 적을 두 번 넣지 않을 때 씁니다.",
                "if e not in jobs:\n    jobs.append(e)");
            Help(BlockKind.Not, BlockKind.In, "not 조건 · 반대로", "조건의 참과 거짓을 뒤집습니다. not target.is_dead()는 '아직 살아 있으면'이에요.",
                "while not target.is_dead():\n    shoot(target)");
            Help(BlockKind.DictLiteral, BlockKind.Len, "{키: 값} · 사전",
                "키마다 값을 하나씩 짝지어 둡니다. 키는 Enemy.Healer 같은 종류, 값은 숫자예요(빈 칸 짝은 빠지고, 한 블록에 3쌍까지). 팀 보드에 두면 모든 로봇이 같은 표를 봅니다.",
                "# 팀 보드\npriority = {Enemy.Healer: 0, Enemy.Shielder: 1, Enemy.Normal: 2}");
            Help(BlockKind.Index, BlockKind.DictLiteral, "d[k] · 사전에서 꺼내기",
                "사전에서 키에 짝지어진 값을 꺼냅니다. 사전에 없는 키를 꺼내면 오류(KeyError)가 나요. 리스트에 쓰면 그 번째 값입니다(jobs[0]이 맨 앞).",
                "best = get_nearest_enemy()\nfor e in get_enemies():\n    if priority[e.type] < priority[best.type]:\n        best = e\nshoot(best.weak_point())");
            Help(BlockKind.GetRobots, BlockKind.GetEnemies, "get_robots() · 로봇 목록",
                "배치된 모든 로봇의 리스트입니다. for r in get_robots():로 하나씩 보며 r.hp(체력)와 r.role(종류)을 읽을 수 있어요.",
                "for r in get_robots():\n    if r.hp < 30:\n        buff(r)");
            Help(BlockKind.Role, BlockKind.Top, "robot.role · 로봇 종류",
                "로봇의 종류입니다. Robot.Melee(근접), Robot.Ranged(원거리), Robot.Support(지원)와 비교하세요. 로봇 변수 뒤에 점을 찍고 role을 고르세요(hp도 있어요).",
                "for r in get_robots():\n    if r.role == Robot.Ranged:\n        buff(r)");
            Help(BlockKind.RobotRoleName, BlockKind.EnemyTypeName, "Robot.Melee · 로봇 종류 이름",
                "로봇 종류의 이름입니다. 고르는 칸에서 Melee(근접), Ranged(원거리), Support(지원)를 고르고 r.role과 비교할 때 씁니다.",
                "if r.role == Robot.Support:\n    continue");
            Help(BlockKind.BuffRobot, BlockKind.Buff, "buff(r) · 한 로봇 강화",
                $"지원 로봇이 고른 로봇 하나를 강화하고 체력을 {DefenseSimulation.BuffRepair:0} 채워 줍니다. 거리와 상관없이 닿아요. 빈칸 없는 buff()는 지금처럼 범위 안 로봇 모두를 강화합니다.",
                "for r in get_robots():\n    if r.hp < 30:\n        buff(r)");
            Help(BlockKind.WeakPoint, BlockKind.Right, "enemy.weak_point() · 약점",
                "지금 맞힐 수 있는 부분을 돌려줍니다: 큐 지네는 맨 앞(front()), 스택 지네는 맨 위(top()), 군단 트리는 왼쪽부터 내려가 닿는 살아 있는 잎, 그 밖의 적은 자기 자신.\n" +
                "3스테이지에서 e.left·e.right와 while로 트리를 손수 내려가 본 그 순회를 한 번에 해 주는 추상화예요. 손으로 먼저 해 보고, 4스테이지부터 줄여 씁니다.",
                "e = get_nearest_enemy()\nshoot(e.weak_point())");
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
