import assert from "node:assert/strict";
import test from "node:test";
import {getAttemptFeedbackPresentation,getAttemptReviewNotice} from "../src/utils/attemptFeedbackPresentation.ts";
import type {AttemptFeedbackAnalysisDto} from "../src/types/learning.ts";
const grade = {source:"Deterministic",isCorrect:true,maxScore:10};
const analysis = (extra: Partial<AttemptFeedbackAnalysisDto>): AttemptFeedbackAnalysisDto => ({
  analysisId:"test",schemaVersion:"1.0",missingSteps:[],rootCauseNodes:[],feedback:"Rule-based feedback",
  isFallback:false,needsTeacherReview:true,hasTeacherOverride:false,...extra,
});
test("practice fallback never promises a teacher review and preserves deterministic grading",()=>{
  const a=analysis({isFallback:true});
  const presentation=getAttemptFeedbackPresentation(grade,a,"NeedsTeacherReview");
  const notice=getAttemptReviewNotice(presentation,a,true)!;
  assert.equal(notice.title,"Phân tích AI chưa hoàn tất");
  assert.doesNotMatch(notice.explanation,/giáo viên|duyệt điểm/);
  assert.match(notice.explanation,/thử chấm lại AI/);assert.equal(grade.isCorrect,true);
});
test("practice disagreement calls for checking rather than a nonexistent teacher queue",()=>{
  const a=analysis({answerAssessment:"Incorrect",suggestedScore:3});
  const notice=getAttemptReviewNotice(getAttemptFeedbackPresentation(grade,a,"NeedsTeacherReview"),a,true)!;
  assert.equal(notice.title,"Phân tích cần kiểm tra");assert.doesNotMatch(notice.title+notice.explanation,/giáo viên/);
});
test("assignment AI proposal continues to require teacher approval",()=>{
  const a=analysis({suggestedScore:8});
  const notice=getAttemptReviewNotice(getAttemptFeedbackPresentation(grade,a,"NeedsTeacherReview"),a)!;
  assert.equal(notice.title,"AI đã chấm · Chờ giáo viên duyệt điểm");assert.match(notice.explanation,/chốt điểm chính thức/);
});
test("assignment manual fallback retains teacher review and completed work gets no warning",()=>{
  const a=analysis({isFallback:true});
  const p=getAttemptFeedbackPresentation({...grade,source:"PendingTeacher",reasonCode:"MANUAL_MODE"},a,"NeedsTeacherReview");
  assert.match(getAttemptReviewNotice(p,a,false)!.title,/giáo viên/);
  assert.equal(getAttemptReviewNotice(getAttemptFeedbackPresentation(grade,null,"Completed"),null,true),null);
});
