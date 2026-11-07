"""The stage map's bouncing arrow as a UI sticker: Kenney's arrowDown (Game Icons, CC0, opengameart.org/content/game-icons)
filled white (tinted by GUI.color), with the same ink outline and drop shadow as ui_sprites.py.
arrow_sticker.py <Kenney PNG/Black/2x/arrowDown.png> <out ui_arrow.png>"""
import sys
from PIL import Image, ImageFilter

src, out = sys.argv[1], sys.argv[2]
INK = (38, 42, 62)
size, line, shadow = 128, 7, 7
shape = Image.open(src).convert("RGBA").getchannel("A")
shape = shape.resize((size - 2 * line, size - 2 * line), Image.LANCZOS)
mask = Image.new("L", (size, size), 0); mask.paste(shape, (line, line))
outline = mask.filter(ImageFilter.MaxFilter(2 * line + 1))  # the shape grown by the line width

img = Image.new("RGBA", (size, size + shadow), (0, 0, 0, 0))
img.paste(Image.new("RGBA", (size, size), INK + (77,)), (0, shadow), outline)
img.paste(Image.new("RGBA", (size, size), INK + (255,)), (0, 0), outline)
img.paste(Image.new("RGBA", (size, size), (255, 255, 255, 255)), (0, 0), mask)
img.save(out)
print(out, img.size)
