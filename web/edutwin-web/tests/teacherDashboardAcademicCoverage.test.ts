import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";

const source=fs.readFileSync(new URL("../src/pages/teacher/TeacherClassDashboardView.tsx",import.meta.url),"utf8");
test("teacher dashboard explains the authoritative curriculum scope and unassessed coverage",()=>{
  assert.match(source,/academicCoverage\.hasAppliedCurriculum/);
  assert.match(source,/academicCoverage\.applicableTopicCount/);
  assert.match(source,/academicCoverage\.unassessedStudentTopicCount/);
  assert.match(source,/không tính là điểm 0 hay nhóm yếu/);
  assert.match(source,/hệ thống không lấy toàn bộ đồ thị môn thay thế/);
});
test("empty gap groups are not presented as proof that everyone is strong",()=>{
  assert.match(source,/Chưa ghi nhận nhóm dưới ngưỡng từ bằng chứng đã có/);
  assert.doesNotMatch(source,/Không có nhóm học sinh nào bị hổng kiến thức nghiêm trọng/);
});
test("weak topic cards expose assessed and unassessed student counts",()=>{
  assert.match(source,/topic\.assessedStudentCount/);
  assert.match(source,/topic\.unassessedStudentCount/);
});
