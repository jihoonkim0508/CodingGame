import json
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
# Docs/flow_stage1 JSON is the dialogue source; the editor mirrors it into the Unity TextAssets.
sources = sorted((root / "Docs/flow_stage1").glob("*.json"))
assert len(sources) == 5
sections = 0
dialogues = 0
for wave, source in enumerate(sources, 1):
    data = json.loads(source.read_text(encoding="utf-8"))
    assert data["stage_id"] == f"1-{wave}"
    assert data["implementation_contract"]["stage_order"] == [f"1-{n}" for n in range(1, 6)]
    assert data["implementation_contract"]["preserve_terms"] == ["damage", "range", "enemyDistance", "attackDistance", "if", "<=", "Attack()"]
    assert len(data["sections"]) == (8 if wave == 5 else 5)
    markdown = source.with_suffix(".md").read_text(encoding="utf-8")
    imported = root / "Assets/!_Project/Defense/Tutorial/Stage1" / source.name
    assert source.read_bytes() == imported.read_bytes(), f"Unity copy out of date (open Unity or run CodingGame > Stage 1 대사 JSON 동기화): {source.name}"
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
