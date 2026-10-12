import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";
const source=fs.readFileSync(new URL('../src/components/teacher/QuestionImportModal.tsx',import.meta.url),'utf8');
const start=source.indexOf('  const handleReset ='),end=source.indexOf('  const downloadCsvTemplate =',start);
const code=ts.transpileModule(source.slice(start,end),{compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.None}}).outputText;
function run(isOpen:boolean){
 const state:Record<string,unknown>={SelectedFile:'old.csv',PreviewData:{previewToken:'consumed'},PreviewError:'old',ConfirmError:'old',ImportResult:{importedCount:1}};
 const c:Record<string,any>={isOpen,fileInputRef:{current:{value:'old.csv'}},useCallback:(fn:unknown)=>fn};
 for(const key of Object.keys(state))c['set'+key]=(v:unknown)=>{state[key]=v;};
 c.useEffect=(fn:()=>void,deps:unknown[])=>{assert.equal(deps[0],isOpen);fn();};
 vm.createContext(c);vm.runInContext(code,c);return {state,c};
}
test('opening import after success removes previous file, consumed preview and result',()=>{
 const {state,c}=run(true);for(const value of Object.values(state))assert.equal(value,null);
 assert.equal(c.fileInputRef.current.value,'');
});
test('closed import does not erase its in-flight state during render',()=>{
 const {state,c}=run(false);assert.equal(state.SelectedFile,'old.csv');assert.deepEqual(state.ImportResult,{importedCount:1});
 assert.equal(c.fileInputRef.current.value,'old.csv');
});
test('the production topic selector excludes inactive graph nodes',()=>{
 const predicate=source.match(/\.filter\((\(node\) => node\.isActive)\)/)?.[1];
 assert.ok(predicate,'Topic options must filter active nodes');
 const c:Record<string,any>={};vm.createContext(c);vm.runInContext('globalThis.active='+predicate,c);
 const nodes=[{nodeId:'active',isActive:true},{nodeId:'inactive',isActive:false}];
 assert.deepEqual(nodes.filter(c.active).map(n=>n.nodeId),['active']);
});
