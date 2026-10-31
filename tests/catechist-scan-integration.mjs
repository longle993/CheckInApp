import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const base='http://127.0.0.1:5083/api/v1';
const secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));let token;
async function req(path,body,expected=200){const r=await fetch(base+path,{method:body?'POST':'GET',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{})});const text=await r.text();assert.equal(r.status,expected,`${path}: ${text.slice(0,200)}`);return text?JSON.parse(text):null;}
token=(await req('/auth/login',{email:secrets.email,password:secrets.password,client:'mobile'})).accessToken;
const day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date()),tag=Date.now().toString(36);
const y=await req('/catalog/years',{name:'GLV QR QA '+tag,startDate:day,endDate:day,isActive:false});
const teacher=await req('/catalog/catechists',{fullName:'GLV QR QA '+tag,saintName:'Giuse'});
const other=await req('/catalog/catechists',{fullName:'GLV other QA '+tag});
let teacherSession,studentSession;
for(const glv of [true,false]){
 const cls=await req('/catalog/classes',{name:`GLV card ${glv} ${tag}`,code:`GLV-${glv}-${tag}`,academicYearId:y.id});
 await req('/catalog/assignments',{classRoomId:cls.id,catechistId:teacher.id,from:day});
 await req('/catalog/schedules',{classRoomId:cls.id,from:day,until:day,daysMask:127,opensAt:'00:00',closesAt:'23:59',studentAttendanceEnabled:!glv,catechistAttendanceEnabled:glv});
 const ss=(await req(`/attendance-sessions?from=${day}&until=${day}&classId=${cls.id}`)).items[0];if(glv)teacherSession=ss;else studentSession=ss;
}
const card=await req(`/catechists/${teacher.id}/credential`,{});assert.ok(card.credential.startsWith('CG1-'));
const scan=(credential,id,key,method='QR')=>req(`/attendance-sessions/${id}/check-ins`,{credential,clientRequestId:key,method});
const first=await scan(card.credential,teacherSession.id,'glv-qr-'+tag);assert.equal(first.personType,'Catechist');assert.equal(first.resultCode,'Created');
assert.deepEqual(await scan(card.credential,teacherSession.id,'glv-qr-'+tag),first);
assert.equal((await scan(card.credential,teacherSession.id,'glv-bar-'+tag,'Barcode')).resultCode,'AlreadyCheckedIn');
const teacherRows=(await req(`/attendance-sessions/${teacherSession.id}/catechists`)).items;assert.equal(teacherRows.filter(x=>x.record).length,1);
assert.equal((await req(`/attendance-sessions/${teacherSession.id}/roster`)).items.length,0);
await req(`/attendance-sessions/${studentSession.id}/check-ins`,{credential:card.credential,clientRequestId:'disabled-'+tag},409);
const wrong=await req(`/catechists/${other.id}/credential`,{});await req(`/attendance-sessions/${teacherSession.id}/check-ins`,{credential:wrong.credential,clientRequestId:'wrong-'+tag},409);
const fresh=await req(`/catechists/${teacher.id}/credential`,{});await req(`/attendance-sessions/${teacherSession.id}/check-ins`,{credential:card.credential,clientRequestId:'revoked-'+tag},400);
assert.equal((await scan(fresh.credential,teacherSession.id,'fresh-'+tag,'Barcode')).resultCode,'AlreadyCheckedIn');
await fs.writeFile('.local/ui-qa/catechist-scan-fixture.json',JSON.stringify({teacherId:teacher.id,teacherName:teacher.fullName,sessionId:teacherSession.id}));
console.log('PASS: teacher-only QR, Code128 duplicate, retry, roster, disabled GLV, wrong assignment and revoked card on SQL Server.');
