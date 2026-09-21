"""Draws the two flag icons used by the language switcher in the window title bar.

Run by hand when the flags need changing; the PNGs it writes are committed:

    python tools/draw-flags.py

The flags are drawn rather than downloaded so that nothing in Assets/ arrives from a source
nobody can re-check. They are simplified on purpose -- at the 24x16 px they are shown at, the
counterchange of the Union Jack's saltire and the detail of the Slovak arms are both below the
resolution of a single pixel, and drawing them faithfully would only add ways to be wrong.

Needs Pillow. It is not a build dependency: the build consumes the PNGs, not this script.
"""

from PIL import Image, ImageDraw

SCALE = 12                      # supersample, then let Lanczos do the antialiasing
OUT_W, OUT_H = 96, 64           # 4x the 24x16 the window draws them at
W, H = OUT_W * SCALE, OUT_H * SCALE

SK_BLUE = (11, 78, 162)
SK_RED = (238, 28, 37)
UK_BLUE = (1, 33, 105)
UK_RED = (200, 16, 46)
WHITE = (255, 255, 255)


def save(image, name):
    image.resize((OUT_W, OUT_H), Image.LANCZOS).save(name)
    print("napisane " + name)


def bezier(p0, p1, p2, steps=60):
    """Quadratic curve, sampled. Pillow draws polygons, not paths."""
    points = []
    for i in range(steps + 1):
        t = i / steps
        u = 1 - t
        points.append((u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0],
                       u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1]))
    return points


def shield(cx, cy, w, h):
    """A gothic shield: square shoulders, straight flanks, curving to a point."""
    left, right = cx - w / 2, cx + w / 2
    top, bottom = cy - h / 2, cy + h / 2
    shoulder = top + h * 0.45
    points = [(left, top), (right, top), (right, shoulder)]
    points += bezier((right, shoulder), (right, bottom - h * 0.08), (cx, bottom))
    points += bezier((cx, bottom), (left, bottom - h * 0.08), (left, shoulder))
    return points


def slovakia():
    image = Image.new("RGB", (W, H), WHITE)
    draw = ImageDraw.Draw(image)
    draw.rectangle([0, H / 3, W, 2 * H / 3], fill=SK_BLUE)
    draw.rectangle([0, 2 * H / 3, W, H], fill=SK_RED)

    # The arms sit left of centre and overlap all three bands, as on the real flag.
    aw, ah = W * 0.33, H * 0.66
    cx, cy = W * 0.37, H * 0.50
    draw.polygon(shield(cx, cy, aw * 1.16, ah * 1.12), fill=WHITE)

    # The contents are drawn on their own layer and let through a shield-shaped mask, so the
    # hills can overrun the foot of the shield without painting over the flag behind it.
    layer = Image.new("RGB", (W, H), SK_RED)
    inner = ImageDraw.Draw(layer)

    foot = cy + ah / 2
    for offset, lift in ((-0.26, 0.62), (0.26, 0.62), (0.0, 1.0)):
        hx = cx + aw * offset
        hw = aw * 0.42
        hh = ah * 0.30 * lift
        inner.ellipse([hx - hw, foot - hh * 2, hx + hw, foot + hh], fill=SK_BLUE)

    # The double cross, standing on the middle hill.
    bar = aw * 0.13
    top = cy - ah * 0.34
    base = cy + ah * 0.14
    inner.rectangle([cx - bar / 2, top, cx + bar / 2, base], fill=WHITE)
    for arm, y in ((aw * 0.20, cy - ah * 0.20), (aw * 0.29, cy - ah * 0.01)):
        inner.rectangle([cx - arm, y - bar / 2, cx + arm, y + bar / 2], fill=WHITE)

    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(shield(cx, cy, aw, ah), fill=255)
    image.paste(layer, (0, 0), mask)

    save(image, "src/FriWorld.Launcher.App/Assets/flag-sk.png")


def band(draw, a, b, width, colour):
    """A straight band of the given perpendicular width, drawn as a quadrilateral."""
    (x0, y0), (x1, y1) = a, b
    dx, dy = x1 - x0, y1 - y0
    length = (dx * dx + dy * dy) ** 0.5
    nx, ny = -dy / length * width / 2, dx / length * width / 2
    draw.polygon([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny),
                  (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], fill=colour)


def united_kingdom():
    image = Image.new("RGB", (W, H), UK_BLUE)
    draw = ImageDraw.Draw(image)

    # Saltire first, so the upright cross of St George lies over it.
    for a, b in (((0, 0), (W, H)), ((0, H), (W, 0))):
        band(draw, a, b, H * 0.30, WHITE)
    for a, b in (((0, 0), (W, H)), ((0, H), (W, 0))):
        band(draw, a, b, H * 0.10, UK_RED)

    draw.rectangle([0, H / 2 - H * 0.165, W, H / 2 + H * 0.165], fill=WHITE)
    draw.rectangle([W / 2 - H * 0.165, 0, W / 2 + H * 0.165, H], fill=WHITE)
    draw.rectangle([0, H / 2 - H * 0.10, W, H / 2 + H * 0.10], fill=UK_RED)
    draw.rectangle([W / 2 - H * 0.10, 0, W / 2 + H * 0.10, H], fill=UK_RED)

    save(image, "src/FriWorld.Launcher.App/Assets/flag-en.png")


slovakia()
united_kingdom()
