import test from "node:test";
import assert from "node:assert/strict";
import { frameworkTitle } from "../src/utils/knowledgeFrameworkTitle.ts";

test("API-null, missing, empty and whitespace titles have a searchable display fallback", () => {
  for (const title of [null, undefined, "", " \t "]) {
    const version = { title, gradeLevel: 12 };
    assert.equal(frameworkTitle(version), "Khung Khối 12");
    assert.ok(frameworkTitle(version).toLocaleLowerCase("vi").includes("khung khối 12"));
    assert.equal(version.title, title, "display fallback never changes API data");
  }
  assert.equal(frameworkTitle({ title: null, gradeLevel: 10 }), "Khung Khối 10");
  assert.equal(frameworkTitle({ title: null, gradeLevel: 11 }), "Khung Khối 11");
});

test("named titles retain Vietnamese text and can be searched case-insensitively", () => {
  const version = { title: "  Toán siêu cấp  ", gradeLevel: 12 };
  assert.equal(frameworkTitle(version), "Toán siêu cấp");
  assert.ok(frameworkTitle(version).toLocaleLowerCase("vi").includes("TOÁN SIÊU CẤP".toLocaleLowerCase("vi")));
  assert.equal(version.title, "  Toán siêu cấp  ");
});
