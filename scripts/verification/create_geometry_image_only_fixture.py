"""Deterministic Vietnamese Grade 10 statement + geometry fixture, no network/API."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "storage" / "verification" / "geometry10-full-statement.png"
image = Image.new("RGB", (1600, 1200), "white")
draw = ImageDraw.Draw(image)
font_dir = Path("C:/Windows/Fonts")
regular = str(font_dir / "arial.ttf")
bold = str(font_dir / "arialbd.ttf")

def text(position, value, size=38, strong=False, color="#172033"):
    draw.text(position, value, font=ImageFont.truetype(bold if strong else regular, size), fill=color)

text((70, 45), "HÌNH HỌC 10 — ĐỀ BÀI TRONG ẢNH", 47, True)
draw.line((70, 115, 1530, 115), fill="#d2d9e5", width=3)
text((70, 150), "Cho tam giác ABC có AB = 13 cm, AC = 15 cm, BC = 14 cm.")
text((70, 210), "M là trung điểm BC. H là chân đường cao kẻ từ A xuống BC.")
text((70, 270), "Hình chỉ minh họa; không suy ra số đo bằng cách đo ảnh.", 32)

# Coordinates represent B=(0,0), C=(14,0), A=(5,12), so the triangle
# is exactly 13–14–15, AH=12 and AM=sqrt(148). Labels do not reveal answers.
B, C, A = (440, 810), (1000, 810), (640, 330)
H, M = (640, 810), (720, 810)
draw.line((B, A, C, B), fill="#172033", width=5)
draw.line((A, M), fill="#4f46e5", width=4)
for y in range(A[1], H[1], 23):
    draw.line((H[0], y, H[0], min(y + 12, H[1])), fill="#64748b", width=3)
draw.line((640, 789, 661, 789, 661, 810), fill="#64748b", width=3)
for p, label, offset in [(A, "A", (-45, -10)), (B, "B", (-40, 7)),
                          (C, "C", (10, 7)), (H, "H", (-24, 14)), (M, "M", (-8, 14))]:
    draw.ellipse((p[0]-5, p[1]-5, p[0]+5, p[1]+5), fill="#172033")
    text((p[0]+offset[0], p[1]+offset[1]), label, 35, True)
text((430, 535), "13 cm", 35)
text((865, 525), "15 cm", 35)
text((850, 756), "14 cm", 35)
draw.line((580, 799, 580, 821), fill="#172033", width=3)
draw.line((860, 799, 860, 821), fill="#172033", width=3)

draw.line((70, 890, 1530, 890), fill="#d2d9e5", width=3)
text((70, 925), "a) Tính diện tích tam giác ABC và độ dài AH.", 39, True)
text((70, 985), "b) Tính độ dài đường trung tuyến AM (kết quả chính xác).", 39, True)
text((70, 1045), "c) Tính bán kính đường tròn ngoại tiếp tam giác ABC.", 39, True)
text((70, 1120), "Trình bày lập luận bằng tiếng Việt, ghi rõ đơn vị và các công thức dùng.", 31)
OUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUT, "PNG")
print(f"Created {OUT.name}: {image.width}x{image.height}, {OUT.stat().st_size} bytes")
