#!/usr/bin/env python3
# Draws the tile, logo and splash images in src/EmulatorMK61.WP81/Assets: a small MK-61 - green
# indicator over a 5 x 3 block of keys (yellow F, blue K, the rest grey and black), on transparent
# so the tile takes its background from the manifest (#404040). Needs Pillow.
import os
from PIL import Image, ImageDraw

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "EmulatorMK61.WP81", "Assets")
os.makedirs(out, exist_ok=True)

YELLOW, BLUE, GREY, BLACK, RED, GREEN = "#f5e345", "#71e3ff", "#cccccc", "#111111", "#f00505", "#77ff77"


def calculator(size):
    """The calculator face in a size x size square (100-unit design grid)."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    k = size / 100.0
    r = lambda x0, y0, x1, y1: [x0 * k, y0 * k, x1 * k, y1 * k]
    d.rounded_rectangle(r(14, 6, 86, 94), radius=6 * k, fill="#262626", outline="#888888", width=max(1, int(1.5 * k)))
    d.rectangle(r(21, 13, 79, 31), fill="#103810")
    # "-61" in segment strokes on the indicator.
    w = max(1, int(2.6 * k))
    d.line([(42 * k, 22 * k), (48 * k, 22 * k)], fill=GREEN, width=w)
    d.line([(60 * k, 17 * k), (54 * k, 17 * k), (54 * k, 27 * k), (60 * k, 27 * k), (60 * k, 22 * k), (54 * k, 22 * k)], fill=GREEN, width=w)
    d.line([(68 * k, 17 * k), (68 * k, 27 * k)], fill=GREEN, width=w)
    colours = [[YELLOW, BLACK, BLACK, BLACK, BLACK], [BLUE, BLACK, BLACK, BLACK, BLACK],
               [GREY, GREY, GREY, GREY, GREY], [GREY, GREY, GREY, GREY, RED]]
    for row, cols in enumerate(colours):
        for col, c in enumerate(cols):
            x, y = 21 + col * 12, 39 + row * 13
            d.rounded_rectangle(r(x, y, x + 9, y + 9), radius=1.5 * k, fill=c,
                                outline="#888888" if c == BLACK else None)
    return img


def tile(w, h, frac=0.8, lift=0.0):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    s = int(min(w, h) * frac)
    img.alpha_composite(calculator(s), ((w - s) // 2, (h - s) // 2 - int(h * lift)))
    return img


# The medium and wide tiles show the name at the bottom: a smaller, raised calculator.
tile(150, 150, 0.66, 0.06).save(os.path.join(out, "Square150x150Logo.png"))
tile(71, 71, 0.9).save(os.path.join(out, "Square71x71Logo.png"))
tile(44, 44, 1.0).save(os.path.join(out, "Square44x44Logo.png"))
tile(310, 150, 0.66, 0.06).save(os.path.join(out, "Wide310x150Logo.png"))
store = Image.new("RGBA", (50, 50), "#404040")
store.alpha_composite(calculator(48), (1, 1))
store.save(os.path.join(out, "StoreLogo.png"))
splash = Image.new("RGBA", (480, 800), (0, 0, 0, 0))
splash.alpha_composite(calculator(220), (130, 290))
splash.save(os.path.join(out, "SplashScreen.png"))
print("wrote assets to", os.path.normpath(out))
