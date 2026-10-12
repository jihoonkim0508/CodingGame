using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CodingGame.BlockCoding
{
    public enum BlockKind
    {
        Attack, Wait, While, If, Elif, Else, NearestEnemy, Distance,
        PositionX, PositionY, Number, Comparison, True, False, DeclareVariable, Variable,
        For, Break, Continue, Slash, Block, Boom, Shot, Slow, Buff
    }

    [Serializable]
    public sealed class CodeBlock
    {
        public BlockKind Kind;
        public string Value = "0";
        public List<CodeBlock> Arguments = new List<CodeBlock>();
        // Nested command blocks use Body.
        public List<CodeBlock> Body = new List<CodeBlock>();

        public CodeBlock(BlockKind kind, string value = "0", params CodeBlock[] arguments)
        {
            Kind = kind;
            Value = value;
            Arguments.AddRange(arguments);
        }
    }

    /// <summary>Python literal helpers shared by the command-block compiler and UI.</summary>
    public static class PythonBlockCompiler
    {
        public static readonly string[] Comparisons = { "==", "<", ">", "<=", ">=" };
        static readonly Regex Number = new Regex(@"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z");

        public static string NormalizeNumber(string text)
        {
            if (string.IsNullOrEmpty(text) || !Number.IsMatch(text)) throw new FormatException("올바른 숫자를 입력해 주세요.");
            // Keep arbitrary-length input as text; parsing as float would lose precision or impose a range.
            string sign = text[0] == '-' ? "-" : "";
            string unsigned = text.TrimStart('+', '-');
            int end = unsigned.IndexOfAny(new[] { '.', 'e', 'E' });
            if (end < 0) end = unsigned.Length;
            string integer = unsigned.Substring(0, end).TrimStart('0');
            return sign + (integer.Length == 0 ? "0" : integer) + unsigned.Substring(end);
        }
    }
}
