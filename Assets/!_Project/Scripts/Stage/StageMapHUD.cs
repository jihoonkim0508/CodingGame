using System;
using CodingGame.Art.StageSelection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodingGame.StageMap
{
    [DisallowMultipleComponent]
    public sealed class StageMapHUD : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        public static readonly Color LockColor = new Color(1f, .27f, .09f);
        public static readonly Color LockBackground = new Color(.88f,.93f,.98f,.98f);
        public static readonly Color OpenColor = new Color(1f,.72f,.30f);
        public static readonly Color PurifiedColor = new Color(.35f,1f,.67f);
        static readonly Color PanelColor = new Color(.025f, .055f, .09f, .96f);
        sealed class IslandCard
        {
            public RectTransform root, badge, shackle, purifiedSeal;
            public CanvasGroup badgeGroup;
            public Image border, glow, body, leftShackle, rightShackle, topShackle;
            public TMP_Text title, number, state, condition;
            public Button button;
        }
        Canvas canvas;
        RectTransform canvasRect;
        Camera mapCamera;
        StageNode[] nodes;
        IslandCard[] cards;
        TMP_Text guide, toast, hint;
        RectTransform toastPanel, hintRect;
        StageNode recommended, unlockTarget;
        bool cinematic, entering;
        float toastUntil;
        public CanvasScaler Scaler { get; private set; }

        public void Configure(StageNode[] stageNodes, Camera camera, Action<StageNode> select)
        {
            nodes = stageNodes; mapCamera = camera;
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

            var guidePanel = Panel("Robot guide", canvasRect, new Vector2(760, 112), PanelColor);
            guidePanel.anchorMin = guidePanel.anchorMax = new Vector2(0, 1);
            guidePanel.pivot = new Vector2(0, 1); guidePanel.anchoredPosition = new Vector2(30, -30);
            Text("Guide title", guidePanel, "도우미 로봇", 28, new Color(.35f,.94f,1), new Vector2(22,-22), new Vector2(700,34), false);
            guide = Text("Guide message", guidePanel, "", 25, Color.white, new Vector2(22,-62), new Vector2(710,34), false);

            toastPanel = Panel("Map notification", canvasRect, new Vector2(880, 68), new Color(.06f,.10f,.15f,.98f));
            toastPanel.anchorMin = toastPanel.anchorMax = new Vector2(.5f,0);
            toastPanel.pivot = new Vector2(.5f,0); toastPanel.anchoredPosition = new Vector2(0,55);
            toast = Text("Notification text", toastPanel, "", 28, LockColor, Vector2.zero, toastPanel.sizeDelta, true);
            toastPanel.gameObject.SetActive(false);
            hint = Text("Recommended island", canvasRect, "", 29, new Color(1,.90f,.38f), Vector2.zero, new Vector2(540,80), true);
            hintRect = hint.rectTransform; hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f,.5f); hintRect.pivot = new Vector2(.5f,.5f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var dev = Text("Development shortcuts", canvasRect, "DEV  Z: 임시 클리어    Q: 초기화", 24, new Color(.62f,.74f,.84f), Vector2.zero, new Vector2(650,40), false);
            dev.rectTransform.anchorMin = dev.rectTransform.anchorMax = Vector2.zero;
            dev.rectTransform.pivot = Vector2.zero; dev.rectTransform.anchoredPosition = new Vector2(30,12);
#endif
            cards = new IslandCard[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var card = cards[i] = new IslandCard();
                card.root = Panel("Stage " + node.Number + " card", canvasRect, new Vector2(440,178), PanelColor);
                card.root.anchorMin = card.root.anchorMax = new Vector2(.5f,.5f); card.root.pivot = new Vector2(.5f,1);
                card.button = card.root.gameObject.AddComponent<Button>();
                card.button.targetGraphic = card.root.GetComponent<Image>();
                card.root.GetComponent<Image>().raycastTarget = true;
                card.button.onClick.AddListener(() => select(node));
                card.border = Image("Status accent", card.root, new Vector2(0,-2), new Vector2(440,3), LockColor);
                card.number = Text("Stage number", card.root, $"STAGE {node.Number:00}", 19, LockColor, new Vector2(0,-12), new Vector2(440,25), true);
                var island = node.GetComponent<StageIslandView>();
                card.title = Text("Island title", card.root, island ? island.stageTitle : $"스테이지 {node.Number}", 27, Color.white, new Vector2(0,-41), new Vector2(440,36), true);
                card.state = Text("Availability", card.root, "", 33, LockColor, new Vector2(0,-84), new Vector2(440,43), true);
                card.condition = Text("Unlock condition", card.root, "", 24, new Color(1,.94f,.76f), new Vector2(0,-132), new Vector2(440,32), true);
                BuildLock(card, node.Number);
                card.purifiedSeal=Panel("Stage "+node.Number+" purified seal",canvasRect,new Vector2(190,58),new Color(.83f,1,.91f,.97f));
                card.purifiedSeal.anchorMin=card.purifiedSeal.anchorMax=new Vector2(.5f,.5f);card.purifiedSeal.pivot=new Vector2(.5f,.5f);
                Text("Purified seal text",card.purifiedSeal,"정화 완료",27,new Color(.025f,.39f,.23f),new Vector2(20,-10),new Vector2(145,38),true);
                var check=Image("Purified check short",card.purifiedSeal,new Vector2(21,-30),new Vector2(17,6),new Color(.04f,.6f,.32f),false);
                check.rectTransform.localRotation=Quaternion.Euler(0,0,-42);
                check=Image("Purified check long",card.purifiedSeal,new Vector2(31,-39),new Vector2(32,6),new Color(.04f,.6f,.32f),false);
                check.rectTransform.localRotation=Quaternion.Euler(0,0,48);
            }
        }

        void BuildLock(IslandCard card, int number)
        {
            card.badge = Panel("Stage " + number + " lock", canvasRect, new Vector2(104,116), LockBackground);
            card.badge.anchorMin = card.badge.anchorMax = new Vector2(.5f,.5f); card.badge.pivot = new Vector2(.5f,.5f);
            card.badgeGroup = card.badge.gameObject.AddComponent<CanvasGroup>();
            card.badgeGroup.blocksRaycasts = false; card.badgeGroup.interactable = false;
            card.glow = Image("Orange lock outline", card.badge, new Vector2(0,-2), new Vector2(104,4), LockColor);
            card.body = Image("Red orange lock body", card.badge, new Vector2(24,-55), new Vector2(56,43), LockColor, false);
            Image("Keyhole", card.body.rectTransform, new Vector2(25,-11), new Vector2(6,20), new Color(.3f,.08f,.025f), false);
            card.shackle = Panel("Opening shackle", card.badge, new Vector2(38,39), Color.clear);
            card.shackle.anchorMin = card.shackle.anchorMax = new Vector2(0,1); card.shackle.pivot = new Vector2(1,0);
            card.shackle.anchoredPosition = new Vector2(71,-59);
            card.leftShackle = Image("Shackle left", card.shackle, Vector2.zero, new Vector2(8,39), LockColor, false);
            card.topShackle = Image("Shackle top", card.shackle, Vector2.zero, new Vector2(38,8), LockColor, false);
            card.rightShackle = Image("Shackle right", card.shackle, new Vector2(30,0), new Vector2(8,39), LockColor, false);
        }

        public void Refresh(StageNode next, string message)
        {
            recommended = next; guide.text = message;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                bool locked = node.State == StageState.Locked;
                var island = node.GetComponent<StageIslandView>();
                Color tint = locked ? new Color(1,.49f,.28f) : node.State == StageState.Cleared ? PurifiedColor : OpenColor;
                card.border.color = card.number.color = card.state.color = tint;
                card.state.text = locked ? "미정화 · 봉인 중" : node.State == StageState.Cleared ? "정화 완료" : "개방 · 미정화";
                card.condition.text = locked ? $"{node.Number-1}스테이지 정화 후 개방" : node.State == StageState.Cleared ? "오류 제거 완료 · 다시 도전" : "도전해서 오류를 정화하세요";
                card.condition.color = locked ? new Color(1,.94f,.76f) : new Color(.8f,.9f,.96f);
            }
        }

        public void SetCinematic(bool active, StageNode target = null) { cinematic = active; unlockTarget = target; }
        public void SetEntering() { entering = true; hint.gameObject.SetActive(false); guide.text = "선택한 스테이지로 이동하고 있어!"; }
        public void ShowToast(string message, float seconds = 2.5f, Color? tint = null)
        {
            toast.text = message; toast.color=tint??LockColor; toastUntil = Time.unscaledTime + seconds;
        }

        void LateUpdate()
        {
            if (!mapCamera || cards == null) return;
            toastPanel.gameObject.SetActive(!entering && Time.unscaledTime < toastUntil);
            var size = canvasRect.rect.size;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                card.root.gameObject.SetActive(!entering && !cinematic);
                card.button.interactable = !entering && !cinematic;
                Vector2 p = Project(node.transform.position + new Vector3(0,-.8f,-3.1f));
                float width = Mathf.Min(440, size.x * .29f);
                card.root.sizeDelta = new Vector2(width,178);
                // Keep text and accents inside the card when the aspect ratio narrows.
                SetWidth(card.title,width); SetWidth(card.number,width); SetWidth(card.state,width); SetWidth(card.condition,width);
                card.border.rectTransform.sizeDelta = new Vector2(width,3);
                card.root.anchoredPosition = new Vector2(Mathf.Clamp(p.x,-size.x*.5f+width*.5f+12,size.x*.5f-width*.5f-12),
                    Mathf.Clamp(p.y+18,-size.y*.5f+250,size.y*.5f-165));
                bool showLock = !entering && (node.State == StageState.Locked || node.LockVisual.IsLocked || node.LockVisual.IsReleasing);
                card.badge.gameObject.SetActive(showLock && (!cinematic || node == unlockTarget));
                float release = node.LockVisual.ReleaseProgress;
                float reaction = node.LockVisual.Rejection;
                var bounds = node.VisualBounds;
                Vector2 badgePosition = Project(bounds.center + Vector3.up * bounds.extents.y * .3f);
                badgePosition.x += Mathf.Sin(Time.unscaledTime*65)*reaction*7;
                badgePosition.y += release*45;
                card.badge.anchoredPosition = badgePosition;
                card.badge.localScale = Vector3.one * ((cinematic && node==unlockTarget?1.75f:1)*(1 + Mathf.Sin(release*Mathf.PI)*.16f));
                card.badgeGroup.alpha = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,release));
                card.shackle.localRotation = Quaternion.Euler(0,0,-Mathf.SmoothStep(0,1,release)*38);
                Color lockTint = Color.Lerp(LockColor,new Color(1,.39f,.12f),.15f+.1f*Mathf.Sin(Time.unscaledTime*2));
                if (node.LockVisual.IsReleasing) lockTint = Color.Lerp(lockTint,OpenColor,release);
                card.body.color = card.leftShackle.color = card.rightShackle.color = card.topShackle.color = lockTint;
                var island=node.GetComponent<StageIslandView>();
                bool purified=node.State==StageState.Cleared && (!island || island.Purification>.88f);
                card.purifiedSeal.gameObject.SetActive(purified&&!entering&&(!cinematic||node==unlockTarget));
                card.purifiedSeal.anchoredPosition=Project(bounds.center+Vector3.up*bounds.extents.y*.45f);
            }
            bool showHint = recommended && !cinematic && !entering;
            hint.gameObject.SetActive(showHint);
            if (showHint)
            {
                var bounds = recommended.VisualBounds;
                var p = Project(bounds.center + Vector3.up*bounds.extents.y);
                hintRect.anchoredPosition = p + Vector2.up*(115 + Mathf.Abs(Mathf.Sin(Time.unscaledTime*3))*18);
                hint.text = $"{recommended.Number}스테이지 · 여기를 클릭!\n▼";
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
