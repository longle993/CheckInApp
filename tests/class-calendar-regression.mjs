import { chromium } from 'file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
import fs from 'node:fs/promises';import assert from 'node:assert/strict';
const secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));
const base='http://127.0.0.1:5083/api/v1', web='http://127.0.0.1:5175';
const login=await fetch(base+'/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:secrets.email,password:secrets.password,client:'mobile'})}).then(r=>r.json());
const tag=Date.now().toString(36),day=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Ho_Chi_Minh'}).format(new Date());
async function createYear(suffix) {const r=await fetch(base+'/catalog/years',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+login.accessToken},body:JSON.stringify({name:`Class calendar QA ${tag} ${suffix}`,startDate:day,endDate:day,isActive:false})});assert.equal(r.status,200);return r.json();}
const source=await createYear('A'),dest=await createYear('B');
const browser=await chromium.launch({channel:'chrome'}), page=await browser.newPage({viewport:{width:1440,height:900}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto(web);await page.getByLabel('Email').fill(secrets.email);await page.getByLabel('Mật khẩu').fill(secrets.password);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.getByRole('heading',{name:'Điểm danh hôm nay',exact:true}).waitFor();
 await page.getByLabel('Năm học',{exact:true}).selectOption(source.id);await page.evaluate(()=>location.hash='lop');await page.getByLabel('Tìm lớp học').fill('no-matching-old-filter');await page.getByRole('button',{name:'Tạo lớp học',exact:true}).click();
 const modal=page.locator('section[aria-modal="true"]');await modal.getByLabel('Tên lớp').fill('Lớp mới QA '+tag);await modal.getByLabel('Mã lớp').fill('QA-'+tag);await modal.getByLabel('Năm học',{exact:true}).selectOption(dest.id);await modal.getByRole('button',{name:'Lưu',exact:true}).click();await modal.waitFor({state:'hidden'});
 await page.locator('tr').filter({hasText:'Lớp mới QA '+tag}).waitFor();assert.equal(await page.getByLabel('Năm học',{exact:true}).inputValue(),dest.id);assert.equal(await page.getByLabel('Tìm lớp học').inputValue(),('QA-'+tag).toUpperCase());
 for(const [width,height] of [[1440,900],[768,650],[375,640]]) {
  await page.setViewportSize({width,height});await page.evaluate(()=>location.hash='hocsinh');await page.getByRole('button',{name:'Thêm học sinh',exact:true}).click();await modal.getByRole('button',{name:'Ngày sinh',exact:true}).click();
  const calendar=page.locator('.calendar-pop');await calendar.waitFor();
  async function visible(){const b=await calendar.boundingBox();assert.ok(b.x>=0&&b.y>=0&&b.x+b.width<=width+1&&b.y+b.height<=height+1,JSON.stringify({width,height,b}));const last=calendar.locator('.dategrid button').last();const clickable=await last.evaluate(e=>{const b=e.getBoundingClientRect();return document.elementFromPoint(b.x+b.width/2,b.y+b.height/2)===e;});assert.ok(clickable,'Last date is clickable without scrolling modal');}
  await visible();await calendar.getByLabel('Tháng sau').click();await visible();await page.screenshot({path:`.local/ui-qa/calendar-fixed-${width}.png`});await page.keyboard.press('Escape');await calendar.waitFor({state:'hidden'});await modal.waitFor();await modal.getByRole('button',{name:'Hủy',exact:true}).click();
 }
 assert.deepEqual(errors,[]);console.log('PASS: created class visible across year/search filters; full calendar clickable at 1440/768/375; Escape preserves form; no JS errors.');
} finally {await browser.close();}
