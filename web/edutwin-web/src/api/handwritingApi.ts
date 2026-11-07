import { httpClient } from "./httpClient";

interface ApiResponse<T> {
  data: T;
  meta: {
    traceId?: string;
    timestamp: string;
  };
}

/**
 * Chép chữ viết tay trong ảnh nháp (data URL PNG từ Scratchpad) thành văn bản.
 * Kết quả chỉ là bản chép gợi ý: học sinh phải kiểm tra, sửa và xác nhận
 * trong ô lập luận trước khi nộp — server không lưu nội dung này.
 */
export const transcribeHandwriting = async (imageDataUrl: string): Promise<string> => {
  const response = await httpClient.post<ApiResponse<{ text: string }>>(
    "/learning/handwriting/transcribe",
    { imageBase64: imageDataUrl }
  );
  return response.data.data.text;
};
