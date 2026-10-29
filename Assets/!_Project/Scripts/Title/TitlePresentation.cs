using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CodingGame.TitleScreen
{
    // A layered illustration: the two armies move independently of the fixed city.
    public sealed class TitlePresentation : MonoBehaviour
    {
        [Header("Team logo splash")]
        public Image teamLogo;
        public CanvasGroup teamGroup, battlefieldGroup, logoGroup, menuGroup, skipGroup;
        public RectTransform canvasRect, battlefieldRect, friendlyRect, enemyRect, logoRect;
        public Button startButton, quitButton;
        public TitleAtmosphere atmosphere;
        [Header("Rear, middle and front layers enter in overlapping beats")]
        public TitleEntranceLayer[] entranceLayers = new TitleEntranceLayer[0];
        [Header("Intro timing (unscaled seconds)")]
        [Min(0)] public float openingBlackHold = .4f;
        [Min(.1f)] public float teamFadeIn = .8f;
        [Min(0)] public float teamHold = 1.15f;
        [Min(.1f)] public float teamFadeOut = .55f;
        [Min(.1f)] public float battlefieldFadeIn = .75f;
        [Min(.1f)] public float factionEntrance = 1.4f;
        [Min(.1f)] public float titleFadeIn = .65f;
        [Min(.1f)] public float menuFadeIn = .55f;
        [Header("Restrained idle motion")]
        public bool idleMotion = true;
        [Range(0, 3)] public float idlePixels = .8f;

        float elapsed, idleTime;
        int readyFrame = -1;
        bool preview;
        public bool IsReady { get; private set; }
        public bool CanSkipTeamLogo => !IsReady && elapsed < TeamEnd;
        public float Elapsed => elapsed;
        public float TeamEnd => openingBlackHold + teamFadeIn + teamHold + teamFadeOut;
        public float FactionsStart => TeamEnd + battlefieldFadeIn * .65f;
        public float EntranceDuration
        {
            get
            {
                float end = factionEntrance;
                foreach (var layer in entranceLayers) if (layer) end = Mathf.Max(end, layer.EndTime);
                return end;
            }
        }
        public float LogoStart => FactionsStart + EntranceDuration;
        public float FactionElapsed => elapsed - FactionsStart;
        public float MenuStart => LogoStart + titleFadeIn;
        public float Duration => MenuStart + menuFadeIn;

        void Awake()
        {
            preview = false;
            elapsed = 0;
            idleTime = 0;
            readyFrame = -1;
            IsReady = false;
            var backdrop = canvasRect.Find("Black Backdrop");
            if (backdrop && backdrop.TryGetComponent<Image>(out var black)) black.color = Color.black;
            ConfigureMouseOnlyMenu();
            Apply(0);
            SetInteraction(false);
            // GameFlow keeps its existing name-based Start / Quit bindings.
            startButton.onClick.AddListener(LockMenu);
            quitButton.onClick.AddListener(LockMenu);
        }

        void Update()
        {
            if (preview) return;
            if (!IsReady)
            {
                bool skip = Keyboard.current != null &&
                    (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
                skip |= Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                skip |= Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame);
                // Let the opening black frame render before a held launch click can skip.
                if (skip && CanSkipTeamLogo && elapsed > openingBlackHold + .15f) SkipIntro();
                else
                {
                    // Cold-start shader/driver stalls must not jump over the splash.
                    elapsed = Mathf.Min(Duration, elapsed + Mathf.Min(Time.unscaledDeltaTime, 1f / 15f));
                    Apply(elapsed);
                    if (elapsed >= Duration) CompleteIntro();
                }
            }
            else if (readyFrame >= 0 && Time.frameCount > readyFrame + 1)
            {
                SetInteraction(true);
                readyFrame = -1;
            }
            if (IsReady && idleMotion)
            {
                idleTime += Time.unscaledDeltaTime;
                if (entranceLayers.Length > 0)
                    foreach (var layer in entranceLayers) if (layer) layer.Evaluate(EntranceDuration, idleTime, idlePixels);
                else
                {
                    friendlyRect.anchoredPosition = new Vector2(0, Mathf.Sin(idleTime * .62f) * idlePixels);
                    enemyRect.anchoredPosition = new Vector2(0, Mathf.Sin(idleTime * .57f + 1.2f) * idlePixels);
                }
            }
            FitBattlefield();
        }

        public void SkipIntro()
        {
            // Skip only the team splash. The battlefield and all entrance beats still play.
            if (!CanSkipTeamLogo) return;
            elapsed = TeamEnd;
            Apply(elapsed);
        }

        void CompleteIntro()
        {
            IsReady = true;
            readyFrame = Time.frameCount;
            if (skipGroup) skipGroup.alpha = 0;
        }

        void SetInteraction(bool value)
        {
            menuGroup.interactable = value;
            menuGroup.blocksRaycasts = value;
            startButton.interactable = value;
            quitButton.interactable = value;
        }

        void LockMenu() => SetInteraction(false);

        public void ConfigureMouseOnlyMenu()
        {
            foreach (var button in new[] { startButton, quitButton })
            {
                if (!button) continue;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
            }
            foreach (var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (events.gameObject.scene != gameObject.scene) continue;
                events.sendNavigationEvents = false;
                events.firstSelectedGameObject = null;
                if (Application.isPlaying) events.SetSelectedGameObject(null);
                var input = events.GetComponent<InputSystemUIInputModule>();
                if (input) { input.move = null; input.submit = null; input.cancel = null; }
            }
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static float Progress(float t, float start, float duration) => Ease((t - start) / Mathf.Max(.01f, duration));

        void Apply(float time)
        {
            teamGroup.alpha = Progress(time, openingBlackHold, teamFadeIn) * (1 - Progress(time, openingBlackHold + teamFadeIn + teamHold, teamFadeOut));
            battlefieldGroup.alpha = Progress(time, TeamEnd, battlefieldFadeIn);
            if (entranceLayers.Length > 0)
            {
                friendlyRect.anchoredPosition = enemyRect.anchoredPosition = Vector2.zero;
                foreach (var layer in entranceLayers) if (layer) layer.Evaluate(time - FactionsStart);
            }
            else
            {
                float entrance = Progress(time, FactionsStart, factionEntrance);
                friendlyRect.anchoredPosition = new Vector2(Mathf.Lerp(-1000, 0, entrance), 0);
                enemyRect.anchoredPosition = new Vector2(Mathf.Lerp(1000, 0, entrance), 0);
            }
            logoGroup.alpha = Progress(time, LogoStart, titleFadeIn);
            logoRect.anchoredPosition = new Vector2(0, Mathf.Lerp(155, 175, logoGroup.alpha));
            logoRect.localScale = Vector3.one * Mathf.Lerp(.975f, 1, logoGroup.alpha);
            menuGroup.alpha = Progress(time, MenuStart, menuFadeIn);
            if (skipGroup) skipGroup.alpha = time > openingBlackHold + .5f && time < TeamEnd ? .55f : 0;
            if (atmosphere) atmosphere.MotionTime = Mathf.Max(0, time - FactionsStart);
            FitBattlefield();
        }

        void FitBattlefield()
        {
            if (!canvasRect || !battlefieldRect) return;
            Vector2 size = canvasRect.rect.size;
            float cover = Mathf.Max(size.x / 1920f, size.y / 1080f) * 1.012f;
            battlefieldRect.localScale = Vector3.one * cover;
        }

#if UNITY_EDITOR
        // Deterministic authoring previews; runtime always begins with DF on black.
        public void PreviewAt(float seconds)
        {
            preview = true;
            Apply(seconds);
            SetInteraction(false);
        }
#endif
    }
}
