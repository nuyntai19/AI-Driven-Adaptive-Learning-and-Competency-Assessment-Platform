import { TeacherModal } from "./TeacherOverlays";
import { relationInfo, relationTypes } from "../../utils/knowledgeRelations";

export function RelationHelpDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  return <TeacherModal isOpen={open} title="Chọn quan hệ phù hợp" description="Đọc mũi tên từ chủ đề nguồn A sang chủ đề đích B." size="lg" onClose={onClose}>
    <div className="kg-help-table"><table><thead><tr><th>Bạn muốn diễn đạt</th><th>Quan hệ nên chọn</th><th>Ví dụ A → B</th></tr></thead>
      <tbody>{relationTypes.map(type => <tr key={type}><td>{relationInfo[type].intent}</td><td><strong>{relationInfo[type].label}</strong><p>{relationInfo[type].effect}</p></td><td>{relationInfo[type].example}</td></tr>)}</tbody></table></div>
    <div className="kg-hint mt-4 space-y-2"><p>Với <strong>Thuộc về</strong>, A là phần nhỏ, B là phần bao chứa.</p>
      <p><strong>Nhóm cha</strong> tổ chức cây nội dung, được cấu hình riêng; không tự tạo quan hệ Thuộc về hay Tiên quyết.</p>
      <p><strong>Gây lỗi</strong> mô tả khả năng nguồn lỗi; hiện chưa đồng nghĩa hệ thống tự truy lỗi và giao bài theo cạnh này. Trọng số 1 không có nghĩa chắc chắn sai 100%.</p>
    </div>
  </TeacherModal>;
}
