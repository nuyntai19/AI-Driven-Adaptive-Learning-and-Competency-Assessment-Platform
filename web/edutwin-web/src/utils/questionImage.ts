export const MAX_QUESTION_IMAGE_BYTES = 2 * 1024 * 1024;
export const IMAGE_ONLY_QUESTION_TEXT = "Đọc đề bài trong ảnh đính kèm.";

export function questionImageFileError(file: Pick<File, "size" | "type">): string | null {
  if (!["image/png", "image/jpeg", "image/webp"].includes(file.type)) return "Chỉ nhận ảnh PNG, JPG hoặc WebP; không nhận SVG/GIF.";
  if (file.size <= 0 || file.size > 8 * 1024 * 1024) return "Ảnh gốc phải có dung lượng từ 1 byte đến 8 MB.";
  return null;
}

export async function normalizeQuestionImage(file: File): Promise<string> {
  const error = questionImageFileError(file); if (error) throw new Error(error);
  const url = URL.createObjectURL(file);
  try {
    const image = new Image();
    await new Promise<void>((resolve, reject) => { image.onload = () => resolve(); image.onerror = () => reject(new Error("Không đọc được ảnh. Vui lòng chọn một ảnh khác.")); image.src = url; });
    if (image.naturalWidth * image.naturalHeight > 24_000_000) throw new Error("Ảnh quá lớn. Hãy cắt riêng phần đề bài trước khi đính kèm.");
    const ratio = Math.min(1, 2048 / Math.max(image.naturalWidth, image.naturalHeight));
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(image.naturalWidth * ratio)); canvas.height = Math.max(1, Math.round(image.naturalHeight * ratio));
    const ctx = canvas.getContext("2d"); if (!ctx) throw new Error("Trình duyệt không hỗ trợ xử lý ảnh.");
    ctx.fillStyle = "#fff"; ctx.fillRect(0, 0, canvas.width, canvas.height); ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
    const png = await new Promise<Blob>((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("Không thể chuẩn hóa ảnh.")), "image/png"));
    if (png.size > MAX_QUESTION_IMAGE_BYTES) throw new Error("Ảnh sau xử lý vượt 2 MB. Hãy cắt ảnh chỉ còn đề và hình vẽ để giữ chữ rõ nét.");
    return await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result)); reader.onerror = () => reject(new Error("Không đọc được dữ liệu ảnh.")); reader.readAsDataURL(png); });
  } finally { URL.revokeObjectURL(url); }
}
