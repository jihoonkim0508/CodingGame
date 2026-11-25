using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Healer.asset and Shielder.asset in Defense/Data as copies of Enemy.asset (its model and icon: placeholder art until the
    // stage-2 monsters land; DefenseActorView tints them). GameData apply puts them in the campaign when Balance.md uses them.
    // Also QueueCentipede.asset and StackCentipede.asset the same way (3-4).
    public static class SupportEnemies
    {
        const string Folder = "Assets/!_Project/Defense/Data";

        public static void Create()
        {
            Make("Healer", "힐러", "몇 초마다 주변 적의 체력을 회복시킵니다. 먼저 쓰러뜨리지 않으면 다른 적이 잘 죽지 않습니다.",
                new EnemySpec { type = EnemyType.Healer, health = 30, speed = 1.4f, damage = 6, attackInterval = 1.2f, abilityInterval = 2.5f, abilityRadius = 3, abilityAmount = 10 });
            Make("Shielder", "보호막병", "몇 초마다 주변 적에게 체력보다 먼저 피해를 받는 보호막을 씌웁니다. break_shield()로 깨세요.",
                new EnemySpec { type = EnemyType.Shielder, health = 30, speed = 1.5f, damage = 6, attackInterval = 1.2f, abilityInterval = 3, abilityRadius = 2.5f, abilityAmount = 12 });
            // Centipedes: a Balance.md row is one centipede, its count the segments it starts with, its interval their spacing.
            Make("QueueCentipede", "큐 지네", "마디가 한 줄로 걷는 지네. 맨 앞 마디(가장 먼저 들어온 마디)만 피해를 받습니다. e.front()를 노리세요.",
                new EnemySpec { type = EnemyType.Queue, health = 10, speed = 1.2f, damage = 6, attackInterval = 1.2f });
            Make("StackCentipede", "스택 지네", "몇 초마다 꼬리에 새 마디를 쌓는 지네(최대 8마디). 맨 위 마디(가장 나중에 들어온 꼬리)만 피해를 받습니다. e.top()을 노리세요.",
                new EnemySpec { type = EnemyType.Stack, health = 10, speed = 1.2f, damage = 6, attackInterval = 1.2f, abilityInterval = 4, segmentCap = 8 });
            AssetDatabase.SaveAssets();
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
