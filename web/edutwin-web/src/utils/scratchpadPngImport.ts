const MAX_BYTES = 5_242_880;
const PNG_SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];

const PHOTO_MAX_BYTES = 20 * 1024 * 1024;

// Notebook photos arrive as JPEG/WebP from phone cameras. Convert them to a
// PNG the attachment + transcription pipeline accepts, downscaling stepwise
// until the PNG fits the shared 5 MB cap (handwriting stays legible at these
// widths for a single notebook page).
export async function readScratchpadPhoto(file: Blob): Promise<{ blob: Blob; dataUrl: string }> {
  if (!['image/jpeg', 'image/webp', 'image/png'].includes(file.type) || file.size < 1 || file.size > PHOTO_MAX_BYTES)
    throw new Error('Chỉ nhận ảnh JPG, WebP hoặc PNG, tối đa 20 MB.');
  const url = URL.createObjectURL(file);
  try {
    const img = await new Promise<HTMLImageElement>((resolve, reject) => {
      const el = new Image();
      el.onload = () => resolve(el);
      el.onerror = () => reject(new Error('Không đọc được tệp ảnh.'));
      el.src = url;
    });
    for (const maxWidth of [1600, 1280, 1024]) {
      const scale = Math.min(1, maxWidth / (img.naturalWidth || maxWidth));
      const w = Math.max(1, Math.round((img.naturalWidth || maxWidth) * scale));
      const h = Math.max(1, Math.round((img.naturalHeight || maxWidth) * scale));
      const canvas = document.createElement('canvas');
      canvas.width = w; canvas.height = h;
      const ctx = canvas.getContext('2d');
      if (!ctx) throw new Error('Trình duyệt không hỗ trợ xử lý ảnh.');
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, w, h);
      ctx.drawImage(img, 0, 0, w, h);
      const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
      if (!blob) throw new Error('Không chuyển được ảnh sang PNG.');
      if (blob.size <= MAX_BYTES || maxWidth === 1024) {
        const dataUrl = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onloadend = () => resolve(reader.result as string);
          reader.onerror = () => reject(new Error('Không đọc được ảnh đã chuyển.'));
          reader.readAsDataURL(blob);
        });
        return { blob, dataUrl };
      }
    }
    throw new Error('Ảnh vẫn quá lớn sau khi nén.');
  } finally {
    URL.revokeObjectURL(url);
  }
}

// Preserve the original bytes: importing an attachment is not redrawing/recompressing it.
// The server independently validates all PNG chunks, CRCs and decoded content on upload.
export async function readScratchpadPng(file: Blob): Promise<{ blob: Blob; dataUrl: string }> {
  if (file.type !== 'image/png' || file.size < 33 || file.size > MAX_BYTES)
    throw new Error('Chỉ nhận ảnh PNG không rỗng, tối đa 5 MB.');
  const bytes = new Uint8Array(await file.arrayBuffer());
  const header = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (!PNG_SIGNATURE.every((value, i) => bytes[i] === value) || header.getUint32(8) !== 13 ||
      String.fromCharCode(...bytes.subarray(12, 16)) !== 'IHDR')
    throw new Error('Tệp không phải ảnh PNG hợp lệ.');
  const width = header.getUint32(16), height = header.getUint32(20);
  if (width < 1 || height < 1 || width > 4096 || height > 4096)
    throw new Error('Ảnh nháp phải có kích thước từ 1 đến 4096 pixel mỗi chiều.');
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += 8192)
    binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
  return { blob: file, dataUrl: `data:image/png;base64,${btoa(binary)}` };
}
