const MAX_BYTES = 5_242_880;
const PNG_SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];

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
