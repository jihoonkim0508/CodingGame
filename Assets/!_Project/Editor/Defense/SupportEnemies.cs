using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Healer.asset and Shielder.asset in Defense/Data as copies of Enemy.asset (their own models, prefabs and icons come from
    // SupportMonsterArt.Apply). GameData apply puts them in the campaign when Balance.md uses them.
    // Also QueueCentipede.asset and StackCentipede.asset the same way (3-4), the Reflector (3-3, its model from SupportMonsterArt),
    // and TreeLegion.asset (3-3) with its own models: TreeLegion.prefab (the root) and TreeLegionNode.prefab (the nodes below it)
    // from the Blender models in Defense/Models/TreeLegion (Art~/TreeLegion/tree_legion.py), each a copy of Enemy.prefab with its model swapped.
    public static class SupportEnemies
    {
        const string Folder = "Assets/!_Project/Defense/Data";
        public const string StackDescription = "상자가 한 층씩 쌓여 걷는 스택. 몇 초마다 맨 위에 새 층이 떨어집니다(최대 8층). 맨 위 층(가장 나중에 쌓인 층)만 피해를 받고, 그냥 쏘면 맨 아래 층에 막힙니다. e.top()을 노리세요.";

        public static void Create()
        {
            Make("Healer", "힐러", "몇 초마다 주변 적의 체력을 회복시킵니다. 먼저 쓰러뜨리지 않으면 다른 적이 잘 죽지 않습니다.",
                new EnemySpec { type = EnemyType.Healer, health = 30, speed = 1.4f, damage = 6, attackInterval = 1.2f, abilityInterval = 2.5f, abilityRadius = 3, abilityAmount = 10 });
            Make("Shielder", "보호막병", "몇 초마다 주변 적에게 체력보다 먼저 피해를 받는 보호막을 씌웁니다. break_shield()로 깨세요.",
                new EnemySpec { type = EnemyType.Shielder, health = 30, speed = 1.5f, damage = 6, attackInterval = 1.2f, abilityInterval = 3, abilityRadius = 2.5f, abilityAmount = 12 });
            // Centipedes: a Balance.md row is one centipede, its count the segments it starts with, its interval their spacing.
            Make("QueueCentipede", "큐 지네", "마디가 한 줄로 걷는 지네. 맨 앞 마디(가장 먼저 들어온 마디)만 피해를 받습니다. e.front()를 노리세요.",
                new EnemySpec { type = EnemyType.Queue, health = 10, speed = 1.2f, damage = 6, attackInterval = 1.2f });
            Make("StackCentipede", "스택 지네", StackDescription,
                new EnemySpec { type = EnemyType.Stack, health = 10, speed = 1.2f, damage = 6, attackInterval = 1.2f, abilityInterval = 4, segmentCap = 8 });
            // Reflector (3-3): every 3 s normal, then 2 s reflecting (hits go back to the robot that made them).
            Make("Reflector", "반사병", "몇 초마다 거울처럼 빛나는 반사 상태가 됩니다. 그동안 맞은 공격은 쏜 로봇에게 튕겨 갑니다. e.reflecting이면 건너뛰세요.",
                new EnemySpec { type = EnemyType.Reflector, health = 24, speed = 1.2f, damage = 6, attackInterval = 1.2f, reflectOn = 2, reflectOff = 3 });
            // A tree legion: a Balance.md row's count is how many trees, each a whole tree of random shape (TreeNode.Roll).
            // ponytail: its wave-panel icon is still Enemy.asset's; render a tree icon when the art is final.
            Make("TreeLegion", "군단 트리", "대장(뿌리) 아래로 가지가 갈라진 이진 트리. 자식이 살아 있는 마디는 피해를 받지 않으니 잎부터 쓰러뜨리세요. " +
                "get_enemies()에는 대장만 나옵니다. e.left·e.right로 내려가세요.",
                new EnemySpec { type = EnemyType.Tree, health = 14, speed = .9f, damage = 6, attackInterval = 1.2f });
            var tree = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{Folder}/TreeLegion.asset");
            tree.prefab = TreeModel("TreeLegion", 1.5f); tree.nodePrefab = TreeModel("TreeLegionNode", .45f);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
        }
        // Enemy.prefab with its Model swapped for the tree model (one palette material), the health bar at barHeight.
        static DefenseActorView TreeModel(string name, float barHeight)
        {
            const string models = "Assets/!_Project/Defense/Models/TreeLegion";
            // The palette is 4x4 cells the UVs sit in: no filtering, mipmaps or compression to blend them.
            var palette = (TextureImporter)AssetImporter.GetAtPath($"{models}/TreePalette.png");
            if (palette.filterMode != FilterMode.Point)
            {
                palette.filterMode = FilterMode.Point; palette.mipmapEnabled = false; palette.textureCompression = TextureImporterCompression.Uncompressed;
                palette.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{models}/TreeLegion.mat");
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TreeLegion" };
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{models}/TreePalette.png"));
                material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .35f);
                AssetDatabase.CreateAsset(material, $"{models}/TreeLegion.mat");
            }
            var root = PrefabUtility.LoadPrefabContents("Assets/!_Project/Defense/Prefabs/Enemy.prefab");
            try
            {
                Object.DestroyImmediate(root.transform.Find("Model").gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{models}/{name}.fbx"), root.transform);
                model.name = "Model";
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                var view = root.GetComponent<DefenseActorView>();
                var fields = new SerializedObject(view);
                fields.FindProperty("body").objectReferenceValue = model.GetComponentsInChildren<Renderer>().OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
                fields.ApplyModifiedPropertiesWithoutUndo();
                var bar = (Transform)fields.FindProperty("healthRoot").objectReferenceValue;
                bar.localPosition = new Vector3(0, barHeight, 0);
                ((Transform)fields.FindProperty("head").objectReferenceValue).localPosition = new Vector3(0, barHeight * .8f, 0);
                return PrefabUtility.SaveAsPrefabAsset(root, $"Assets/!_Project/Defense/Prefabs/{name}.prefab").GetComponent<DefenseActorView>();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Make(string file, string name, string description, EnemySpec stats)
        {
            string path = $"{Folder}/{file}.asset";
            if (!AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path) && !AssetDatabase.CopyAsset($"{Folder}/Enemy.asset", path))
                throw new System.InvalidOperationException(path + " 복사 실패");
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            enemy.displayName = name; enemy.description = description; enemy.stats = stats;
            EditorUtility.SetDirty(enemy);
            Debug.Log("[SupportEnemies] " + path);
        }
    }
}
