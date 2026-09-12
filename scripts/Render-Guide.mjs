// Draw editable native Skechu objects, load the SKC through Skechu's importer,
// and export with its opt-in command API. Uses a fresh, headless browser profile.
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {createRequire} from 'node:module';
const {chromium} = createRequire(import.meta.url)('playwright');
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const skechuRoot = process.env.SKECHU_ROOT;
if (!skechuRoot || !fs.existsSync(path.join(skechuRoot, 'app', 'index.html'))) {
  throw new Error('Set SKECHU_ROOT to a Skechu-PPT source checkout. See docs/media/README.md.');
}
const appRoot = path.resolve(skechuRoot, 'app');
const outputRoot = path.join(repo, 'docs', 'media');
fs.mkdirSync(outputRoot, {recursive: true});
const colors = {paper:'#101b25', card:'#1b2b39', text:'#f4f6fa', muted:'#b9c6d4', line:'#385267', blue:'#79c7ff', blueBg:'#163953', amber:'#ffc580', amberBg:'#493523', green:'#77dfc8'};
let counter = 0;
const shape = (type, rest) => ({id:'guide-'+(++counter), type, r:0, opacity:1, ...rest});
const box = (x,y,w,h,fill,stroke='none',radius=18,strokeWidth=2) => shape('box',{x,y,w,h,fill,stroke,strokeWidth,radius});
const text = (x,y,w,h,value,size=26,color=colors.text,bold=false,align='left') => shape('text',{x,y,w,h,text:value,size,color,bold,align,valign:'middle',box:true,lineHeight:1.28,fontFamily:'Microsoft JhengHei, Segoe UI, Arial, sans-serif'});
const arrow = (x1,y1,x2,y2,color=colors.green,width=5,head=17) => shape('arrow',{points:[{x:x1,y:y1},{x:x2,y:y2}],color,width,head,endHead:head>0,startHead:false,headShape:'triangle',smoothnessDefault:0});
const circle = (x,y,d,fill) => shape('ellipse',{x,y,w:d,h:d,fill,stroke:'none',strokeWidth:0});
function monitor(x,y,accent) {
  return [box(x,y,180,112,colors.paper,accent,12,3),box(x+13,y+13,154,74,colors.card,'none',5),box(x+76,y+115,28,18,accent,'none',3),box(x+40,y+133,100,7,accent,'none',3),
    box(x+56,y+26,66,49,'none',accent,4,2),circle(x+69,y+36,10,accent),arrow(x+68,y+62,x+82,y+47,accent,2,0),arrow(x+82,y+47,x+110,y+66,accent,2,0)];
}
function overview(lang) {
  const zh=lang==='zh';const items=[];
  items.push(text(60,40,1380,28,'GOOGLE CHROME REMOTE DESKTOP  /  IMAGE PASTE',20,colors.green,true));
  items.push(text(60,83,1400,73,zh?'截圖在本機，貼上在遠端':'Screenshot locally. Paste remotely.',48,colors.text,true));
  items.push(text(60,164,1400,42,zh?'兩台電腦各啟動一個工具；只需安裝一次。':'Run one helper on each computer. Set up your pair once.',25,colors.muted));
  items.push(box(60,235,590,422,colors.card),box(910,235,590,422,colors.card));
  items.push(box(84,259,182,38,colors.blueBg),text(95,259,160,38,zh?'本機 · 傳送端':'LOCAL · SENDER',19,colors.blue,true,'center'));
  items.push(box(934,259,242,38,colors.amberBg),text(945,259,220,38,zh?'遠端 · 接收端':'REMOTE · RECEIVER',19,colors.amber,true,'center'));
  items.push(...monitor(105,329,colors.blue),...monitor(955,329,colors.amber));
  items.push(text(317,328,300,54,zh?'你手邊的電腦':'Your own computer',27,colors.text,true));
  items.push(text(317,384,290,75,zh?'家裡電腦或筆電\n在這裡截圖':'Home PC or laptop\nCapture here',22,colors.muted));
  items.push(text(1167,328,302,54,zh?'被你控制的電腦':'The controlled PC',26,colors.text,true));
  items.push(text(1167,385,280,75,zh?'辦公室或實驗室\n在這裡貼上':'Office or lab desktop\nPaste here',22,colors.muted));
  items.push(box(90,512,530,63,colors.blueBg),text(90,512,530,63,'Win + Shift + S',31,colors.blue,true,'center'));
  items.push(box(940,512,530,63,colors.amberBg),text(940,512,530,63,'Ctrl + V',31,colors.amber,true,'center'));
  items.push(text(90,587,530,43,zh?'截完圖，自動傳送，不用另存檔':'Capture → send automatically',23,colors.muted,false,'center'));
  items.push(text(935,583,540,54,zh?'等進度 100%，再貼到目標程式':'Wait for 100%, then paste into your app',21,colors.muted,false,'center'));
  items.push(arrow(676,421,883,421),text(660,335,239,58,zh?'圖片自動送達':'Automatic transfer',22,colors.green,true,'center'),text(664,445,231,63,zh?'加密傳輸\n不用反覆點遠端視窗':'Encrypted transfer\nNo repeated refocusing',18,colors.muted,false,'center'));
  items.push(box(60,695,1440,102,'#152530','none',16),text(88,711,1386,69,zh?'第一次請先安裝「遠端接收端」，再建立「本機傳送端」。\n每次使用：兩端連線已確認 → 本機重新截圖 → 遠端 Ctrl+V。':'First install the remote receiver, then create your local sender.\nDaily use: both connected → new local screenshot → Ctrl+V remotely.',23,colors.text));
  return {id:'overview-'+lang,name:'overview-'+lang,canvasWidth:1560,canvasHeight:835,canvasColor:colors.paper,canvasOpacity:1,items};
}
function installation(lang) {
  const zh=lang==='zh';const items=[];
  items.push(text(54,35,990,30,'FIRST-TIME SETUP  /  WINDOWS',20,colors.green,true));
  items.push(text(54,80,990,60,zh?'安裝順序：先遠端，再本機':'Install remotely first, then locally',39,colors.text,true));
  items.push(text(54,144,990,38,zh?'步驟 1–3 都在同一台遠端電腦、同一個解壓縮資料夾完成。':'Steps 1–3 use the SAME extracted folder on the REMOTE computer.',21,colors.muted));
  const steps=zh?[
    ['遠端','下載原始碼並「全部解壓縮」','GitHub → Code → Download ZIP\n不要在 ZIP 裡直接執行檔案。'],
    ['遠端','雙擊 1-Install-Receiver.cmd','安裝並開啟接收端；等待視窗顯示「連線已確認」。'],
    ['仍在遠端','雙擊 2-Create-Sender.cmd','在同一個資料夾建立傳送端；不要另下載一份專案。'],
    ['遠端 → 本機','把 Sender.exe 下載到本機','Google Remote 側邊欄 → 下載檔案\n選 dist / RemoteImageBridge-Sender.exe'],
    ['本機','雙擊 RemoteImageBridge-Sender.exe','在家裡電腦執行；等待「連線已確認」，再重新截圖。'],
    ['遠端','看到 100% 後，按 Ctrl + V','先點要貼上的程式輸入區，再貼上圖片。']
  ]:[
    ['REMOTE','Download source → Extract All','GitHub → Code → Download ZIP\nDo not run the scripts from inside the ZIP.'],
    ['REMOTE','Run 1-Install-Receiver.cmd','Wait until the receiver window says the connection is verified.'],
    ['STILL REMOTE','Run 2-Create-Sender.cmd','Keep using the SAME folder. Do not start from a new download.'],
    ['REMOTE → LOCAL','Download the Sender EXE locally','Remote Desktop sidebar → Download file\nChoose dist / RemoteImageBridge-Sender.exe'],
    ['LOCAL','Run RemoteImageBridge-Sender.exe','Wait for connection verified, then take a NEW screenshot.'],
    ['REMOTE','Wait for 100%, then Ctrl + V','Click the destination app input area and paste the image.']
  ];
  steps.forEach(([role,title,detail],i)=>{
    const y=213+i*176;const accent=i===4?colors.blue:colors.amber;
    if(i<steps.length-1)items.push(arrow(78,y+60,78,y+190,colors.line,3,0));
    items.push(circle(53,y+20,50,accent),text(53,y+20,50,50,String(i+1),25,colors.paper,true,'center'));
    items.push(box(129,y,915,158,colors.card),text(154,y+10,852,26,role,17,accent,true));
    items.push(text(154,y+43,862,41,title,25,colors.text,true));
    items.push(text(154,y+88,862,57,detail,20,colors.muted));
  });
  items.push(text(54,1290,990,60,zh?'接收端重開後網址會變：按「重新配對」。\n分享給朋友時，分享 GitHub 原始碼，不要分享自己的配對 EXE。':'Receiver restarted? Use Re-pair to refresh its address.\nShare the GitHub source with friends, not your private paired EXEs.',19,colors.muted));
  return {id:'installation-'+lang,name:'installation-'+lang,canvasWidth:1100,canvasHeight:1380,canvasColor:colors.paper,canvasOpacity:1,items};
}
const pages=[overview('zh'),overview('en'),installation('zh'),installation('en')];
const project={version:2,project:{id:'image-paste-guide',name:'Chrome Remote Desktop Image Paste — Guide',activePageId:pages[0].id,pages}};
const skcPath=path.join(outputRoot,'image-paste-guide.skc');
fs.writeFileSync(skcPath,JSON.stringify(project,null,2)+'\n');
const contentTypes={'.html':'text/html','.js':'application/javascript','.json':'application/json','.css':'text/css','.svg':'image/svg+xml','.png':'image/png','.woff2':'font/woff2','.woff':'font/woff','.ttf':'font/ttf','.wasm':'application/wasm'};
const server=http.createServer((request,response)=>{
  const pathname=decodeURIComponent(new URL(request.url,'http://localhost').pathname);
  const target=path.resolve(appRoot,'.'+(pathname==='/'?'/index.html':pathname));
  if(!target.startsWith(appRoot+path.sep)||!fs.existsSync(target)||!fs.statSync(target).isFile()){response.writeHead(404);response.end();return;}
  response.setHeader('Content-Type',contentTypes[path.extname(target)]||'application/octet-stream');
  response.setHeader('Cache-Control','no-store');fs.createReadStream(target).pipe(response);
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await chromium.launch({channel:'chrome',headless:true});
const report=[];
try{
  const context=await browser.newContext({viewport:{width:1740,height:1100},deviceScaleFactor:1,serviceWorkers:'block'});
  const page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.goto(`http://127.0.0.1:${server.address().port}/?mode=web&storage=image-paste-guide-${Date.now()}`);
  await page.waitForFunction(()=>window.skechu&&workspaceReady);
  await page.locator('#load-json').setInputFiles(skcPath);
  await page.waitForFunction(()=>activeProject()?.name==='Chrome Remote Desktop Image Paste — Guide');
  for(const sourcePage of pages){
    await page.evaluate(name=>openPage(activeProject().pages.find(p=>p.name===name).id),sourcePage.name);
    const menu=page.locator('.export-menu');
    if(!await menu.evaluate(node=>node.open))await menu.locator('summary').first().click();
    await page.locator('#automation-tools').click();
    await page.locator('.automation-panel [data-enable]').click();
    await page.waitForFunction(()=>window.skechu.status().enabled);
    const read=await page.evaluate(()=>window.skechu.execute('read_document',{limit:200}));
    const exported=await page.evaluate(context=>window.skechu.execute('export_svg',{context}),read.context);
    assert.ok(exported.svg.includes('<svg'));
    assert.ok(!exported.svg.includes('<image'),'Diagrams must contain native editable objects, not a flattened reference.');
    const svgPath=path.join(outputRoot,sourcePage.name+'.svg');fs.writeFileSync(svgPath,exported.svg+'\n');
    const preview=await context.newPage();
    await preview.setViewportSize({width:sourcePage.canvasWidth,height:sourcePage.canvasHeight});
    await preview.setContent('<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;background:'+colors.paper+'}svg{display:block;width:100vw;height:100vh}</style></head><body>'+exported.svg+'</body></html>');
    await preview.evaluate(()=>document.fonts.ready);
    const overflow=await preview.evaluate(()=>{const root=document.querySelector('svg').viewBox.baseVal;return [...document.querySelectorAll('text')].filter(node=>{const b=node.getBBox();return b.x<0||b.y<0||b.x+b.width>root.width+1||b.y+b.height>root.height+1}).map(n=>n.textContent);});
    assert.deepEqual(overflow,[],sourcePage.name+' contains clipped text');
    await preview.screenshot({path:path.join(outputRoot,sourcePage.name+'.png')});
    report.push({page:sourcePage.name,objects:sourcePage.items.length,svgBytes:Buffer.byteLength(exported.svg),width:sourcePage.canvasWidth,height:sourcePage.canvasHeight});
    await preview.close();
    await page.locator('.automation-panel [data-disable]').click();
    await page.locator('.automation-panel [data-close]').click();
  }
  assert.deepEqual(errors,[],'Skechu emitted browser errors');
  console.log(JSON.stringify({renderer:'Skechu importer + window.skechu export_svg',pages:report},null,2));
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
