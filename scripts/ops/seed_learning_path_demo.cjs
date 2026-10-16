// Explicitly requested local demo content. Adds questions only; never adds grades/evidence or rewrites old work.
const assert=require('node:assert/strict');const fs=require('node:fs');
const {mysql,readEnv,sqlString}=require('./mysql_admin.cjs');
const tenses=[
 ['Hiện tại đơn: I ___ to school every day. (go)','go','Chủ ngữ I dùng động từ nguyên mẫu ở hiện tại đơn.'],
 ['Hiện tại đơn: She ___ English on Mondays. (study)','studies','Chủ ngữ she: study đổi y thành ies.'],
 ['Hiện tại tiếp diễn: Look! They are ___ football. (play)','playing','Sau are ở thì tiếp diễn dùng V-ing: playing.'],
 ['Quá khứ đơn: We ___ a film yesterday. (watch)','watched','Yesterday chỉ quá khứ; watch là động từ có quy tắc, thêm ed.'],
 ['Quá khứ đơn: He ___ to the library last Sunday. (go)','went','Quá khứ của go là went.'],
 ['Hiện tại hoàn thành: I have ___ my homework. (finish)','finished','Sau have dùng quá khứ phân từ finished.'],
 ['Hiện tại đơn phủ định: She does not ___ coffee. (drink)','drink','Sau does not, động từ chính trở về nguyên mẫu.'],
 ['Quá khứ đơn nghi vấn: Did you ___ him yesterday? (see)','see','Did đã mang dấu hiệu quá khứ nên động từ chính là see.'],
 ['Hiện tại tiếp diễn: My brother is ___ a book now. (read)','reading','Now và is yêu cầu dạng reading.'],
 ['Tương lai đơn: They will ___ us tomorrow. (visit)','visit','Sau will dùng động từ nguyên mẫu visit.'],
 ['Quá khứ tiếp diễn: At 8 p.m. yesterday, I was ___ dinner. (cook)','cooking','Was + V-ing diễn tả việc đang xảy ra tại một thời điểm quá khứ.'],
 ['Hiện tại hoàn thành: She has ___ in this city since 2020. (live)','lived','Since nêu mốc bắt đầu; sau has dùng quá khứ phân từ lived.'],
 ['Hiện tại đơn: Water ___ at 100 degrees Celsius at standard pressure. (boil)','boils','Sự thật khoa học dùng hiện tại đơn; water là chủ ngữ số ít.'],
 ['Quá khứ đơn phủ định: We did not ___ the bus. (take)','take','Sau did not dùng take, không dùng took.'],
 ['Hiện tại hoàn thành: Have you ever ___ sushi? (eat)','eaten','Quá khứ phân từ của eat là eaten.'],
 ['Quá khứ hoàn thành: By the time I arrived, the train had ___. (leave)','left','Had + quá khứ phân từ diễn tả sự việc xảy ra trước một việc quá khứ khác.'],
 ['Hiện tại hoàn thành tiếp diễn: She has been ___ for two hours. (study)','studying','Has been + V-ing nhấn mạnh quá trình kéo dài.'],
 ['Quá khứ tiếp diễn: While I was ___, the phone rang. (sleep)','sleeping','Hành động nền đang diễn ra dùng was sleeping; cuộc gọi xen vào ở quá khứ đơn.'],
 ['Tương lai hoàn thành: By next Friday, we will have ___ the project. (complete)','completed','Will have + quá khứ phân từ diễn tả việc hoàn tất trước một mốc tương lai.'],
 ['Quá khứ hoàn thành: He had already ___ when we called. (leave)','left','Việc rời đi xảy ra trước cuộc gọi trong quá khứ, nên dùng had left.'],
];
const relative=[
 ['Điền who hoặc which: The student ___ won the prize is my friend.','who','Student chỉ người, làm chủ ngữ trong mệnh đề, dùng who.'],
 ['Điền who hoặc which: The bicycle ___ is outside belongs to me.','which','Bicycle chỉ vật, dùng which.'],
 ['Điền whose hoặc who: The boy ___ father is a doctor lives next door.','whose','Whose diễn tả quan hệ sở hữu: cha của cậu bé.'],
 ['Điền where hoặc when: This is the school ___ I study.','where','School là nơi chốn; where thay cho at which trong ngữ cảnh này.'],
 ['Điền where hoặc when: I remember the day ___ we first met.','when','Day là thời điểm; dùng when.'],
 ['Điền who hoặc which: The woman ___ teaches maths is very kind.','who','Woman chỉ người và là chủ ngữ của teaches.'],
 ['Điền whose hoặc which: The car ___ engine broke down is old.','whose','Whose có thể chỉ sở hữu của vật: động cơ của chiếc xe.'],
 ['Điền which hoặc where: The museum ___ we visited was interesting.','which','Sau visited cần tân ngữ chỉ vật; không dùng trạng từ where.'],
 ['Điền where hoặc which: This is the house ___ she was born.','where','Was born cần địa điểm, có thể diễn đạt in which.'],
 ['Điền who hoặc whose: A teacher ___ listens carefully can help students.','who','Thiếu chủ ngữ chỉ người của listens, dùng who.'],
 ['Điền that hoặc who: Everything ___ he said was true.','that','Sau đại từ bất định everything, dùng that trong mệnh đề xác định.'],
 ['Điền whose hoặc who: The girl ___ bag was lost is waiting here.','whose','Trước danh từ bag cần quan hệ sở hữu whose.'],
 ['Điền which hoặc who: My laptop, ___ I bought last year, still works.','which','Mệnh đề không xác định về vật sau dấu phẩy dùng which.'],
 ['Điền who hoặc that: Ms Lan, ___ teaches us English, is absent today.','who','Mệnh đề không xác định về người dùng who; không dùng that sau dấu phẩy.'],
 ['Điền whom hoặc whose: The person to ___ I spoke was helpful.','whom','Sau giới từ to đặt trước đại từ quan hệ chỉ người, dùng whom.'],
 ['Điền which hoặc where: The town in ___ she lives is quiet.','which','Giới từ in đã có sẵn, dùng which; không lặp chức năng của where.'],
 ['Điền whose hoặc whom: The author ___ novel won the award is here.','whose','Novel thuộc về author nên dùng whose.'],
 ['Điền which hoặc that: He missed the bus, ___ made him late.','which','Which có thể thay cho cả mệnh đề trước trong mệnh đề bổ sung.'],
 ['Điền whom hoặc whose: The colleague with ___ I work is friendly.','whom','Sau with ở vị trí này dùng whom chỉ người.'],
 ['Điền when hoặc which: The year in ___ they moved was 2010.','which','Đã có in nên dùng which, không dùng in when.'],
];
const vocabulary=[
 ['Chọn từ đúng: Please ___ attention to the instructions. (pay / make)','pay','Cụm cố định là pay attention.'],
 ['Chọn từ đúng: We need to ___ a decision today. (make / do)','make','Cụm cố định là make a decision.'],
 ['Chọn từ đúng: She is interested ___ science. (in / on)','in','Interested đi với giới từ in.'],
 ['Chọn từ đúng: The opposite of expensive is ___. (cheap / tall)','cheap','Cheap nghĩa là rẻ, trái nghĩa với expensive.'],
 ['Chọn từ đúng: I ___ a mistake in my answer. (made / did)','made','Dùng make a mistake; made là dạng quá khứ.'],
 ['Chọn từ đúng: He is good ___ solving problems. (at / in)','at','Cấu trúc good at + danh từ hoặc V-ing.'],
 ['Chọn từ đúng: Please ___ your homework before dinner. (do / make)','do','Bài tập về nhà đi với do homework.'],
 ['Chọn từ đúng: Can you ___ me your pen? (lend / borrow)','lend','Người được hỏi cho mượn bút nên dùng lend; borrow là mượn từ người khác.'],
 ['Chọn từ đúng: I need to ___ a book from the library. (borrow / lend)','borrow','Chủ ngữ nhận sách để dùng tạm nên dùng borrow.'],
 ['Chọn từ đúng: We arrived ___ the airport early. (at / on)','at','Arrive at dùng cho địa điểm như sân bay.'],
 ['Chọn từ đúng: The exam was difficult; ___, she passed. (however / because)','however','Hai vế tương phản nên dùng however.'],
 ['Chọn từ đúng: Regular practice can ___ your skills. (improve / prevent)','improve','Improve nghĩa là cải thiện, phù hợp với skills.'],
 ['Chọn từ đúng: He felt ___ after running five kilometres. (exhausted / exhausting)','exhausted','Người có cảm giác mệt dùng exhausted; exhausting mô tả thứ gây mệt.'],
 ['Chọn từ đúng: The lecture was ___. (interested / interesting)','interesting','Bài giảng gây hứng thú được mô tả bằng interesting.'],
 ['Chọn từ đúng: She takes ___ for her actions. (responsibility / responsible)','responsibility','Sau takes ở cụm take responsibility cần danh từ.'],
 ['Chọn từ đúng: The company aims to ___ its environmental impact. (reduce / raise)','reduce','Mục tiêu giảm tác động môi trường dùng reduce.'],
 ['Chọn từ đúng: The evidence is not ___ to support that conclusion. (sufficient / superficial)','sufficient','Sufficient nghĩa là đủ, dùng để nói bằng chứng có đủ hay không.'],
 ['Chọn từ đúng: His explanation was clear and ___. (coherent / reluctant)','coherent','Coherent nghĩa là mạch lạc, phù hợp với explanation.'],
 ['Chọn từ đúng: We must ___ between facts and opinions. (distinguish / extinguish)','distinguish','Distinguish between nghĩa là phân biệt; extinguish là dập tắt.'],
 ['Chọn từ đúng: The results are ___ with our earlier findings. (consistent / persistent)','consistent','Consistent with nghĩa là nhất quán với.'],
];
const banks={'Thì':tenses,'Mệnh đề quan hệ':relative,'Từ vựng theo ngữ cảnh':vocabulary};
function seed(database){
 const rows=mysql(`SELECT n.center_id,n.subject_id,n.node_id,n.node_name,MIN(q.question_id) FROM knowledge_nodes n JOIN centers c ON c.center_id=n.center_id JOIN questions q ON q.center_id=n.center_id AND q.primary_topic_node_id=n.node_id WHERE c.center_code IN ('EDUTWIN_A','EDUTWIN_B') AND n.is_deleted=0 AND n.is_active=1 AND q.is_deleted=0 GROUP BY n.center_id,n.subject_id,n.node_id,n.node_name;`,database).trim().split(/\r?\n/).filter(Boolean);
 let added=0;const scopes=[];
 for(const row of rows){const [center,subject,node,name,base]=row.split('\t');if(!banks[name])continue;assert.match(node,/^\d+$/);assert.match(base,/^\d+$/);
  let sql='CREATE TEMPORARY TABLE learning_demo_question LIKE questions;';
  banks[name].forEach(([question,answer,solution],index)=>{
   const id=910000000000+Number(node)*100+index+1;assert.ok(Number.isSafeInteger(id));
   const current=mysql(`SELECT question_text FROM questions WHERE center_id=${sqlString(center)} AND question_id=${id};`,database).trim();
   if(current){assert.ok(current.startsWith('[DỮ LIỆU DÙNG THỬ]'), 'Reserved ID belongs to existing content; refusing overwrite');return;}
   sql+=`DELETE FROM learning_demo_question; INSERT INTO learning_demo_question SELECT * FROM questions WHERE center_id=${sqlString(center)} AND question_id=${base};
    UPDATE learning_demo_question SET question_id=${id},question_type='ShortAnswer',answer_evaluation_mode='TextExact',question_text=${sqlString('[DỮ LIỆU DÙNG THỬ] '+question+' Chỉ nhập từ/cụm từ còn thiếu.')},correct_answer=${sqlString(answer)},solution=${sqlString(solution)},expected_reasoning=${sqlString('Xác định yêu cầu và dấu hiệu trong câu, chọn quy tắc phù hợp, rồi đối chiếu lại đáp án với toàn bộ ngữ cảnh. '+solution)},difficulty=${index<10?2:index<15?3:4},grading_criteria=JSON_OBJECT(),reasoning_required=0,has_image=0,status='Active',is_deleted=0,max_score=10,estimated_time_seconds=60,created_at=UTC_TIMESTAMP(6),updated_at=UTC_TIMESTAMP(6),row_version=1;
    INSERT INTO questions SELECT * FROM learning_demo_question;`;added++;
  });sql+='DROP TEMPORARY TABLE learning_demo_question;';mysql(sql,database);scopes.push({subject,node,skill:name});
 }
 return {added,scopes,kind:'Clearly labelled local demo questions only; no Attempts or evidence inserted'};
}
if(require.main===module){const cfg=readEnv();assert.equal(cfg.ASPNETCORE_ENVIRONMENT||'Development','Development','Demo seed is local-development only');const result=seed(cfg.MYSQL_DATABASE);fs.mkdirSync('scratch',{recursive:true});fs.writeFileSync('scratch/v4-demo-seed.json',JSON.stringify(result,null,2));console.log(JSON.stringify({added:result.added,skills:result.scopes.length}));}
module.exports={seed};
