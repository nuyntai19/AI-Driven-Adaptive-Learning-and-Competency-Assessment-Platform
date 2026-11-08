import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
const source=fs.readFileSync(new URL('../src/components/teacher/QuestionImportModal.tsx',import.meta.url),'utf8');
const start=source.indexOf('  const invalidate = useCallback'),end=source.indexOf('  useEffect(() => () =>',start);
const code=ts.transpileModule(source.slice(start,end),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.None}}).outputText;
function run(){
 const state:Record<string,unknown>={Preview:{previewToken:'consumed'},PreviewError:'old',ConfirmError:'old',Result:{importedCount:1}};
 let aborted=false,discarded=false;
 const c:Record<string,any>={isOpen:true,actor:{userId:'u',centerId:'c'},generation:{current:0},previewAbort:{current:{abort(){aborted=true;}}},
  discardSession:()=>{discarded=true;return Promise.resolve();},useCallback:(fn:unknown)=>fn,setLoading(){},setExplicitSubset(){},setSelectedKeys(){},setExpandedKeys(){}};
 for(const key of Object.keys(state))c['set'+key]=(v:unknown)=>{state[key]=v;};
 c.useEffect=(fn:()=>void,deps:unknown[])=>{assert.equal(deps[0],true);fn();};
 vm.createContext(c);vm.runInContext(code,c);return {state,aborted:()=>aborted,discarded:()=>discarded};
}
test('opening import after success removes consumed preview, errors and result',()=>{
 const {state,aborted,discarded}=run();
 for(const value of Object.values(state))assert.equal(value,null);
 assert.equal(aborted(),true);assert.equal(discarded(),true);
});
test('closing import clears the chosen file and its input',()=>{
 assert.match(source,/function close\(\) \{ if \(!confirming\) \{ resetFile\(\); onClose\(\); \} \}/);
 assert.match(source,/function resetFile\(\) \{ chooseFile\(null\); if \(fileInput\.current\) fileInput\.current\.value = ""; \}/);
});
test('the production topic selector excludes inactive graph nodes',()=>{
 const predicate=source.match(/\.filter\((\(node\) => node\.isActive[^)]*)\)/)?.[1];
 assert.ok(predicate,'Topic options must filter active nodes');
 const c:Record<string,any>={};vm.createContext(c);vm.runInContext('globalThis.active='+predicate,c);
 const nodes=[{nodeId:'active',isActive:true,nodeType:'Topic'},{nodeId:'inactive',isActive:false,nodeType:'Topic'}];
 assert.deepEqual(nodes.filter(c.active).map(n=>n.nodeId),['active']);
});
