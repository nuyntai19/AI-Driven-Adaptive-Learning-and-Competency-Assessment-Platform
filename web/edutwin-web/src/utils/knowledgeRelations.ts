import type { KnowledgeRelationType, KnowledgeNodeType } from "../types/knowledgeGraph";

export const relationInfo: Record<KnowledgeRelationType, { label: string; intent: string; example: string; effect: string; outgoing: string; incoming: string; color: string }> = {
  PartOf: { label: "Thuộc về", intent: "A là một phần của B", example: "Đạo hàm → Giải tích", effect: "Phân chia nội dung; không tự yêu cầu học B trước A.", outgoing: "Thuộc về →", incoming: "Có thành phần ←", color: "#8b5cf6" },
  RelatedTo: { label: "Liên quan", intent: "A và B có nội dung liên hệ", example: "Hàm số bậc hai → Phương trình bậc hai", effect: "Liên hệ nội dung; không tự khóa thứ tự học.", outgoing: "Liên quan đến →", incoming: "Có liên kết liên quan từ ←", color: "#64748b" },
  CausesErrorIn: { label: "Gây lỗi", intent: "Sai kiến thức A có thể dẫn đến sai B", example: "Quy tắc dấu → Giải bất phương trình", effect: "Mô tả khả năng nguồn lỗi; không tự bắt học lại A.", outgoing: "Lỗ hổng tại đây có thể gây lỗi ở →", incoming: "Lỗi ở đây có thể liên quan lỗ hổng tại ←", color: "#f43f5e" },
  PrerequisiteOf: { label: "Tiên quyết", intent: "Cần đáp ứng nền tảng A trước khi tiến tới B", example: "Phương trình bậc nhất → Hệ phương trình bậc nhất", effect: "Lộ trình dùng để xét nền tảng và thứ tự theo năng lực hiện có.", outgoing: "Là nền tảng cho →", incoming: "Cần nền tảng từ ←", color: "#06b6d4" },
};
export const relationTypes = Object.keys(relationInfo) as KnowledgeRelationType[];
export const graphNodeLabels: Record<KnowledgeNodeType, string> = { Subject: "Môn học", Chapter: "Chương", Topic: "Chủ đề", Skill: "Kỹ năng", Concept: "Khái niệm" };
export const graphNodeColors: Record<KnowledgeNodeType, string> = { Subject: "#3b82f6", Chapter: "#a855f7", Topic: "#06b6d4", Skill: "#10b981", Concept: "#f59e0b" };
export const graphEdgeColors = Object.fromEntries(relationTypes.map(type => [type, relationInfo[type].color])) as Record<KnowledgeRelationType, string>;
