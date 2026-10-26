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
        bool changingScene;

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
            gameObject.AddComponent<GraphicRaycaster>();

            fade = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            var rect = fade.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            fade.color = Color.black;

            // Subscribed before the first scene loads, so this also fires for it.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TitleScene) BindTitleButtons();
            else if (scene.name == StageScene) StageMap.StageMapBootstrap.Install();
            StartCoroutine(FadeInAfterLoad());
        }

        // Title buttons are wired by name; the scene stores no OnClick calls.
        static void BindTitleButtons()
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "GameStartBtn") button.onClick.AddListener(() => LoadScene(StageScene));
                else if (button.name == "GameQuitBtn") button.onClick.AddListener(QuitGame);
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
