import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import type { ClassDto } from "../src/types/organization.ts";
import {groupCurriculumClasses,needsNewGradeException,activeApplicationClassCount} from "../src/utils/curriculumApplicationClasses.ts";
const cls=(id:string,grade:number|null,overrides:Partial<ClassDto>={}):ClassDto=>({classId:id,className:id,gradeLevel:grade,academicYear:"2026",status:"Active",learningScope:"Current",
  teacher:{teacherId:"t",displayName:"Teacher"},subject:{subjectId:"math",subjectName:"Math"},studentCount:1,rowVersion:"1",...overrides});
test("Math 12 defaults to matching grade; other grades are explicit exceptions and unrelated classes never enter",()=>{
  const groups=groupCurriculumClasses([cls("12",12),cls("11",11),cls("10",10),cls("unknown",null),cls("archived",12,{status:"Archived",learningScope:"History"}),
    cls("otherTeacher",12,{teacher:{teacherId:"other",displayName:"Other"}}),cls("english",12,{subject:{subjectId:"en",subjectName:"English"}})],"t","math",12,[]);
  assert.deepEqual(groups.matching.map(c=>c.classId),["12"]);
  assert.deepEqual(groups.exceptions.map(c=>c.classId),["11","10"]);
});
test("existing unknown-grade applications stay selectable rather than silently being removed",()=>{
  const groups=groupCurriculumClasses([cls("legacy",null)],"t","math",12,["legacy"]);
  assert.equal(groups.exceptions.length,1);assert.equal(groups.eligible.length,1);
  assert.equal(needsNewGradeException(groups.eligible,["legacy"],["legacy"],12),false);
});
test("a new cross-grade assignment requires reason; unchanged exception and same-grade do not",()=>{
  const classes=[cls("12",12),cls("11",11)];
  assert.equal(needsNewGradeException(classes,["12"],[],12),false);
  assert.equal(needsNewGradeException(classes,["11"],[],12),true);
  assert.equal(needsNewGradeException(classes,["11"],["11"],12),false);
  assert.equal(needsNewGradeException(classes,["12"],[],null),true);
});
test("active count distinguishes current, ended and paused class configurations",()=>{
  assert.equal(activeApplicationClassCount([{classId:"c",endedAt:null},{classId:"c",endedAt:null},{classId:"old",endedAt:"2026-01-01"},{classId:"pause",endedAt:null,pausedByClass:true}]),1);
});
test("published editor has one application component and no disabled duplicate class selector",()=>{
  const source=fs.readFileSync(new URL("../src/pages/teacher/TeacherCurriculumEditorView.tsx",import.meta.url),"utf8");
  assert.match(source,/isDraft && !isCreateMode && <div/);assert.match(source,/Lớp dự kiến áp dụng/);
  assert.doesNotMatch(source,/Lớp Học Áp Dụng/);
  assert.equal((source.match(/<CurriculumApplicationPanel /g)||[]).length,1);
  assert.match(source,/gradeLevel=\{gradeLevel === "" \? null : Number\(gradeLevel\)\}/);
  const list=fs.readFileSync(new URL("../src/pages/teacher/TeacherCurriculumListView.tsx",import.meta.url),"utf8");
  assert.match(list,/"Lớp dự kiến" : "Lớp đang áp dụng"/);
  assert.match(list,/label="Đã Xuất Bản \(Published\)"/);
  assert.doesNotMatch(list,/label="Đang Áp Dụng \(Published\)"/);
});
test("application form protects unsaved choices from refetch and invalidates list as well as detail",()=>{
  const source=fs.readFileSync(new URL("../src/components/teacher/CurriculumApplicationPanel.tsx",import.meta.url),"utf8");
  assert.match(source,/if \(dirty \|\| !applications.data/);
  assert.match(source,/baseVersion !== applications.data.rowVersion/);
  assert.match(source,/rowVersion:baseVersion/);
  assert.match(source,/queryKey:\["teacherCurriculums"\]/);
  assert.match(source,/requiresGradeReason && !gradeReason.trim\(\)/);
  assert.match(source,/Hủy lựa chọn chưa lưu/);
  assert.match(source,/ẩn danh sách không bỏ gán lớp/);
});
