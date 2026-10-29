"""Compose the verified Unity renders without altering the rendered assets."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 22)
sets = {
    'UnityCharacterMaterials.png': [
        ('Monster', 'Play_MonsterWalk.png'),
        ('Melee robot', 'Play_MeleeAttack.png'),
        ('Ranged robot', 'Play_RangedAttack.png'),
    ],
    'UnityMonsterDissolve.png': [
        ('Dissolve 0', 'Play_MonsterWalk.png'),
        ('Dissolve 0.5', 'Play_MonsterDissolve.png'),
        ('Dissolve 1', 'Play_MonsterDissolveComplete.png'),
    ],
}
for output, entries in sets.items():
    sheet = Image.new('RGB', (1200, 440), (20, 28, 40))
    draw = ImageDraw.Draw(sheet)
    for column, (label, file) in enumerate(entries):
        image = Image.open(ROOT / 'Reports~' / file).convert('RGB')
        sheet.paste(image.resize((400, 400), Image.Resampling.LANCZOS), (column * 400, 40))
        draw.text((column * 400 + 12, 9), label, fill='white', font=font)
    sheet.save(ROOT / 'Previews~' / output)
print('UNITY_RENDER_SHEETS_COMPLETE')
