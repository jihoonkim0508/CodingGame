"""The tutorial's pointer as a UI sticker: a chunky down arrow (wide head on a short shaft, corners rounded) filled white
(tinted by Image.color) with the ink outline of ui_sprites.py and no shadow: TutorialPointer turns it to point up or down
and lays its own shadow under it, so the shadow always falls downward. pointer_sticker.py <out ui_pointer.png>"""
import sys
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
INK = (38, 42, 62)
size, line, k = 128, 7, 4  # px; k = supersampling
round_px = 7

# Shaft 40 wide, head 100 wide; the tip at the bottom.
shaft = [(44, 12), (84, 12), (84, 58), (44, 58)]
head = [(14, 52), (114, 52), (64, 114)]
big = Image.new("L", (size * k, size * k), 0)
draw = ImageDraw.Draw(big)
for poly in (shaft, head):
    draw.polygon([(x * k, y * k) for x, y in poly], fill=255)
# Round the corners: blur, then cut at half (convex corners round off, the shaft-head notch fills a little).
big = big.filter(ImageFilter.GaussianBlur(round_px * k)).point(lambda v: 255 if v >= 128 else 0)
mask = big.resize((size, size), Image.LANCZOS)
outline = mask.filter(ImageFilter.MaxFilter(2 * line + 1))  # the shape grown by the line width

img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
img.paste(Image.new("RGBA", (size, size), INK + (255,)), (0, 0), outline)
img.paste(Image.new("RGBA", (size, size), (255, 255, 255, 255)), (0, 0), mask)
img.save(out)
print(out, img.size)
