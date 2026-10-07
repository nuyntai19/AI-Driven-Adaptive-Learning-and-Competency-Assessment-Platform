"""Create deterministic synthetic scratchpad PNGs; no real student images."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

destination = Path(__file__).resolve().parents[2] / "storage" / "verification" / "ai-vision-fixtures"
destination.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 30)
small = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 26)
for valid in (True, False):
    picture = Image.new("RGB", (820, 380), "white")
    draw = ImageDraw.Draw(picture)
    for x in range(0, 820, 24):
        draw.line((x, 0, x, 380), fill="#e8edf5")
    for y in range(0, 380, 24):
        draw.line((0, y, 820, y), fill="#e8edf5")
    ink = "#1c3b83"
    draw.line([(70, 80), (70, 280), (330, 280), (70, 80)], fill=ink, width=4)
    draw.line([(70, 260), (90, 260), (90, 280)], fill=ink, width=3)
    draw.text((25, 165), "3", font=font, fill=ink)
    draw.text((185, 290), "4", font=font, fill=ink)
    draw.text((195, 150), "c = ?", font=font, fill=ink)
    if valid:
        lines = ["c^2 = 3^2 + 4^2", "c^2 = 9 + 16 = 25", "c = sqrt(25) = 5"]
    else:
        lines = ["c = 3 + 4", "c = 7"]
    for i, line in enumerate(lines):
        draw.text((385, 105 + i * 65), line, font=small, fill=ink)
    picture.save(destination / ("triangle-valid.png" if valid else "triangle-invalid.png"))
print(destination)
