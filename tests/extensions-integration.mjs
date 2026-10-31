import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const base=process.env.API_URL || 'http://127.0.0.1:5083/api/v1';
const secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));
let token;
async function request(path,body,method=body?'POST':'GET',expected=200){ const r=await fetch(base+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:`Bearer ${token}`}:{})},...(body?{body:JSON.stringify(body)}:{})});const text=await r.text();assert.equal(r.status,expected,`${method} ${path}: ${text.slice(0,200)}`);return text?JSON.parse(text):null; }
token=(await request('/auth/login',{email:secrets.email,password:secrets.password,client:'mobile'})).accessToken;
const day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date()),tag=Date.now().toString(36);
const y=await request('/catalog/years',{name:'Feature QA '+tag,startDate:day,endDate:day,isActive:false});
const c=await request('/catalog/classes',{name:'Feature QA '+tag,code:tag,academicYearId:y.id});
const student=await request('/catalog/students',{fullName:'Nguyễn Ánh QA '+tag,saintName:'Maria',dateOfBirth:'2016-01-01',classRoomId:c.id});
const teacher=await request('/catalog/catechists',{fullName:'Giáo lý viên QA '+tag,saintName:'Giuse'});
await request('/catalog/assignments',{classRoomId:c.id,catechistId:teacher.id,from:day});
await request('/catalog/schedules',{classRoomId:c.id,from:day,until:day,daysMask:127,opensAt:'00:00',closesAt:'23:59'});
const session=(await request(`/attendance-sessions?from=${day}&until=${day}&classId=${c.id}`)).items[0];
const card=await request(`/students/${student.id}/credential`,{});
const record=await request(`/attendance-sessions/${session.id}/check-ins`,{credential:card.credential,clientRequestId:'qa-'+tag});
const checkedInAtUtc=new Date(Date.parse(day+'T00:00:00+07:00')+60000).toISOString(),checkedOutAtUtc=new Date(Date.parse(checkedInAtUtc)+60000).toISOString();
await request(`/attendance-sessions/${session.id}/times/${student.id}`,{checkedInAtUtc,checkedOutAtUtc,version:record.version,reason:'QA điều chỉnh giờ'},'PUT',204);
assert.ok((await request(`/attendance-sessions/${session.id}/roster`)).items[0].checkedOutAtUtc);
await request(`/attendance-sessions/${session.id}/times/${teacher.id}?teacher=true`,{checkedInAtUtc,checkedOutAtUtc,version:null,reason:'QA điểm danh GLV'},'PUT',204);
assert.ok((await request(`/attendance-sessions/${session.id}/catechists`)).items[0].record.checkedOutAtUtc);
await request(`/attendance-sessions/${session.id}/times/${teacher.id}?teacher=true`,{checkedInAtUtc,checkedOutAtUtc,version:null,reason:'QA conflict'},'PUT',409);
for(const kind of ['students','catechists','classes']){
  const q=kind==='students'?`classId=${c.id}`:kind==='classes'?`yearId=${y.id}`:'';
  const r=await fetch(`${base}/excel/${kind}?${q}`,{headers:{Authorization:`Bearer ${token}`}});assert.equal(r.status,200);
  const bytes=Buffer.from(await r.arrayBuffer());assert.equal(bytes.subarray(0,2).toString(),'PK');
  const skipped=await request('/excel/'+kind,{base64:bytes.toString('base64'),...(kind==='students'?{classId:c.id}:kind==='classes'?{yearId:y.id}:{})});assert.ok(skipped.results.length>0);assert.ok(skipped.results.every(x=>x.success&&x.skipped),JSON.stringify(skipped));
  const result=await request('/excel/'+kind,{base64:bytes.toString('base64'),overwriteExisting:true,...(kind==='students'?{classId:c.id}:kind==='classes'?{yearId:y.id}:{})});assert.ok(result.results.every(x=>x.success&&!x.skipped),JSON.stringify(result));
}
await request('/excel/students',{base64:'bad'},'POST',400);
for (const [hs,glv] of [[true,false],[false,true],[true,true]]) {
 const cls=await request('/catalog/classes',{name:`Targets ${hs}-${glv} ${tag}`,code:`${hs}-${glv}-${tag}`,academicYearId:y.id});
 await request('/catalog/students',{fullName:'Target HS '+tag,saintName:'Maria',dateOfBirth:'2016-01-01',classRoomId:cls.id});
 await request('/catalog/assignments',{classRoomId:cls.id,catechistId:teacher.id,from:day});
 await request('/catalog/schedules',{classRoomId:cls.id,from:day,until:day,daysMask:127,opensAt:'00:00',closesAt:'23:59',studentAttendanceEnabled:hs,catechistAttendanceEnabled:glv});
 const ss=(await request(`/attendance-sessions?from=${day}&until=${day}&classId=${cls.id}`)).items[0];
 assert.equal(ss.studentAttendanceEnabled,hs);assert.equal(ss.catechistAttendanceEnabled,glv);
 assert.equal((await request(`/attendance-sessions/${ss.id}/roster`)).items.length,hs?1:0);
 if(!hs) await request(`/attendance-sessions/${ss.id}/check-ins`,{credential:card.credential,clientRequestId:'disabled-'+tag},'POST',409);
 await request(`/attendance-sessions/${ss.id}/times/${teacher.id}?teacher=true`,{checkedInAtUtc,checkedOutAtUtc,version:null,reason:'QA targets'},'PUT',glv?204:409);
}
await request('/catalog/schedules',{classRoomId:c.id,from:day,until:day,daysMask:127,opensAt:'00:00',closesAt:'23:59',studentAttendanceEnabled:false,catechistAttendanceEnabled:false},'POST',400);
const role=await request('/catalog/roles',{name:'Feature no Excel '+tag,permissions:((1n<<0n)|(1n<<2n)|(1n<<4n)|(1n<<10n)).toString(),globalScope:true});
const password='Qa!'+crypto.randomUUID();const u=await request('/catalog/users',{email:`qa-${tag}@example.test`,password,fullName:'QA quyền'});
await request(`/users/${u.id}/approve`,{roleId:role.id,catechistId:null},'POST',204);
token=(await request('/auth/login',{email:`qa-${tag}@example.test`,password,client:'mobile'})).accessToken;
for(const kind of ['students','catechists','classes']) {await request('/excel/'+kind,undefined,'GET',403);await request('/excel/'+kind,{base64:''},'POST',403);}
await request(`/attendance-sessions/${session.id}/times/${teacher.id}?teacher=true`,{checkedInAtUtc,checkedOutAtUtc,version:null,reason:'QA denied'},'PUT',403);
console.log('PASS: SQL migration; student/teacher times; concurrency; 3 Excel round trips; invalid file; 7 permission denials.');
