const { chromium } = require('playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
(async () => {
 const assert = require('node:assert/strict');
 const browser = await chromium.launch({headless:true,...(process.env.BROWSER_PATH ? {executablePath:process.env.BROWSER_PATH} : {channel:'msedge'})});
 const context = await browser.newContext();
 await context.route('https://audit.local/**', async route => {
  const url = new URL(route.request().url());
  const rel = decodeURIComponent(url.pathname);
  const file = rel.endsWith('.html') ? path.join(root,process.env.RENDERED_DIR || 'audit/rendered',path.basename(rel)) : path.join(root,'src/Bisuteria.Web/wwwroot',rel);
  if (!file.startsWith(root + path.sep)) return route.abort();
  try {
   const headers = {};
   if (rel.endsWith('.html')) {
    const html = await fs.readFile(file, 'utf8');
    const nonce = html.match(/nonce="([^"]+)"/)?.[1].replace(/&#x2B;/g, '+').replace(/&#x2F;/g, '/');
    if (nonce) headers['Content-Security-Policy'] = `default-src 'self'; script-src 'self' 'nonce-${nonce}'; script-src-attr 'none'; style-src 'self' https://fonts.googleapis.com 'nonce-${nonce}'; style-src-attr 'none'; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`;
   }
   const contentType = { '.html': 'text/html', '.css': 'text/css', '.js': 'application/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.woff2': 'font/woff2' }[path.extname(file)];
   await route.fulfill({path:file,headers,contentType});
  } catch { await route.fulfill({status:404,body:'No encontrado'}); }
 });
 const page = await context.newPage();
 const results=[];
 await fs.mkdir(path.join(root,'audit/screenshots'),{recursive:true});
 let errors=[];
 page.on('pageerror', e=>errors.push(e.message));
 page.on('console', m => { if(m.type() === 'error' && m.text().includes('Content Security Policy')) errors.push(m.text()); });
 for(const name of ['home','catalog','product','contact','login','register','forgot','profile','cart','checkout','admin','admin-products','admin-form','customizer','developer','developer-roles','developer-audit','developer-email']) {
  for(const width of [390,768,1440]) {
   await page.setViewportSize({width,height:900});errors=[];
   await page.goto('https://audit.local/'+name+'.html',{waitUntil:'load',timeout:20000});
   await page.evaluate(()=>{document.getElementById('appLoader')?.remove();document.querySelectorAll('.animate-on-scroll').forEach(e=>e.classList.add('visible'));});
   await page.evaluate(async()=>{
    const images=[...document.images];
    images.forEach(img=>{img.loading='eager';});
    await Promise.all(images.map(img=>img.decode().catch(()=>{})));
   });
   const metrics=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,width:innerWidth,brokenImages:[...document.images].filter(x=>!x.complete||x.naturalWidth===0).length,overflow:[...document.querySelectorAll('body *')].filter(e=>{let r=e.getBoundingClientRect();return r.right>innerWidth+2&&r.width>0&&getComputedStyle(e).position!=='fixed';}).slice(0,8).map(e=>e.tagName+'.'+e.className)}));
   results.push({name,width,...metrics,errors:[...errors]});
   await page.screenshot({path:path.join(root,'audit/screenshots',name+'-'+width+'.png'),fullPage:true,animations:'disabled'});
  }
  console.log(name+': comprobado en 3 tamaños');
 }
 await page.setViewportSize({width:390,height:900});
 await page.goto('https://audit.local/admin.html',{waitUntil:'load'});
 await page.locator('#adminMenuToggle').click();
 assert.equal(await page.locator('#adminMenuToggle').getAttribute('aria-expanded'),'true');
 await page.keyboard.press('Escape');
 assert.equal(await page.locator('#adminMenuToggle').getAttribute('aria-expanded'),'false');
 await page.locator('.admin-notifications summary').click();
 assert.equal(await page.locator('.admin-notifications').getAttribute('open'),'');
 await page.locator('.admin-notifications summary').click();
 await page.locator('#adminTheme').click();
 assert.equal(await page.locator('html').getAttribute('data-admin-theme'),'dark');
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth <= innerWidth),true);
 // Finalizar transiciones antes de medir colores y capturar el tema.
 await page.screenshot({path:path.join(root,'audit/screenshots/admin-dark-390.png'),fullPage:true,animations:'disabled'});
 const contrast = await page.evaluate(() => {
  const rgb = color => color.match(/[\d.]+/g).map(Number);
  const luminance = color => rgb(color).slice(0,3).map(v => {
   v /= 255; return v <= .04045 ? v / 12.92 : ((v + .055) / 1.055) ** 2.4;
  }).reduce((sum,v,i) => sum + v * [.2126,.7152,.0722][i],0);
  return [...document.querySelectorAll('.form-label,.admin-sidebar .nav-link,.btn-outline-dark,#adminTheme')].map(el => {
   let ancestor = el;
   while (ancestor.parentElement && (rgb(getComputedStyle(ancestor).backgroundColor)[3] ?? 1) === 0) ancestor = ancestor.parentElement;
   const foreground = luminance(getComputedStyle(el).color);
   const background = luminance(getComputedStyle(ancestor).backgroundColor);
   return {text:el.textContent.trim(),ratio:(Math.max(foreground,background)+.05)/(Math.min(foreground,background)+.05)};
  });
 });
 assert.ok(contrast.length > 0);
 assert.deepEqual(contrast.filter(item => item.ratio < 4.5),[], 'Contraste insuficiente en modo oscuro');
 await fs.writeFile(path.join(root,'audit/visual-contrast.json'),JSON.stringify(contrast,null,2));
 await page.locator('#adminTheme').click();
 assert.equal(await page.locator('html').getAttribute('data-admin-theme'),'light');
 await page.goto('https://audit.local/customizer.html' ,{waitUntil:'load'});
 await page.evaluate(()=>document.getElementById('appLoader')?.remove());
 const basePrice=Number(await page.locator('.product-option.active').getAttribute('data-price'));
 await page.locator('#cText').fill('Ana');
 assert.equal(await page.locator('#previewText').textContent(),'Ana');
 assert.equal(await page.locator('#previewPrice').textContent(),'$'+(basePrice+4).toFixed(2));
 await page.locator('#cGift').check();
 assert.equal(await page.locator('#previewPrice').textContent(),'$'+(basePrice+7).toFixed(2));
 await page.locator('.navbar-toggler').click();
 await page.waitForFunction(()=>document.querySelector('#mainNav').classList.contains('show'));
 assert.equal(await page.locator('.navbar-toggler').getAttribute('aria-expanded'),'true');
 await page.locator('.navbar-toggler').click();
 await page.waitForFunction(()=>!document.querySelector('#mainNav').classList.contains('show'));
 await fs.writeFile(path.join(root,'audit/visual-interactions.json'),JSON.stringify({inputDirectory:process.env.RENDERED_DIR||'audit/rendered',passed:['texto de personalización','recargo por texto','recargo por regalo','abrir menú móvil','cerrar menú móvil','abrir menú Admin','cerrar Admin con Escape','abrir notificaciones','activar modo oscuro','restaurar modo claro']},null,2));
 await fs.writeFile(path.join(root,'audit/visual-results.json'),JSON.stringify(results,null,2));
 const issues=results.filter(x=>x.scroll>x.width||x.errors.length||x.brokenImages);
 console.log(JSON.stringify({checks:results.length,issues},null,2));
 if(issues.length) process.exitCode=1;
 await browser.close();
})().catch(error=>{console.error(error);process.exitCode=1;});
