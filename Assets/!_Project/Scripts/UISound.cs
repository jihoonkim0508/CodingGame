using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CodingGame
{
    // Interface sounds for every scene: a click under any enabled button, toggle or field the mouse presses, and named
    // sounds (Resources/Sounds/<kind>) for moments like opening the code editor or dropping a block. Kept across scenes;
    // the Esc menu's sound switch silences it with the rest through the listener.
    public sealed class UISound : MonoBehaviour
    {
        static UISound instance;
        AudioSource source;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var root = new GameObject("UISound");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<UISound>();
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            foreach (var clip in Resources.LoadAll<AudioClip>("Sounds")) clips[clip.name] = clip;
        }

        public static void Play(string kind, float volume = 1)
        {
            if (instance && instance.clips.TryGetValue(kind, out var clip)) instance.source.PlayOneShot(clip, volume);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || !EventSystem.current) return;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, hits);
            if (hits.Count == 0) return;
            var control = hits[0].gameObject.GetComponentInParent<Selectable>();
            if (control && control.IsInteractable()) Play("click");
        }
    }
}
