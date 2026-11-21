import { useState } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { TeacherAssignmentEditorView } from "../../src/pages/teacher/TeacherAssignmentEditorView";
import { TeacherStudentTwinView } from "../../src/pages/teacher/TeacherStudentTwinView";
import { httpClient } from "../../src/api/httpClient";
import { useAuthStore } from "../../src/stores/authStore";
import { permissions } from "../../src/auth/permissions";
import "../../src/index.css";
const subject = {subjectId:"12345678-1234-5678-9abc-123456789abc", subjectName:"Toán", isActive:true};
const classes = ["class1", "class2"].map(classId => ({classId, className:classId, subject, gradeLevel:10, status:"Active", academicYear:"2026", teacher:{teacherId:"teacher"}}));
const students = [{studentId:"s1",fullName:"Học sinh Một",status:"Active"},{studentId:"s2",fullName:"Học sinh Hai",status:"Active"}];
const meta = {page:1,totalPages:1,totalItems:2};
const requests: string[] = []; Object.assign(window,{smokeState:{requests}});
httpClient.defaults.adapter = async config => {
 const url=config.url!; requests.push(url); let data: unknown;
 if(url.includes("/twin")) data={studentId:"s1",subjectId:"12345678-1234-5678-9abc-123456789abc",topics:[],behavior:{attemptCount:11,avgTimeSpentSeconds:30,skipRate:0,changeAnswerRate:0},history:[{historyId:"1",topicNodeId:"101",topicName:"Hàm số",eventSource:"RuleFallback",learningContext:"AdaptivePractice",contextLabel:"Luyện tập thích ứng",previousMastery:0,newMastery:5,delta:5,explanation:"Đúng trong luyện tập",recordedAt:new Date().toISOString()}]};
 else if(url.includes("/students/") && !url.includes("/classes/")) data={...students[0],classes: config.url!.endsWith("s2") ? [classes[0]] : classes};
 else if(url.includes("/classes/") && url.endsWith("/students")) data=students;
 else if(url.includes("/classes/")) data=classes.find(c=>url.endsWith(c.classId));
 else if(url.endsWith("/classes")) data=classes;
 else if(url.includes("/subjects/")) data=subject;
 else if(url.endsWith("/subjects")) data=[subject];
 else if(url.includes("/nodes")) data=[{nodeId:"101",nodeName:"Hàm số",nodeType:"Topic",subjectId:"12345678-1234-5678-9abc-123456789abc",isActive:true}];
 else if(url.endsWith("/questions")) data=[{questionId:"q1",questionText:"Câu hỏi",questionType:"ShortAnswer",difficulty:1,maxScore:1,gradeLevel:10}];
 else throw new Error("Unexpected API: "+url);
 return {config,data:{data,meta},status:200,statusText:"OK",headers:{}};
};
useAuthStore.setState({accessToken:"fixture",sessionStatus:"authenticated",user:{userId:"teacher",centerId:"center",centerName:"Center",username:"teacher",displayName:"Teacher",accountType:"Teacher",role:"Teacher",roles:[],authorizationVersion:1,permissions:Object.values(permissions)}});
const query = "subjectId=12345678-1234-5678-9abc-123456789abc&topicNodeId=101&gradeLevel=10&studentIds=s1";
function Fixture(){
 const [route,setRoute]=useState("/edit?classId=class1&"+query);
 return <><button id="full" onClick={()=>setRoute("/edit?classId=class1&"+query)}>Full</button><button id="multiple" onClick={()=>setRoute("/edit?"+query)}>Multiple</button><button id="unique" onClick={()=>setRoute("/edit?"+query.replace("s1","s2"))}>Unique</button><button id="twin" onClick={()=>setRoute("/twin/s1?subjectId=12345678-1234-5678-9abc-123456789abc")}>Twin</button>
 <QueryClientProvider key={route} client={new QueryClient({defaultOptions:{queries:{retry:false}}})}><MemoryRouter initialEntries={[route]}><Routes><Route path="/edit" element={<TeacherAssignmentEditorView/>}/><Route path="/twin/:studentId" element={<TeacherStudentTwinView/>}/></Routes></MemoryRouter></QueryClientProvider></>;
}
createRoot(document.getElementById("root")!).render(<Fixture/>);
