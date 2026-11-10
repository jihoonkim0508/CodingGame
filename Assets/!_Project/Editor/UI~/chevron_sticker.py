"""The tutorial's road arrow as a UI sticker: an open chevron (›) with round ends, filled white (tinted by Image.color), with
the same ink outline and drop shadow as ui_sprites.py. Points right. chevron_sticker.py <out ui_chevron.png>"""
import sys
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
INK = (38, 42, 62)
size, line, shadow, k = 96, 6, 6, 4  # px; k = supersampling
stroke = 22
points = [(30, 16), (66, 48), (30, 80)]

big = Image.new("L", (size * k, size * k), 0)
draw = ImageDraw.Draw(big)
draw.line([(x * k, y * k) for x, y in points], fill=255, width=stroke * k, joint="curve")
for x, y in points:  # round caps and joint
    r = stroke * k / 2
    draw.ellipse((x * k - r, y * k - r, x * k + r, y * k + r), fill=255)
mask = big.resize((size, size), Image.LANCZOS)
outline = mask.filter(ImageFilter.MaxFilter(2 * line + 1))  # the shape grown by the line width

img = Image.new("RGBA", (size, size + shadow), (0, 0, 0, 0))
img.paste(Image.new("RGBA", (size, size), INK + (77,)), (0, shadow), outline)
img.paste(Image.new("RGBA", (size, size), INK + (255,)), (0, 0), outline)
img.paste(Image.new("RGBA", (size, size), (255, 255, 255, 255)), (0, 0), mask)
img.save(out)
print(out, img.size)
