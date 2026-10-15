namespace CodingGame.Defense
{
    // Names shared by tutorial data (TutorialStep.completionSignal / trigger), the game hooks that report them,
    // and TutorialWatcher. Keep spellings in sync with the sequence assets.
    public static class TutorialSignals
    {
        // Reported by DefenseBattle / DefenseCodeEditor / DefenseAttackPreview through ReportTutorialAction.
        public const string TargetClicked = "TargetClicked", RobotPlaced = "RobotPlaced", EditorOpened = "EditorOpened",
            EditorClosed = "EditorClosed", BattleStarted = "BattleStarted", PreviewHit = "PreviewHit";
        // Produced by TutorialWatcher from the live simulation.
        public const string FirstKill = "FirstKill", FirstAttack = "FirstAttack", Leak = "Leak", WaveCleared = "WaveCleared", ProgramMatches = "ProgramMatches";
        // Produced by TutorialDirector: a loose condition block in the editor reads as the step's expectedCode.
        public const string ConditionBuilt = "ConditionBuilt";
        // Speaker whose lines show as the system alert banner instead of the dialogue box.
        public const string SystemSpeaker = "시스템";
    }

    // Target keys bound in TutorialInputBridge (scene) and checked by DefenseBattle.TutorialAllows.
    public static class TutorialKeys
    {
        public const string RewardPanel = "Battle.RewardPanel";
        public const string Start = "Battle.Start", Develop = "Battle.Develop", Deployment = "Battle.DeploymentArea", SelectedRobot = "Robot.Selected",
            DropZone = "Editor.ProgramDropZone", Palette = "Editor.BlockPalette", Close = "Editor.Close", Clear = "Editor.RestoreAutomatic",
            Hint = "Editor.Hint", Preview = "Editor.Preview", CoreHealth = "Battle.CoreHealth", Route = "Battle.Route";
        public const string CodePrefix = "Editor.Code.", PalettePrefix = "Editor.Palette.";
        const string UnitPrefix = "Robot.Unit.", RangePrefix = "Robot.Range.";
        public static string RobotButton(int index) => "Battle.Robot." + index;
        // The index-th placed robot (by id, from 0) and its attack range circle; DefenseBattle registers them.
        public static string Unit(int index) => UnitPrefix + index;
        public static string Range(int index) => RangePrefix + index;
        // World targets the game registers at run time; they need no scene binding.
        public static bool IsRuntime(string key) => key != null && (key == Route || key.StartsWith(UnitPrefix) || key.StartsWith(RangePrefix));
        // Positions inside the code editor (Editor.Code.*, Editor.Palette.*), resolved by DefenseCodeEditor each frame.
        public static bool IsEditorArea(string key) => key != null && (key.StartsWith(CodePrefix) || key.StartsWith(PalettePrefix));
    }
}
