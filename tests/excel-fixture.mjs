import {execFileSync} from 'node:child_process';
const python='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe';
export function workbook(bytes,rows){return JSON.parse(execFileSync(python,['-X','utf8','-c',`import sys,json,base64,zipfile,io,xml.etree.ElementTree as E
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
