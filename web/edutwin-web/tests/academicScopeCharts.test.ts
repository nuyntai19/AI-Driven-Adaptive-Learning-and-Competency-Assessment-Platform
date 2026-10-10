import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { ATLAS_PAGE_SIZE, groupCompetencies, wrapTopicLabel } from "../src/utils/competencyGroups.ts";
import { studentScopeUrl } from "../src/utils/studentAcademicNavigation.ts";

test("81 topics are grouped without dropping any and Atlas has at most six nodes per page", () => {
  const inputs = Array.from({length:81}, (_, i) => ({name:`Topic ${i}`,score:50,fullMark:100,groupId:`${i%3}`,groupName:`Chapter ${i%3}`,evidenceCount:1}));
  const groups = groupCompetencies(inputs);
  assert.equal(groups.length,3); assert.equal(groups.reduce((n,g)=>n+g.topicCount,0),81);
  assert.equal(ATLAS_PAGE_SIZE,6);
  const source=fs.readFileSync(new URL("../src/components/student/StudentKnowledgeMap.tsx",import.meta.url),"utf8");
  assert.match(source,/slice\(currentPage \* ATLAS_PAGE_SIZE, \(currentPage \+ 1\) \* ATLAS_PAGE_SIZE\)/);
});
test("group scores are weighted observed evidence, not invented zero scores for unseen topics", () => {
  const groups=groupCompetencies([{name:"A",score:100,fullMark:100,groupId:"g",weight:1,evidenceCount:1},
    {name:"B",score:0,fullMark:100,groupId:"g",weight:3,evidenceCount:1},
    {name:"Unseen",score:0,fullMark:100,groupId:"g",weight:100,evidenceCount:0}]);
  assert.equal(groups[0].score,25); assert.equal(groups[0].assessedCount,2); assert.equal(groups[0].topicCount,3);
  assert.equal(groupCompetencies([{name:"Unseen",score:0,fullMark:100,evidenceCount:0}])[0].score,null);
});
test("long topic labels have bounded lines and full names remain in accessible nodes",()=>{
  assert.ok(wrapTopicLabel("Một chủ đề rất dài có nhiều từ và cần được chia thành các dòng giới hạn cho biểu đồ").length<=3);
  const source=fs.readFileSync(new URL("../src/components/student/StudentKnowledgeMap.tsx",import.meta.url),"utf8");
  assert.match(source,/aria-label=\{`Chuyên đề \$\{node.topicName\}`\}/);
});
test("student context exposes class/history view and no curriculum assignment control",()=>{
  const source=fs.readFileSync(new URL("../src/components/student/StudentAcademicContext.tsx",import.meta.url),"utf8");
  assert.match(source,/Lớp học đang xem/); assert.match(source,/Xem lịch sử/); assert.match(source,/Giáo trình do giáo viên áp dụng/);
  assert.doesNotMatch(source,/httpClient\.(put|post|patch)|curriculumApi|updateClasses|ApplyCurriculumRequest/);
});
test("dashboard, Twin and assignment links preserve the selected academic view without mutation fields",()=>{
  const params = new URLSearchParams("subjectId=math&classId=legacy&history=true&curriculumId=not-a-student-option");
  assert.equal(studentScopeUrl("/hoc-tap/bai-tap",params),"/hoc-tap/bai-tap?subjectId=math&classId=legacy&history=true");
  assert.equal(studentScopeUrl("/hoc-tap/tong-quan",new URLSearchParams("classId=orphan&history=false")),"/hoc-tap/tong-quan");
  assert.equal(studentScopeUrl("/hoc-tap/luyen-tap/10", params, {assignmentId:"test"}),"/hoc-tap/luyen-tap/10?assignmentId=test&subjectId=math&classId=legacy&history=true");
  for(const page of ["StudentDashboardPage","StudentTwinPage","StudentAssignmentsPage","StudentAssignmentDetailPage","LearningPlayerPage"]){
    const source=fs.readFileSync(new URL(`../src/pages/${page}.tsx`,import.meta.url),"utf8");
    assert.match(source,/studentScopeUrl\(/);
  }
});
test("teacher application save waits for every class page and cannot treat a failed class fetch as deselection",()=>{
  const source=fs.readFileSync(new URL("../src/components/teacher/CurriculumApplicationPanel.tsx",import.meta.url),"utf8");
  assert.match(source,/page<=first.meta.totalPages/);
  assert.match(source,/teacherId:actor, subjectId/);
  assert.match(source,/disabled=\{save.isPending \|\| applications.isPending \|\| classes.isPending \|\| classes.isError \|\| stale \|\| !dirty/);
});
