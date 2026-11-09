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
            instance = null; Loading = false; Developer = false;
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

        // Loading screen: true while the screen is covered for a scene change. Intros that run on their own clock (the map's
        // build, the title, 디버's typing, the system alert) wait for it, so they start when the scene shows.
        public static bool Loading { get; private set; }
        const float LoadingShowAfter = .25f; // a quick load shows no loading card, only the fade
        CanvasGroup loadingCard;
        RectTransform loadingFill;
        float coveredAt;
        void BuildLoadingCard()
        {
            var card = new GameObject("Loading", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            card.SetParent(transform, false); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(520, 160);
            loadingCard = card.GetComponent<CanvasGroup>(); loadingCard.alpha = 0; loadingCard.blocksRaycasts = false;
            var label = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
            label.rectTransform.SetParent(card, false); label.rectTransform.sizeDelta = new Vector2(520, 70); label.rectTransform.anchoredPosition = new Vector2(0, 30);
            label.font = UITheme.Display; label.fontSize = 44; label.alignment = TMPro.TextAlignmentOptions.Center; label.color = UITheme.Paper;
            label.text = "불러오는 중"; label.raycastTarget = false;
            Image Bar(string name, Transform parent, Color color)
            {
                var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(parent, false); UITheme.Style(image, UITheme.Pill, color); image.raycastTarget = false;
                return image;
            }
            var track = Bar("Track", card, new Color(1, 1, 1, .14f)).rectTransform;
            track.sizeDelta = new Vector2(420, 16); track.anchoredPosition = new Vector2(0, -36);
            loadingFill = Bar("Fill", track, UITheme.Primary).rectTransform;
            loadingFill.anchorMin = Vector2.zero; loadingFill.anchorMax = new Vector2(0, 1); loadingFill.pivot = new Vector2(0, .5f); loadingFill.sizeDelta = Vector2.zero;
        }
        // The card shows once the cover has lasted LoadingShowAfter; its bar fills to progress (0 to 1).
        void ShowLoading(float progress)
        {
            if (!loadingCard) BuildLoadingCard();
            loadingCard.alpha = Mathf.Clamp01((Time.unscaledTime - coveredAt - LoadingShowAfter) / .15f);
            loadingFill.anchorMax = new Vector2(Mathf.Max(loadingFill.anchorMax.x, Mathf.Clamp01(progress)), 1);
        }

        // Developer mode (the battle's F1 panel and 5x speed, the stage map's Z/Q keys): ten clicks in a row on the title's
        // top-left corner turn it on, ten more off; it lasts while the game runs (like clears and stars).
        public static bool Developer { get; private set; }
        const int DeveloperClicks = 10;
        const float DeveloperCorner = 120, DeveloperClickGap = 1; // 1080p pixels from the corner; seconds between clicks
        int cornerClicks; float lastCornerClick;
        TMPro.TextMeshProUGUI toast; float toastUntil;
        void Update()
        {
            if (toast) toast.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / .4f);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && SceneManager.GetActiveScene().name == TitleScene) TitleClick(mouse.position.ReadValue());
        }
        // A click on the title (screen pixels): the tenth in a row on the corner, each within a second, toggles developer mode.
        void TitleClick(Vector2 at)
        {
            float corner = DeveloperCorner * Screen.height / 1080f;
            bool inCorner = at.x <= corner && at.y >= Screen.height - corner;
            cornerClicks = !inCorner ? 0 : Time.unscaledTime - lastCornerClick <= DeveloperClickGap ? cornerClicks + 1 : 1;
            lastCornerClick = Time.unscaledTime;
            if (cornerClicks < DeveloperClicks) return;
            cornerClicks = 0; Developer = !Developer;
            ShowToast(Developer ? "개발자 모드 켜짐" : "개발자 모드 꺼짐");
        }
        void ShowToast(string text)
        {
            if (!toast)
            {
                toast = new GameObject("Toast", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
                var rect = toast.rectTransform; rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(24, -20); rect.sizeDelta = new Vector2(600, 50);
                toast.font = UITheme.Display; toast.fontSize = 30; toast.color = UITheme.Primary; toast.raycastTarget = false;
                toast.outlineWidth = .25f; toast.outlineColor = UITheme.Ink;
            }
            toast.text = text; toastUntil = Time.unscaledTime + 2;
        }

        // Title buttons are wired by name; the scene stores no OnClick calls.
        static void BindTitleButtons()
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button.name == "GameStartBtn") button.onClick.AddListener(() => LoadScene(StageScene));
                else if (button.name == "GameQuitBtn") button.onClick.AddListener(QuitGame);
                // The title animates its own buttons (TitleButtonMotion); the old plain-button highlight is gone (13차).
            }
        }

        // The new scene renders under the black for a few frames before it shows: its shaders compile and its textures upload
        // there (the Editor compiles shaders in the background and draws them blank until done), not in sight.
        IEnumerator FadeInAfterLoad()
        {
            fade.gameObject.SetActive(true);
            fade.color = Color.black;
            if (!Loading) { Loading = true; coveredAt = Time.unscaledTime; } // the first scene, or one loaded without LoadScene
            for (int frame = 0; frame < 3; frame++) { ShowLoading(.9f + frame * .03f); yield return null; }
#if UNITY_EDITOR
            for (float waited = 0; !Application.isBatchMode && UnityEditor.ShaderUtil.anythingCompiling && waited < 15; waited += Time.unscaledDeltaTime)
            { ShowLoading(.97f); yield return null; }
#endif
            ShowLoading(1);
            if (loadingCard.alpha > 0) yield return new WaitForSecondsRealtime(.15f); // the full bar, briefly
            loadingCard.alpha = 0; loadingFill.anchorMax = new Vector2(0, 1);
            Loading = false;
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
            Loading = true; coveredAt = Time.unscaledTime;
            var load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone) { ShowLoading(load.progress); yield return null; } // 0 to .9 while loading
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
