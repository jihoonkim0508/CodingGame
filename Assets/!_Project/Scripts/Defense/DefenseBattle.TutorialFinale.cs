using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.Defense
{
    public sealed partial class DefenseBattle
    {
        int shownBoss;
        float bossEntranceAt = -10;
        TutorialSpotlight bossEntrance;
        public bool IsTutorialFinale => Simulation != null && stage == 1 && Simulation.WaveIndex == 4 && Simulation.Options.tutorial;
        public bool FinaleEntrancePlaying => bossEntrance && bossEntrance.gameObject.activeSelf;

        void CancelFinalePresentation()
        {
            shownBoss = 0; bossEntranceAt = -10;
            if (bossEntrance) bossEntrance.gameObject.SetActive(false);
            if (selectionCamera) selectionCamera.Restore();
        }
        void TickFinalePresentation()
        {
            if (!IsTutorialFinale || Simulation.Phase == BattlePhase.Defeat)
            { if (FinaleEntrancePlaying) CancelFinalePresentation(); return; }
            if (Simulation.FinaleBossId != 0 && shownBoss != Simulation.FinaleBossId)
            {
                shownBoss = Simulation.FinaleBossId; bossEntranceAt = Time.unscaledTime;
                var boss = Simulation.Enemies.FirstOrDefault(e => e.Id == shownBoss);
                if (boss != null) selectionCamera.Focus(DefenseViews.World(boss.Position));
                if (!bossEntrance)
                {
                    var canvas = new GameObject("Boss entrance overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                    canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.GetComponent<Canvas>().sortingOrder = 60;
                    var scale = canvas.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = .5f;
                    canvas.transform.SetParent(transform, false);
                    bossEntrance = new GameObject("Boss focus", typeof(RectTransform), typeof(TutorialSpotlight)).GetComponent<TutorialSpotlight>();
                    bossEntrance.transform.SetParent(canvas.transform, false);
                    var rect = bossEntrance.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                    bossEntrance.raycastTarget = false;
                }
                bossEntrance.gameObject.SetActive(true); bossEntrance.Pulse();
            }
            if (!FinaleEntrancePlaying) return;
            if (Time.unscaledTime - bossEntranceAt >= 1.3f || Simulation.Phase != BattlePhase.Running)
            { bossEntrance.gameObject.SetActive(false); selectionCamera.Restore(); return; }
            var enemy = Simulation.Enemies.FirstOrDefault(e => e.Id == shownBoss);
            if (enemy == null) { bossEntrance.gameObject.SetActive(false); selectionCamera.Restore(); return; }
            var centre = battleCamera.WorldToScreenPoint(DefenseViews.World(enemy.Position, 1));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bossEntrance.rectTransform, centre, null, out var point);
            var box = new Rect(point - new Vector2(100, 115), new Vector2(200, 230));
            bossEntrance.SetHoles(Array.Empty<Rect>(), new[] { box }, new[] { box }, Array.Empty<Rect>(), 12, true);
        }
        public void RetryTutorialFinale()
        {
            if (!IsTutorialFinale || !TutorialAllows(TutorialKeys.RetryFinale) || !Simulation.RetryFinale()) return;
            CancelFinalePresentation(); ClearDrops(); ClearCombatVisuals(); projectileViews.Clear(); hitEffects.Clear();
            selected = 0; BattleSpeed = 1; rangeRing.Hide(); effectRing.Hide();
            SyncViews(0); SyncTutorialTargets(); RefreshHUD();
            tutorial.RestartFinale();
        }
        public void QuitTutorialFinale()
        {
            if (!IsTutorialFinale || Simulation.Phase != BattlePhase.Defeat || !TutorialAllows(TutorialKeys.Quit)) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
