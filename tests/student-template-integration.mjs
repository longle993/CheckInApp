import fs from 'node:fs/promises';import assert from 'node:assert/strict';import {execFileSync} from 'node:child_process';
const base='http://127.0.0.1:5083/api/v1',secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));let token;
async function req(path,body,method=body?'POST':'GET',expected=200){const r=await fetch(base+path,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},...(body?{body:JSON.stringify(body)}:{})});const text=await r.text();assert.equal(r.status,expected,`${path}: ${text.slice(0,250)}`);return text?JSON.parse(text):null;}
token=(await req('/auth/login',{email:secrets.email,password:secrets.password,client:'mobile'})).accessToken;
const tag=Date.now().toString(36),day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date());
const year=await req('/catalog/years',{name:'Vietnamese Excel QA '+tag,startDate:day,endDate:day,isActive:false});
const cls=await req('/catalog/classes',{name:'Excel HS QA '+tag,code:'VN-'+tag,academicYearId:year.id});
await req('/excel/students?template=true',undefined,'GET',400);
const r=await fetch(`${base}/excel/students?template=true&classId=${cls.id}`,{headers:{Authorization:'Bearer '+token}});assert.equal(r.status,200);const template=Buffer.from(await r.arrayBuffer());
const python='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';
function workbook(bytes,rows){return JSON.parse(execFileSync(python,['-X','utf8','-c',`import sys,json,base64,zipfile,io,xml.etree.ElementTree as E
d=json.load(sys.stdin);z=zipfile.ZipFile(io.BytesIO(base64.b64decode(d['base64'])));n='http://schemas.openxmlformats.org/spreadsheetml/2006/main';root=E.fromstring(z.read('xl/worksheets/sheet1.xml'));data=root.find('{'+n+'}sheetData');headers=[''.join(t.text or '' for t in c.iter('{'+n+'}t')) for c in list(data)[0]]
if 'rows' in d:
 for r in list(data)[1:]:data.remove(r)
 for i,values in enumerate(d['rows'],2):
  row=E.SubElement(data,'{'+n+'}row',r=str(i))
  for j,value in enumerate(values):
   c=E.SubElement(row,'{'+n+'}c',r=chr(65+j)+str(i),t='inlineStr');s=E.SubElement(c,'{'+n+'}is');E.SubElement(s,'{'+n+'}t').text=str(value)
 out=io.BytesIO()
 with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as target:
  for f in z.infolist():target.writestr(f.filename,E.tostring(root,encoding='utf-8') if f.filename=='xl/worksheets/sheet1.xml' else z.read(f.filename))
 print(json.dumps({'headers':headers,'base64':base64.b64encode(out.getvalue()).decode()}))
else:print(json.dumps({'headers':headers,'rows':[[''.join(t.text or '' for t in c.iter('{'+n+'}t')) for c in row] for row in list(data)[1:]]}))`],{input:JSON.stringify({base64:bytes.toString('base64'),...(rows?{rows}:{})}),encoding:'utf8',windowsHide:true}));}
assert.deepEqual(workbook(template).headers,['Tên Thánh','Họ và tên đệm','Tên','Ngày sinh']);
const family='Nguyễn Văn QA '+tag,given='An',row=['Maria',family,given,'01/02/2016'];
const payload=workbook(template,[row]).base64;const imported=await req('/excel/students',{base64:payload,classId:cls.id});assert.equal(imported.results[0].success,true,JSON.stringify(imported));
let students=(await req('/catalog/students?classId='+cls.id)).items;assert.equal(students.length,1);let s=students[0];assert.equal(s.familyName,family);assert.equal(s.givenName,given);assert.equal(s.fullName,family+' '+given);assert.equal(s.dateOfBirth,'2016-02-01');assert.ok(s.code.startsWith('HS'));
const oldId=s.id,oldCode=s.code,changed=workbook(template,[['Anna',family,given,'01/02/2016']]).base64;
assert.equal((await req('/excel/students',{base64:changed,classId:cls.id})).results[0].skipped,true);
assert.equal((await req('/catalog/students?classId='+cls.id)).items[0].saintName,'Maria');
assert.equal((await req('/excel/students',{base64:changed,classId:cls.id,overwriteExisting:true})).results[0].success,true);
s=(await req('/catalog/students?classId='+cls.id)).items[0];assert.equal(s.saintName,'Anna');assert.equal(s.id,oldId);assert.equal(s.code,oldCode);
await req('/excel/students',{base64:payload},'POST',400);
const bad=workbook(template,[['Maria',family,'Bình','31/02/2016']]).base64;assert.equal((await req('/excel/students',{base64:bad,classId:cls.id})).results[0].success,false);
const out=await fetch(`${base}/excel/students?classId=${cls.id}`,{headers:{Authorization:'Bearer '+token}});assert.equal(out.status,200);const exported=Buffer.from(await out.arrayBuffer());assert.deepEqual(workbook(exported).headers,workbook(template).headers);assert.deepEqual(workbook(exported).rows[0],['Anna',family,given,'01/02/2016']);
await req('/catalog/students',{familyName:family,givenName:given,saintName:'Maria',dateOfBirth:'2016-02-01',classRoomId:cls.id});
const ambiguous=await req('/excel/students',{base64:changed,classId:cls.id,overwriteExisting:true});assert.equal(ambiguous.results[0].success,false);assert.match(ambiguous.results[0].message,/nhiều học sinh/);
const legacy=await req('/catalog/students',{fullName:'Trần Ngọc Ánh '+tag,dateOfBirth:'2016-01-02',classRoomId:cls.id});assert.equal(legacy.givenName,tag);
const existing=(await req('/catalog/students?pageSize=100')).items;assert.ok(existing.every(x=>x.givenName && (x.familyName+' '+x.givenName).trim().replace(/\s+/g,' ')===x.fullName.trim().replace(/\s+/g,' ')),'Existing records backfilled');
await fs.writeFile('.local/ui-qa/student-template-fixture.json',JSON.stringify({classId:cls.id,yearId:year.id,familyName:family,givenName:given,fullName:family+' '+given,studentId:oldId}));await fs.writeFile('.local/ui-qa/student-template-input.xlsx',Buffer.from(workbook(template,[['Maria','Trần Văn UI '+tag,'Bảo','02/03/2016']]).base64,'base64'));
console.log('PASS: 4 Vietnamese headers; implicit class/ID/code; split names; overwrite/skip; invalid date/missing class/ambiguous match; exported dates; legacy migration.');
