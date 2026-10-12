using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CodingGame
{
    public sealed class GameFlow : MonoBehaviour
    {
        const float FadeSeconds = 0.35f;
        static GameFlow instance;
        Image fade;
        GameObject fadeCover;
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

            fadeCover = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fadeCover.transform.SetParent(transform, false);
            var rect = (RectTransform)fadeCover.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            fade = fadeCover.GetComponent<Image>();
            fade.color = Color.black;
            fade.raycastTarget = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start() => StartCoroutine(FadeTo(0f));

        void OnDestroy()
        {
            if (instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindTitleButtons(scene);
            if (scene.name == "Stage") StageMap.StageMapBootstrap.EnsureInstalled();
            StartCoroutine(FadeInAfterLoad());
        }

        void BindTitleButtons(Scene scene)
        {
            if (scene.name != "Title") return;
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "GameStartBtn") button.onClick.AddListener(() => LoadScene("Stage"));
                else if (button.name == "GameQuitBtn") button.onClick.AddListener(QuitGame);
            }
        }

        IEnumerator FadeInAfterLoad()
        {
            fadeCover.SetActive(true);
            fade.color = Color.black;
            yield return FadeTo(0f);
            changingScene = false;
            fade.raycastTarget = false;
            fadeCover.SetActive(false);
        }

        IEnumerator FadeTo(float target)
        {
            float start = fade.color.a;
            float elapsed = 0f;
            while (elapsed < FadeSeconds)
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

        IEnumerator ChangeScene(string sceneName)
        {
            changingScene = true;
            fadeCover.SetActive(true);
            fade.raycastTarget = true;
            yield return FadeTo(1f);
            SceneManager.LoadScene(sceneName);
        }

        public static void LoadScene(string sceneName)
        {
            if (!instance || instance.changingScene) return;
            instance.StartCoroutine(instance.ChangeScene(sceneName));
        }

        public static void FinishDefense(bool cleared) => LoadScene(cleared ? "Stage" : "Title");

        public static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
