"""Run after dotnet run --project Tests/BlockCoding/BlockCoding.Check.csproj."""
import ast
import json
import math
from pathlib import Path
import sys
import tempfile

path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(tempfile.gettempdir()) / "CodingGame-BlockCoding-check.json"
programs = json.loads(path.read_text(encoding="utf-8"))
events = []
position = [0.0, 0.0]


class Enemy:
    def __init__(self, x, y):
        self.x, self.y = x, y

    def get_distance(self, x, y):
        return math.hypot(self.x - x, self.y - y)


enemy = Enemy(3.0, 4.0)


def wait(seconds):
    events.append(("wait", seconds))
    position[0] += 1


scope = {
    "get_nearest_enemy": lambda: enemy,
    "get_pos_x": lambda: position[0],
    "get_pos_y": lambda: position[1],
    "attack": lambda target: events.append(("attack", target)),
    "wait": wait,
}
for source in programs.values():
    ast.parse(source)
    exec(compile(source, "<generated block program>", "exec"), scope)

assert enemy.get_distance(0, 0) == 5.0
scope["branch"]()
assert events.pop() == ("attack", enemy)
enemy.x = 100
position[0] = 2
scope["branch"]()
assert events.pop() == ("wait", 0.25)
position[0] = 0
scope["branch"]()
assert events.pop() == ("wait", 1)
position[0] = 0
for name in ("loop", "false_loop", "after_loop"):
    try:
        scope[name]()
    except RuntimeError as error:
        assert str(error) == "while condition is false"
    else:
        raise AssertionError(f"{name} must raise when the condition is false")
assert events == [("wait", 0.25), ("wait", 0.25)]
scope["empty"]()
events.clear()
scope["variables"]()
assert events == [("attack", enemy), ("wait", 0.5)]
events.clear()
scope["variable_condition"]()
assert events == [("wait", 0.25)]
print("PASS: actual generated Python 3 syntax, all branches, nested suites, false and changing while conditions.")
