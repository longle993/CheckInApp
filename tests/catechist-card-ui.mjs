import { chromium } from 'file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
import fs from 'node:fs/promises';import assert from 'node:assert/strict';
const secrets=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));const fixture=JSON.parse(await fs.readFile('.local/ui-qa/catechist-scan-fixture.json','utf8'));
const browser=await chromium.launch({channel:'chrome'});const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
try{
 await page.goto('http://127.0.0.1:5175/');await page.getByLabel('Email').fill(secrets.email);await page.getByLabel('Mật khẩu').fill(secrets.password);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.getByRole('heading',{name:'Điểm danh hôm nay',exact:true}).waitFor();
 await page.evaluate(()=>location.hash='glv');await page.getByLabel('Tìm giáo lý viên').fill(fixture.teacherName);
 const issued=page.waitForResponse(r=>r.url().includes(`/catechists/${fixture.teacherId}/credential`) && r.request().method()==='POST');
 await page.locator('tr').filter({hasText:fixture.teacherName}).getByRole('button',{name:'Cấp thẻ',exact:true}).click();const card=await (await issued).json();
 for(const format of ['QR','Barcode']){await page.getByLabel('Định dạng thẻ').selectOption(format);await page.waitForTimeout(150);assert.match(await page.locator('#print-card').innerText(),/THẺ GIÁO LÝ VIÊN/);assert.equal(await page.locator('#print-card svg').count(),1);const box=await page.locator('#print-card').boundingBox();assert.ok(Math.abs(box.width-325.04)<1);await page.screenshot({path:`.local/ui-qa/glv-card-${format}.png`});}
 await page.getByRole('button',{name:'Đóng',exact:true}).last().click();await page.evaluate(()=>location.hash='quet');await page.getByLabel('Chọn buổi').selectOption(fixture.sessionId);
 await page.getByRole('heading',{name:'Quét thẻ giáo lý viên',exact:true}).waitFor();await page.getByLabel('Mã thẻ từ đầu đọc USB').fill(card.credential);await page.getByRole('button',{name:'Kiểm tra',exact:true}).click();await page.locator('.scan-result').waitFor();assert.ok((await page.locator('.scan-result').innerText()).includes('GLV · '+fixture.teacherName));
 await page.screenshot({path:'.local/ui-qa/glv-scan.png'});assert.deepEqual(errors,[]);console.log('PASS: issue teacher QR/Barcode card, 86x50mm, scanner visible in GLV-only session, scan result typed GLV and roster shown; no JS errors.');
}finally{await browser.close();}
