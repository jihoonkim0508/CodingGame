using System;
using System.Linq;
using CodingGame.Art.StageSelection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class StageMapHUD : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        public static readonly Color LockColor = new Color(1f, .64f, .19f);
        public static readonly Color LockBackground = new Color(.065f,.095f,.14f); // opaque: 3% of the orange rim under it reads brown in linear blending
        public static readonly Color OpenColor = new Color(1f,.72f,.30f);
        public static readonly Color PurifiedColor = new Color(.35f,1f,.67f);
        static readonly Color PanelColor = new Color(.025f, .055f, .09f, .96f);
        // One island's card, lock badge and purified seal, saved in Stage.unity (13차 125) and matched to its node by stage.
        [Serializable] public sealed class IslandCard
        {
            public int stage;
            public RectTransform root, badge, purifiedSeal;
            public CanvasGroup badgeGroup;
            public CanvasGroup purifiedGroup;
            [NonSerialized] public float purifiedShownAt;
            public Image border, glow, body; // glow: the lock badge's soft glow; body: the lock icon
            public TMP_Text title, number, state, goal; // goal: what to do there, under the state
            public Button button;
        }
        // Built by Build() in the editor (Tools/Optimize/125 Stage Map) and saved with the scene: no UI is made at load.
        [SerializeField] Canvas canvas;
        [SerializeField] IslandCard[] cards = Array.Empty<IslandCard>();
        [SerializeField] RectTransform hint;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] GameObject devLine, guideDialogue;
        // Kenney's lock, closed and open (Board Game Icons, CC0: ThirdParty/Kenney/BoardGameIcons/License.txt), on a rounded
        // badge (ui_fill) with a soft glow (ui_glow); set by Tools/Learning UI/Stage Lock Icon.
        [SerializeField] Sprite lockIcon, lockOpenIcon, lockPlate, lockGlow;
        public GameObject GuideDialogue => guideDialogue;
        public bool Baked => canvas && cards.Length > 0 && cards.All(card => card.goal) && hint && hintLabel;
        RectTransform canvasRect;
        Camera mapCamera;
        StageCameraController cameraMotion;
        StageNode[] nodes;
        StageNode recommended, unlockTarget;
        bool cinematic, entering;
        bool HideSelectionUI => cinematic || entering || (cameraMotion && cameraMotion.IsMoving);
        // The pointer ("<title> · 여기야!" over a down arrow) shows over the recommended island when this is true.
        public bool IsHintVisible => recommended && !HideSelectionUI;
        public CanvasScaler Scaler { get; private set; }

        public void Configure(StageNode[] stageNodes, Camera camera, StageCameraController motion, Action<StageNode> select)
        {
            nodes = stageNodes; mapCamera = camera; cameraMotion = motion;
            if (!Baked || nodes.Any(node => cards.All(card => card.stage != node.Number))) Build(nodes.Select(Island).ToArray());
            // The cards in node order (the scene saves them by stage number).
            cards = nodes.Select(node => cards.First(card => card.stage == node.Number)).ToArray();
            canvasRect = (RectTransform)canvas.transform;
            Scaler = canvas.GetComponent<CanvasScaler>();
            for (int i = 0; i < nodes.Length; i++) { var node = nodes[i]; cards[i].button.onClick.AddListener(() => select(node)); }
            devLine.SetActive(GameFlow.Developer); // the Z/Q line, developer mode only
        }

        // The whole map UI, once: in the editor to save it with Stage.unity, or at load if a scene has none.
        public static (int number, string title) Island(StageNode node) =>
            (node.Number, node.GetComponent<StageIslandView>() is StageIslandView view && view ? view.stageTitle : $"스테이지 {node.Number}");
        public void Build((int number, string title)[] islands)
        {
            if (canvas) DestroyImmediate(canvas.gameObject);
            var root = new GameObject("StageMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            canvasRect = (RectTransform)root.transform;
            Scaler = root.GetComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Scaler.referenceResolution = ReferenceResolution;
            Scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Scaler.matchWidthOrHeight = .5f;

            cards = new IslandCard[islands.Length];
            for (int i = 0; i < islands.Length; i++)
            {
                var (number, title) = islands[i];
                var card = cards[i] = new IslandCard { stage = number };
                card.root = Panel("Stage " + number + " card", canvasRect, new Vector2(400,CardHeight), PanelColor);
                card.root.anchorMin = card.root.anchorMax = new Vector2(.5f,.5f); card.root.pivot = new Vector2(.5f,1);
                card.button = card.root.gameObject.AddComponent<Button>();
                card.button.targetGraphic = card.root.GetComponent<Image>();
                card.root.GetComponent<Image>().raycastTarget = true;
                card.border = Image("Status accent", card.root, new Vector2(0,-2), new Vector2(400,2), LockColor);
                card.number = Text("Stage number", card.root, $"STAGE {number:00}", 19, LockColor, new Vector2(0,-10), new Vector2(400,25), true);
                card.title = Text("Island title", card.root, title, 27, Color.white, new Vector2(0,-39), new Vector2(400,36), true);
                card.state = Text("Availability", card.root, "", 28, LockColor, new Vector2(0,-82), new Vector2(400,38), true);
                card.goal = Text("Unlock condition", card.root, "", 24, GoalColor, new Vector2(0,-124), new Vector2(400,32), true);
                BuildLock(card, number);
                BuildPurifiedSeal(card, number);
            }
            BuildHint();
            var dev = Text("DEV keys", canvasRect, "DEV  Z: 임시 클리어    Q: 초기화", 18, new Color(1, 1, 1, .6f), Vector2.zero, new Vector2(420, 28), false);
            dev.rectTransform.anchorMin = dev.rectTransform.anchorMax = dev.rectTransform.pivot = Vector2.zero;
            dev.rectTransform.anchoredPosition = new Vector2(96, 10); // right of the "Dev" mark (GameFlow) in the corner dev.fontStyle = FontStyles.Normal;
            devLine = dev.gameObject;
            var guide = Resources.Load<GameObject>(StageMapController.GuideDialogue);
            if (guide && !guideDialogue) { guideDialogue = Instantiate(guide, transform); guideDialogue.name = "GuideDialogue"; }
        }

        // "<title> · 여기야!" on a Primary pill over a bouncing down arrow, as the old IMGUI pointer drew it (1080p pixels).
        void BuildHint()
        {
            hint = Panel("Enter hint", canvasRect, new Vector2(400, 111), Color.clear);
            hint.anchorMin = hint.anchorMax = new Vector2(.5f, .5f); hint.pivot = new Vector2(.5f, 0);
            var arrow = Image("Arrow", hint, Vector2.zero, new Vector2(60, 63), UITheme.Primary);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = arrow.rectTransform.pivot = new Vector2(.5f, 0);
            arrow.sprite = Resources.Load<Sprite>("UI/ui_arrow"); arrow.preserveAspect = true;
            var plate = Image("Plate", hint, new Vector2(0, 61), new Vector2(0, 50), UITheme.Primary);
            plate.rectTransform.anchorMin = new Vector2(0, 0); plate.rectTransform.anchorMax = new Vector2(1, 0); plate.rectTransform.pivot = new Vector2(.5f, 0);
            plate.sprite = UITheme.Pill; plate.type = UnityEngine.UI.Image.Type.Sliced;
            hintLabel = Text("Label", plate.rectTransform, "", 22, UITheme.Ink, Vector2.zero, Vector2.zero, true);
            hintLabel.rectTransform.anchorMin = Vector2.zero; hintLabel.rectTransform.anchorMax = Vector2.one;
            hintLabel.rectTransform.offsetMin = new Vector2(0, 5); hintLabel.rectTransform.offsetMax = Vector2.zero;
            if (UITheme.Display) hintLabel.font = UITheme.Display;
            hintLabel.fontStyle = FontStyles.Normal; hintLabel.alignment = TextAlignmentOptions.Center;
            hint.gameObject.SetActive(false);
        }

        void BuildPurifiedSeal(IslandCard card,int number)
        {
            card.purifiedSeal=Panel("Stage "+number+" purified seal",canvasRect,new Vector2(310,190),Color.clear);
            card.purifiedSeal.anchorMin=card.purifiedSeal.anchorMax=new Vector2(.5f,.5f);card.purifiedSeal.pivot=new Vector2(.5f,.5f);
            card.purifiedGroup=card.purifiedSeal.gameObject.AddComponent<CanvasGroup>();
            card.purifiedGroup.blocksRaycasts=false;card.purifiedGroup.interactable=false;
            var emblem=new GameObject("Purification shield and light",typeof(RectTransform),typeof(CanvasRenderer),typeof(StageMapSymbolGraphic));
            var rect=(RectTransform)emblem.transform;rect.SetParent(card.purifiedSeal,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(0,-60);rect.sizeDelta=new Vector2(180,160);
            emblem.GetComponent<StageMapSymbolGraphic>().raycastTarget=false;
            Image("Purified title backing",card.purifiedSeal,new Vector2(0,-120),new Vector2(286,47),new Color(.025f,.16f,.12f,.94f));
            Text("Purified seal text",card.purifiedSeal,"정화 완료",38,new Color(.76f,1,.87f),new Vector2(0,-119),new Vector2(286,48),true);
            var caption=Text("Purified seal caption",card.purifiedSeal,"PURIFIED",15,new Color(.53f,.9f,.73f),new Vector2(0,-172),new Vector2(280,22),true);caption.characterSpacing=5;
        }

        const float CardHeight = 170;
        static readonly Color GoalColor = new Color(.8f,.9f,.96f), LockedGoalColor = new Color(1,.94f,.76f);
        Vector2 CardPosition(StageNode node,Vector2 size,float width)
        {
            Vector2 p=Project(node.transform.position+new Vector3(0,-.8f,-3.1f));
            return new Vector2(Mathf.Clamp(p.x,-size.x*.5f+width*.5f+12,size.x*.5f-width*.5f-12),Mathf.Clamp(p.y+18,-size.y*.5f+48+CardHeight,size.y*.5f-165));
        }

        void BuildLock(IslandCard card, int number)
        {
            card.badge = Panel("Stage " + number + " lock", canvasRect, new Vector2(160,160), LockColor);
            card.badge.anchorMin = card.badge.anchorMax = new Vector2(.5f,.5f); card.badge.pivot = new Vector2(.5f,.5f);
            card.badgeGroup = card.badge.gameObject.AddComponent<CanvasGroup>();
            card.badgeGroup.blocksRaycasts = false; card.badgeGroup.interactable = false;
            card.glow = card.badge.GetComponent<Image>(); card.glow.sprite = lockGlow;
            // A 6 px orange rim round the navy plate: ui_fill is sliced with a 16 px radius, scaled to each corner radius.
            foreach (var (label, size, radius, color) in new[] { ("Badge rim", 112f, 30f, LockColor), ("Badge", 100f, 24f, LockBackground) })
            {
                var plate = Image(label, card.badge, new Vector2(0,-(160-size)/2), new Vector2(size,size), color);
                plate.sprite = lockPlate; plate.type = UnityEngine.UI.Image.Type.Sliced; plate.pixelsPerUnitMultiplier = 16f/radius;
            }
            card.body = Image("Lock icon", card.badge, new Vector2(0,-47), new Vector2(62,62), LockColor);
            card.body.sprite = lockIcon; card.body.preserveAspect = true;
        }
#if UNITY_EDITOR
        // The saved badges built again with the lock sprites (the rest of the saved map UI stays as it is).
        public void RebuildLocks(Sprite closed, Sprite open, Sprite plate, Sprite glow)
        {
            lockIcon = closed; lockOpenIcon = open; lockPlate = plate; lockGlow = glow; canvasRect = (RectTransform)canvas.transform;
            foreach (var card in cards)
            {
                int order = card.badge ? card.badge.GetSiblingIndex() : canvasRect.childCount;
                if (card.badge) DestroyImmediate(card.badge.gameObject);
                BuildLock(card, card.stage);
                card.badge.SetSiblingIndex(order);
            }
        }
        public bool LockIconBaked => lockIcon && lockOpenIcon && lockPlate && lockGlow
            && cards.All(card => card.body && card.body.sprite == lockIcon && card.glow && card.glow.sprite == lockGlow);
#endif

        public void Refresh(StageNode next)
        {
            recommended = next;
            if (next)
            {
                hintLabel.text = $"{StageProgress.Title(next.Number)} · 여기야!";
                hint.sizeDelta = new Vector2(hintLabel.GetPreferredValues(hintLabel.text).x + 48, hint.sizeDelta.y);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                bool locked = node.State == StageState.Locked;
                Color tint = locked ? LockColor : node.State == StageState.Cleared ? PurifiedColor : OpenColor;
                card.border.color = card.number.color = card.state.color = tint;
                card.state.text = locked ? "봉인 중" : node.State == StageState.Cleared ? "정화 완료" : "도전 가능";
                card.state.fontSize=node.State==StageState.Cleared?32:28;
                card.goal.text = locked ? $"{node.Number-1}스테이지 정화 후 개방" : node.State == StageState.Cleared ? "오류 제거 완료 · 다시 도전" : "도전해서 오류를 정화하세요";
                card.goal.color = locked ? LockedGoalColor : GoalColor;
            }
        }

        public void SetCinematic(bool active, StageNode target = null)
        {
            cinematic=active;unlockTarget=target;
        }
        public void SetEntering()
        {
            entering=true;
        }

        void LateUpdate()
        {
            if (!mapCamera || canvasRect == null) return;
            bool quiet=HideSelectionUI;
            var size = canvasRect.rect.size;
            // The pointer over the recommended island, bouncing.
            Vector3 top = recommended ? mapCamera.WorldToScreenPoint(recommended.VisualBounds.center + Vector3.up * recommended.VisualBounds.extents.y) : Vector3.zero;
            bool pointing = IsHintVisible && top.z > 0;
            if (hint.gameObject.activeSelf != pointing) hint.gameObject.SetActive(pointing);
            if (pointing)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, top, null, out var at);
                hint.anchoredPosition = at + new Vector2(0, 15 + 12 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4)) * 16);
                // Hovering the island the hint points at swells the hint by a third (it grows up from the arrow's tip).
                float lift = recommended.TryGetComponent<StageIslandView>(out var island) ? island.HoverLift : 0;
                hint.localScale = Vector3.one * (1 + .35f * lift);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                card.root.gameObject.SetActive(!quiet);
                card.button.interactable = !quiet;
                float width = Mathf.Min(400, size.x * .28f);
                card.root.sizeDelta = new Vector2(width,CardHeight);
                // Keep text and accents inside the card when the aspect ratio narrows.
                SetWidth(card.title,width); SetWidth(card.number,width); SetWidth(card.state,width); SetWidth(card.goal,width);
                card.border.rectTransform.sizeDelta = new Vector2(width,2);
                card.root.anchoredPosition = CardPosition(node,size,width);
                bool showLock = !entering && (node.State == StageState.Locked || node.LockVisual.IsLocked || node.LockVisual.IsReleasing);
                card.badge.gameObject.SetActive(showLock && (!cinematic || node == unlockTarget));
                float release = node.LockVisual.ReleaseProgress;
                float reaction = node.LockVisual.Rejection;
                var bounds = node.VisualBounds;
                var badgeAt = bounds.center + Vector3.up * bounds.extents.y * .3f;
                // Across, the badge centres on the island and its lock barrier together (the barrier sticks out on one side).
                if (node.LockVisual.WallBounds is Bounds wall) { var both = bounds; both.Encapsulate(wall); badgeAt.x = both.center.x; }
                Vector2 badgePosition = Project(badgeAt);
                badgePosition.x += Mathf.Sin(Time.unscaledTime*65)*reaction*7;
                badgePosition.y += release*45;
                card.badge.anchoredPosition = badgePosition;
                card.badge.localScale = Vector3.one * ((cinematic && node==unlockTarget?1.75f:1)*(1 + Mathf.Sin(release*Mathf.PI)*.16f));
                card.badgeGroup.alpha = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,release));
                card.body.rectTransform.localRotation = Quaternion.Euler(0,0,-Mathf.Sin(release*Mathf.PI)*14); // a shake as it opens
                Color lockTint = Color.Lerp(LockColor,new Color(1,.74f,.32f),.15f+.1f*Mathf.Sin(Time.unscaledTime*2));
                if (node.LockVisual.IsReleasing) lockTint = Color.Lerp(lockTint,OpenColor,release);
                card.body.color = lockTint;
                card.body.sprite = release > .2f ? lockOpenIcon : lockIcon; // it springs open as the shake starts
                card.glow.color = new Color(lockTint.r,lockTint.g,lockTint.b,.4f+.15f*Mathf.Sin(Time.unscaledTime*2)+release*.4f);
                var island=node.GetComponent<StageIslandView>();
                bool purified=node.State==StageState.Cleared && (!island || island.Purification>.88f);
                bool showPurified=purified&&!entering&&cinematic&&node==unlockTarget;
                if(showPurified&&!card.purifiedSeal.gameObject.activeSelf)card.purifiedShownAt=Time.unscaledTime;
                card.purifiedSeal.gameObject.SetActive(showPurified);
                float age=Mathf.Max(0,Time.unscaledTime-card.purifiedShownAt);
                float reveal=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.3f));
                card.purifiedGroup.alpha=reveal;
                Vector2 sealPosition=new Vector2(0,-size.y*.5f+180);
                sealPosition.y+=(1-reveal)*18;
                sealPosition.y=Mathf.Clamp(sealPosition.y,-size.y*.5f+220,size.y*.5f-190);
                card.purifiedSeal.anchoredPosition=sealPosition;
                card.purifiedSeal.localScale=Vector3.one*((cinematic?1.25f:1)*(1+.13f*Mathf.Exp(-age*7)*Mathf.Sin(age*15)));
            }
        }

        Vector2 Project(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,mapCamera.WorldToScreenPoint(world),null,out var point);
            return point;
        }

        static void SetWidth(TMP_Text text,float width)
        {
            if (!Mathf.Approximately(text.rectTransform.sizeDelta.x,width))
                text.rectTransform.sizeDelta = new Vector2(width,text.rectTransform.sizeDelta.y);
        }

        public Rect CardScreenRect(int index)
        {
            var corners = new Vector3[4]; cards[index].root.GetWorldCorners(corners);
            return new Rect(corners[0].x,Screen.height-corners[1].y,corners[2].x-corners[0].x,corners[1].y-corners[0].y);
        }

        static RectTransform Panel(string label, Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject(label,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent,false); rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }
        static Image Image(string label, Transform parent, Vector2 position, Vector2 size, Color color, bool centered = true)
        {
            var rect = Panel(label,parent,size,color);
            rect.anchorMin = rect.anchorMax = new Vector2(centered ? .5f : 0,1);
            rect.pivot = new Vector2(centered ? .5f : 0,1); rect.anchoredPosition = position;
            return rect.GetComponent<Image>();
        }
        static TMP_Text Text(string label, Transform parent, string value, float fontSize, Color color, Vector2 position, Vector2 size, bool center)
        {
            var go = new GameObject(label,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(parent,false); rect.sizeDelta = size;
            rect.anchorMin = rect.anchorMax = new Vector2(center ? .5f : 0,1); rect.pivot = new Vector2(center ? .5f : 0,1); rect.anchoredPosition = position;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
            text.text = value; text.fontSize = fontSize; text.color = color; text.raycastTarget = false;
            text.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.fontStyle = FontStyles.Bold;
            return text;
        }
    }
}
