"""Build the RGBControl Windows icon and its vector source without dependencies."""

from __future__ import annotations

import math
import struct
import zlib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
COLORS = (
    (255, 94, 109),
    (255, 163, 68),
    (255, 222, 91),
    (83, 224, 153),
    (73, 199, 247),
    (171, 124, 255),
)
SIZES = (16, 24, 32, 48, 64, 128, 256)
BLADE = ((-7, -13), (-27, -47), (-20, -57), (8, -50), (22, -29), (8, -14))


def blades():
    for angle in (0, 2 * math.pi / 3, 4 * math.pi / 3):
        c, s = math.cos(angle), math.sin(angle)
        yield tuple((128 + x * c - y * s, 128 + x * s + y * c) for x, y in BLADE)


BLADES = tuple(blades())


def inside_polygon(x, y, polygon):
    inside = False
    previous_x, previous_y = polygon[-1]
    for current_x, current_y in polygon:
        if (current_y > y) != (previous_y > y):
            crossing = (previous_x - current_x) * (y - current_y) / (previous_y - current_y) + current_x
            if x < crossing:
                inside = not inside
        previous_x, previous_y = current_x, current_y
    return inside


def sample(x, y):
    # Rounded tile, with a generous clear border around the small 16 px icon.
    corner_x = max(58 - x, 0, x - 198)
    corner_y = max(58 - y, 0, y - 198)
    if corner_x * corner_x + corner_y * corner_y > 46 * 46:
        return (0, 0, 0, 0)

    dx, dy = x - 128, y - 128
    radius = math.hypot(dx, dy)
    glow = max(0, 1 - radius / 175)
    color = (int(10 + 11 * glow), int(17 + 17 * glow), int(33 + 20 * glow), 255)

    if radius <= 111:
        color = (28, 41, 62, 255)

    if 75 <= radius <= 103:
        sector_angle = (math.degrees(math.atan2(dy, dx)) + 90) % 360
        sector = int(sector_angle // 60)
        if 4 <= sector_angle % 60 <= 56:
            color = (*COLORS[sector], 255)

    if radius <= 67:
        color = (35, 50, 73, 255)
    if radius <= 57:
        color = (23, 36, 56, 255)

    for blade in BLADES:
        if inside_polygon(x, y, blade):
            color = (229, 241, 252, 255)
            break

    if radius <= 14:
        color = (23, 36, 56, 255)
    if radius <= 7:
        color = (229, 241, 252, 255)
    return color


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def png(size):
    samples = 8 if size == 16 else 6 if size == 24 else 4 if size <= 48 else 3 if size == 64 else 2
    rows = bytearray()
    for py in range(size):
        rows.append(0)
        for px in range(size):
            red = green = blue = alpha = 0
            for sy in range(samples):
                y = (py + (sy + 0.5) / samples) * 256 / size
                for sx in range(samples):
                    x = (px + (sx + 0.5) / samples) * 256 / size
                    r, g, b, a = sample(x, y)
                    red += r * a
                    green += g * a
                    blue += b * a
                    alpha += a
            n = samples * samples
            a = round(alpha / n)
            rows.extend((round(red / alpha) if alpha else 0,
                         round(green / alpha) if alpha else 0,
                         round(blue / alpha) if alpha else 0, a))
    header = b"\x89PNG\r\n\x1a\n"
    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return header + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(rows), 9)) + chunk(b"IEND", b"")


def svg():
    lines = [
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" role="img" aria-label="RGBControl rainbow fan icon">',
        '  <defs><radialGradient id="tile"><stop stop-color="#1b334f"/><stop offset="1" stop-color="#0a1121"/></radialGradient></defs>',
        '  <rect x="12" y="12" width="232" height="232" rx="46" fill="url(#tile)"/>',
        '  <circle cx="128" cy="128" r="111" fill="#1c293e"/>',
    ]
    for i, rgb in enumerate(COLORS):
        start = math.radians(-90 + i * 60 + 4)
        end = math.radians(-90 + i * 60 + 56)
        x1, y1 = 128 + 89 * math.cos(start), 128 + 89 * math.sin(start)
        x2, y2 = 128 + 89 * math.cos(end), 128 + 89 * math.sin(end)
        color = "#%02x%02x%02x" % rgb
        lines.append(f'  <path d="M{x1:.3f} {y1:.3f} A89 89 0 0 1 {x2:.3f} {y2:.3f}" fill="none" stroke="{color}" stroke-width="28"/>')
    lines.extend((
        '  <circle cx="128" cy="128" r="67" fill="#233249"/>',
        '  <circle cx="128" cy="128" r="57" fill="#172438"/>',
    ))
    for blade in BLADES:
        points = " ".join(f"{x:.3f},{y:.3f}" for x, y in blade)
        lines.append(f'  <polygon points="{points}" fill="#e5f1fc"/>')
    lines.extend((
        '  <circle cx="128" cy="128" r="14" fill="#172438"/>',
        '  <circle cx="128" cy="128" r="7" fill="#e5f1fc"/>',
        '</svg>',
    ))
    return "\n".join(lines) + "\n"


def main():
    (ROOT / "assets").mkdir(exist_ok=True)
    images = [(size, png(size)) for size in SIZES]
    (ROOT / "assets" / "RGBControl.png").write_bytes(images[-1][1])
    (ROOT / "assets" / "RGBControl.svg").write_text(svg(), encoding="utf-8")

    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = bytearray()
    for size, data in images:
        entries.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    (ROOT / "RGBControl.ico").write_bytes(header + entries + b"".join(data for _, data in images))


if __name__ == "__main__":
    main()
