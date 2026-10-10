import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { fitGraphCamera, cameraViewBox, zoomGraphCamera, panGraphCamera, graphNeighborhood, graphLabelLines } from "../src/utils/knowledgeGraphViewport.ts";
import { computeDeterministicDagLayout } from "../src/utils/knowledgeGraphLayout.ts";
import type { KnowledgeGraphNodeDto, KnowledgeGraphEdgeDto } from "../src/types/knowledgeGraph.ts";

const node = (id: string, parent: string | null = null): KnowledgeGraphNodeDto => ({nodeId:id,nodeName:`Chủ đề ${id}`,nodeCode:id,nodeType:"Topic",parentNodeId:parent,isActive:true,orderIndex:Number(id),examImportance:3,estimatedLearningMinutes:90,rowVersion:"1",description:null});
const edge = (a:string,b:string): KnowledgeGraphEdgeDto => ({edgeId:`${a}-${b}`,sourceNodeId:a,targetNodeId:b,relationType:"PrerequisiteOf",weight:1,rowVersion:"1"});

for (const viewport of [{width:320,height:580},{width:768,height:580},{width:1240,height:580}]) {
  test(`all 96 nodes fit inside a ${viewport.width}px viewport, including scales below the old 60% limit`, () => {
    const nodes=Array.from({length:96},(_,i)=>node(String(i)));
    const edges=nodes.slice(1).filter((_,i)=>i%3===0).map(n=>edge("0",n.nodeId));
    const layout=computeDeterministicDagLayout(nodes,edges,{cardWidth:240,cardHeight:100,gapY:28,paddingX:50,paddingY:50,minWidth:340,minHeight:200});
    const camera=fitGraphCamera(layout,viewport), view=cameraViewBox(camera,viewport);
    assert.ok(camera.scale>0 && camera.scale<0.6);
    for(const p of layout.positions.values()){
      assert.ok(p.x>=view.x-1e-8 && p.y>=view.y-1e-8);
      assert.ok(p.x+240<=view.x+view.width+1e-8 && p.y+100<=view.y+view.height+1e-8);
    }
  });
}
test("fit is finite for an empty graph and preserves the physical aspect ratio",()=>{
  const size={width:400,height:580},camera=fitGraphCamera({width:0,height:0},size),view=cameraViewBox(camera,size);
  assert.ok(Number.isFinite(camera.scale));assert.equal(view.width/view.height,size.width/size.height);
});
test("zoom retains center, allows complete overview of large graphs and caps magnification",()=>{
  const camera={x:1800,y:2000,scale:0.03};
  assert.deepEqual(zoomGraphCamera(camera,0.01,0.02),{...camera,scale:0.02});
  assert.deepEqual(zoomGraphCamera(camera,1000,0.02),{...camera,scale:2});
});
test("drag translates pixel distances to graph coordinates at the current scale",()=>{
  assert.deepEqual(panGraphCamera({x:100,y:100,scale:0.5},20,-30),{x:60,y:160,scale:0.5});
});
test("focus keeps direct relationships, real parent/children and never dangling edges",()=>{
  const nodes=[node("1","5"),node("2"),node("3","1"),node("4"),node("5")];
  const edges=[edge("1","2"),edge("2","4"),edge("1","missing")];
  const result=graphNeighborhood(nodes,edges,"1");
  assert.deepEqual(result.nodes.map(n=>n.nodeId),["1","2","3","5"]);
  assert.deepEqual(result.edges.map(e=>e.edgeId),["1-2"]);
  assert.equal(nodes.length,5); assert.equal(edges.length,3);
});
test("a hidden/deleted focus returns a complete valid overview, not an empty or orphaned graph",()=>{
  const result=graphNeighborhood([node("1"),node("2")],[edge("1","2"),edge("1","missing")],"missing");
  assert.equal(result.nodes.length,2);assert.equal(result.edges.length,1);
});
test("labels are bounded to two lines while the canvas keeps full accessible names",()=>{
  assert.equal(graphLabelLines("Một chủ đề rất dài với nhiều từ để hiển thị trong hai dòng có giới hạn mà không chồng sang nút bên cạnh").length,2);
  const longToken = graphLabelLines("X".repeat(200));
  assert.equal(longToken.length, 2); assert.ok(longToken.every(line => line.length <= 29));
  const source=fs.readFileSync(new URL("../src/components/teacher/KnowledgeGraphCanvas.tsx",import.meta.url),"utf8");
  assert.match(source,/aria-label=\{`Điểm tri thức: \$\{node.nodeName\}`\}/);
  assert.match(source,/<title>\{node.nodeName\}/);
  assert.doesNotMatch(source,/httpClient|knowledgeGraphApi|mutate\(/);
});
test("curriculum library filter is inside the same padded page container as its heading",()=>{
  const source=fs.readFileSync(new URL("../src/pages/teacher/TeacherCurriculumListView.tsx",import.meta.url),"utf8");
  assert.match(source,/return \(\s*<div className="th-page-container">\s*<label/);
  assert.equal((source.match(/className="th-page-container"/g)||[]).length,1);
});
test("manager report has responsive gutters and graph inspector stacks on narrower screens",()=>{
  const report=fs.readFileSync(new URL("../src/pages/CenterClassReportPage.tsx",import.meta.url),"utf8");
  assert.match(report,/mx-auto p-4 sm:p-6 lg:p-8/);
  const graph=fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx",import.meta.url),"utf8");
  assert.match(graph,/2xl:grid-cols-\[minmax\(0,1fr\)_340px\]/);
  assert.match(graph,/overflowWrap: "anywhere"/);
  assert.doesNotMatch(graph,/transformOrigin: "top center"|Math.max\(0.6/);
});
test("chapter selector uses actual node types and parent IDs, never infers grade groups from node codes",()=>{
  const canvas=fs.readFileSync(new URL("../src/components/teacher/KnowledgeGraphCanvas.tsx",import.meta.url),"utf8");
  assert.match(canvas,/Chương hoặc nhóm tri thức đang xem/);
  assert.match(canvas,/child.parentNodeId === n.nodeId/);
  assert.doesNotMatch(canvas,/nodeCode\.(startsWith|match)|MATH10|MATH11|MATH12/);
});
