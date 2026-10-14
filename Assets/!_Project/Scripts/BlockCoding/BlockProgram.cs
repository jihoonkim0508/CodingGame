using System;
using System.Collections.Generic;

namespace CodingGame.BlockCoding
{
    public enum BlockKind
    {
        Attack, Wait, While, If, Elif, Else, NearestEnemy, Distance,
        PositionX, PositionY, Number, Comparison, True, False, DeclareVariable, Variable,
        For, Break, Continue, Slash, Block, Boom, Shot, Slow, Buff, Hp, MyAttackRange, MyPos
    }

    [Serializable]
    public sealed class CodeBlock
    {
        public BlockKind Kind;
        public string Value = "0";
        public List<CodeBlock> Arguments = new List<CodeBlock>();
        public List<CodeBlock> Body = new List<CodeBlock>();

        public CodeBlock(BlockKind kind, string value = "0", params CodeBlock[] arguments)
        {
            Kind = kind;
            Value = value;
            Arguments.AddRange(arguments);
        }
    }
}
