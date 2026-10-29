using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class MeleeSwingTestHarness
{
 public static void Run()
 {
  CodingGame.Art.CharacterAnimation.MeleeSwingRevision.Refresh();
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Melee swing playback check").AddComponent<MeleeSwingPlayProbe>();
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/MeleeSwingCheck.unity");
  EditorApplication.EnterPlaymode();
 }
}
