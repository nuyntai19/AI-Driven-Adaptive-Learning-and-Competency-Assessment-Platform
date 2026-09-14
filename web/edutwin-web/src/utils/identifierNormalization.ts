export const normalizeToAscii = (text: string, toUpper = false): string => {
  if (!text) return "";
  const normalized = text
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .trim();
  return toUpper ? normalized.toUpperCase() : normalized;
};
