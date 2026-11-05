using System.Linq;

namespace CodingGame.Defense
{
    public sealed partial class TutorialDirector
    {
        public const string StageTwoProgram = "StageTwoProgram", StageTwoObservation = "StageTwoObservation",
            StageTwoShot = "StageTwoShot", StageTwoImpact = "StageTwoImpact";
        public const string TeachingUnit = "Robot.Unit.Teaching", TeachingRange = "Robot.Range.Teaching";
        public RobotState StageTwoRobot => session?.Robots.Where(r => r.Spec.role ==
            (session.WaveIndex == 2 || session.WaveIndex == 3 ? RobotRole.Shooter : RobotRole.Warrior)).OrderByDescending(r => r.Id).FirstOrDefault();
        bool ObservationWaiting => Current?.completionSignal == StageTwoObservation &&
            !battle.AttackPreview.ObservationReady(Current.previewIteration);
        void TickStageTwo(TutorialStep step)
        {
            if (!active || active.stage != 2) return;
            if (step.stageTwoRestartOnly && !battle.StageTwoRestarted) { Enter(progressStep + 1); return; }
            var preview = battle.AttackPreview;
            var robot = StageTwoRobot;
            if (step.completionSignal == TutorialSignals.StageTwoAttackPlaced && Editor.IsOpen && Editor.EditingId == robot?.Id &&
                Editor.Panel.Program.StartChain().FirstOrDefault()?.Body is CodingGame.BlockCoding.CommandDropZone body &&
                body.Blocks.Count >= 2 && (body.Blocks[1].BaseKind == CodingGame.BlockCoding.BlockKind.Shot || body.Blocks[1].BaseKind == CodingGame.BlockCoding.BlockKind.Attack))
                Signal(TutorialSignals.StageTwoAttackPlaced);
            if (step.completionSignal == StageTwoProgram && robot != null && Editor.IsOpen && Editor.EditingId == robot.Id && Editor.Panel.IsApplied &&
                StageTwoLearning.Matches(robot.Program?.CopyBlocks(), robot.Spec.role, step.expectedCode == "3" ? 3 : 0)) Signal(StageTwoProgram);
            if (step.completionSignal == StageTwoObservation && preview.IsShowing && Editor.EditingId == robot?.Id)
            {
                if (step.kind == TutorialStepKind.Dialogue && viewedStep == progressStep && view.IsVisible)
                    view.SetNavigation(viewedStep > 0, !ObservationWaiting);
                else if (preview.ObservationReady(step.previewIteration)) Signal(StageTwoObservation);
            }
            if (step.completionSignal == StageTwoShot && preview.IsShowing && Editor.EditingId == robot?.Id && preview.PreviewShotSeen) Signal(StageTwoShot);
            if (step.completionSignal == StageTwoImpact && preview.IsShowing && Editor.EditingId == robot?.Id && preview.PreviewImpactSeen) Signal(StageTwoImpact);
            if (Current == step && step.completionSignal == StageTwoObservation && preview.ObservationProgram?.ObservedPath is string path)
                bridge.SetFocus((step.texts[0].focusKeys ?? System.Array.Empty<string>()).Concat(new[] { path }).ToArray());
        }
    }
}
