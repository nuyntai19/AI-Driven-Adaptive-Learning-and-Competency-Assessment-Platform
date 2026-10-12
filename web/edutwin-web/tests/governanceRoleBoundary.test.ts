import assert from "node:assert/strict";
import test from "node:test";
import { readFileSync } from "node:fs";
import ts from "typescript";
import { resolveCenterManagerLayoutAccess, resolveAcademicRedirect } from "../src/routes/academicRoutingHelpers.ts";

test("governance actor boundary rejects shared read permissions from other actors", () => {
  for (const accountType of ["Teacher", "Student", "PlatformAdmin"] as const)
    assert.equal(resolveCenterManagerLayoutAccess({ accountType }).allowed, false);
  assert.equal(resolveCenterManagerLayoutAccess({ accountType: "CenterManager" }).allowed, true);
});

test("real route tree guards governance pages and keeps teacher legacy redirects reachable", () => {
  const source = readFileSync(new URL("../src/App.tsx", import.meta.url), "utf8");
  const ast = ts.createSourceFile("App.tsx", source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const routes = new Map<string, boolean>();
  const visit = (node: ts.Node, insideGovernance: boolean) => {
    if (ts.isJsxElement(node)) {
      const attributes = node.openingElement.attributes.properties;
      insideGovernance ||= attributes.some(a => a.getText(ast).includes("CenterManagerLayoutBoundary"));
    }
    if (ts.isJsxSelfClosingElement(node)) {
      const path = node.attributes.properties.find(a => ts.isJsxAttribute(a) && a.name.getText(ast) === "path");
      if (path && ts.isJsxAttribute(path) && path.initializer && ts.isStringLiteral(path.initializer))
        routes.set(path.initializer.text, insideGovernance);
    }
    ts.forEachChild(node, child => visit(child, insideGovernance));
  };
  visit(ast, false);
  for (const path of ["/quan-ly/giao-vien", "/quan-ly/hoc-sinh", "/quan-ly/lop-hoc", "/quan-ly/mon-hoc", "/quan-ly/phan-quyen", "/quan-ly/tong-quan-trung-tam"])
    assert.equal(routes.get(path), true, path);
  for (const path of ["/kien-thuc/do-thi", "/quan-ly/giao-trinh", "/quan-ly/cau-hoi", "/quan-ly/bai-tap"])
    assert.equal(routes.get(path), false, path);
  assert.equal(resolveAcademicRedirect({ accountType: "Teacher" }, "/giao-vien/do-thi-tri-thuc"), "/giao-vien/do-thi-tri-thuc");
  assert.equal(resolveAcademicRedirect({ accountType: "Student" }, "/giao-vien/do-thi-tri-thuc"), "/khong-co-quyen");
});

test("actual layout executes the strict actor check instead of rendering an unguarded outlet", () => {
  const source = readFileSync(new URL("../src/layouts/CenterManagerLayout.tsx", import.meta.url), "utf8");
  const boundary = source.slice(source.indexOf("export function CenterManagerLayoutBoundary"));
  assert.match(boundary, /resolveCenterManagerLayoutAccess\(user\)/);
  assert.match(boundary, /if \(!access.allowed\)/);
  assert.match(boundary, /return <Navigate/);
  assert.doesNotMatch(boundary, /<Outlet/);
});
