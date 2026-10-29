from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation/Previews~')
paths=sorted((root/'Melee_Attack').glob('*.png'))
frames=[Image.open(path).convert('RGB') for path in paths]
palette=frames[0].quantize(colors=240)
frames[0].quantize(palette=palette).save(root/'Melee_Attack.gif',save_all=True,append_images=[f.quantize(palette=palette) for f in frames[1:]],duration=[67]*(len(frames)-1)+[350],loop=0,disposal=2)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',19)
sheet=Image.new('RGB',(1200,540),(20,28,40));draw=ImageDraw.Draw(sheet)
for column,(number,label) in enumerate([(1,'Ready'),(9,'Windup'),(13,'Slash'),(17,'Follow through'),(23,'Recover')]):
    picture=Image.open(root/'Melee_Attack'/f'{number:03d}.png').convert('RGB').resize((240,240),Image.Resampling.LANCZOS)
    sheet.paste(picture,(column*240,30));draw.text((column*240+8,5),label,fill='white',font=font)
# A second row shows the Unity shader at the corresponding attack poses.
for column,(number,label) in enumerate([(0,'Unity / ready'),(27,'Windup'),(40,'Slash'),(50,'Follow through'),(63,'Recover')]):
    path=root/f'MeleeSwing_{number:03d}.png'
    if path.exists():
        picture=Image.open(path).convert('RGB').resize((240,240),Image.Resampling.LANCZOS)
        sheet.paste(picture,(column*240,300));draw.text((column*240+8,275),label,fill='white',font=font)
sheet.save(root/'MeleeSwingContactSheet.jpg',quality=93)
print('MELEE_SWING_PREVIEWS_COMPLETE')
