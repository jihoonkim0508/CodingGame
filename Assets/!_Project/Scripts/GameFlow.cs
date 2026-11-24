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
            instance = null; Loading = false; Developer = Debug.isDebugBuild; developerByHand = false; // the Editor and development builds start with it
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance) return;
            var root = new GameObject("GameFlow");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameFlow>();
            root.AddComponent<Defense.DefenseDevWindow>(); // F1·F2 in every scene (developer rights)
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
            blackOnly = BlackOnly(scene.name);
            StartCoroutine(FadeInAfterLoad());
        }

        // Loading screen: true while the screen is covered for a scene change. Intros that run on their own clock (the map's
        // build, the title, 디버's typing, the system alert) wait for it, so they start when the scene shows.
        public static bool Loading { get; private set; }
        const float LoadingMinSeconds = 1; // once shown, the loading screen stays this long (real time) even for a quick load
        // Assets/ThisIsCodingLoading (the team's loading screen), a nested canvas drawn above the fade.
        GameObject loadingScreen;
        float loadingShownAt;
        // The title and the stage map load under plain black; only the battle shows the loading screen.
        bool blackOnly;
        static bool BlackOnly(string scene) => scene == TitleScene || scene == StageScene;
        void ShowLoading()
        {
            if (!loadingScreen)
            {
                loadingScreen = Instantiate(Resources.Load<GameObject>("LoadingScreen"), transform, false);
                var rect = (RectTransform)loadingScreen.transform; // the prefab's root is a 0x0 canvas: stretch it over the screen
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                loadingScreen.SetActive(false);
            }
            if (blackOnly || loadingScreen.activeSelf) return;
            loadingScreen.SetActive(true); loadingShownAt = Time.realtimeSinceStartup;
        }

        // Developer rights (the F1 jump panel and F2 balance window in every scene, the battle's F1 panel, 5x speed, the dialogue ids, the stage map's Z/Q keys):
        // on from the start in the Editor and development builds; in a release build ten clicks in a row on the screen's
        // top-left corner, in any scene, turn them on, ten more off. They last while the game runs, shown by "Dev" in the bottom-left corner.
        public static bool Developer { get; private set; }
        // 5x battle speed: in the Editor, or once developer mode is turned on by hand (the corner clicks). A development build
        // starts with developer rights for its tools, and its play testers met 5x where players stop at 3x (play feedback 2-9).
        public static bool FastSpeed => Developer && (Application.isEditor || developerByHand);
        static bool developerByHand;
        TMPro.TextMeshProUGUI devMark;
        const int DeveloperClicks = 10;
        const float DeveloperCorner = 120, DeveloperClickGap = 1; // 1080p pixels from the corner; seconds between clicks
        int cornerClicks; float lastCornerClick;
        TMPro.TextMeshProUGUI toast; float toastUntil;
        void Update()
        {
            if (toast) toast.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / .4f);
            if (Developer && !devMark)
            {
                devMark = new GameObject("Dev", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)).GetComponent<TMPro.TextMeshProUGUI>();
                var rect = devMark.rectTransform; rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(16, 10); rect.sizeDelta = new Vector2(120, 40);
                devMark.font = UITheme.Display; devMark.fontSize = 28; devMark.color = UITheme.Primary; devMark.text = "Dev"; devMark.raycastTarget = false;
                devMark.outlineWidth = .25f; devMark.outlineColor = UITheme.Ink;
            }
            if (devMark) devMark.gameObject.SetActive(Developer);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) CornerClick(mouse.position.ReadValue());
        }
        // A click in any scene (screen pixels): the tenth in a row on the corner, each within a second, toggles developer mode.
        void CornerClick(Vector2 at)
        {
            float corner = DeveloperCorner * Screen.height / 1080f;
            bool inCorner = at.x <= corner && at.y >= Screen.height - corner;
            cornerClicks = !inCorner ? 0 : Time.unscaledTime - lastCornerClick <= DeveloperClickGap ? cornerClicks + 1 : 1;
            lastCornerClick = Time.unscaledTime;
            if (cornerClicks < DeveloperClicks) return;
            cornerClicks = 0; Developer = developerByHand = !Developer;
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
            Loading = true; // also the first scene, or one loaded without LoadScene
            ShowLoading();
            for (int frame = 0; frame < 3; frame++) yield return null;
#if UNITY_EDITOR
            for (float waited = 0; !Application.isBatchMode && UnityEditor.ShaderUtil.anythingCompiling && waited < 15; waited += Time.unscaledDeltaTime)
                yield return null;
#endif
            if (loadingScreen.activeSelf)
            {
                while (Time.realtimeSinceStartup - loadingShownAt < LoadingMinSeconds) yield return null;
                loadingScreen.SetActive(false);
            }
            Loading = false;
            // One frame still under full black with Loading off, so what waits for it (1-1's alarm) is up before the black lifts.
            yield return null;
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
            Loading = true; blackOnly = BlackOnly(sceneName);
            ShowLoading();
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        // delay: seconds to wait before the fade starts (the stage map's camera flies to the stage first). False while a
        // change is under way (nothing happens then).
        public static bool LoadScene(string sceneName, float delay = 0)
        {
            if (!instance || instance.changingScene) return false;
            instance.StartCoroutine(instance.ChangeScene(sceneName, delay));
            return true;
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
