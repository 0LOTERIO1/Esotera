"""One-off: process Esotera logo (transparent bg, crop, favicon, OG)."""
from __future__ import annotations

import os
from PIL import Image

SRC = r"C:\Users\pedro\.cursor\projects\c-Esotera\assets\c__Users_pedro_AppData_Roaming_Cursor_User_workspaceStorage_empty-window_images_ChatGPT_Image_30_de_jul._de_2026__09_46_46-d5013630-5d2e-42cc-87e9-fe1a19d02684.png"
OUT_DIR = r"C:\Esotera\public\images\brand"
PUBLIC = r"C:\Esotera\public"


def remove_white_bg(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            mx = max(r, g, b)
            mn = min(r, g, b)
            if mx >= 245 and (mx - mn) <= 18:
                pixels[x, y] = (r, g, b, 0)
            elif mx >= 230 and (mx - mn) <= 12:
                fade = int(255 * (245 - mx) / 15)
                pixels[x, y] = (r, g, b, max(0, min(a, fade)))
    return img


def crop_content(img: Image.Image, pad: int = 8) -> Image.Image:
    bbox = img.getbbox()
    if not bbox:
        return img
    w, h = img.size
    left = max(0, bbox[0] - pad)
    top = max(0, bbox[1] - pad)
    right = min(w, bbox[2] + pad)
    bottom = min(h, bbox[3] + pad)
    return img.crop((left, top, right, bottom))


def make_white_variant(img: Image.Image) -> Image.Image:
    white = img.copy()
    wp = white.load()
    ww, wh = white.size
    for y in range(wh):
        for x in range(ww):
            r, g, b, a = wp[x, y]
            if a < 10:
                continue
            is_yellow = r > 180 and g > 160 and b < 120
            is_teal = (
                (g > 100 or b > 100)
                and b >= r - 20
                and g >= r - 10
                and not is_yellow
                and r < 200
            )
            if is_teal:
                lum = int(0.299 * r + 0.587 * g + 0.114 * b)
                v = min(255, int(lum * 1.35 + 40))
                wp[x, y] = (v, v, v, a)
    return white


def make_favicon(img: Image.Image) -> Image.Image:
    cw, ch = img.size
    sun_w = min(ch, int(cw * 0.38))
    sun = img.crop((0, 0, sun_w, ch))
    side = max(sun.size)
    favicon = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    ox = (side - sun.size[0]) // 2
    oy = (side - sun.size[1]) // 2
    favicon.paste(sun, (ox, oy), sun)
    return favicon


def make_og(img: Image.Image) -> Image.Image:
    og = Image.new("RGBA", (1200, 630), (0, 0, 0, 0))
    for y in range(630):
        t = y / 629
        r = int(250 * (1 - t) + 232 * t)
        g = int(247 * (1 - t) + 242 * t)
        b = int(240 * (1 - t) + 236 * t)
        for x in range(1200):
            hx = abs(x - 600) / 600
            rr = max(0, min(255, int(r - hx * 6)))
            gg = max(0, min(255, int(g + hx * 4)))
            bb = max(0, min(255, int(b + hx * 8)))
            og.putpixel((x, y), (rr, gg, bb, 255))

    max_w = 840
    scale = max_w / img.width
    logo_og = img.resize(
        (int(img.width * scale), int(img.height * scale)),
        Image.Resampling.LANCZOS,
    )
    lx = (1200 - logo_og.width) // 2
    ly = (630 - logo_og.height) // 2
    og.paste(logo_og, (lx, ly), logo_og)
    return og.convert("RGB")


def main() -> None:
    os.makedirs(OUT_DIR, exist_ok=True)
    img = crop_content(remove_white_bg(Image.open(SRC)))

    dark_path = os.path.join(OUT_DIR, "esotera-logo-dark.png")
    img.save(dark_path, "PNG", optimize=True)
    img.save(os.path.join(OUT_DIR, "esotera-logo.png"), "PNG", optimize=True)
    img.save(os.path.join(OUT_DIR, "esotera-logo.webp"), "WEBP", quality=90, method=6)

    white = make_white_variant(img)
    white.save(os.path.join(OUT_DIR, "esotera-logo-white.png"), "PNG", optimize=True)

    # Keep a copy of processed as "original" replacement for brand pack
    img.save(os.path.join(OUT_DIR, "esotera-logo-original.png"), "PNG", optimize=True)

    favicon = make_favicon(img)
    favicon.resize((32, 32), Image.Resampling.LANCZOS).save(
        os.path.join(PUBLIC, "favicon.ico"), format="ICO", sizes=[(32, 32)]
    )
    favicon.resize((48, 48), Image.Resampling.LANCZOS).save(
        os.path.join(PUBLIC, "icon.png"), "PNG"
    )
    favicon.resize((180, 180), Image.Resampling.LANCZOS).save(
        os.path.join(PUBLIC, "apple-icon.png"), "PNG"
    )
    favicon.resize((192, 192), Image.Resampling.LANCZOS).save(
        os.path.join(PUBLIC, "icon-192.png"), "PNG"
    )
    favicon.resize((512, 512), Image.Resampling.LANCZOS).save(
        os.path.join(PUBLIC, "icon-512.png"), "PNG"
    )

    og = make_og(img)
    og.save(os.path.join(PUBLIC, "og-image.png"), "PNG", optimize=True)
    og.save(os.path.join(OUT_DIR, "og-image.png"), "PNG", optimize=True)

    print("dark", Image.open(dark_path).size, os.path.getsize(dark_path))
    print("white", white.size, os.path.getsize(os.path.join(OUT_DIR, "esotera-logo-white.png")))
    print("og", og.size, os.path.getsize(os.path.join(PUBLIC, "og-image.png")))
    print("done")


if __name__ == "__main__":
    main()
