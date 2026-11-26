using System;
using System.Collections.Generic;

namespace CodingGame.BlockCoding
{
    public enum BlockKind
    {
        Attack, Wait, While, If, Elif, Else, NearestEnemy, Distance,
        PositionX, PositionY, Number, Comparison, True, False, DeclareVariable, Variable,
        For, Break, Continue, Slash, Block, Boom, Shot, Slow, Buff, Hp, MyAttackRange, MyPos,
        IsDead, Arithmetic,
        EnemyTypeOf, // enemy.type: a member of an enemy variable (receiver in Arguments[0])
        EnemyTypeName, // Enemy.Normal / Enemy.Fast / Enemy.Tank: Value is the name
        ForEach, // for e in get_enemies(): Value is the loop variable's name, Arguments[0] the list
        GetEnemies, // get_enemies(): the active enemies, front of the road first (only as a for-each's list)
        Shield, // enemy.shield: a member like hp
        BreakShield, // break_shield(enemy): the shooter's second action
        Front, Top, // enemy.front() / enemy.top(): a centipede's hittable end as an Enemy (queue: first in, stack: last in)
        Size, // enemy.size: how many segments its centipede has (1 for any other enemy)
        // Scenes and assets store these as ints: append only. 39..41 came with the reflector, 42..45 with the tree legion
        // (they were 39..42 on that branch; the merge rewrote its scene/asset ints, see BlockKindOrderCheck).
        And, // a and b: two conditions
        Reflecting, // enemy.reflecting: a member condition like is_dead() (a Reflector bouncing hits back)
        MyHp, // my_hp(): the robot's own health
        Left, Right, // enemy.left / enemy.right: a tree node's child as an Enemy, None when it has none (or it died)
        Or, // a or b: two conditions
        WhileCondition, // while <condition>: a loop that tests its condition before every turn
        // The team (stage 4): lists, dicts and robots as objects. A list or dict made on the team board is one object every
        // robot's code shares (DefenseTeamBoard).
        EmptyList, // []: a new empty list
        DictLiteral, // {k: v, k: v, k: v}: three key/value holes; a pair left empty is left out
        Append, // x.append(v): statement, receiver in Arguments[0]
        Pop, // x.pop(0): a list's member, takes its first item out
        Len, // len(x)
        In, // v in x / v not in x: Value is "in" or "not in"
        Not, // not c
        Index, // d[k]: a dict's value (or a list's item)
        GetRobots, // get_robots(): the placed robots
        Role, // r.role: a robot's member
        RobotRoleName, // Robot.Melee / Robot.Ranged / Robot.Tank / Robot.Utility: Value is the name
        BuffRobot, // retired with Buff (2026-11-26): kept for the ints after it, nothing makes or compiles either
        WeakPoint // e.weak_point(): the part of the enemy that can be hit now
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
