using System;
using System.Linq;
using UnityEngine;

namespace CodingGame.Defense
{
    [Serializable] public sealed class StageOneDialogue { public string speaker, text; }
    [Serializable] public sealed class StageOneExample { public string label, language, code; }
    [Serializable] public sealed class StageOneInteraction { public string prompt, player_action, expected_input, success_feedback, retry_hint; }
    [Serializable] public sealed class StageOneSection
    {
        public string section_title, direction;
        public StageOneDialogue[] dialogues;
        public StageOneExample[] code_examples;
        public StageOneInteraction interaction;
    }
    [Serializable] public sealed class StageOneContract { public string[] stage_order, preserve_terms; public string direction, progression, code_binding; }
    [Serializable] public sealed class StageOneContent
    {
        public string format_version, package_id, package_type, locale, title, stage_id;
        public StageOneContract implementation_contract;
        public string[] learning_goals;
        public StageOneSection[] sections;
        public static StageOneContent Read(TextAsset asset, int wave)
        {
            if (!asset) throw new InvalidOperationException("Stage 1 JSON 참조 누락");
            var data = JsonUtility.FromJson<StageOneContent>(asset.text);
            if (data == null || data.format_version != "1.0" || data.stage_id != "1-" + wave || data.package_type != "tutorial_stage_content" ||
                data.locale != "ko-KR" || string.IsNullOrWhiteSpace(data.title) || data.sections == null || data.sections.Length != (wave == 5 ? 8 : 5) ||
                data.implementation_contract?.stage_order == null || !data.implementation_contract.stage_order.SequenceEqual(new[]{"1-1","1-2","1-3","1-4","1-5"}) ||
                data.sections.Any(s => s == null || string.IsNullOrWhiteSpace(s.section_title) || s.dialogues == null || s.dialogues.Length == 0 ||
                    s.dialogues.Any(d => d == null || string.IsNullOrWhiteSpace(d.speaker) || string.IsNullOrWhiteSpace(d.text)) || s.code_examples == null))
                throw new InvalidOperationException(asset.name + ": Stage 1 콘텐츠 형식·순서를 확인하세요.");
            return data;
        }
    }
}
