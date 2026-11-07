using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CodingGame
{
    // Created before the first scene loads and kept across scenes: owns the fade and the Title → Stage → Defense flow.
    public sealed class GameFlow : MonoBehaviour
    {
        public const string TitleScene = "Title", StageScene = "Stage", DefenseScene = "Defense";
        const float FadeSeconds = 0.35f;
        static GameFlow instance;
        Image fade;
        // The title's music (Resources/Music/title, CC0: Resources/Music/CREDITS.txt), under the interface sounds; it plays only on
        // the title and fades with the screen.
        AudioSource music;
        const float MusicVolume = .55f;
        bool changingScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            if (instance) SceneManager.sceneLoaded -= instance.OnSceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var root = new GameObject("GameFlow");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameFlow>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();

            fade = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            var rect = fade.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            fade.color = Color.black;
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false; music.spatialBlend = 0; music.volume = 0;
            music.clip = Resources.Load<AudioClip>("Music/title");

            // Subscribed before the first scene loads, so this also fires for it.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TitleScene) BindTitleButtons();
            else if (scene.name == StageScene) StageMap.StageMapBootstrap.Install();
            if (scene.name != TitleScene) music.Stop();
            else if (music.clip && !music.isPlaying) music.Play();
            StartCoroutine(FadeInAfterLoad());
        }

        // Title buttons are wired by name; the scene stores no OnClick calls.
        static void BindTitleButtons()
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "GameStartBtn") button.onClick.AddListener(() => LoadScene(StageScene));
                else if (button.name == "GameQuitBtn") button.onClick.AddListener(QuitGame);
                else continue;
                // The art title animates its own buttons; the text highlight is only for plain buttons.
                if (!button.TryGetComponent<TitleMenuButton>(out _) && !button.TryGetComponent<TitleScreen.TitleButtonMotion>(out _)) button.gameObject.AddComponent<TitleMenuButton>();
            }
        }

        IEnumerator FadeInAfterLoad()
        {
            fade.gameObject.SetActive(true);
            fade.color = Color.black;
            yield return FadeTo(0f);
            changingScene = false;
            fade.raycastTarget = false;
            fade.gameObject.SetActive(false);
        }

        IEnumerator FadeTo(float target)
        {
            float start = fade.color.a;
            for (float elapsed = 0f; elapsed < FadeSeconds;)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFade(Mathf.Lerp(start, target, elapsed / FadeSeconds));
                yield return null;
            }
            SetFade(target);
        }

        void SetFade(float alpha)
        {
            Color color = fade.color;
            color.a = alpha;
            fade.color = color;
            fade.raycastTarget = alpha > 0.001f || changingScene;
            music.volume = MusicVolume * (1 - alpha);
        }

        IEnumerator ChangeScene(string sceneName, float delay)
        {
            changingScene = true;
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            fade.gameObject.SetActive(true);
            yield return FadeTo(1f);
            SceneManager.LoadScene(sceneName);
        }

        // delay: seconds to wait before the fade starts (the stage map's camera flies to the stage first).
        public static void LoadScene(string sceneName, float delay = 0)
        {
            if (!instance || instance.changingScene) return;
            instance.StartCoroutine(instance.ChangeScene(sceneName, delay));
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
