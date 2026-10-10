import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { shouldStartAssignment } from "../src/utils/assignmentReviewTiming.ts";
import type { StudentAssignmentDetailDto } from "../src/types/assignments.ts";
const read=(file:string)=>fs.readFileSync(new URL(`../src/${file}`,import.meta.url),"utf8");

test("an unfinished archived assignment never starts its timer",()=>{
  const assignment: StudentAssignmentDetailDto={assignmentId:"synthetic",title:"Synthetic",instructions:null,dueAt:null,isReadOnly:true,
    progress:{status:"NotStarted",completedQuestionCount:0,totalQuestionCount:1},questions:[]};
  assert.equal(shouldStartAssignment(assignment),false);
  assert.equal(shouldStartAssignment({...assignment,isReadOnly:false}),true);
});
test("adaptive practice does not fetch or submit while the selected learning scope is read-only",()=>{
  const page=read("pages/LearningPlayerPage.tsx");
  assert.match(page,/enabled: learningAccess.writable && !assignmentId/);
  assert.match(page,/getNextQuestion\(subjectId, learningAccess.classId, isHistory\)/);
  assert.match(page,/classReadOnly \|\| \(!assignmentId && !learningAccess.writable\)/);
  assert.match(page,/classReadOnlyRef.current \|\| !assignmentId/);
  assert.match(page,/history: isHistory/);assert.match(page,/disabled=\{!learningAccess.writable\}/);
});
test("learning path generation and progress require writable access and keep class context",()=>{
  const page=read("pages/StudentLearningPathPage.tsx");
  assert.equal((page.match(/enabled: !!effectiveSubjectId && access.writable/g)||[]).length,3);
  assert.match(page,/if \(access.readOnly\) return <StudentLearningReadOnlyNotice/);
  assert.match(page,/await startSessionMutation.mutateAsync\(sessionId\)/);
  assert.match(page,/access.classId, access.history/);assert.match(page,/history: access.history/);
  assert.match(page,/navigate\(`\/hoc-tap\/luyen-tap\?\$\{p\}`\)/);
});
test("historical assignment navigation is review-only and the notice is not a fake historical path",()=>{
  const page=read("pages/StudentAssignmentDetailPage.tsx");
  assert.match(page,/assignment.isReadOnly === true \|\| searchParams.get\("history"\) === "true"/);
  assert.match(page,/isAssignmentFinished \|\| isReadOnly/);
  assert.match(read("components/student/StudentLearningReadOnlyNotice.tsx"),/không được coi là bản lưu lịch sử riêng/);
  assert.match(read("api/learningPathApi.ts"),/classId:classId \|\| undefined, history/);
});

test("a rejected class bookmark is reconciled before enabling new learning",()=>{
  const hook=read("hooks/useStudentLearningAccess.ts");
  assert.match(hook,/getReconciledStudentAcademicContext\(subjectId, classId, history\)/);
  assert.match(hook,/query.data.selectedClassId !== classId/);
  assert.match(hook,/query.isPending \|\| selectionChanging/);
  assert.match(hook,/writable: !pending && !readOnly/);
});
