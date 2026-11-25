using UnityEditor;
using UnityEngine;

namespace CodingGame.Defense.Editor
{
    // Healer.asset and Shielder.asset in Defense/Data as copies of Enemy.asset (its model and icon: placeholder art until the
    // stage-2 monsters land; DefenseActorView tints them). GameData apply puts them in the campaign when Balance.md uses them.
    public static class SupportEnemies
    {
        const string Folder = "Assets/!_Project/Defense/Data";

        public static void Create()
        {
            Make("Healer", "힐러", "몇 초마다 주변 적의 체력을 회복시킵니다. 먼저 쓰러뜨리지 않으면 다른 적이 잘 죽지 않습니다.",
                new EnemySpec { type = EnemyType.Healer, health = 30, speed = 1.4f, damage = 6, attackInterval = 1.2f, abilityInterval = 2.5f, abilityRadius = 3, abilityAmount = 10 });
            Make("Shielder", "보호막병", "몇 초마다 주변 적에게 체력보다 먼저 피해를 받는 보호막을 씌웁니다. break_shield()로 깨세요.",
                new EnemySpec { type = EnemyType.Shielder, health = 30, speed = 1.5f, damage = 6, attackInterval = 1.2f, abilityInterval = 3, abilityRadius = 2.5f, abilityAmount = 12 });
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
