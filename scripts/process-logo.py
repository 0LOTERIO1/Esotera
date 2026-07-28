from PIL import Image
import numpy as np
from pathlib import Path

src = Path(r"C:\Esotera\public\images\brand\esotera-logo-original.png")
img = Image.open(src).convert("RGBA")
arr = np.array(img).astype(np.float32)
r, g, b = arr[:, :, 0], arr[:, :, 1], arr[:, :, 2]
h, w = r.shape

corners = np.concatenate(
    [
        arr[0:8, 0:8].reshape(-1, 4),
        arr[0:8, -8:].reshape(-1, 4),
        arr[-8:, 0:8].reshape(-1, 4),
        arr[-8:, -8:].reshape(-1, 4),
    ],
    axis=0,
)
bg = corners.mean(axis=0)[:3]
print("bg approx", bg)

dist = np.sqrt((r - bg[0]) ** 2 + (g - bg[1]) ** 2 + (b - bg[2]) ** 2)
luma = 0.299 * r + 0.587 * g + 0.114 * b
is_bg = dist < 55
is_logo = (~is_bg) & (luma > 160)

alpha = np.where(is_bg, 0, np.clip((dist - 20) / 35 * 255, 0, 255))
alpha = np.where(is_logo, np.maximum(alpha, 220), alpha)
alpha = alpha.astype(np.uint8)

ys, xs = np.where(alpha > 10)
if len(xs) == 0:
    raise SystemExit("no logo pixels found")
pad = 4
y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + pad + 1)
x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + pad + 1)

white = np.zeros((h, w, 4), dtype=np.uint8)
white[:, :, 0:3] = 255
white[:, :, 3] = alpha
white_crop = Image.fromarray(white[y0:y1, x0:x1], "RGBA")

dark = np.zeros((h, w, 4), dtype=np.uint8)
dark[:, :, 0] = 0x12
dark[:, :, 1] = 0x3B
dark[:, :, 2] = 0x5D
dark[:, :, 3] = alpha
dark_crop = Image.fromarray(dark[y0:y1, x0:x1], "RGBA")

out = Path(r"C:\Esotera\public\images\brand")
white_crop.save(out / "esotera-logo-white.png")
dark_crop.save(out / "esotera-logo-dark.png")
print("saved", white_crop.size, dark_crop.size)
