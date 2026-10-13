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
        public const string FirstKill = "FirstKill", Leak = "Leak", WaveCleared = "WaveCleared", ProgramMatches = "ProgramMatches";
        // Speaker whose lines show as the system alert banner instead of the dialogue box.
        public const string SystemSpeaker = "시스템";
    }

    // Target keys bound in TutorialInputBridge (scene) and checked by DefenseBattle.TutorialAllows.
    public static class TutorialKeys
    {
        public const string Start = "Battle.Start", Develop = "Battle.Develop", Deployment = "Battle.DeploymentArea", SelectedRobot = "Robot.Selected",
            DropZone = "Editor.ProgramDropZone", Palette = "Editor.BlockPalette", Close = "Editor.Close", Clear = "Editor.RestoreAutomatic",
            Hint = "Editor.Hint", Preview = "Editor.Preview";
        public static string RobotButton(int index) => "Battle.Robot." + index;
    }
}
