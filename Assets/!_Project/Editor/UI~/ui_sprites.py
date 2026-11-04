"""The toy-workshop UI sprites: white shapes (tinted by Image.color) with a baked dark-ink outline and drop shadow.
ui_sprites.py <out folder>  -> PNGs plus borders.txt (9-slice borders L,B,R,T in pixels, Unity's order)."""
import os, sys
from PIL import Image, ImageDraw

out = sys.argv[1]
os.makedirs(out, exist_ok=True)
SS = 4  # supersampling
INK = (38, 42, 62)
borders = {}

def finish(name, img, border):
    w, h = img.size
    img = img.resize((w // SS, h // SS), Image.LANCZOS)
    img.save(os.path.join(out, name + ".png"))
    borders[name] = border

def rounded(draw, box, radius, fill):
    draw.rounded_rectangle([v * SS for v in box], radius=radius * SS, fill=fill)

def sticker(name, w, h, radius, line, shadow, lip=0, shadow_alpha=.3):
    img = Image.new("RGBA", (w * SS, (h + shadow) * SS), (0, 0, 0, 0))
    shade = Image.new("RGBA", img.size, (0, 0, 0, 0))
    rounded(ImageDraw.Draw(shade), (0, shadow, w - 1, h - 1 + shadow), radius, INK + (int(255 * shadow_alpha),))
    img = Image.alpha_composite(img, shade)
    d = ImageDraw.Draw(img)
    rounded(d, (0, 0, w - 1, h - 1), radius, INK + (255,))
    inner = (line, line, w - 1 - line, h - 1 - line)
    if lip:
        rounded(d, inner, radius - line, (204, 204, 204, 255))  # tinted, the lip is the colour at 80%
        rounded(d, (inner[0], inner[1], inner[2], inner[3] - lip), radius - line, (255, 255, 255, 255))
    else:
        rounded(d, inner, radius - line, (255, 255, 255, 255))
    finish(name, img, (radius + 4, radius + 4 + shadow + lip, radius + 4, radius + 4))

sticker("ui_card", 96, 96, 20, 4, 6)
sticker("ui_card_flat", 96, 96, 16, 3, 0)          # inner panels: outline, no shadow
sticker("ui_button", 96, 96, 18, 4, 5, lip=7)
sticker("ui_pill", 96, 48, 23, 3, 4)
sticker("ui_chip", 64, 40, 19, 3, 0)               # small tags over the map

# Header and deck bars: flat paper, a thick ink edge towards the map and a shadow beyond it.
def bar(name, top):
    w, h, line, shadow = 16, 40, 5, 8
    img = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    paper = (0, shadow + line, w, h) if top else (0, 0, w, h - shadow - line)
    edge = (0, shadow, w, shadow + line) if top else (0, h - shadow - line, w, h - shadow)
    shade = (0, 0, w, shadow) if top else (0, h - shadow, w, h)
    for i in range(shadow * SS):  # shadow fades away from the edge
        a = int(90 * (i / (shadow * SS)) if top else 90 * (1 - i / (shadow * SS)))
        y = shade[1] * SS + i
        d.line([(0, y), (w * SS, y)], fill=INK + (a,))
    d.rectangle([v * SS for v in edge], fill=INK + (255,))
    d.rectangle([v * SS for v in paper], fill=(255, 255, 255, 255))
    finish(name, img, (2, 2, 2, shadow + line + 2) if top else (2, shadow + line + 2, 2, 2))  # (L, B, R, T)

bar("ui_bar_bottom", True)   # deck at the bottom: its edge faces up
bar("ui_bar_top", False)     # header at the top: its edge faces down

# A round badge for 디버's face.
img = Image.new("RGBA", (128 * SS, 134 * SS), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.ellipse([0, 6 * SS, 127 * SS, 133 * SS], fill=INK + (77,))
d.ellipse([0, 0, 127 * SS, 127 * SS], fill=INK + (255,))
d.ellipse([5 * SS, 5 * SS, 122 * SS, 122 * SS], fill=(255, 255, 255, 255))
finish("ui_circle", img, (0, 0, 0, 0))

# Graph paper for the code area (tiled): faint minor lines, stronger every fourth.
tile, step = 128, 32
img = Image.new("RGBA", (tile, tile), (255, 255, 255, 255))
d = ImageDraw.Draw(img)
for i in range(0, tile, step):
    shade = 214 if i == 0 else 234
    d.line([(i, 0), (i, tile)], fill=(shade, shade, shade, 255), width=2 if i == 0 else 1)
    d.line([(0, i), (tile, i)], fill=(shade, shade, shade, 255), width=2 if i == 0 else 1)
img.save(os.path.join(out, "ui_grid.png"))
borders["ui_grid"] = (0, 0, 0, 0)

with open(os.path.join(out, "borders.txt"), "w") as f:
    for name, b in borders.items(): f.write(f"{name} {' '.join(map(str, b))}\n")
print("\n".join(f"{n} {b}" for n, b in borders.items()))
