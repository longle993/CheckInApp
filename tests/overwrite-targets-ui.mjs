import { chromium } from 'file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const credentials=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));
const browser=await chromium.launch({channel:'chrome'});const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto('http://127.0.0.1:5175/');await page.getByLabel('Email').fill(credentials.email);await page.getByLabel('Mật khẩu').fill(credentials.password);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.getByRole('heading',{name:'Điểm danh hôm nay',exact:true}).waitFor();
 for(const route of ['hocsinh','glv','lop']){
  await page.evaluate(r=>location.hash=r,route);const box=page.getByLabel('Chép đè dữ liệu đã có');await box.waitFor();assert.equal(await box.isChecked(),false);await box.check();assert.equal(await box.isChecked(),true);
 }
 await page.evaluate(()=>location.hash='lich');await page.getByRole('button',{name:'Cấu hình lịch',exact:true}).click();
 const hs=page.getByLabel('HS — Điểm danh học sinh',{exact:true}),glv=page.getByLabel('GLV — Điểm danh giáo lý viên',{exact:true});await hs.waitFor();
 await hs.check();await glv.uncheck();assert.ok(await hs.isChecked());assert.ok(!await glv.isChecked());await hs.uncheck();await glv.check();assert.ok(!await hs.isChecked());assert.ok(await glv.isChecked());await hs.check();assert.ok(await hs.isChecked()&&await glv.isChecked());
 await page.screenshot({path:'.local/ui-qa/schedule-targets.png'});await page.getByRole('button',{name:'Hủy',exact:true}).click();
 await page.getByRole('button',{name:'Ngày nghỉ / bổ sung',exact:true}).click();await hs.waitFor();await glv.waitFor();await page.getByRole('button',{name:'Hủy',exact:true}).click();
 assert.deepEqual(errors,[]);console.log('PASS: overwrite checkbox defaults off on three catalogs; HS/GLV independent in schedule and per-date exception; no JS errors.');
} finally {await browser.close();}
