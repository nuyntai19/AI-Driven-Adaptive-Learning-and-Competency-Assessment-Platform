import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { abbreviateNodeName, buildNodeCodePrefix, formatNodeCode, isStandardNodeCode, nodeCodeSyntax, subjectGradeSegment } from "../src/utils/knowledgeNodeCode.ts";

test("prefixes match the agreed examples: [Môn][Khối]_[Loại]_[Tên viết tắt]", () => {
  const cases: [string, number, Parameters<typeof buildNodeCodePrefix>[0]["nodeType"], string, string][] = [
    ["MATH", 10, "Topic", "Phương trình bậc hai một ẩn", "MATH10_T_PTBH"],
    ["MATH", 10, "Chapter", "Mệnh đề và tập hợp", "MATH10_C_MDTH"],
    ["MATH", 12, "Skill", "Tìm cực trị của hàm số", "MATH12_S_CTHS"],
    ["ENG", 10, "Concept", "Mệnh đề quan hệ xác định", "ENG10_K_MDQH"],
    ["MATH", 12, "Topic", "Giải tích thực và phức", "MATH12_T_GTTP"],
  ];
  for (const [subjectCode, gradeLevel, nodeType, nodeName, expected] of cases)
    assert.equal(buildNodeCodePrefix({ subjectCode, gradeLevel, nodeType, nodeName }), expected, nodeName);
  assert.equal(formatNodeCode("MATH10_T_PTBH", 1), "MATH10_T_PTBH_01");
  assert.equal(formatNodeCode("MATH10_T_PTBH", 12), "MATH10_T_PTBH_12");
  assert.equal(formatNodeCode("MATH10_T_PTBH", 100), "MATH10_T_PTBH_100");
});

test("abbreviation: stop words matched with accents, numbers kept, single words truncated", () => {
  assert.equal(abbreviateNodeName("Mệnh đề và tập hợp"), "MDTH", "'đề' is not the stop word 'để'");
  assert.equal(abbreviateNodeName("Vẽ đồ thị hàm số"), "VDTH", "'vẽ' is not the stop word 'về'");
  assert.equal(abbreviateNodeName("Ôn tập về số phức"), "OTSP");
  assert.equal(abbreviateNodeName("Đường tròn"), "DT", "đ → D");
  assert.equal(abbreviateNodeName("Bài 3: Hàm số mũ"), "B3HS");
  assert.equal(abbreviateNodeName("Logarit"), "LOGA");
  assert.equal(abbreviateNodeName("  Tìm   GTLN – GTNN  "), "GG");
  assert.equal(abbreviateNodeName("Tìm"), "TIM", "a lone verb is still a name");
  assert.equal(abbreviateNodeName("và"), "VA", "only stop words: keep them rather than return nothing");
  assert.equal(abbreviateNodeName("!!!"), "");
  assert.equal(abbreviateNodeName("Present perfect and past simple"), "PPPS");
});

test("subject + grade segment does not duplicate a grade already in the subject code", () => {
  assert.equal(subjectGradeSegment("MATH", 10), "MATH10");
  assert.equal(subjectGradeSegment("TOAN12", 12), "TOAN12");
  assert.equal(subjectGradeSegment("TOAN12", 10), "TOAN1210");
  assert.equal(subjectGradeSegment("Tiếng Anh", 11), "TIENGANH11");
  assert.equal(subjectGradeSegment("MATH", undefined), "MATH");
  assert.equal(subjectGradeSegment("", 10), "");
});

test("no prefix until name and subject give one; prefixes leave room for the sequence", () => {
  assert.equal(buildNodeCodePrefix({ subjectCode: "MATH", gradeLevel: 10, nodeType: "Topic", nodeName: "" }), null);
  assert.equal(buildNodeCodePrefix({ subjectCode: "", gradeLevel: 10, nodeType: "Topic", nodeName: "Hàm số" }), null);
  const long = buildNodeCodePrefix({ subjectCode: "X".repeat(60), gradeLevel: 10, nodeType: "Topic", nodeName: "Hàm số" });
  assert.equal(long, null);
});

test("standard pattern accepts generated codes and rejects free-form ones", () => {
  for (const ok of ["MATH10_T_PTBH_01", "ENG10_K_MDQH_02", "TOAN12_C_B3HS_100"]) assert.ok(isStandardNodeCode(ok), ok);
  for (const bad of ["MATH10_PTBH", "MATH.LOG.01", "math10_t_ptbh_01", "MATH10_X_PTBH_01", "MATH10_T_PTBH_1", `${"A".repeat(45)}_T_B_01`])
    assert.ok(!isStandardNodeCode(bad), bad);
  assert.equal(nodeCodeSyntax, "[Môn][Khối]_[Loại]_[Tên viết tắt]_[STT]");
});

test("create modal: code is generated in real time from the server sequence, never guessed locally", () => {
  const view = fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx", import.meta.url), "utf8");
  assert.match(view, /placeholder=\{`Cú pháp tự sinh: \$\{nodeCodeSyntax\}`\}/);
  assert.doesNotMatch(view, /Ví dụ: MATH10_PTBH/);
  assert.match(view, /knowledgeGraphApi\.suggestNodeCode\(selectedSubjectId, nodeCodePrefix!, signal\)/);
  assert.match(view, /enabled: isAddNodeOpen && nodeCodeMode === "auto"/);
  assert.match(view, /details\.errorCode === "DUPLICATE_RESOURCE" && nodeCodeMode === "auto"/);
  assert.match(view, /Dùng mã tự sinh/);
  assert.match(view, /isStandardNodeCode\(trimmedCode\)/);
  const api = fs.readFileSync(new URL("../src/api/knowledgeGraphApi.ts", import.meta.url), "utf8");
  assert.match(api, /"\/knowledge\/nodes\/code-suggestion"/);
});
