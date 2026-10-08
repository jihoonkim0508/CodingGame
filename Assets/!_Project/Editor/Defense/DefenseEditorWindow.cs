using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodingGame.BlockCoding;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CodingGame.Defense.Editor
{
    public sealed class DefenseEditorWindow : EditorWindow
    {
        const string Root = "Assets/!_Project/Editor/Defense/";
        [SerializeField] DefenseBattle battle;
        [SerializeField] DefenseCampaignDefinition campaign;
        [SerializeField] TutorialSequence sequence;
        [SerializeField] DefenseTestScenario scenario;
        [SerializeField] string lastReport;
        DefenseCampaignDefinition playContent;
        readonly List<Object> copies = new List<Object>();
        readonly Queue<LogEntry> logs = new Queue<LogEntry>();
        readonly Dictionary<string,string> robotResults = new Dictionary<string,string>();
        readonly Dictionary<string,Metric> metrics = new Dictionary<string,Metric>();
        [SerializeField] DefenseTestOptions options = new DefenseTestOptions();
        ScrollView content;
        Label status, feedback, live;
        ListView logView;
        string tab = "세션", resultState = "대기", runId;
        [SerializeField] int testStage=1, testWave=1, tutorialStep, robotId, quantity=1;
        const string ToolPlayKey="CodingGame.ToolPlay";
        bool pendingToolStart;
        [SerializeField] bool accumulate=true;
        bool campaignRun, scenarioRun, geometry;
        double nextRefresh, runStart, timeout=180;
        DefenseBattle subscribed;
        string runSettings;
        System.Diagnostics.Process checkProcess;
        System.Threading.Tasks.Task<string> checkOutput, checkError;
        double checkStarted;
        int lastWave=-1, lastStage=-1;
        [Serializable] public sealed class LogEntry
        {
            public string run, origin, kind, reason;
            public int stage,wave,source,target;
            public double time;
            public float amount,appliedAmount,radius;
        }
        [Serializable] public sealed class Metric {public string origin,entity;public int id,kills,leaks;public float damageDealt,damageTaken;}
        [Serializable] sealed class RunSnapshot {public string content;public RobotSpec[] robots;public EnemySpec[] enemies;public int stage,wave;public bool previousStageRewards;}
        [Serializable] sealed class Report
        {
            public string run, result, campaignPath, campaignGuid, settings;
            public DefenseTestOptions options;
            public LogEntry[] events;
            public Metric[] metrics;
            public string[] robots;
            public int kills,leaks,baseHealth;
            public double simulationTime;
        }
        [Serializable] sealed class ContentChanges
        {
            public string campaignGuid, content;
            public string[] robotValues, enemyValues;
        }
        void CaptureChanges()
        {
            if (!playContent || !campaign) return;
            playContent.ValidateContent();
            if(playContent.robots.Length!=campaign.robots.Length||playContent.enemies.Length!=campaign.enemies.Length)throw new Exception("정의 목록 변경은 Edit Mode에서 편집하세요.");
            var value=Instantiate(playContent);
            value.name=campaign.name;
            value.robots=campaign.robots;value.enemies=campaign.enemies;
            for(int stage=0;stage<value.stages.Length;stage++)foreach(var wave in value.stages[stage].waves)foreach(var group in wave.groups)
                group.enemy=campaign.enemies[Array.IndexOf(playContent.enemies,group.enemy)];
            var snapshot=new ContentChanges {campaignGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(campaign)),content=JsonUtility.ToJson(value),
                robotValues=playContent.robots.Select(JsonUtility.ToJson).ToArray(),enemyValues=playContent.enemies.Select(JsonUtility.ToJson).ToArray()};
            SessionState.SetString("CodingGame.PendingContent",JsonUtility.ToJson(snapshot));DestroyImmediate(value);
        }
        void PendingChanges()
        {
            if(EditorApplication.isPlaying||!campaign)return;
            var json=SessionState.GetString("CodingGame.PendingContent","");if(string.IsNullOrEmpty(json))return;
            var saved=JsonUtility.FromJson<ContentChanges>(json);
            if(saved.campaignGuid!=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(campaign)))return;
            var diffs=new List<string>();
            if(saved.content!=JsonUtility.ToJson(campaign))diffs.Add("캠페인 규칙·웨이브·보급");
            for(int i=0;i<saved.robotValues.Length&&i<campaign.robots.Length;i++)if(saved.robotValues[i]!=JsonUtility.ToJson(campaign.robots[i]))diffs.Add(campaign.robots[i].displayName);
            for(int i=0;i<saved.enemyValues.Length&&i<campaign.enemies.Length;i++)if(saved.enemyValues[i]!=JsonUtility.ToJson(campaign.enemies[i]))diffs.Add(campaign.enemies[i].displayName);
            if(diffs.Count==0){SessionState.EraseString("CodingGame.PendingContent");return;}
            var fold=new Foldout{text="Play 변경값 비교 (원본 미반영)"};
            foreach(var label in diffs)fold.Add(new Label(label));
            fold.Add(new TextField("원본 JSON"){multiline=true,isReadOnly=true,value=JsonUtility.ToJson(campaign,true)});
            fold.Add(new TextField("테스트 JSON"){multiline=true,isReadOnly=true,value=saved.content});content.Add(fold);
            Buttons(("수치 적용 확정",()=>{PersistChanges(saved);Build();}),("테스트값 폐기",()=>{SessionState.EraseString("CodingGame.PendingContent");Build();}));
        }
        void PersistChanges(ContentChanges saved)
        {
            if(saved==null||saved.campaignGuid!=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(campaign)))throw new Exception("저장할 캠페인이 일치하지 않습니다.");
            if(saved.robotValues.Length!=campaign.robots.Length||saved.enemyValues.Length!=campaign.enemies.Length)throw new Exception("참조 목록 변경은 Edit Mode에서 편집하세요.");
            var targets=new Object[]{campaign}.Concat(campaign.robots).Concat(campaign.enemies).ToArray();
            Undo.RecordObjects(targets,"수치 적용 확정");
            JsonUtility.FromJsonOverwrite(saved.content,campaign);
            for(int i=0;i<campaign.robots.Length;i++)JsonUtility.FromJsonOverwrite(saved.robotValues[i],campaign.robots[i]);
            for(int i=0;i<campaign.enemies.Length;i++)JsonUtility.FromJsonOverwrite(saved.enemyValues[i],campaign.enemies[i]);
            foreach(var item in targets){EditorUtility.SetDirty(item);AssetDatabase.SaveAssetIfDirty(item);}
            SessionState.EraseString("CodingGame.PendingContent");
        }
        void ApplyBalance(){var c=Editable;if(!c)throw new Exception("현재 전투에 캠페인 asset을 연결하세요.");c.ValidateContent();var b=RequireBattle();b.EditorApplyBalance(c);runSettings=JsonUtility.ToJson(new RunSnapshot{content=JsonUtility.ToJson(c),robots=b.Simulation.Setup.ActionProfiles,enemies=c.enemies.Select(e=>e.stats.Copy()).ToArray(),stage=b.Stage,wave=b.Simulation.WaveIndex+1,previousStageRewards=accumulate});Build();}
        void ConfirmBalance(){ApplyBalance();CaptureChanges();PersistChanges(JsonUtility.FromJson<ContentChanges>(SessionState.GetString("CodingGame.PendingContent","")));feedback.text="런타임 적용 및 원본 저장 완료";}
        void ResolveScene()
        {
            var scene=SceneManager.GetActiveScene();
            var battles=scene.IsValid()?scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DefenseBattle>(true)).ToArray():Array.Empty<DefenseBattle>();
            var current=battles.Length==1?battles[0]:null;
            var assigned=current?current.EditorCampaign:null;
            if(battle==current&&campaign==assigned)return;
            DisposeCopies();battle=current;campaign=assigned;Subscribe(battle);
        }
        [MenuItem("CodingGame/개발 에디터")]
        public static void Open() => GetWindow<DefenseEditorWindow>("CodingGame 개발 에디터");
        void OnEnable()
        {
            EditorApplication.update += UpdateEditor;
            EditorApplication.playModeStateChanged += PlayChanged;
            SceneView.duringSceneGui += DrawGeometry;
            Application.logMessageReceived += UnityLog;
        }
        void OnDisable()
        {
            EditorApplication.update -= UpdateEditor;
            EditorApplication.playModeStateChanged -= PlayChanged;
            SceneView.duringSceneGui -= DrawGeometry;
            Application.logMessageReceived -= UnityLog;
            if(campaignRun||scenarioRun||checkProcess!=null)StopCheck("중단: 창 닫힘");Subscribe(null);if(!EditorApplication.isPlaying)DisposeCopies();
        }
        public void CreateGUI()
        {
            ResolveScene();
            minSize=new Vector2(760,520);
            rootVisualElement.Clear();
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"DefenseEditorWindow.uxml").CloneTree(rootVisualElement);
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Root+"DefenseEditorWindow.uss"));
            status=rootVisualElement.Q<Label>("sessionStatus"); feedback=rootVisualElement.Q<Label>("feedback");
            content=rootVisualElement.Q<ScrollView>("content");
            var toolbar=rootVisualElement.Q("toolbar");
            toolbar.Add(new Label($"현재 씬: {SceneManager.GetActiveScene().name} · 전투: {(battle?battle.name:"없음 또는 여러 개")} · 설정: {(campaign?campaign.name:"미연결")}"));
            toolbar.Add(new Button(()=>Act(Validate)){text="설정 검사"});
            foreach(var name in new[]{"세션","밸런스","캠페인","보급·해금","테스트 ON/OFF","튜토리얼","블록·미리보기","진단·검증"})
            {
                var captured=name; rootVisualElement.Q("navigation").Add(new Button(()=>{tab=captured;SessionState.SetString("CodingGame.EditorTab",tab);Build();}){text=name});
            }
            tab=SessionState.GetString("CodingGame.EditorTab",tab); Subscribe(battle); Build();
        }
        ObjectField Choose<T>(string label,T value,Action<T> changed) where T:Object
        {
            var field=new ObjectField(label){objectType=typeof(T),allowSceneObjects=typeof(T)==typeof(DefenseBattle),value=value};
            field.RegisterValueChangedCallback(e=>changed(e.newValue as T));return field;
        }
        void Act(Action action) { try {feedback.text="";action();if(string.IsNullOrEmpty(feedback.text))feedback.text="완료";}catch(Exception e){feedback.text=e.Message;StopCheck("선행 조건 미충족: "+e.Message);} }
        void Heading(string text) {var label=new Label(text);label.AddToClassList("section");content.Add(label);}
        void Note(string text){var label=new Label(text);label.AddToClassList("readout");content.Add(label);}
        void Buttons(params (string name,Action action)[] actions)
        {
            var row=new VisualElement();row.AddToClassList("row");
            foreach(var item in actions){var a=item.action;row.Add(new Button(()=>Act(a)){text=item.name});}content.Add(row);
        }
        void Property(Object target,params string[] names)
        {
            if(!target)return;
            var so=new SerializedObject(target);var parent=new VisualElement();
            foreach(var name in names){var p=so.FindProperty(name);if(p!=null)parent.Add(new PropertyField(p));}
            parent.Bind(so); content.Add(parent);
        }
        void Whole(Object target)
        {
            if(!target)return;var so=new SerializedObject(target);var p=so.GetIterator();var fields=new VisualElement();
            if(p.NextVisible(true))do{if(p.name!="m_Script")fields.Add(new PropertyField(p.Copy()));}while(p.NextVisible(false));
            fields.Bind(so);content.Add(fields);
        }
        void Integer(string label,int value,Action<int> change){var f=new IntegerField(label){value=value};f.RegisterValueChangedCallback(e=>change(e.newValue));content.Add(f);}
        void Build()
        {
            if(content==null)return;content.Clear();live=null;logView=null;PendingChanges();
            if(EditorApplication.isPlaying&&!SessionState.GetBool(ToolPlayKey,false)){Note("일반 Play: 개발 에디터 비활성. 종료 후 이 창에서 Play를 시작하세요.");Buttons(("Play 종료",()=>EditorApplication.isPlaying=false));return;}
            Note(EditorApplication.isPlaying?"적용: 즉시 수치 반영·체력 초기화·투사체 제거. 수치 적용 확정: 원본 asset에도 저장. 보상 수령 상태 유지.":"Edit Mode: 현재 씬에 연결된 asset 편집. Undo/Redo 지원.");
            if(EditorApplication.isPlaying)Buttons(("적용",ApplyBalance),("수치 적용 확정",ConfirmBalance));
            switch(tab)
            {
                case "세션": Session();break;
                case "밸런스": Balance();break;
                case "캠페인": Campaign();break;
                case "보급·해금": Inventory();break;
                case "테스트 ON/OFF": Options();break;
                case "튜토리얼": Tutorial();break;
                case "블록·미리보기": Coding();break;
                default: Diagnostics();break;
            }
            live=new Label();live.AddToClassList("readout");content.Add(live);Refresh();
        }
        DefenseCampaignDefinition Editable
        {
            get
            {
                if(!EditorApplication.isPlaying)return campaign;
                if(!playContent && campaign)
                {
                    campaign.ValidateContent();playContent=Instantiate(campaign);playContent.name=campaign.name;copies.Add(playContent);
                    playContent.robots=campaign.robots.Select(r=>{var c=Instantiate(r);c.name=r.name;copies.Add(c);return c;}).ToArray();
                    playContent.enemies=campaign.enemies.Select(e=>{var c=Instantiate(e);c.name=e.name;copies.Add(c);return c;}).ToArray();
                    for(int s=0;s<playContent.stages.Length;s++)foreach(var w in playContent.stages[s].waves)foreach(var g in w.groups)
                        g.enemy=playContent.enemies[Array.IndexOf(campaign.enemies,g.enemy)];
                }
                return playContent;
            }
        }
        void Session()
        {
            Integer("시작 스테이지",testStage,v=>testStage=v);Integer("시작 웨이브",testWave,v=>testWave=v);
            var supply=new Toggle("이전스테이지까지의 보상"){value=accumulate};supply.RegisterValueChangedCallback(e=>accumulate=e.newValue);content.Add(supply);
            Buttons(("Play 시작 (개발툴 포함)",()=>{SessionState.SetBool(ToolPlayKey,true);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;}),("Play 종료",()=>EditorApplication.isPlaying=false));
            Buttons(("단독 웨이브 초기화",()=>StartScenario(false)),("변경값으로 재시작",()=>StartScenario(false)),("캠페인 1-1 초기화",()=>StartScenario(true)));
            Buttons(("전투 시작",()=>RequireBattle().StartBattle()),("일시정지 / 재개",()=>RequireBattle().TogglePause()),("한 틱",()=>RequireBattle().EditorStep()),("1 / 2배속",()=>RequireBattle().ToggleSpeed()),("보상 / 다음 단계",()=>RequireBattle().ContinueCampaign()));
            content.Add(Choose("테스트 시나리오",scenario,v=>{scenario=v;Build();}));
            if(scenario && !EditorApplication.isPlaying) {Property(scenario,"stage","wave","coins","previousStageRewards","timeoutSeconds","robots","blocks","placements","options");}
            Buttons(("현재 조건으로 시나리오 저장",SaveScenario),("시나리오 실행",RunScenario));
        }
        DefenseBattle RequireBattle(){if(!battle || !EditorApplication.isPlaying || battle.Simulation==null||!SessionState.GetBool(ToolPlayKey,false))throw new InvalidOperationException("전투가 있는 씬에서 개발 에디터의 Play를 시작하세요.");return battle;}
        void StartScenario(bool normal)
        {
            var b=RequireBattle();campaignRun=scenarioRun=false;b.EditorManualClock=false;
            if(checkProcess!=null)throw new InvalidOperationException("실행 중인 로직 검사를 먼저 중단하세요.");
            b.EditorStartSession(normal?1:testStage,normal?1:testWave,normal||accumulate,options,Editable);
            runId=Guid.NewGuid().ToString("N");logs.Clear();robotResults.Clear();runStart=EditorApplication.timeSinceStartup;
            metrics.Clear();var c=Editable;
            runSettings=JsonUtility.ToJson(new RunSnapshot {content=c?JsonUtility.ToJson(c):"legacy scene",robots=b.Simulation.Setup.ActionProfiles,enemies=c?c.enemies.Select(e=>e.stats.Copy()).ToArray():b.Simulation.Setup.Waves.SelectMany(w=>w).Select(g=>g.Enemy.Copy()).ToArray(),stage=b.Stage,wave=b.Simulation.WaveIndex+1,previousStageRewards=normal||accumulate});resultState=normal?"캠페인 준비":"단독 웨이브 준비";lastStage=lastWave=-1;Refresh();
        }
        void Balance()
        {
            var c=Editable;if(!c){Note("캠페인 탭에서 현재 콘텐츠를 이관하세요.");return;}
            Heading("공통 밸런스");Property(c,"baseHealth","robotLimit","rules","permissions");
            if(EditorApplication.isPlaying&&battle&&battle.Simulation!=null)
            {var applied=new Foldout{text="현재 세션 적용값"};applied.Add(new TextField("규칙"){isReadOnly=true,multiline=true,value=JsonUtility.ToJson(battle.Simulation.Setup.RulesConfig,true)});foreach(var spec in battle.Simulation.Setup.ActionProfiles)applied.Add(new TextField(spec.role.ToString()){isReadOnly=true,multiline=true,value=JsonUtility.ToJson(spec,true)});content.Add(applied);}
            foreach(var r in c.robots){var fold=new Foldout{text=r.displayName};var so=new SerializedObject(r);fold.Add(new PropertyField(so.FindProperty("stats")));fold.Add(new PropertyField(so.FindProperty("description")));fold.Bind(so);content.Add(fold);}
            foreach(var e in c.enemies){var fold=new Foldout{text=e.displayName};var so=new SerializedObject(e);fold.Add(new PropertyField(so.FindProperty("stats")));fold.Add(new PropertyField(so.FindProperty("description")));fold.Bind(so);content.Add(fold);}
            var shooter=c.robots.FirstOrDefault(r=>r.stats.role==RobotRole.Shooter);
            if(shooter){Heading("원거리 거리별 기본 피해 / 주기 DPS");foreach(float d in new[]{.5f,1,2,4,8,10})Note($"거리 {d}: 피해 {shooter.stats.damage*Rules.RangedDamageFactor(shooter.stats.range,d):0.##}, DPS {shooter.stats.damage*Rules.RangedDamageFactor(shooter.stats.range,d)/shooter.stats.interval:0.##} (강화·버프 없음)");}
            Buttons(("원본 저장",SaveContent));
        }
        void Campaign()
        {
            Buttons(("현재 씬 콘텐츠 이관",Migrate),("원본 저장",SaveContent));
            var c=Editable;if(!c)return;
            if(EditorApplication.isPlaying)Property(c,"stages");else Property(c,"stages","robots","enemies");Heading("최종 스폰 계획");
            for(int s=0;s<c.stages.Length;s++)for(int w=0;w<c.stages[s].waves.Length;w++)
            {
                var f=new Foldout{text=$"{s+1}-{w+1}"};
                foreach(var g in c.stages[s].waves[w].groups){if(!g.enemy)continue;var v=c.rules.Scale(g.enemy.stats,s+1,w);f.Add(new Label($"경로 {g.route} · {g.enemy.displayName} ×{g.count} · HP {g.enemy.stats.health:0.##}/{v.health:0.##} · 속도 {g.enemy.stats.speed:0.##}/{v.speed:0.##} · {g.delay}s / {g.interval}s"));}content.Add(f);
            }
        }
        void SaveContent()
        {
            if(EditorApplication.isPlaying){ConfirmBalance();return;}
            Validate();if(campaign){AssetDatabase.SaveAssetIfDirty(campaign);foreach(var r in campaign.robots)AssetDatabase.SaveAssetIfDirty(r);foreach(var e in campaign.enemies)AssetDatabase.SaveAssetIfDirty(e);}if(sequence)AssetDatabase.SaveAssetIfDirty(sequence);
        }
        void Migrate()
        {
            if(EditorApplication.isPlaying || !battle)throw new InvalidOperationException("Edit Mode에서 전투 대상을 선택하세요.");
            var path=EditorUtility.SaveFilePanelInProject("캠페인 저장","DefenseCampaign","asset","");
            if(!string.IsNullOrEmpty(path))MigrateTo(path);
        }
        public void MigrateTo(string path)
        {
            if(EditorApplication.isPlaying || !battle)throw new InvalidOperationException("Edit Mode에서 전투 대상을 선택하세요.");
            if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.Ordinal)||AssetDatabase.LoadMainAssetAtPath(path))throw new ArgumentException("새 Assets/*.asset 경로 필요");
            var c=battle.EditorExportCampaign();c.ValidateContent();
            var before=battle.EditorReadSetup();
            for(int s=1;s<=c.stages.Length;s++){var after=c.ReadWaves(s);for(int w=0;w<after.Length;w++)for(int g=0;g<after[w].Length;g++){
                var prior=(before.CampaignWaves!=null?before.CampaignWaves[s-1]:before.Waves)[w][g];
                var a=before.RulesConfig.Scale(prior.Enemy,s,w);var b=c.rules.Scale(after[w][g].Enemy,s,w);
                if(JsonUtility.ToJson(a)!=JsonUtility.ToJson(b)||prior.Count!=after[w][g].Count||prior.RouteIndex!=after[w][g].RouteIndex||prior.Delay!=after[w][g].Delay||prior.Interval!=after[w][g].Interval)throw new Exception("이관 전후 값 불일치");}}
            if(JsonUtility.ToJson(before.Progression)!=JsonUtility.ToJson(c.progression))throw new Exception("보급·보상 이관 불일치");
            AssetDatabase.CreateAsset(c,path);Undo.RegisterCreatedObjectUndo(c,"캠페인 이관");
            var so=new SerializedObject(battle);so.FindProperty("campaign").objectReferenceValue=c;so.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(battle);campaign=c;Build();
        }
        void Inventory()
        {
            if(Editable)Property(Editable,"progression");
            Integer("추가 수량",quantity,v=>quantity=v);
            var role=new EnumField("로봇",RobotRole.Shooter);content.Add(role);
            var kind=new EnumField("블록",BlockKind.Shot);content.Add(kind);
            Buttons(("로봇 추가",()=>RequireBattle().Simulation.GrantRobot((RobotRole)role.value,quantity)),("블록 추가",()=>RequireBattle().Simulation.GrantBlock((BlockKind)kind.value,quantity)),("전체 추가 (+10/+50)",()=>RequireBattle().EditorGrantAll()));
            Integer("테스트 코인",100,v=>Act(()=>RequireBattle().EditorSetCoins(v)));
            Buttons(("원본 저장",SaveContent));
        }
        void Options()
        {
            Note("계산 토글은 다음 틱, 표시 토글은 즉시 적용. 정상값 복원은 현재 전투 상태를 되돌리지 않습니다.");
            var labels=new[]{"튜토리얼 실행","튜토리얼 입력 제한","로봇 코드 실행","적 이동","적 공격","적 스폰","기지 피해","로봇 피해","적에게 공격 피해","웨이브 시간 제한","공격 횟수 제한","설치 개수 제한","인벤토리 제한","전체 해금","준비 보급","종료 보상","처치 드롭","사거리 표시","피격 범위","발사 이펙트","피격 이펙트","슬래시 이펙트","투사체 표시","미리보기 실행","상세 이벤트 기록"};
            int i=0;foreach(var field in typeof(DefenseTestOptions).GetFields())
            {
                var f=field;var toggle=new Toggle(labels[i++]){value=(bool)f.GetValue(options)};
                toggle.RegisterValueChangedCallback(e=>{f.SetValue(options,e.newValue);if(battle&&EditorApplication.isPlaying)Act(()=>battle.EditorSetOptions(options));});content.Add(toggle);
            }
            Buttons(("정상값 복원",()=>{options=new DefenseTestOptions();if(battle&&EditorApplication.isPlaying)battle.EditorSetOptions(options);Build();}));
        }
        void Tutorial()
        {
            content.Add(Choose("시퀀스",sequence,v=>{sequence=v;Build();}));
            if(sequence){if(EditorApplication.isPlaying)Note("실행은 복제본을 사용합니다. 원본 편집은 Edit Mode에서 가능합니다.");else Whole(sequence);}
            Integer("테스트 step (0부터)",tutorialStep,v=>tutorialStep=v);
            Buttons(("선택 step 실행",()=>RequireBattle().Tutorial.EditorRun(sequence,tutorialStep)),("안내 종료",()=>RequireBattle().Tutorial.EditorStop()),("이전",()=>RequireBattle().Tutorial.EditorPrevious()),("다음",()=>RequireBattle().Tutorial.EditorNext()),("타이핑 완료",()=>RequireBattle().Tutorial.EditorFinishTyping()));
            Buttons(("목표 가림 검사",CheckTargets),("원본 저장",SaveContent));
            if(battle&&battle.Tutorial)Property(battle.Tutorial.EditorBridge,"targets");
        }
        void Coding()
        {
            Integer("로봇 ID",robotId,v=>robotId=v);
            Buttons(("블록 개발 툴 열기",()=>RequireBattle().EditorCode.Open(robotId)),("닫기",()=>RequireBattle().EditorCode.Close()),("적용",()=>RequireBattle().EditorCode.Panel.Apply()),("Undo",()=>RequireBattle().EditorCode.Panel.Undo()),("Redo",()=>RequireBattle().EditorCode.Panel.Redo()));
            var toggle=new Toggle("Scene View 블록 경계·클릭 삼각형·스냅 후보"){value=geometry};toggle.RegisterValueChangedCallback(e=>{geometry=e.newValue;SceneView.RepaintAll();});content.Add(toggle);
            Note("실제 블록 작성은 Game View의 기존 개발 툴을 사용합니다.");
        }
        void Diagnostics()
        {
            Buttons(("설정 검사",Validate),("캠페인 1~5 실행",BeginCampaignCheck),("선택 시나리오 검사",RunScenario),("검사 중단",()=>StopCheck("중단")),("결과 JSON 저장",SaveReport));
            Buttons(("기존 전투 로직 검사",()=>RunLogicCheck("Tests/Defense/Defense.Check.csproj")),("기존 블록 컴파일 검사",()=>RunLogicCheck("Tests/BlockCoding/BlockCoding.Check.csproj")),("Editor 옵션 검사",()=>RunLogicCheck("Tests/Defense/EditorOptions.Check.csproj")));
            var seconds=new FloatField("검사 제한 시간 (실제 초)"){value=(float)timeout};seconds.RegisterValueChangedCallback(e=>timeout=Math.Max(1,e.newValue));content.Add(seconds);
            Heading("최근 1,000 이벤트");
            logView=new ListView{fixedItemHeight=22,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};
            logView.makeItem=()=>new Label();logView.bindItem=(e,i)=>{var l=(LogEntry)logView.itemsSource[i];((Label)e).text=$"{l.origin} {l.stage}-{l.wave} {l.time:0.00}s {l.kind} {l.source}/{l.target} {l.amount:0.##} {l.reason}";};logView.style.height=240;content.Add(logView);
        }
        void Validate()
        {
            if(!battle)throw new InvalidOperationException("전투 대상 연결 필요");
            Editable?.ValidateContent();battle.EditorReadSetup().Validate();
            if(sequence){var ids=new HashSet<string>();foreach(var step in sequence.steps){if(step==null||string.IsNullOrWhiteSpace(step.stepId)||!ids.Add(step.stepId)||step.texts.Length==0)throw new Exception("튜토리얼 step ID·대사 확인 필요");foreach(var key in step.targetKeys)if(!battle.Tutorial.EditorBridge.HasTarget(key))throw new Exception("대상 키 누락: "+key);}}
            feedback.text="설정 검사 통과";
        }
        void Subscribe(DefenseBattle value){if(subscribed==value)return;if(subscribed)subscribed.EditorCombatEvent-=Combat;subscribed=value;if(subscribed)subscribed.EditorCombatEvent+=Combat;}
        void Combat(string origin,DefenseSimulation sim,CombatEvent e)
        {
            Metric Get(string entity,int id){var key=origin+"/"+entity+"/"+id;if(!metrics.TryGetValue(key,out var m))metrics.Add(key,m=new Metric{origin=origin,entity=entity,id=id});return m;}
            if(e.Kind=="hit"||e.Kind=="projectile-hit"){Get("robot",e.Source).damageDealt+=e.AppliedAmount;Get("enemy",e.Target).damageTaken+=e.AppliedAmount;}
            if(e.Kind=="robot-damage"){Get("enemy",e.Source).damageDealt+=e.AppliedAmount;Get("robot",e.Target).damageTaken+=e.AppliedAmount;}
            if(e.Kind=="killed")Get("robot",e.Source).kills++;
            if(e.Kind=="leaked")Get("enemy",e.Source).leaks++;
            if(!options.detailedLog)return;
            AddLog(new LogEntry{run=runId,origin=origin,stage=sim.Setup.Stage,wave=sim.WaveIndex+1,time=sim.Time,kind=e.Kind,reason=e.Reason,source=e.Source,target=e.Target,amount=e.Amount,appliedAmount=e.AppliedAmount,radius=e.Radius});
        }
        void AddLog(LogEntry entry){while(logs.Count>=1000)logs.Dequeue();logs.Enqueue(entry);}
        void UnityLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)AddLog(new LogEntry{run=runId,origin="Unity",kind=type.ToString(),reason=message});}
        void UpdateEditor()
        {
            if(EditorApplication.timeSinceStartup<nextRefresh)return;nextRefresh=EditorApplication.timeSinceStartup+.2;
            var previous=battle;var previousCampaign=campaign;ResolveScene();
            if(previous!=battle||previousCampaign!=campaign)CreateGUI();
            Subscribe(battle);
            if(pendingToolStart&&battle&&battle.Simulation!=null){pendingToolStart=false;Act(()=>{StartScenario(false);battle.EditorEnableDeveloper();});}
            if(checkProcess!=null)
            {
                if(EditorApplication.timeSinceStartup-checkStarted>timeout){CancelLogicCheck();resultState="실패: 로직 검사 시간 초과";}
                else if(checkProcess.HasExited&&checkOutput.IsCompleted&&checkError.IsCompleted)
                {resultState=checkProcess.ExitCode==0?"통과: 순수 로직 검사":"실패: 순수 로직 검사";feedback.text=checkOutput.Result+checkError.Result;AddLog(new LogEntry{origin="LogicCheck",kind=resultState,reason=feedback.text});checkProcess.Dispose();checkProcess=null;}
            }
            if(campaignRun)Act(AdvanceCampaignCheck);
            else if(scenarioRun) {var sim=battle?battle.Simulation:null;if(sim==null)StopCheck("선행 조건 미충족: 전투 연결 끊김");else if(EditorApplication.timeSinceStartup-runStart>timeout)StopCheck("실패: 제한 시간 초과");else if(sim.Phase==BattlePhase.Defeat)StopCheck("실패: 기지 파괴");else if(sim.Phase==BattlePhase.Reward||sim.Phase==BattlePhase.Victory)StopCheck("통과: 선택 웨이브 실행 (UI 별도)");}
            Refresh();
        }
        void Refresh()
        {
            if(status==null)return;var sim=battle?battle.Simulation:null;
            status.text=$"{(EditorApplication.isPlaying?"Play":"Edit")} · {(battle?battle.name:"대상 없음")} · {resultState}";
            if(sim==null){if(live!=null)live.text="";return;}
            status.text+=$" · {sim.Setup.Stage}-{sim.WaveIndex+1} {sim.Phase} · HP {sim.BaseHealth} · 처치 {sim.Kills} / 누수 {sim.Leaks}";
            if(live!=null){live.text=$"공격 상한 {sim.AttackLimitPerRobot}, 반복문 {sim.LoopsUnlocked}, 남은 적 {sim.RemainingEnemies}, 드롭 정책 {(sim.Lesson!=null?"학습 종료 보상":"필요 블록 드롭")}\n";
                if(tab=="블록·미리보기"||tab=="진단·검증")foreach(var r in sim.Robots){live.text+=$"ID {r.Id} {r.Spec.role} Lv{r.Level} HP {r.Health} 공격 {r.AttacksThisWave} 결과 {r.LastResult} 실행 {r.Program?.CompletedRuns} 대기 {r.Program?.WaitRemaining(sim.Time):0.##} 오류 {r.Program?.Fault}\n{r.Program?.Source}\n";}
                if(tab=="보급·해금")live.text+=string.Join("\n",sim.RobotInventory.Select(p=>$"{p.Key}: {p.Value}, 해금 {sim.RobotUnlocked(p.Key)}"))+"\n"+string.Join("\n",sim.Inventory.Select(p=>$"{p.Key}: 보유 {p.Value}, 잔여 {sim.Available(p.Key)}"));
                if(tab=="튜토리얼")live.text+=$"현재 step: {battle.Tutorial.EditorStep?.stepId}, 신호: {battle.Tutorial.EditorStep?.completionSignal}";
            }
            foreach(var r in sim.Robots){string key=$"{runId}/{r.Id}";var value=$"{r.LastResult}/{r.Program?.Fault}";if(robotResults.TryGetValue(key,out var old)&&old!=value&&options.detailedLog)AddLog(new LogEntry{run=runId,origin="Battle",stage=sim.Setup.Stage,wave=sim.WaveIndex+1,time=sim.Time,kind="action-state",source=r.Id,reason=value});robotResults[key]=value;}
            if(logView!=null){logView.itemsSource=logs.ToList();logView.RefreshItems();}
        }
        void PlayChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode){try{CaptureChanges();if(battle&&battle.Simulation!=null)lastReport=CurrentReport();}catch(Exception e){if(feedback!=null)feedback.text=e.Message;}StopCheck("중단: Play 종료");Subscribe(null);}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(ToolPlayKey,false);pendingToolStart=false;DisposeCopies();options=new DefenseTestOptions();CreateGUI();}
            if(state==PlayModeStateChange.EnteredPlayMode){pendingToolStart=SessionState.GetBool(ToolPlayKey,false);ResolveScene();CreateGUI();}
        }
        void DisposeCopies(){foreach(var c in copies)if(c)DestroyImmediate(c);copies.Clear();playContent=null;}
        void SaveScenario()
        {
            var path=EditorUtility.SaveFilePanelInProject("테스트 조건 저장","Scenario","asset","");if(string.IsNullOrEmpty(path))return;
            var s=CreateInstance<DefenseTestScenario>();s.stage=testStage;s.wave=testWave;s.previousStageRewards=accumulate;s.options=options.Copy();s.timeoutSeconds=(float)timeout;
            if(battle&&battle.Simulation!=null){var sim=battle.Simulation;s.stage=sim.Setup.Stage;s.wave=sim.WaveIndex+1;s.coins=sim.Coins;s.robots=sim.RobotInventory.Select(p=>new RobotStock{role=p.Key,count=p.Value}).ToArray();s.blocks=sim.Inventory.Select(p=>new BlockStock{kind=p.Key,count=p.Value}).ToArray();s.placements=sim.Robots.Select(r=>new TestRobotPlacement{definitionIndex=r.DefinitionIndex,position=new Vector3(r.Position.X,0,r.Position.Y),functionName=r.Program?.Name??"robot",level=r.Level,health=r.Health??-1,Blocks=r.Program?.CopyBlocks().ToArray()??Array.Empty<CodeBlock>()}).ToArray();}
            AssetDatabase.CreateAsset(s,path);scenario=s;Build();
        }
        void RunScenario()
        {
            if(!scenario)throw new Exception("시나리오 선택 필요");testStage=scenario.stage;testWave=scenario.wave;accumulate=scenario.previousStageRewards;options=scenario.options.Copy();timeout=scenario.timeoutSeconds;StartScenario(false);
            var b=RequireBattle();var sim=b.Simulation;sim.SetCoins(scenario.coins);sim.RobotInventory.Clear();sim.Inventory.Clear();
            foreach(var r in scenario.robots){if(r.count<0)throw new Exception("음수 로봇 재고");sim.RobotInventory.Add(r.role,r.count);}foreach(var k in scenario.blocks){if(k.count<0)throw new Exception("음수 블록 재고");sim.Inventory.Add(DefenseProgression.Canonical(k.kind),k.count);}
            foreach(var p in scenario.placements){var role=b.DefinitionRole(p.definitionIndex);sim.GrantRobot(role,1);if(!b.PlaceRobot(p.definitionIndex,p.position))throw new Exception("시나리오 배치 실패");b.EditorRestoreRobotState(b.SelectedId,p.level,p.health);sim.ApplyProgram(b.SelectedId,p.functionName,p.Blocks);}
            b.StartBattle();if(sim.Phase!=BattlePhase.Running)throw new Exception(sim.StartError()??"전투 시작 차단");scenarioRun=true;resultState="시나리오 실행 중";
        }
        void SaveReport()
        {
            Directory.CreateDirectory("TestResults/Editor");
            var json=EditorApplication.isPlaying?CurrentReport():lastReport;
            if(string.IsNullOrEmpty(json))json=JsonUtility.ToJson(new Report{result=resultState,events=logs.ToArray()},true);
            File.WriteAllText($"TestResults/Editor/{runId??Guid.NewGuid().ToString("N")}.json",json);
            feedback.text="TestResults/Editor에 JSON 저장 완료";
        }
        string CurrentReport()
        {
            var sim=RequireBattle().Simulation;
            var report=new Report{run=runId,result=resultState,campaignPath=AssetDatabase.GetAssetPath(campaign),campaignGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(campaign)),settings=runSettings,options=options.Copy(),events=logs.ToArray(),metrics=metrics.Values.ToArray(),robots=sim.Robots.Select(r=>$"{r.Id} {r.Spec.role} HP={r.Health} attacks={r.AttacksThisWave}").ToArray(),kills=sim.Kills,leaks=sim.Leaks,baseHealth=sim.BaseHealth,simulationTime=sim.Time};
            return JsonUtility.ToJson(report,true);
        }
        void StopCheck(string result){bool active=campaignRun||scenarioRun;CancelLogicCheck();campaignRun=scenarioRun=false;resultState=result;if(battle){battle.EditorManualClock=false;if(active&&battle.Simulation?.Phase==BattlePhase.Running)battle.TogglePause();}}
        void RunLogicCheck(string project)
        {
            if(checkProcess!=null||campaignRun||scenarioRun)throw new InvalidOperationException("실행 중인 검사를 먼저 중단하세요.");
            checkProcess=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dotnet","run --project "+project){WorkingDirectory=Directory.GetCurrentDirectory(),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});
            checkOutput=checkProcess.StandardOutput.ReadToEndAsync();checkError=checkProcess.StandardError.ReadToEndAsync();checkStarted=EditorApplication.timeSinceStartup;resultState="순수 로직 검사 실행 중";
        }
        void CancelLogicCheck(){if(checkProcess==null)return;if(!checkProcess.HasExited)checkProcess.Kill();checkProcess.Dispose();checkProcess=null;}
        void BeginCampaignCheck(){options=new DefenseTestOptions{tutorial=false,detailedLog=true};StartScenario(true);battle.EditorManualClock=true;campaignRun=true;resultState="캠페인 실행 중";}
        void AdvanceCampaignCheck()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            var b=RequireBattle();var sim=b.Simulation;
            if(EditorApplication.timeSinceStartup-runStart>timeout){StopCheck("실패: 제한 시간 초과");return;}
            if(sim.Phase==BattlePhase.Defeat){StopCheck("실패: 기지 파괴");return;}
            if(sim.Phase==BattlePhase.Reward){b.ContinueCampaign();return;}
            if(sim.Phase==BattlePhase.Victory){if(sim.CanAdvanceStage){b.ContinueCampaign();return;}StopCheck("통과: 캠페인 진행 (자동 배치, UI 별도)");return;}
            if(sim.Phase==BattlePhase.Running){for(int tick=0;tick<300&&sim.Phase==BattlePhase.Running;tick++)sim.Advance(DefenseSimulation.TickDuration);return;}
            if(sim.Phase!=BattlePhase.Ready)return;
            if(lastStage==sim.Setup.Stage&&lastWave==sim.WaveIndex)return;lastStage=sim.Setup.Stage;lastWave=sim.WaveIndex;
            for(int index=0;index<6;index++)
            {
                var def=b.RobotDefinition(index);var kind=new[]{BlockKind.Buff,BlockKind.Slash,BlockKind.Block,BlockKind.Boom,BlockKind.Shot,BlockKind.Slow}[(int)def.stats.role];
                if(sim.RobotAvailable(def.stats.role)<1||sim.Available(kind)<1)continue;
                var candidates=new List<System.Numerics.Vector2>();
                for(float x=sim.Setup.Min.X+.5f;x<sim.Setup.Max.X;x+=1)for(float y=sim.Setup.Min.Y+.5f;y<sim.Setup.Max.Y;y+=1){var p=new System.Numerics.Vector2(x,y);if(sim.PlacementError(def.stats,p)==null)candidates.Add(p);}
                var best=candidates.OrderByDescending(p=>Coverage(sim,def.stats,p)).FirstOrDefault();
                if(candidates.Count>0&&b.PlaceRobot(index,new Vector3(best.X,0,best.Y)))sim.ApplyProgram(b.SelectedId,"robot",new[]{new CodeBlock(kind)});
            }
            foreach(var robot in sim.Robots) { ProgramRobot(sim,robot);sim.UpgradeRobot(robot.Id); }
            b.StartBattle();if(sim.Phase!=BattlePhase.Running)throw new Exception(sim.StartError()??"전투 시작 차단");
        }
        static void ProgramRobot(DefenseSimulation sim, RobotState bot)
        {
            sim.ApplyProgram(bot.Id,"robot_"+bot.Id,Array.Empty<CodeBlock>());
            var kind=new[]{BlockKind.Buff,BlockKind.Slash,BlockKind.Block,BlockKind.Boom,BlockKind.Shot,BlockKind.Slow}[(int)bot.Spec.role];
            CodeBlock command=new CodeBlock(kind);
            if(sim.Available(BlockKind.While,bot.Id)>0 && sim.Available(BlockKind.True,bot.Id)>0)
            {var loop=new CodeBlock(BlockKind.While,"0",new CodeBlock(BlockKind.True));loop.Body.Add(command);command=loop;}
            else if(sim.Available(BlockKind.For,bot.Id)>0 && sim.Available(BlockKind.Number,bot.Id)>0)
            {var loop=new CodeBlock(BlockKind.For,"0",new CodeBlock(BlockKind.Number,"100"));loop.Body.Add(command);command=loop;}
            sim.ApplyProgram(bot.Id,"robot_"+bot.Id,new[]{command});
        }
        // ponytail: reuse the existing campaign check's sampled coverage heuristic; this tests progression, not optimal player strategy.
        static float Coverage(DefenseSimulation sim,RobotSpec spec,System.Numerics.Vector2 p)
        {
            float score=0;foreach(var route in sim.Setup.Routes)for(int i=1;i<route.Points.Length;i++)for(int sample=0;sample<=20;sample++)
                if(System.Numerics.Vector2.Distance(p,System.Numerics.Vector2.Lerp(route.Points[i-1],route.Points[i],sample/20f))<=spec.range)score+=System.Numerics.Vector2.Distance(route.Points[i-1],route.Points[i])/21;
            return spec.role==RobotRole.Buffer?sim.Robots.Count(r=>System.Numerics.Vector2.Distance(r.Position,p)<=spec.range)*100+score:score;
        }
        void CheckTargets()
        {
            var b=RequireBattle();var step=b.Tutorial.EditorStep;if(step==null)throw new Exception("실행 중인 step이 없습니다.");
            var bridge=b.Tutorial.EditorBridge;var errors=new List<string>();
            foreach(var target in bridge.EditorAreas(step))
            {
                if(!bridge.CanPerformAction(target.key))errors.Add(target.key+" 행동 차단");
                for(int x=0;x<3;x++)for(int y=0;y<3;y++)
                {
                    var point=new Vector2(Mathf.Lerp(target.area.xMin,target.area.xMax,(x+.5f)/3),Mathf.Lerp(target.area.yMin,target.area.yMax,(y+.5f)/3));
                    var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=point};
                    var hits=new List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
                    if(target.ui ? hits.Count==0||!(hits[0].gameObject.transform==target.ui||hits[0].gameObject.transform.IsChildOf(target.ui)) : hits.Count>0)
                        errors.Add(target.key+" 투영 영역 가림: "+(hits.Count>0?hits[0].gameObject.name:"입력 대상 없음"));
                }
            }
            if(errors.Count>0)throw new Exception(string.Join("\n",errors.Distinct()));feedback.text="현재 배치 UI·월드 목표 9점 입력 검사 통과";
        }
        void DrawGeometry(SceneView scene)
        {
            if(!geometry||!battle||!EditorApplication.isPlaying||!battle.EditorCode.IsOpen)return;
            foreach(var block in battle.EditorCode.Panel.Program.AllBlocks()){
                var shape=block.GetComponent<CommandBlockShape>();if(!shape)continue;var triangles=shape.EditorTriangles;Handles.color=Color.cyan;
                for(int i=0;i+2<triangles.Count;i+=3){var a=shape.transform.TransformPoint(triangles[i].position);var b=shape.transform.TransformPoint(triangles[i+1].position);var c=shape.transform.TransformPoint(triangles[i+2].position);Handles.DrawLine(a,b);Handles.DrawLine(b,c);Handles.DrawLine(c,a);}}
            var target=battle.EditorCode.Panel.EditorSnapTarget;
            if(target){var corners=new Vector3[4];((RectTransform)target.transform).GetWorldCorners(corners);Handles.color=Color.yellow;Handles.DrawLine(corners[0],corners[1]);Handles.DrawLine(corners[1],corners[2]);Handles.DrawLine(corners[2],corners[3]);Handles.DrawLine(corners[3],corners[0]);Handles.Label(corners[1],"현재 스냅 후보");}
        }
    }
}
