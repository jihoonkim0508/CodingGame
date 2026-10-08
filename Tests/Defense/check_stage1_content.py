import json
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
# The Unity asset is the single source; Docs keeps only the readable Markdown.
sources = sorted((root / "Assets/!_Project/Defense/Tutorial/Stage1").glob("*.json"))
assert len(sources) == 5
sections = 0
dialogues = 0
for wave, source in enumerate(sources, 1):
    data = json.loads(source.read_text(encoding="utf-8"))
    assert data["stage_id"] == f"1-{wave}"
    assert data["implementation_contract"]["stage_order"] == [f"1-{n}" for n in range(1, 6)]
    assert data["implementation_contract"]["preserve_terms"] == ["damage", "range", "enemyDistance", "attackDistance", "if", "<=", "Attack()"]
    assert len(data["sections"]) == (8 if wave == 5 else 5)
    markdown = (root / "Docs/stage1_flow" / source.with_suffix(".md").name).read_text(encoding="utf-8")
    for section in data["sections"]:
        assert section["section_title"] in markdown
        assert section["direction"] in markdown
        for dialogue in section["dialogues"]:
            assert dialogue["speaker"] and dialogue["text"] in markdown
            dialogues += 1
        for example in section["code_examples"]:
            assert example["code"] in markdown
        sections += 1
assert sections == 28
generated = (Path(tempfile.gettempdir()) / "CodingGame-StageOne-generated.py").read_text(encoding="utf-8")
hits = []
class Enemy:
    def __init__(self, distance): self.distance = distance
    def get_distance(self, x, y): return self.distance
target = None
scope = {"get_nearest_enemy": lambda: target, "get_pos_x": lambda: 0, "get_pos_y": lambda: 0,
         "attack": lambda enemy: hits.append(enemy)}
exec(compile(generated, "StageOne-generated.py", "exec"), scope)
scope["tutorial"]()
assert not hits
target = Enemy(15)
scope["tutorial"]()
assert not hits
target = Enemy(10)
scope["tutorial"]()
assert hits == [target]
print(f"PASS: 5 JSON/Markdown pairs, {sections} sections, {dialogues} dialogues, exact imported content, contract and learning terms")
print("PASS: actual generated Python, absent/far/boundary enemy, native distance and attack API")
