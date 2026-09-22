"""Execute the actual C#-generated nested-block output with Python 3 API stubs."""
import ast
import json
from pathlib import Path
import sys
import tempfile

path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(tempfile.gettempdir()) / "CodingGame-BlockCoding-check.json"
programs = json.loads(path.read_text(encoding="utf-8"))
events = []
position = [0]


class Enemy:
    def get_distance(self, x, y):
        return ((3 - x) ** 2 + (4 - y) ** 2) ** 0.5


enemy = Enemy()


def wait(seconds):
    events.append(("wait", seconds))
    position[0] += 1


scope = {
    "get_nearest_enemy": lambda: enemy,
    "get_pos_x": lambda: position[0],
    "get_pos_y": lambda: 0,
    "attack": lambda target: events.append(("attack", target)),
    "wait": wait,
}
for name, source in programs.items():
    if name.startswith("tree_"):
        ast.parse(source)
        exec(compile(source, "<nested blocks>", "exec"), scope)

expected = {
    "tree_loop": [("wait", 2), ("wait", 3)],
    "tree_enemy": [("attack", enemy)],
    "tree_while": [("wait", 0.25)],
    "tree_nested_break": [("wait", 0.25)] * 2,
    "tree_paths": [("attack", enemy)],
    "tree_empty": [],
    "tree_empty_body": [],
    "tree_chain": [("wait", 7)],
    "tree_unicode": [("attack", enemy)],
}
for name, result in expected.items():
    events.clear()
    position[0] = 0
    scope[name]()
    assert events == result, (name, events, result)

print("PASS: generated Python 3 syntax and execution for nested loops, break/continue, 64 elif branches, methods and variables.")
