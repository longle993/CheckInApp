import { chromium } from 'file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const credentials=JSON.parse((await fs.readFile('.local/docker-integration/secrets.json','utf8')).replace(/^\uFEFF/,''));
const browser=await chromium.launch({channel:"chrome"});const page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
try {
 await page.goto('http://127.0.0.1:5175/');await page.getByLabel('Email').fill(credentials.email);await page.getByLabel('Mật khẩu').fill(credentials.password);await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.getByRole('heading',{name:'Điểm danh hôm nay',exact:true}).waitFor();
 await page.evaluate(()=>location.hash='hocsinh');await page.getByLabel('Tìm học sinh').fill('Nguyễn Ánh QA');await page.locator('tr').filter({hasText:'Nguyễn Ánh QA'}).first().getByRole('button',{name:'Cấp thẻ',exact:true}).click();await page.getByRole('dialog').waitFor();
 for(const format of ['QR','Barcode']) {await page.getByLabel('Định dạng thẻ').selectOption(format);await page.waitForTimeout(200);const box=await page.locator('#print-card').boundingBox();assert.ok(Math.abs(box.width-86*96/25.4)<1);assert.ok(Math.abs(box.height-50*96/25.4)<1);assert.equal(await page.locator('#print-card svg').count(),1);assert.ok(await page.locator('#print-card').innerText().then(s=>s.includes('Ngày sinh:')&&s.includes('Năm học:')));await page.screenshot({path:`.local/ui-qa/card-${format}.png`});await page.pdf({path:`.local/ui-qa/card-${format}.pdf`,preferCSSPageSize:true,printBackground:true});}
 await page.getByRole('button',{name:'Đóng',exact:true}).last().click();await page.getByRole('button',{name:'Thêm học sinh',exact:true}).click();await page.getByRole('dialog').getByLabel('Lớp học',{exact:true}).selectOption({index:1});assert.equal(await page.getByRole('dialog').locator('select').count(),1);await page.getByRole('button',{name:'Hủy',exact:true}).click();
 await page.evaluate(()=>location.hash='quet');await page.getByRole('heading',{name:'Giờ vào / ra và điểm danh giáo lý viên',exact:true}).waitFor();
 assert.deepEqual(errors,[]);console.log('PASS: QR/Barcode dimensions 86x50mm, full fields, print PDF, class select in modal, presence panel; no browser errors.');
} finally {await browser.close();}



