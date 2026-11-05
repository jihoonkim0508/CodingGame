using System.Linq;
using TMPro;
using UnityEngine;

namespace CodingGame.Defense
{
    public sealed partial class DefenseAttackPreview
    {
        bool observing, observationRunning;
        int commandsBeforeStep;
        int requestedIteration;
        double settleUntil;
        public bool PreviewShotSeen { get; private set; }
        public bool PreviewImpactSeen { get; private set; }
        public DefenseProgram ObservationProgram => simulation?.Robots.FirstOrDefault()?.Program;
        public void ObserveOnce()
        {
            if (!IsShowing) return;
            observing = true; requestedIteration = 0; ResetPreview();
        }
        public void ObserveUntil(int iteration, bool restart = false)
        {
            if (restart || !observing) ObserveOnce();
            requestedIteration = iteration;
        }
        public bool ObservationReady(int iteration)
        {
            var program = ObservationProgram;
            return observing && !observationRunning && program != null &&
                (iteration > 0 ? program.FinishedIterations >= iteration : program.ObservationFinished);
        }
        public void EndObservation()
        {
            if (!observing) return;
            observing = false;
            if (IsShowing) ResetPreview();
        }
        void NextObservationCommand()
        {
            if (!observing || observationRunning) return;
            var program = ObservationProgram;
            if (program == null || program.ObservationFinished) return;
            commandsBeforeStep = program.ObservedCommands; settleUntil = 0;
            program.AdvanceObservation(); observationRunning = true;
        }
        void TickObservation(ref float dt)
        {
            if (!observing) return;
            var program = ObservationProgram;
            if (program == null || program.Fault != null) { observationRunning = false; dt = 0; return; }
            if (battle.CodeEditor.HintOpen) { dt = 0; return; }
            if (!observationRunning)
            {
                if (ObservationReady(requestedIteration)) { dt = 0; return; }
                NextObservationCommand();
            }
            if (program.ObservedCommands > commandsBeforeStep && settleUntil == 0)
                settleUntil = simulation.Time + System.Math.Max(1, selected.Interval);
            if (settleUntil > 0 && simulation.Time >= settleUntil)
            { observationRunning = false; dt = 0; }
        }
        void RefreshObservation()
        {
            var program = ObservationProgram;
            if (program == null) { status.text = "블록을 연결하면 실행 순서를 확인할 수 있어요."; return; }
            var bot = simulation.Robots[0];
            string round = program.IterationTotal > 0 ? $"회차 {program.Iteration}/{program.IterationTotal} · " : "";
            string action = program.ObservedKind == CodingGame.BlockCoding.BlockKind.DeclareVariable ? "적 찾기 → 저장" : "공격 명령";
            if (program.ObservedCommands == 0) action = "실행 준비";
            if (program.ObservationFinished) action = "프로그램 종료";
            status.text = $"{round}{action}\n공격 실행 {bot.AttacksThisWave}회 · 처치 {simulation.Kills} · {program.Fault ?? "실행 흐름을 살펴보세요"}";
        }
    }
}
