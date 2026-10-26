namespace CodingGame.Defense
{
    // Names shared by tutorial data (TutorialStep.completionSignal / trigger), the game hooks that report them,
    // and TutorialWatcher. Keep spellings in sync with the sequence assets.
    public static class TutorialSignals
    {
        // Reported by DefenseBattle / DefenseCodeEditor / DefenseAttackPreview through ReportTutorialAction.
        public const string TargetClicked = "TargetClicked", RobotPlaced = "RobotPlaced", EditorOpened = "EditorOpened",
            EditorClosed = "EditorClosed", BattleStarted = "BattleStarted", PreviewHit = "PreviewHit",
            PreviewSingleHit = "PreviewSingleHit", PreviewMultiHit = "PreviewMultiHit";
        // Produced by TutorialWatcher from the live simulation.
        public const string FirstKill = "FirstKill", FirstAttack = "FirstAttack", Leak = "Leak", WaveCleared = "WaveCleared", ProgramMatches = "ProgramMatches";
        // Produced by TutorialDirector: a loose condition block in the editor reads as the step's expectedCode.
        public const string ConditionBuilt = "ConditionBuilt";
        // Produced by TutorialDirector: the editor slot named by the step's expectedCode (an Editor.Code.Arg.* key) holds a block,
        // before the line compiles (1-1 D04 places the variable block first, the name is typed in the next step).
        public const string SlotFilled = "SlotFilled";
        // Also checked on the draft: the slot was clicked (SlotSelected), a name field or comparison picker holds a value
        // (DraftValueMatches, expectedCode "Editor.Code.Name.….|value"). 1-1 and 1-3 teach one small action per line.
        public const string SlotSelected = "SlotSelected", DraftValueMatches = "DraftValueMatches";
        // Speaker whose lines show as the system alert banner instead of the dialogue box.
        public const string SystemSpeaker = "시스템";
    }

    // Target keys bound in TutorialInputBridge (scene) and checked by DefenseBattle.TutorialAllows.
    public static class TutorialKeys
    {
        public const string Start = "Battle.Start", Pause = "Battle.Pause", Speed = "Battle.ToggleSpeed", Develop = "Battle.Develop", Deployment = "Battle.DeploymentArea", SelectedRobot = "Robot.Selected",
            DropZone = "Editor.ProgramDropZone", Palette = "Editor.BlockPalette", Close = "Editor.Close", RestoreAutomatic = "Editor.RestoreAutomatic",
            Hint = "Editor.Hint", Preview = "Editor.Preview", CoreHealth = "Battle.CoreHealth", Route = "Battle.Route", RewardPanel = "Battle.RewardPanel", NextWave = "Battle.NextWave",
            Floor = "Battle.Floor";
        public const string CodePrefix = "Editor.Code.", PalettePrefix = "Editor.Palette.";
        const string UnitPrefix = "Robot.Unit.", RangePrefix = "Robot.Range.";
        public const string RobotButtonPrefix = "Battle.Robot.";
        public static string RobotButton(int index) => RobotButtonPrefix + index;
        // The index-th placed robot (by id, from 0, any role) and its attack range circle; DefenseBattle registers them.
        // TutorialStep.robotIndex counts warriors only; the two agree while Stage 1 has only warriors.
        public static string Unit(int index) => UnitPrefix + index;
        public static string Range(int index) => RangePrefix + index;
        public static bool IsRange(string key) => key != null && key.StartsWith(RangePrefix, System.StringComparison.Ordinal);
        // World targets the game registers at run time; they need no scene binding.
        public static bool IsRuntime(string key) => key != null && (key == Route || key == Floor || key.StartsWith(UnitPrefix) || key.StartsWith(RangePrefix));
        // Positions inside the code editor (Editor.Code.*, Editor.Palette.*), resolved by DefenseCodeEditor each frame.
        public static bool IsEditorArea(string key) => key != null && (key.StartsWith(CodePrefix) || key.StartsWith(PalettePrefix));
    }
}
