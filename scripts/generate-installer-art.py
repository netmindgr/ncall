"""Generate the installer images (GPL-3.0-or-later, netmind 2026).

Side panel: netmind navy with "nCall by netmind." Small image: the app icon.
Several sizes are produced so Inno Setup can pick one per display scaling.

    python scripts/generate-installer-art.py
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
FONTS = os.path.join(ROOT, 'source', 'res', 'fonts')
OUT = os.path.join(ROOT, 'branding')

NAVY_TOP, NAVY_MID, NAVY_BOTTOM = (0x12, 0x23, 0x3f), (0x0b, 0x16, 0x26), (0x07, 0x0a, 0x12)
BLUE, WHITE, MUTED = (0x3b, 0x82, 0xf6), (0xff, 0xff, 0xff), (0x93, 0xa1, 0xb8)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def side(width, height):
    s = width / 164.0
    img = Image.new('RGB', (width, height))
    px = img.load()
    for y in range(height):
        t = y / (height - 1)
        c = lerp(NAVY_TOP, NAVY_MID, t * 2) if t < 0.5 else lerp(NAVY_MID, NAVY_BOTTOM, (t - 0.5) * 2)
        for x in range(width):
            px[x, y] = c
    # Soft blue glow behind the name.
    glow = Image.new('L', (width, height), 0)
    ImageDraw.Draw(glow).ellipse((-int(60 * s), int(40 * s), int(150 * s), int(220 * s)), fill=70)
    glow = glow.filter(ImageFilter.GaussianBlur(int(40 * s)))
    img = Image.composite(Image.new('RGB', (width, height), BLUE), img, glow)

    d = ImageDraw.Draw(img)
    name = ImageFont.truetype(os.path.join(FONTS, 'OpenSans-600.ttf'), int(34 * s))
    word = ImageFont.truetype(os.path.join(FONTS, 'Comfortaa-700.ttf'), int(13 * s))
    small = ImageFont.truetype(os.path.join(FONTS, 'OpenSans-400.ttf'), int(10.5 * s))
    x, y = int(18 * s), int(96 * s)
    d.text((x, y), 'n', font=name, fill=BLUE)
    d.text((x + d.textlength('n', font=name), y), 'Call', font=name, fill=WHITE)
    y += int(46 * s)
    d.text((x + 1, y), 'by ', font=small, fill=MUTED)
    bx = x + 1 + d.textlength('by ', font=small)
    d.text((bx, y - int(2 * s)), 'netmind', font=word, fill=WHITE)
    d.text((bx + d.textlength('netmind', font=word), y - int(2 * s)), '.', font=word, fill=BLUE)
    d.text((x, height - int(46 * s)), 'Τηλεφωνία SIP', font=small, fill=MUTED)
    d.text((x, height - int(32 * s)), 'για Windows', font=small, fill=MUTED)
    return img


def small_image(size):
    img = Image.new('RGB', size, WHITE)
    icon = Image.open(os.path.join(OUT, 'app-icon.png')).convert('RGBA')
    edge = int(min(size) * 0.86)
    icon = icon.resize((edge, edge), Image.LANCZOS)
    img.paste(icon, ((size[0] - edge) // 2, (size[1] - edge) // 2), icon)
    return img


for scale, (w, h) in {100: (164, 314), 150: (246, 459), 200: (328, 604)}.items():
    side(w, h).save(os.path.join(OUT, 'installer-side-%d.bmp' % scale))
for scale, size in {100: (55, 55), 150: (83, 80), 200: (110, 106)}.items():
    small_image(size).save(os.path.join(OUT, 'installer-small-%d.bmp' % scale))
print('ok')
