using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CodingGame.Art.MapPainter.Editor
{
    public sealed class ModularMapPainterWindow : EditorWindow
    {
        [SerializeField] private ModularMapRoot map;
        [SerializeField] private bool painting;
        [SerializeField] private int layer;
        [SerializeField] private int tool;
        [SerializeField] private int brush = 1;
        [SerializeField] private bool rectangle;
        [SerializeField] private bool gridVisible = true;
        [SerializeField] private GameObject savedMap;
        [SerializeField] private string prefabName = "ModularMap";
        // 0 off, 1 place base, 2 add start, 3 draw the selected start's path.
        [SerializeField] private int marker;
        [SerializeField] private int selectedRoute = -1;
        private Vector2 scroll;
        private GridCell hover, start, last;
        private bool hasHover, dragging, eraseStroke;
        private int undoGroup = -1;
        private int control;
        [MenuItem("Tools/Art/Modular Map Painter")]
        public static ModularMapPainterWindow Open()
        {
            var window = GetWindow<ModularMapPainterWindow>("Map Painter");
            window.minSize = new Vector2(330,400); window.Show(); return window;
        }
        const int MaxCellsPerAction = 4096;
        public void SetMap(ModularMapRoot root) { map = root; Repaint(); }
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += RepaintViews;
            wantsMouseMove = true;
        }
        private void OnDisable()
        {
            MapBuildPreview.Stop();
            FinishStroke(); SceneView.duringSceneGui -= OnSceneGUI; Undo.undoRedoPerformed -= RepaintViews;
        }
        private void RepaintViews() { if(map)map.ApplyFloorColors(); Repaint(); SceneView.RepaintAll(); }
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("모듈 맵 에디터", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("X/Z 격자 · 바닥 윗면 Y 기준", EditorStyles.miniLabel);
            var scene = SceneManager.GetActiveScene();
            bool artScene = scene.path == MapPainterAssets.ArtScene;
            if (!artScene) EditorGUILayout.HelpBox("ArtTestScene을 열어 사용하세요.", MessageType.Info);
            if (!map) map = MapPainterEditing.FindMap(scene);
            map = (ModularMapRoot)EditorGUILayout.ObjectField("작업 맵",map,typeof(ModularMapRoot),true);
            using (new EditorGUI.DisabledScope(!artScene || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                savedMap = (GameObject)EditorGUILayout.ObjectField("불러올 맵 프리팹",savedMap,typeof(GameObject),false);
                using(new EditorGUI.DisabledScope(!savedMap || !savedMap.GetComponent<ModularMapRoot>()))
                    if(GUILayout.Button("프리팹에서 편집용 맵 불러오기"))
                    {
                        RunAction("Load map prefab",()=>
                        {
                            var loaded=(GameObject)PrefabUtility.InstantiatePrefab(savedMap,scene);
                            PrefabUtility.UnpackPrefabInstance(loaded,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                            Undo.RegisterCreatedObjectUndo(loaded,"Load map prefab");
                            map=loaded.GetComponent<ModularMapRoot>(); Selection.activeGameObject=loaded;
                            BlueprintBackdropAssets.Ensure(map,MapPainterEditing.FindCamera(scene));
                        });
                    }
                if (!map && GUILayout.Button("맵 작업 시작"))
                { map = MapPainterEditing.CreateRoot(scene,MapPainterAssets.Setup()); BlueprintBackdropAssets.Ensure(map,MapPainterEditing.FindCamera(scene)); Selection.activeGameObject = map.gameObject; }
            }
            if (!map) { EditorGUILayout.EndScrollView(); return; }
            DrawBuildAnimation();
            bool editable = MapPainterEditing.CanEdit(map);
            if (!editable) EditorGUILayout.HelpBox("ArtTestScene의 맵을 선택하고, 회전 0 / 스케일 1 상태에서 편집하세요. Play Mode에서는 편집하지 않습니다.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!editable||MapBuildPreview.IsRunning))
            {
                DrawMapSize();
                painting = GUILayout.Toggle(painting,painting?"■ 페인트 모드 켜짐":"▶ 페인트 모드 시작","Button");
                if(painting) marker=0;
                if(!painting) FinishStroke();
                layer = GUILayout.Toolbar(layer,new[] { "바닥", "경로" });
                tool = GUILayout.Toolbar(tool,new[] { "칠하기", "지우기" });
                rectangle = EditorGUILayout.Toggle("사각형 영역",rectangle);
                using (new EditorGUI.DisabledScope(rectangle)) brush = EditorGUILayout.IntSlider("브러시 크기",brush,1,8);
                gridVisible = EditorGUILayout.Toggle("격자 표시",gridVisible);
                EditorGUILayout.HelpBox("좌클릭·드래그: 칠하기 / Shift: 지우기\nAlt·우클릭·중클릭: Scene 이동 / Esc: 페인트 종료\nCtrl+Z: 드래그 단위 실행 취소",MessageType.None);
                EditorGUILayout.LabelField($"바닥 {map.floorCells.Count}칸 / 경로 {map.roadCells.Count}칸");
                var roadCells = new HashSet<GridCell>(map.roadCells);
                int missing = roadCells.Count(c => !map.palette.TryRoad(GridTopology.Connections(roadCells,c),out _,out _));
                if (missing > 0) EditorGUILayout.HelpBox($"경로 모델 미등록 {missing}칸: Scene의 주황색 연결선으로 표시합니다. 아래에서 프리팹을 등록하면 배치됩니다.",MessageType.Info);
                if (GUILayout.Button("주변 규칙으로 전체 다시 배치")) RunAction("Rebuild map",()=>MapPainterEditing.RebuildAll(map));
                EditorGUILayout.Space();
                if (GUILayout.Button("Scene 화면을 참고 각도로 맞추기")) MapPainterEditing.FitSceneView(map);
                if (GUILayout.Button("게임 카메라를 맵 크기에 맞추기"))
                {
                    var camera = MapPainterEditing.FindCamera(map.gameObject.scene);
                    if (camera) RunAction("Frame map camera",()=> { MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(map)); EditorSceneManager.MarkSceneDirty(map.gameObject.scene); });
                }
                prefabName = EditorGUILayout.TextField("프리팹 이름",prefabName);
                if (GUILayout.Button("ArtPrefabs에 새 프리팹으로 저장"))
                {
                    FinishStroke();
                    BlueprintBackdropAssets.Ensure(map,MapPainterEditing.FindCamera(scene));
                    MapBuildAssets.Ensure(map);
                    MapPainterAssets.EnsureFolder(MapPainterAssets.MapPrefabs);
                    string safeName = string.Concat(prefabName.Where(c=>!System.IO.Path.GetInvalidFileNameChars().Contains(c))).Trim();
                    if(string.IsNullOrEmpty(safeName)) safeName="ModularMap";
                    string path = AssetDatabase.GenerateUniqueAssetPath(MapPainterAssets.MapPrefabs+"/"+safeName+".prefab");
                    var prefab = PrefabUtility.SaveAsPrefabAsset(map.gameObject,path);
                    EditorGUIUtility.PingObject(prefab);
                }
                DrawRoutes();
                DrawBlueprintBackground();
                DrawFloorColors();
                DrawSurfaceStyle();
                DrawRoadPalette();
            }
            EditorGUILayout.EndScrollView();
        }
        private void DrawBuildAnimation()
        {
            var sequence=map.GetComponent<MapBuildSequence>();
            EditorGUILayout.Space();EditorGUILayout.LabelField("맵 생성 연출",EditorStyles.boldLabel);
            if(!sequence)
            {
                using(new EditorGUI.DisabledScope(!MapPainterEditing.CanEdit(map)))
                    if(GUILayout.Button("생성 연출 추가"))RunAction("Add map build animation",()=>MapBuildAssets.Ensure(map));
                return;
            }
            using(new EditorGUI.DisabledScope(!MapPainterEditing.CanEdit(map)||MapBuildPreview.IsRunning))
            {
                EditorGUI.BeginChangeCheck();
                bool auto=EditorGUILayout.Toggle("스테이지 입장 시 자동 재생",sequence.playOnStart);
                float duration=EditorGUILayout.Slider("전체 시간 (초)",sequence.duration,.5f,6);
                float rise=EditorGUILayout.Slider("바닥 상승 높이",sequence.riseHeight,0,2);
                float glitch=EditorGUILayout.Slider("글리치 강도",sequence.glitch,0,1);
                Color outline=EditorGUILayout.ColorField("윤곽선 색상",sequence.outlineColor);
                if(EditorGUI.EndChangeCheck())RunAction("Change build animation",()=>
                {
                    Undo.RecordObject(sequence,"Change build animation");sequence.playOnStart=auto;sequence.duration=duration;
                    sequence.riseHeight=rise;sequence.glitch=glitch;sequence.outlineColor=outline;
                    EditorUtility.SetDirty(sequence);EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                });
                if(GUILayout.Button("▶ 맵 생성 연출 미리보기")){FinishStroke();painting=false;MapBuildPreview.Start(sequence);}
            }
            if(MapBuildPreview.IsRunning)
            {
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100,18),MapBuildPreview.Progress,"맵 생성 중");
                if(GUILayout.Button("■ 중지 · 원래 맵 복원"))MapBuildPreview.Stop();
                Repaint();
            }
            EditorGUILayout.HelpBox("미리보기는 반복 재생할 수 있습니다. 게임에서는 스테이지 입장 때 재생하고 같은 입장 중의 재도전은 생략합니다.",MessageType.None);
        }
        private void DrawRoutes()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("출발 · 기지 · 이동 경로",EditorStyles.boldLabel);
            int chosen=GUILayout.Toolbar(marker,new[] { "끄기", "기지 지정", "출발 추가", "경로 그리기" });
            if(chosen!=marker) { marker=chosen; if(marker!=0) { painting=false; FinishStroke(); } SceneView.RepaintAll(); }
            if(selectedRoute>=map.routes.Count) selectedRoute=map.routes.Count-1;
            EditorGUILayout.LabelField(map.hasBase?$"기지 ({map.baseCell.x}, {map.baseCell.z})":"기지 미지정");
            for(int i=0;i<map.routes.Count;i++)
            {
                var route=map.routes[i];
                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Toggle(selectedRoute==i,$"S{i+1}","Button",GUILayout.Width(40))&&selectedRoute!=i) { selectedRoute=i; SceneView.RepaintAll(); }
                    EditorGUILayout.LabelField($"({route.start.x}, {route.start.z}) · {route.path.Count}칸 · "+(map.IsComplete(route)?"완료":"미완성"));
                    if(GUILayout.Button("도로 자동",GUILayout.Width(64))&&!MapRouteEditing.AutoRoute(map,route)) ShowNotification(new GUIContent("도로가 출발과 기지를 잇지 않습니다."));
                    if(GUILayout.Button("비우기",GUILayout.Width(48))) MapRouteEditing.ClearPath(map,route);
                    if(GUILayout.Button("삭제",GUILayout.Width(40))) { MapRouteEditing.RemoveRoute(map,i); if(selectedRoute>=i) selectedRoute--; GUIUtility.ExitGUI(); }
                }
            }
            EditorGUILayout.HelpBox("기지·출발: 바닥 칸을 클릭합니다. 출발은 여러 개 둘 수 있습니다.\n경로 그리기: S 버튼으로 출발을 고르고 칸을 클릭·드래그해 기지까지 잇습니다. 도로 위는 도로를 따라 채웁니다.\n경로 위의 칸이나 출발을 다시 클릭하면 그 뒤를 잘라냅니다.",MessageType.None);
            foreach(var problem in MapRouteEditing.Problems(map)) EditorGUILayout.HelpBox(problem,MessageType.Warning);
        }
        // A click or drag cell in the active marker tool. Returns true when the map changed.
        private bool ApplyMarker(GridCell cell,bool dragged)
        {
            switch(marker)
            {
                case 1: return !dragged && MapRouteEditing.SetBase(map,cell);
                case 2:
                    if(dragged) return false;
                    int added=MapRouteEditing.AddStart(map,cell);
                    if(added>=0) selectedRoute=added;
                    return added>=0;
                default:
                    if(selectedRoute<0||selectedRoute>=map.routes.Count) { ShowNotification(new GUIContent("S 버튼으로 출발을 먼저 고르세요.")); return false; }
                    return MapRouteEditing.Extend(map,map.routes[selectedRoute],cell);
            }
        }
        private void DrawMarkers()
        {
            Vector3 Lift(GridCell c)=>map.CellPosition(c)+Vector3.up*(.06f+map.palette.roadLift);
            float radius=map.cellSize*.32f;
            for(int i=0;i<map.routes.Count;i++)
            {
                var route=map.routes[i];
                var color=Color.HSVToRGB((.33f+i*.17f)%1,.75f,1);
                Handles.color=color;
                var line=new[] { route.start }.Concat(route.path).Select(Lift).ToArray();
                if(line.Length>1) Handles.DrawAAPolyLine(i==selectedRoute?8:4,line);
                Handles.DrawSolidDisc(Lift(route.start),Vector3.up,radius);
                Handles.Label(Lift(route.start)+Vector3.up*.3f,$"S{i+1}",EditorStyles.whiteLargeLabel);
            }
            if(!map.hasBase) return;
            Handles.color=new Color(1,.25f,.3f);
            Handles.DrawSolidDisc(Lift(map.baseCell),Vector3.up,radius*1.3f);
            Handles.Label(Lift(map.baseCell)+Vector3.up*.3f,"기지",EditorStyles.whiteLargeLabel);
        }
        private void DrawBlueprintBackground()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("설계도 배경",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled=EditorGUILayout.Toggle("설계도 바닥 표시",map.blueprintBackground);
            Color blue=EditorGUILayout.ColorField("파란 배경",map.blueprintBlue);
            Color lines=EditorGUILayout.ColorField("격자 색상",map.blueprintLines);
            float spacing=EditorGUILayout.FloatField("격자 간격 (월드 단위)",map.blueprintSpacing);
            float width=EditorGUILayout.Slider("선 굵기 (화면 픽셀)",map.blueprintLineWidth,.5f,3);
            float minor=EditorGUILayout.Slider("작은 격자 농도",map.blueprintMinorOpacity,0,1);
            float major=EditorGUILayout.Slider("큰 격자 농도",map.blueprintMajorOpacity,0,1);
            int every=EditorGUILayout.IntSlider("큰 격자 간격 (칸)",map.blueprintMajorEvery,1,10);
            if(EditorGUI.EndChangeCheck())RunAction("Change blueprint background",()=>
            {
                Undo.RecordObject(map,"Change blueprint background");
                var camera=MapPainterEditing.FindCamera(map.gameObject.scene);
                if(camera)Undo.RecordObject(camera,"Change blueprint background");
                map.blueprintBackground=enabled; map.blueprintBlue=blue; map.blueprintLines=lines;
                map.blueprintSpacing=Mathf.Max(.05f,spacing); map.blueprintLineWidth=width;
                map.blueprintMinorOpacity=minor; map.blueprintMajorOpacity=major; map.blueprintMajorEvery=every;
                BlueprintBackdropAssets.Ensure(map,camera);
                EditorUtility.SetDirty(map); EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            });
            EditorGUILayout.HelpBox("맵 아래에 설계도 바닥을 표시합니다. 격자는 조명 영향을 받지 않으며 색상과 간격은 맵 프리팹에 함께 저장됩니다.",MessageType.None);
        }
        private void DrawMapSize()
        {
            EditorGUI.BeginChangeCheck();
            int size=EditorGUILayout.IntSlider("한 칸 크기",map.cellSize,1,8);
            float height=EditorGUILayout.Slider("바닥 두께",map.floorHeight,.1f,3);
            if(EditorGUI.EndChangeCheck())RunAction("Resize map",()=>
            {
                Undo.RecordObject(map,"Resize map"); map.cellSize=size; map.floorHeight=height; map.ApplyFloorColors();
                var camera=MapPainterEditing.FindCamera(map.gameObject.scene); if(camera)MapPainterEditing.FitCamera(camera,MapPainterEditing.MapBounds(map));
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            });
            var bounds=MapPainterEditing.MapBounds(map);
            EditorGUILayout.LabelField($"바닥 크기 {bounds.size.x:0.##} × {bounds.size.z:0.##} · 1칸 {map.cellSize} Unity 단위",EditorStyles.miniLabel);
        }
        private void DrawSurfaceStyle()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("바닥·경로 쉐이더",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            float softness=EditorGUILayout.Slider("부드러운 명암",map.lightSoftness,0,1);
            float shadow=EditorGUILayout.Slider("그림자 농도",map.shadowStrength,0,1);
            float gradient=EditorGUILayout.Slider("몸체 아래쪽 음영",map.bodyGradient,0,1);
            float bevel=EditorGUILayout.Slider("모서리 밝기",map.bevelHighlight,0,1);
            if(EditorGUI.EndChangeCheck())RunAction("Change map shading",()=>
            {
                Undo.RecordObject(map,"Change map shading"); map.lightSoftness=softness; map.shadowStrength=shadow;
                map.bodyGradient=gradient; map.bevelHighlight=bevel; EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            });
        }
        private void DrawFloorColors()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("바닥 색상 · 홈 기준 위 / 아래",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool enabled=EditorGUILayout.Toggle("두 색상 적용",map.useFloorColors);
            Color upper=EditorGUILayout.ColorField(new GUIContent("위쪽 판"),map.upperColor,true,false,false);
            Color lower=EditorGUILayout.ColorField(new GUIContent("아래쪽 몸체"),map.lowerColor,true,false,false);
            if(EditorGUI.EndChangeCheck())RunAction("Change floor colors",()=>
            {
                Undo.RecordObject(map,"Change floor colors"); map.useFloorColors=enabled; map.upperColor=upper; map.lowerColor=lower;
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            });
            EditorGUILayout.HelpBox("홈 가운데를 경계로 두 단색을 적용합니다. 이 맵의 색만 바꾸며, 프리팹 저장 시 함께 보관합니다. 단색에는 UV 수정이 필요 없습니다.",MessageType.None);
            if(GUILayout.Button("원본 FBX 변경 후 바닥 모듈 갱신"))
            {
                FinishStroke(); MapPainterAssets.RefreshFloorModules();
                RunAction("Refresh floor modules",()=>MapPainterEditing.RebuildAll(map));
                RepaintViews();
            }
        }
        private void DrawRoadPalette()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("경로 모델 등록",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool colors=EditorGUILayout.Toggle("경로 두 색상 적용",map.useRoadColors);
            Color inner=EditorGUILayout.ColorField(new GUIContent("안쪽 길"),map.roadInnerColor,true,false,false);
            Color border=EditorGUILayout.ColorField(new GUIContent("테두리·측면"),map.roadBorderColor,true,false,false);
            if(EditorGUI.EndChangeCheck())RunAction("Change road colors",()=>
            {
                Undo.RecordObject(map,"Change road colors"); map.useRoadColors=colors; map.roadInnerColor=inner; map.roadBorderColor=border;
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            });
            if(GUILayout.Button("Map/Road의 경로 모델 갱신"))RunAction("Refresh road models",()=>
            { RoadModuleAssets.Setup(map.palette,true); MapPainterEditing.RebuildAll(map); });
            EditorGUILayout.HelpBox("1칸용 프리팹을 등록하세요. 루트는 칸 중앙의 접촉면(Y=0), 스케일은 1이어야 합니다. 연결 방향은 프리팹의 기본 방향입니다. 회전 보정은 90° 단위입니다.",MessageType.None);
            EditorGUI.BeginChangeCheck();
            var roads = map.palette.roads;
            var prefabs = new GameObject[roads.Count]; var rotations = new int[roads.Count];
            for (int i=0;i<roads.Count;i++)
            {
                prefabs[i]=(GameObject)EditorGUILayout.ObjectField(roads[i].label,roads[i].prefab,typeof(GameObject),false);
                rotations[i]=EditorGUILayout.IntSlider("회전 보정",roads[i].rotationOffset,0,3);
            }
            float lift = EditorGUILayout.FloatField("경로 높이 보정",map.palette.roadLift);
            if (EditorGUI.EndChangeCheck())
            {
                RunAction("Register road modules",()=>
                {
                    Undo.RecordObject(map.palette,"Register road modules");
                    for(int i=0;i<roads.Count;i++) { roads[i].prefab=prefabs[i]; roads[i].rotationOffset=rotations[i]; }
                    map.palette.roadLift=Mathf.Max(0,lift);
                    EditorUtility.SetDirty(map.palette); AssetDatabase.SaveAssetIfDirty(map.palette);
                    MapPainterEditing.RebuildAll(map);
                });
            }
            if (GUILayout.Button("바닥·경로 규칙 에셋 선택")) Selection.activeObject=map.palette;
        }
        private void RunAction(string label,System.Action action)
        {
            MapBuildPreview.Stop();
            FinishStroke(); Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(label);
            action(); Undo.CollapseUndoOperations(group); RepaintViews();
        }
        private static IEnumerable<GridCell> Rectangle(GridCell a,GridCell b)
        {
            for(int z=Mathf.Min(a.z,b.z);z<=Mathf.Max(a.z,b.z);z++)
                for(int x=Mathf.Min(a.x,b.x);x<=Mathf.Max(a.x,b.x);x++) yield return new GridCell(x,z);
        }
        private IEnumerable<GridCell> Brush(GridCell cell)
        {
            int low=-(brush-1)/2, high=brush/2;
            return Rectangle(cell+new GridCell(low,low),cell+new GridCell(high,high));
        }
        private void OnSceneGUI(SceneView view)
        {
            if (!map || !MapPainterEditing.CanEdit(map)||MapBuildPreview.IsRunning) { FinishStroke(); return; }
            Event e=Event.current;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape) { painting=false; marker=0; FinishStroke(); e.Use(); RepaintViews(); }
            bool active=painting && !e.alt && !Tools.viewToolActive;
            bool marking=marker!=0 && !painting && !e.alt && !Tools.viewToolActive;
            control=GUIUtility.GetControlID(FocusType.Passive);
            if((active||marking) && e.type==EventType.Layout) HandleUtility.AddDefaultControl(control);
            var plane=new Plane(Vector3.up,map.transform.position);
            var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);
            hasHover=plane.Raycast(ray,out float distance);
            if(hasHover)
            {
                hover=map.WorldToCell(ray.GetPoint(distance));
            }
            if(e.type==EventType.Repaint)
            {
                if(gridVisible) DrawGrid();
                DrawRoads();
                DrawMarkers();
                if(marking && hasHover) DrawCell(hover,new Color(1,1,1,.2f));
                if(active && hasHover)
                {
                    var cells=rectangle&&dragging?Rectangle(start,hover):Brush(hover);
                    foreach(var cell in cells.Take(MaxCellsPerAction)) DrawCell(cell,tool==1||e.shift?new Color(1,.2f,.2f,.25f):new Color(.2f,.85f,1,.2f));
                }
            }
            if(marking && hasHover && e.button==0 && (e.type==EventType.MouseDown || dragging && e.type==EventType.MouseDrag && marker==3 && !hover.Equals(last)))
            {
                if(e.type==EventType.MouseDown) { dragging=true; Undo.IncrementCurrentGroup(); undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Edit routes"); GUIUtility.hotControl=control; }
                last=hover;
                if(ApplyMarker(hover,e.type==EventType.MouseDrag)) Repaint();
                e.Use(); SceneView.RepaintAll();
            }
            if(active && hasHover && e.type==EventType.MouseDown && e.button==0)
            {
                dragging=true; start=last=hover; eraseStroke=tool==1||e.shift;
                Undo.IncrementCurrentGroup(); undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName(layer==0?"Paint floor":"Paint road");
                GUIUtility.hotControl=control;
                if(!rectangle) MapPainterEditing.Paint(map,Brush(hover),layer==1,eraseStroke);
                e.Use(); RepaintViews();
            }
            if(active && dragging && e.type==EventType.MouseDrag && e.button==0 && hasHover && !e.alt)
            {
                if(!rectangle)
                {
                    long length=System.Math.Abs((long)hover.x-last.x)+System.Math.Abs((long)hover.z-last.z)+1;
                    if(length*brush*brush<=MaxCellsPerAction)
                    {
                        var cells=new HashSet<GridCell>();
                        foreach(var cell in GridTopology.Stroke(last,hover)) cells.UnionWith(Brush(cell));
                        MapPainterEditing.Paint(map,cells,layer==1,eraseStroke);
                    }
                }
                last=hover; e.Use(); RepaintViews();
            }
            if(dragging && e.rawType==EventType.MouseUp && e.button==0)
            {
                if(active && rectangle && hasHover)
                {
                    long count=((long)System.Math.Abs((long)hover.x-start.x)+1)*(System.Math.Abs((long)hover.z-start.z)+1);
                    if(count<=MaxCellsPerAction) MapPainterEditing.Paint(map,Rectangle(start,hover),layer==1,eraseStroke);
                    else ShowNotification(new GUIContent($"한 번에 최대 {MaxCellsPerAction}칸까지 배치합니다."));
                }
                FinishStroke(); e.Use(); RepaintViews();
            }
            if(e.type==EventType.MouseMove) view.Repaint();
        }
        private void FinishStroke()
        {
            if(!dragging)return;
            if(undoGroup>=0) Undo.CollapseUndoOperations(undoGroup);
            GUIUtility.hotControl=0; dragging=false; undoGroup=-1;
        }
        private void DrawGrid()
        {
            GridCell center=hasHover?hover:new GridCell(0,0);
            Handles.color=new Color(.35f,.65f,.85f,.25f);
            for(int i=-14;i<=15;i++)
            {
                float x=center.x+i-.5f,z=center.z+i-.5f;
                Handles.DrawLine(map.transform.position+new Vector3(x*map.cellSize,.004f,(center.z-14.5f)*map.cellSize),map.transform.position+new Vector3(x*map.cellSize,.004f,(center.z+14.5f)*map.cellSize));
                Handles.DrawLine(map.transform.position+new Vector3((center.x-14.5f)*map.cellSize,.004f,z*map.cellSize),map.transform.position+new Vector3((center.x+14.5f)*map.cellSize,.004f,z*map.cellSize));
            }
        }
        private void DrawCell(GridCell cell,Color color)
        {
            Vector3 p=map.CellPosition(cell)+Vector3.up*.012f;
            float half=map.cellSize*.5f;
            Handles.DrawSolidRectangleWithOutline(new[] { p+new Vector3(-half,0,-half),p+new Vector3(-half,0,half),p+new Vector3(half,0,half),p+new Vector3(half,0,-half) },color,new Color(color.r,color.g,color.b,.8f));
        }
        private void DrawRoads()
        {
            if(!painting && layer!=1)return;
            var cells=new HashSet<GridCell>(map.roadCells); Handles.color=new Color(1,.65f,.12f,1);
            foreach(var cell in cells)
            {
                Vector3 p=map.CellPosition(cell)+Vector3.up*(.04f+map.palette.roadLift);
                int mask=GridTopology.Connections(cells,cell);
                if(!map.palette.TryRoad(mask,out _,out _)) Handles.DrawWireDisc(p,Vector3.up,.15f);
                for(int i=0;i<4;i++)if((mask&(1<<i))!=0)
                {
                    var d=GridTopology.Directions[i]; Handles.DrawAAPolyLine(5,p,p+new Vector3(d.x,0,d.z)*map.cellSize*.5f);
                }
            }
        }
    }
    [CustomEditor(typeof(ModularMapRoot))]
    public sealed class ModularMapRootInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var root=(ModularMapRoot)target;
            EditorGUILayout.LabelField($"바닥 {root.floorCells.Count} / 경로 {root.roadCells.Count}");
            if(GUILayout.Button("Map Painter 열기")) ModularMapPainterWindow.Open().SetMap(root);
        }
    }
}
