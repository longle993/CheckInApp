import fs from 'node:fs/promises';import assert from 'node:assert/strict';import {workbook} from './excel-fixture.mjs';
const base='http://127.0.0.1:5083/api/v1',secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));let token;
async function req(path,body,method=body?'POST':'GET',expected=200){const r=await fetch(base+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{})});const text=await r.text();assert.equal(r.status,expected,`${path}: ${text.slice(0,200)}`);return text?JSON.parse(text):null;}
token=(await req('/auth/login',{email:secrets.email,password:secrets.password,client:'mobile'})).accessToken;
const tag=Date.now().toString(36),day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date());
const year=await req('/catalog/years',{name:'Import click QA '+tag,startDate:day,endDate:day,isActive:false}),cls=await req('/catalog/classes',{name:'Import click QA '+tag,code:'CLICK-'+tag,academicYearId:year.id});
async function file(kind,q='template=true'){const r=await fetch(`${base}/excel/${kind}?${q}`,{headers:{Authorization:'Bearer '+token}});assert.equal(r.status,200);return Buffer.from(await r.arrayBuffer());}
const template=await file('catechists');assert.deepEqual(workbook(template).headers,['Tên Thánh','Họ và tên đệm','Tên']);
const family='Lê Văn QA '+tag,given='Minh',payload=workbook(template,[['Giuse',family,given]]).base64;
assert.equal((await req('/excel/catechists',{base64:payload})).results[0].success,true);
let teacher=(await req('/catalog/catechists?q='+encodeURIComponent(family))).items[0];assert.equal(teacher.familyName,family);assert.equal(teacher.givenName,given);assert.equal(teacher.fullName,family+' '+given);const id=teacher.id;
assert.equal((await req('/excel/catechists',{base64:payload})).results[0].skipped,true);assert.equal((await req('/excel/catechists',{base64:payload,overwriteExisting:true})).results[0].success,true);
teacher=(await req('/catalog/catechists?q='+encodeURIComponent(family))).items[0];assert.equal(teacher.id,id);
assert.ok(workbook(await file('catechists','')).rows.some(r=>r[0]==='Giuse'&&r[1]===family&&r[2]===given));
const duplicate=await req('/catalog/catechists',{fullName:family+' '+given,saintName:'Giuse'});assert.equal((await req('/excel/catechists',{base64:payload,overwriteExisting:true})).results[0].success,false);
await req('/catalog/catechists/'+duplicate.id,{familyName:family,givenName:'Khác '+tag,saintName:'Giuse'},'PUT');
const old=(await req('/catalog/catechists')).items;assert.ok(old.every(x=>x.givenName&&(x.familyName+' '+x.givenName).trim().replace(/\s+/g,' ')===x.fullName.trim().replace(/\s+/g,' ')));
const studentTemplate=await file('students',`template=true&classId=${cls.id}`),studentFamily='Nguyễn Văn Click '+tag,teacherFamily='Phạm Văn Click '+tag;
await fs.writeFile('.local/ui-qa/import-clicks-student.xlsx',Buffer.from(workbook(studentTemplate,[['Maria',studentFamily,'An','01/02/2016']]).base64,'base64'));
await fs.writeFile('.local/ui-qa/import-clicks-teacher.xlsx',Buffer.from(workbook(template,[['Anna',teacherFamily,'Bảo']]).base64,'base64'));
await fs.writeFile('.local/ui-qa/import-clicks-fixture.json',JSON.stringify({classId:cls.id,yearId:year.id,studentFamily,teacherFamily}));
console.log('PASS: 3 Vietnamese teacher headers; split/full names; auto ID; skip/overwrite; ambiguity; old names backfilled; export round trip.');
