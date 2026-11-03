using System;
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
        public static readonly Color LockBackground = new Color(.065f,.095f,.14f,.97f);
        public static readonly Color OpenColor = new Color(1f,.72f,.30f);
        public static readonly Color PurifiedColor = new Color(.35f,1f,.67f);
        static readonly Color PanelColor = new Color(.025f, .055f, .09f, .96f);
        sealed class IslandCard
        {
            public RectTransform root, badge, shackle, purifiedSeal;
            public CanvasGroup badgeGroup;
            public CanvasGroup purifiedGroup;
            public float purifiedShownAt;
            public Image border, glow, body, leftShackle, rightShackle, topShackle;
            public TMP_Text title, number, state;
            public Button button;
        }
        Canvas canvas;
        RectTransform canvasRect;
        Camera mapCamera;
        StageCameraController cameraMotion;
        StageNode[] nodes;
        IslandCard[] cards;
        TMP_Text hint;
        public bool IsHintVisible => hint && hint.gameObject.activeSelf;
        RectTransform hintRect;
        StageNode recommended, unlockTarget;
        bool cinematic, entering;
        bool HideSelectionUI => cinematic || entering || (cameraMotion && cameraMotion.IsMoving);
        public CanvasScaler Scaler { get; private set; }

        public void Configure(StageNode[] stageNodes, Camera camera, Action<StageNode> select)
        {
            nodes = stageNodes; mapCamera = camera;
            cameraMotion = GetComponent<StageCameraController>();
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

            hint = Text("Recommended island", canvasRect, "클릭!\n▼", 34, OpenColor, Vector2.zero, new Vector2(150,100), true);
            hintRect = hint.rectTransform; hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f,.5f); hintRect.pivot = new Vector2(.5f,.5f);
            cards = new IslandCard[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var card = cards[i] = new IslandCard();
                card.root = Panel("Stage " + node.Number + " card", canvasRect, new Vector2(400,132), PanelColor);
                card.root.anchorMin = card.root.anchorMax = new Vector2(.5f,.5f); card.root.pivot = new Vector2(.5f,1);
                card.button = card.root.gameObject.AddComponent<Button>();
                card.button.targetGraphic = card.root.GetComponent<Image>();
                card.root.GetComponent<Image>().raycastTarget = true;
                card.button.onClick.AddListener(() => select(node));
                card.border = Image("Status accent", card.root, new Vector2(0,-2), new Vector2(400,2), LockColor);
                card.number = Text("Stage number", card.root, $"STAGE {node.Number:00}", 19, LockColor, new Vector2(0,-10), new Vector2(400,25), true);
                var island = node.GetComponent<StageIslandView>();
                card.title = Text("Island title", card.root, island ? island.stageTitle : $"스테이지 {node.Number}", 27, Color.white, new Vector2(0,-39), new Vector2(400,36), true);
                card.state = Text("Availability", card.root, "", 28, LockColor, new Vector2(0,-82), new Vector2(400,38), true);
                BuildLock(card, node.Number);
                BuildPurifiedSeal(card,node.Number);
            }
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

        Vector2 CardPosition(StageNode node,Vector2 size,float width)
        {
            Vector2 p=Project(node.transform.position+new Vector3(0,-.8f,-3.1f));
            return new Vector2(Mathf.Clamp(p.x,-size.x*.5f+width*.5f+12,size.x*.5f-width*.5f-12),Mathf.Clamp(p.y+18,-size.y*.5f+180,size.y*.5f-165));
        }

        void BuildLock(IslandCard card, int number)
        {
            card.badge = Panel("Stage " + number + " lock", canvasRect, new Vector2(104,116), LockBackground);
            card.badge.anchorMin = card.badge.anchorMax = new Vector2(.5f,.5f); card.badge.pivot = new Vector2(.5f,.5f);
            card.badgeGroup = card.badge.gameObject.AddComponent<CanvasGroup>();
            card.badgeGroup.blocksRaycasts = false; card.badgeGroup.interactable = false;
            card.glow = Image("Orange lock outline", card.badge, new Vector2(0,-2), new Vector2(104,4), LockColor);
            card.body = Image("Orange lock body", card.badge, new Vector2(24,-55), new Vector2(56,43), LockColor, false);
            Image("Keyhole", card.body.rectTransform, new Vector2(25,-11), new Vector2(6,20), new Color(.3f,.08f,.025f), false);
            card.shackle = Panel("Opening shackle", card.badge, new Vector2(38,39), Color.clear);
            card.shackle.anchorMin = card.shackle.anchorMax = new Vector2(0,1); card.shackle.pivot = new Vector2(1,0);
            card.shackle.anchoredPosition = new Vector2(71,-59);
            card.leftShackle = Image("Shackle left", card.shackle, Vector2.zero, new Vector2(8,39), LockColor, false);
            card.topShackle = Image("Shackle top", card.shackle, Vector2.zero, new Vector2(38,8), LockColor, false);
            card.rightShackle = Image("Shackle right", card.shackle, new Vector2(30,0), new Vector2(8,39), LockColor, false);
        }

        public void Refresh(StageNode next)
        {
            recommended = next;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                bool locked = node.State == StageState.Locked;
                Color tint = locked ? LockColor : node.State == StageState.Cleared ? PurifiedColor : OpenColor;
                card.border.color = card.number.color = card.state.color = tint;
                card.state.text = locked ? "봉인 중" : node.State == StageState.Cleared ? "정화 완료" : "도전 가능";
                card.state.fontSize=node.State==StageState.Cleared?32:28;
            }
        }

        public void SetCinematic(bool active, StageNode target = null)
        {
            cinematic=active;unlockTarget=target;
            if(active)hint.gameObject.SetActive(false);
        }
        public void SetEntering()
        {
            entering=true;hint.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!mapCamera || cards == null) return;
            bool quiet=HideSelectionUI;
            var size = canvasRect.rect.size;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i]; var card = cards[i];
                card.root.gameObject.SetActive(!quiet);
                card.button.interactable = !quiet;
                float width = Mathf.Min(400, size.x * .28f);
                card.root.sizeDelta = new Vector2(width,132);
                // Keep text and accents inside the card when the aspect ratio narrows.
                SetWidth(card.title,width); SetWidth(card.number,width); SetWidth(card.state,width);
                card.border.rectTransform.sizeDelta = new Vector2(width,2);
                card.root.anchoredPosition = CardPosition(node,size,width);
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
                Color lockTint = Color.Lerp(LockColor,new Color(1,.74f,.32f),.15f+.1f*Mathf.Sin(Time.unscaledTime*2));
                if (node.LockVisual.IsReleasing) lockTint = Color.Lerp(lockTint,OpenColor,release);
                card.body.color = card.leftShackle.color = card.rightShackle.color = card.topShackle.color = lockTint;
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
            bool showHint = recommended && !quiet;
            hint.gameObject.SetActive(showHint);
            if (showHint)
            {
                var bounds = recommended.VisualBounds;
                var p = Project(bounds.center + Vector3.up*bounds.extents.y);
                hintRect.anchoredPosition = p + Vector2.up*(72 + Mathf.Abs(Mathf.Sin(Time.unscaledTime*3))*10);
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
