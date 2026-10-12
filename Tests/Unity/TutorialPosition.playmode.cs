// Run as Unity MCP Unity_RunCommand.Code in Play Mode. Uses a disposable prefab copy.
using System;
using CodingGame.Defense;
using UnityEditor;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/!_Project/Defense/Tutorial/TutorialOverlay.prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        result.RegisterObjectCreation(root);
        var sequence = ScriptableObject.CreateInstance<TutorialSequence>();
        result.RegisterObjectCreation(sequence);
        try
        {
            var view = root.GetComponentInChildren<TutorialDialogueView>(true);
            var settings = new SerializedObject(view);
            var panel = (GameObject)settings.FindProperty("panel").objectReferenceValue;
            var rect = (RectTransform)panel.transform;
            var min = rect.anchorMin; var max = rect.anchorMax;
            var position = rect.anchoredPosition; var size = rect.sizeDelta; var pivot = rect.pivot;
            float inset = settings.FindProperty("topInset").floatValue;
            view.Initialize(null, null);
            foreach (var placement in new[] { TutorialDialoguePosition.Bottom, TutorialDialoguePosition.Top,
                TutorialDialoguePosition.Bottom, TutorialDialoguePosition.Top })
            {
                view.Show(sequence, placement, "위치 확인", false);
                bool top = placement == TutorialDialoguePosition.Top;
                if (rect.anchorMin != new Vector2(min.x, top ? 1 - max.y : min.y) ||
                    rect.anchorMax != new Vector2(max.x, top ? 1 - min.y : max.y) ||
                    rect.anchoredPosition != new Vector2(position.x, top ? -position.y - inset : position.y) ||
                    rect.pivot != new Vector2(pivot.x, top ? 1 - pivot.y : pivot.y) || rect.sizeDelta != size)
                    throw new Exception("Panel placement/size changed incorrectly: " + placement);
                if (!view.IsVisible || view.IsTyping) throw new Exception("Revisited dialogue must appear without typing");
            }
            if (new TutorialStep().dialoguePosition != TutorialDialoguePosition.Bottom)
                throw new Exception("Existing steps must default to bottom");
            result.Log("PASS: bottom/top switching below the header, returning to earlier position, preserved size, no repeated typing, default bottom.");
        }
        finally { result.DestroyObject(root); result.DestroyObject(sequence); }
    }
}
