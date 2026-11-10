using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PlaybackHarness
{
 public static void SetupAndRun()
 {
  CodingGame.Art.CharacterAnimation.CharacterAnimationSetup.Setup();Run();
 }
 public static void Run()
 {
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  new GameObject("Animation Playback Probe").AddComponent<AnimationPlaybackProbe>();
  EditorSceneManager.SaveScene(scene,"Assets/AnimationPlaybackCheck.unity");EditorApplication.EnterPlaymode();
 }
}
