// Auditoría contra Kestrel real. Las credenciales efímeras llegan por el entorno y nunca se escriben en artefactos.
const { chromium } = require('playwright');
const { default: AxeBuilder } = require('@axe-core/playwright');
const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const base = process.env.AUDIT_BASE_URL;
const phase = process.env.AUDIT_PHASE === 'after' ? 'after' : 'before';
const directory = path.join(root, 'audit/browser', phase);
const report = { phase, generatedAt: new Date().toISOString(), viewportSizes: [360, 768, 1366], pages: [], flows: [], links: [], lighthouse: null, failures: [] };
const required = ['AUDIT_BASE_URL', 'AUDIT_PASSWORD', 'AUDIT_PRODUCT_ID', 'AUDIT_CATEGORY_ID'];
for (const key of required) if (!process.env[key]) throw new Error(`Falta la variable de auditoría ${key}.`);
if (!/^https:\/\/(127\.0\.0\.1|localhost):\d+$/.test(base)) throw new Error('La auditoría solo permite el servidor HTTPS local temporal.');
const internalLinks = new Map();
const redactions = new Set([process.env.AUDIT_PASSWORD].filter(Boolean));
const clean = value => {
  let result = String(value).replace(/(accessToken|token|password|code)=[^&\s]+/gi, '$1=[redactado]');
  for (const secret of redactions) result = result.split(secret).join('[redactado]');
  return result;
};
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));

async function settle(page) {
  await page.locator('#appLoader').waitFor({ state: 'hidden', timeout: 12000 }).catch(() => {});
  await page.evaluate(async () => {
    await Promise.race([document.fonts.ready, new Promise(resolve => setTimeout(resolve, 3000))]);
    for (const image of document.images) image.loading = 'eager';
    await Promise.all([...document.images].map(image => image.decode().catch(() => {})));
    window.scrollTo(0, document.documentElement.scrollHeight);
  });
  await pause(120);
  await page.evaluate(() => window.scrollTo(0, 0));
  await pause(120);
}

async function login(context, role) {
  const page = await context.newPage();
  await page.goto(base + '/Account/Login', { waitUntil: 'domcontentloaded' });
  await page.locator('#Email').fill(`${role.toLowerCase()}@example.test`);
  await page.locator('#Password').fill(process.env.AUDIT_PASSWORD);
  await Promise.all([page.waitForURL(url => !url.pathname.endsWith('/Login')), page.locator('form[action*="Login"] button[type="submit"]').click()]);
  assert.ok(!/\/(Login|TwoFactor|Authenticator)$/.test(new URL(page.url()).pathname), `El acceso temporal de ${role} no terminó.`);
  await page.close();
}

async function inspect(context, name, route, allowedStatus = 200) {
  const page = await context.newPage();
  let errors = [];
  let blockedInlineStyleWarnings = [];
  const failures = [];
  page.on('pageerror', error => errors.push(clean(error.message)));
  page.on('console', message => {
    if (message.type() !== 'error' || !/Content Security Policy/i.test(message.text())) return;
    const location = message.location();
    const issue = { message: clean(message.text()), location };
    // Preserve source-less inline-style warnings separately; the blocked source cannot be attributed to app markup.
    if (!location.url && /Applying inline style violates/.test(message.text())) blockedInlineStyleWarnings.push(issue);
    else errors.push(issue);
  });
  page.on('requestfailed', request => failures.push({ path: new URL(request.url()).pathname, reason: request.failure()?.errorText }));
  try {
    for (const width of report.viewportSizes) {
      errors = [];
      blockedInlineStyleWarnings = [];
      failures.length = 0;
      await page.setViewportSize({ width, height: 900 });
      const response = await page.goto(new URL(route, base).href, { waitUntil: 'domcontentloaded', timeout: 45000 });
      const htmlSource = await response.text().catch(() => '');
      await settle(page);
      const metrics = await page.evaluate(() => {
        const visible = element => {
          const box = element.getBoundingClientRect();
          const style = getComputedStyle(element);
          return box.width > 0 && box.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
        };
        const describe = element => ({ tag: element.tagName, target: element.id ? '#' + element.id : element.tagName.toLowerCase() + (element.classList.length ? '.' + [...element.classList].slice(0, 2).join('.') : ''), text: (element.getAttribute('aria-label') || element.textContent || '').trim().slice(0, 70) });
        const controls = [...document.querySelectorAll('button,input:not([type="hidden"]),select,textarea')].filter(visible);
        const whatsapp = document.querySelector('.whatsapp-float');
        const whatsappBox = whatsapp?.getBoundingClientRect();
        const whatsappOverlaps = whatsappBox && visible(whatsapp) ? [...document.querySelectorAll('a[href],button,input:not([type="hidden"]),select,textarea')]
          .filter(element => element !== whatsapp && !whatsapp.contains(element) && !element.contains(whatsapp) && visible(element))
          .filter(element => {
            const box = element.getBoundingClientRect();
            return Math.min(whatsappBox.right, box.right) - Math.max(whatsappBox.left, box.left) > 3 && Math.min(whatsappBox.bottom, box.bottom) - Math.max(whatsappBox.top, box.top) > 3;
          }).map(describe) : [];
        const overlapping = [];
        for (let first = 0; first < controls.length; first++) for (let second = first + 1; second < controls.length; second++) {
          const left = controls[first]; const right = controls[second];
          if (left.contains(right) || right.contains(left)) continue;
          const a = left.getBoundingClientRect(); const b = right.getBoundingClientRect();
          if (a.top > innerHeight || b.top > innerHeight || a.bottom < 0 || b.bottom < 0) continue;
          if (Math.min(a.right, b.right) - Math.max(a.left, b.left) > 3 && Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top) > 3) overlapping.push([describe(left), describe(right)]);
        }
        return {
          viewport: innerWidth, actualPath: location.pathname, scrollWidth: document.documentElement.scrollWidth,
          checkoutLineCount: document.querySelectorAll('.checkout-line').length,
          checkoutLayoutIssues: [...document.querySelectorAll('.checkout-line')].flatMap(line => {
            const container = line.getBoundingClientRect();
            const children = ['.checkout-thumb', '.checkout-line__content', '.checkout-line__price']
              .map(selector => line.querySelector(selector)).filter(Boolean);
            return children.filter(child => {
              const box = child.getBoundingClientRect();
              return box.left < container.left - 1 || box.right > container.right + 1;
            }).map(child => ({ lineWidth: Math.round(container.width), target: child.className }));
          }),
          brokenImages: [...document.images].filter(image => !image.complete || image.naturalWidth === 0).map(image => new URL(image.src).pathname),
          overflowElements: [...document.querySelectorAll('body *')].filter(element => visible(element) && element.getBoundingClientRect().right > innerWidth + 2 && getComputedStyle(element).position !== 'fixed').slice(0, 12).map(describe),
          overlappingControls: overlapping.slice(0, 12),
          whatsappOverlaps,
          touchTargetCandidates: [...document.querySelectorAll('button,input:not([type="hidden"]),select,textarea,a')].filter(visible).map(element => ({ ...describe(element), width: Math.round(element.getBoundingClientRect().width), height: Math.round(element.getBoundingClientRect().height) })).filter(element => element.width < 44 || element.height < 44).slice(0, 30),
          loaderVisible: [...document.querySelectorAll('#appLoader')].some(visible),
          inlineStyles: [...document.querySelectorAll('style')].map(style => ({ nonce: style.nonce, content: style.textContent.slice(0, 200) })),
          inlineStyleAttributes: [...document.querySelectorAll('[style]')].map(element => ({ target: element.id || element.tagName.toLowerCase(), style: element.getAttribute('style') })).slice(0, 20),
          links: [...document.querySelectorAll('a[href]')].map(anchor => anchor.href).filter(value => value.startsWith(location.origin + '/'))
        };
      });
      const responseStyleBlocks = [...htmlSource.matchAll(/<style\b([^>]*)>([\s\S]*?)<\/style>/gi)].map(match => ({ attributes: match[1].slice(0, 160), content: match[2].trim().slice(0, 300) }));
      for (const link of metrics.links) {
        const url = new URL(link);
        if (!url.hash && !/token|confirmation|resetpassword|logout|proof/i.test(url.href)) internalLinks.set(url.pathname + url.search, context);
      }
      delete metrics.links;
      const accessibility = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']).analyze();
      const record = {
        name, width, path: new URL(route, base).pathname, status: response.status(), expectedStatus: allowedStatus, ...metrics, responseStyleBlocks,
        errors: [...errors], blockedInlineStyleWarnings: [...blockedInlineStyleWarnings], failedRequests: [...failures],
        axe: accessibility.violations.map(violation => ({ id: violation.id, impact: violation.impact, help: violation.help, helpUrl: violation.helpUrl, nodes: violation.nodes.map(node => ({ target: node.target, failureSummary: node.failureSummary })) })),
        axeIncomplete: accessibility.incomplete.map(rule => ({ id: rule.id, impact: rule.impact, targets: rule.nodes.map(node => node.target) }))
      };
      report.pages.push(record);
      await page.screenshot({ path: path.join(directory, `${name}-${width}.png`), fullPage: true, animations: 'disabled' });
      const requiresCheckoutLine = ['checkout', 'confirmation', 'order-details'].includes(name);
      if (record.status !== allowedStatus || record.scrollWidth > width + 1 || record.checkoutLayoutIssues.length || (requiresCheckoutLine && record.checkoutLineCount === 0) || record.brokenImages.length || record.errors.length || record.loaderVisible)
        report.failures.push({ name, width, reason: 'HTTP, desborde, imagen, JavaScript/CSP o loader; ver detalle de página.' });
      if (['checkout', 'confirmation', 'order-details'].includes(name)) {
        const priorTheme = await page.locator('html').getAttribute('data-bs-theme');
        await page.locator('html').evaluate(element => element.setAttribute('data-bs-theme', 'dark'));
        await settle(page);
        const darkMetrics = await page.evaluate(() => ({
          scrollWidth: document.documentElement.scrollWidth,
          checkoutLayoutIssues: [...document.querySelectorAll('.checkout-line')].flatMap(line => {
            const container = line.getBoundingClientRect();
            return ['.checkout-thumb', '.checkout-line__content', '.checkout-line__price']
              .map(selector => line.querySelector(selector)).filter(Boolean)
              .filter(child => { const box = child.getBoundingClientRect(); return box.left < container.left - 1 || box.right > container.right + 1; })
              .map(child => ({ lineWidth: Math.round(container.width), target: child.className }));
          })
        }));
        await page.screenshot({ path: path.join(directory, `${name}-${width}-dark.png`), fullPage: true, animations: 'disabled' });
        if (darkMetrics.scrollWidth > width + 1 || darkMetrics.checkoutLayoutIssues.length)
          report.failures.push({ name, width, theme: 'dark', reason: 'Desborde del resumen en tema oscuro.' });
        if (priorTheme) await page.locator('html').evaluate((element, theme) => element.setAttribute('data-bs-theme', theme), priorTheme);
        else await page.locator('html').evaluate(element => element.removeAttribute('data-bs-theme'));
      }
    }
  } catch (error) { report.failures.push({ name, reason: clean(error.message) }); }
  finally { await page.close(); }
}

async function runFlow(name, operation) {
  try { await operation(); report.flows.push({ name, passed: true }); }
  catch (error) { report.flows.push({ name, passed: false, error: clean(error.message) }); report.failures.push({ name, reason: 'Flujo fallido' }); }
}

async function main() {
  await fs.mkdir(directory, { recursive: true });
  const launchOptions = { headless: true };
  if (process.env.BROWSER_PATH) launchOptions.executablePath = process.env.BROWSER_PATH;
  else if (process.env.BROWSER_CHANNEL) launchOptions.channel = process.env.BROWSER_CHANNEL;
  const browser = await chromium.launch(launchOptions);
  let guestOrderRoute;
  let orderNumber;
  try {
    const anonymous = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'es-EC', reducedMotion: 'reduce' });
    const customer = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'es-EC', reducedMotion: 'reduce' });
    const owner = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'es-EC', reducedMotion: 'reduce' });
    const developer = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'es-EC', reducedMotion: 'reduce' });
    const operator = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'es-EC', reducedMotion: 'reduce' });

    await runFlow('catalogo-carrito-checkout-confirmacion-comprobante', async () => {
      const page = await anonymous.newPage();
      await page.goto(base + '/Catalog/Details?slug=audit-product', { waitUntil: 'domcontentloaded' });
      await settle(page);
      await Promise.all([page.waitForURL('**/Cart'), page.locator('form[action*="Cart/Add"] button[type="submit"]').click()]);
      assert.ok((await page.locator('body').innerText()).includes('Pieza de ensayo aislada'));
      await inspect(anonymous, 'cart', '/Cart');
      await inspect(anonymous, 'checkout', '/Checkout');
      await page.goto(base + '/Checkout', { waitUntil: 'domcontentloaded' });
      assert.equal(new URL(page.url()).pathname, '/Checkout', 'El checkout redirigió antes de mostrar el resumen.');
      assert.ok(await page.locator('.checkout-line').count(), 'El checkout no mostró artículos en el resumen.');
      await page.locator('#CustomerName').fill('Cliente navegador');
      await page.locator('#CustomerEmail').fill('browser-order@example.test');
      await page.locator('#CustomerPhone').fill('+1 202 555 0101');
      await page.locator('input[name="DeliveryMethod"][value="Shipping"]').check();
      await page.locator('#AddressLine1').fill('Dirección ficticia de auditoría');
      await page.locator('#DeliverySector').fill('sector de prueba (auditoria)');
      assert.ok(await page.locator('#DeliverySector').evaluate(element => window.jQuery(element).valid()), 'El navegador rechazó un sector válido.');
      assert.match(await page.locator('#shippingDisplay').innerText(), /2[.,]50/, 'El costo del sector no aparece antes de confirmar.');
      await page.locator('input[name="PaymentMethod"][value="BankTransfer"]').check();
      await page.getByRole('button', { name: /Confirmar pedido/ }).click();
      try { await page.waitForURL('**/Checkout/Confirmation**', { timeout: 8000 }); }
      catch {
        const validation = await page.locator('[data-valmsg-summary="true"], .validation-summary-errors, .text-danger').allTextContents();
        throw new Error(`Checkout no confirmó el pedido. URL: ${new URL(page.url()).pathname}; validación: ${clean(validation.join(' | ').trim() || 'sin mensaje visible')}`);
      }
      assert.ok((await page.locator('h1').innerText()).includes('Gracias'));
      const orderUrl = new URL(page.url());
      guestOrderRoute = orderUrl.pathname + orderUrl.search;
      orderNumber = orderUrl.searchParams.get('orderNumber');
      assert.ok(orderNumber, 'No se generó el número de pedido.');
      // Una imagen PNG producida por el navegador evita depender de un archivo binario externo o una firma falsa.
      const image = await page.locator('.order-number-badge').screenshot({ type: 'png', animations: 'disabled' });
      await page.locator('input[name="proof"]').setInputFiles({ name: 'comprobante-prueba.png', mimeType: 'image/png', buffer: image });
      await Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), page.getByRole('button', { name: 'Enviar comprobante' }).click()]);
      assert.ok((await page.locator('body').innerText()).includes('Comprobantes enviados'));
      await inspect(anonymous, 'confirmation', guestOrderRoute);
      await page.goto(base + '/Order/Track', { waitUntil: 'domcontentloaded' });
      await page.locator('#OrderNumber').fill(orderNumber);
      await page.locator('#Email').fill('browser-order@example.test');
      await Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), page.getByRole('button', { name: 'Consultar pedido' }).click()]);
      assert.ok((await page.locator('body').innerText()).includes(orderNumber), 'El seguimiento de invitado no mostró el pedido autorizado.');
      assert.ok(!(await page.locator('body').innerText()).includes('Dirección ficticia de auditoría'), 'El seguimiento público expuso la dirección.');
      await page.close();
    });

    for (const destination of [
      { name: 'sector-desconocido-por-cotizar', city: 'Manta', sector: 'Barrio nuevo de prueba', pending: true },
      { name: 'sector-con-envio-gratis', city: 'Jaramijó', sector: 'sector gratis de auditoria', pending: false }
    ]) await runFlow(destination.name, async () => {
      const page = await anonymous.newPage();
      await page.setViewportSize({ width: 360, height: 900 });
      await page.goto(base + '/Catalog/Details?slug=audit-product', { waitUntil: 'domcontentloaded' });
      await Promise.all([page.waitForURL('**/Cart'), page.locator('form[action*="Cart/Add"] button[type="submit"]').click()]);
      await page.goto(base + '/Checkout', { waitUntil: 'domcontentloaded' });
      assert.equal(await page.locator('#checkoutOrderDetails').getAttribute('open'), null, 'El resumen móvil debe iniciar plegado.');
      await page.locator('#CustomerName').fill('Cliente de prueba');
      await page.locator('#CustomerEmail').fill('browser-destination@example.test');
      await page.locator('#CustomerPhone').fill('2025550100');
      await page.locator('#City').selectOption(destination.city);
      await page.locator('#DeliverySector').fill(destination.sector);
      await page.locator('#AddressLine1').fill('Dirección ficticia de prueba');
      assert.match(await page.locator('#deliveryZoneHelp').innerText(), destination.pending ? /cotizamos/ : /Envío gratis/);
      await page.locator('#checkoutOrderDetails summary').click();
      assert.match(await page.locator('#shippingDisplay').innerText(), destination.pending ? /por confirmar/ : /Envío gratis/);
      await page.getByRole('button', { name: destination.pending ? 'Enviar pedido para cotización' : 'Confirmar pedido' }).click();
      await page.waitForURL('**/Checkout/Confirmation**', { timeout: 10000 });
      const content = await page.locator('body').innerText();
      assert.match(content, destination.pending ? /pendiente de confirmar|confirme el recargo/ : /Gracias/);
      await page.close();
    });

    if (process.env.BROWSER_AUDIT_QUICK === '1') {
      // Repoblar el carrito de prueba permite revisar su estado móvil después de completar la compra.
      const cartPage = await anonymous.newPage();
      await cartPage.goto(base + '/Catalog/Details?slug=audit-product', { waitUntil: 'domcontentloaded' });
      await Promise.all([cartPage.waitForURL('**/Cart'), cartPage.locator('form[action*="Cart/Add"] button[type="submit"]').click()]);
      await cartPage.close();
      for (const [name, route] of [['home', '/'], ['cart', '/Cart']]) await inspect(anonymous, name, route);
      await login(owner, 'Owner');
      if (orderNumber) await inspect(owner, 'order-details', '/Order/Details?orderNumber=' + encodeURIComponent(orderNumber));
      return;
    }

    await runFlow('registro-login-contacto', async () => {
      const page = await customer.newPage();
      await page.goto(base + '/Account/Register', { waitUntil: 'domcontentloaded' });
      for (const [field, value] of Object.entries({ FirstName: 'Cliente', LastName: 'Navegador', Email: 'browser-register@example.test', Phone: '+1 202 555 0102', Password: process.env.AUDIT_PASSWORD, ConfirmPassword: process.env.AUDIT_PASSWORD })) await page.locator('#' + field).fill(value);
      await Promise.all([page.waitForURL(url => !url.pathname.endsWith('/Register')), page.locator('form[action*="Register"] button[type="submit"]').click()]);
      await login(customer, 'Customer');
      await page.goto(base + '/Contact', { waitUntil: 'domcontentloaded' });
      for (const [field, value] of Object.entries({ Name: 'Cliente navegador', Email: 'browser-contact@example.test', Phone: '+1 202 555 0102', Subject: 'Consulta de prueba', Message: 'Este mensaje ficticio valida el formulario completo de contacto.' })) await page.locator('#' + field).fill(value);
      await Promise.all([page.waitForNavigation({ waitUntil: 'domcontentloaded' }), page.locator('form[action*="Contact"] button[type="submit"]').click()]);
      assert.ok((await page.locator('body').innerText()).includes('Gracias por escribirnos'));
      await page.close();
    });

    const publicPages = [
      ['home', '/'], ['catalog', '/Catalog'], ['search', '/Catalog?search=ensayo'], ['filters', '/Catalog?categorySlug=audit&minPrice=1&maxPrice=50&sort=price_asc'],
      ['product', '/Catalog/Details?slug=audit-product'], ['customizer', `/Customizer?productId=${process.env.AUDIT_PRODUCT_ID}`],
      ['login', '/Account/Login'], ['register', '/Account/Register'], ['forgot', '/Account/ForgotPassword'],
      ['blog', '/Blog'], ['blog-detail', '/Blog/Details?slug=browser-audit'], ['gallery', '/Gallery'], ['contact', '/Contact'], ['about', '/Home/About'],
      ['shipping', '/Home/Shipping'], ['privacy', '/Home/Privacy'], ['terms', '/Home/Terms'], ['guest-order-tracking', '/Order/Track'], ['faq', '/Home/Faq'], ['access-denied', '/Account/AccessDenied'], ['error-template', '/Home/Error'], ['not-found', '/auditoria-ruta-inexistente', 404]
    ];
    for (const [name, route, status = 200] of publicPages) await inspect(anonymous, name, route, status);
    await runFlow('aviso-cookies-solo-necesarias', async () => {
      const page = await anonymous.newPage();
      await page.goto(base + '/', { waitUntil: 'domcontentloaded' });
      const notice = page.locator('#cookieNotice');
      assert.ok(await notice.isVisible(), 'El aviso de cookies no se mostró por primera vez.');
      await page.getByRole('button', { name: 'Entendido' }).click();
      assert.ok(await notice.isHidden(), 'El aviso de cookies no se pudo cerrar.');
      await page.reload({ waitUntil: 'domcontentloaded' });
      assert.ok(await notice.isHidden(), 'La preferencia del aviso no se conservó.');
      await page.close();
    });
    await runFlow('sesion-cliente-perfil-pedidos', async () => {
      await login(customer, 'Customer');
      await inspect(customer, 'profile', '/Account/Profile');
      await inspect(customer, 'my-orders', '/Order');
    });
    await runFlow('sesion-owner-panel', async () => {
      await login(owner, 'Owner');
      for (const [name, route] of [['admin', '/Admin'], ['admin-orders', '/Admin/Orders'], ['admin-products', '/Admin/Products'], ['admin-users', '/Admin/Users'], ['admin-settings', '/Admin/Settings']]) await inspect(owner, name, route);
      if (orderNumber) await inspect(owner, 'admin-order-detail', '/Admin/Orders/Details?orderNumber=' + encodeURIComponent(orderNumber));
      if (orderNumber) await inspect(owner, 'order-details', '/Order/Details?orderNumber=' + encodeURIComponent(orderNumber));
    });
    await runFlow('sesion-developer-panel', async () => {
      await login(developer, 'Developer');
      for (const [name, route] of [['developer', '/Developer/Home'], ['developer-roles', '/Developer/Roles'], ['developer-audit', '/Developer/Audit'], ['developer-email', '/Developer/EmailQueue']]) await inspect(developer, name, route);
    });
    await runFlow('sesion-operator-panel', async () => {
      await login(operator, 'Operator');
      await inspect(operator, 'operator-dashboard', '/Admin');
      await inspect(operator, 'operator-orders', '/Admin/Orders');
      const forbidden = await operator.request.get(base + '/Developer/Home');
      assert.ok(forbidden.status() === 403 || new URL(forbidden.url()).pathname === '/Account/AccessDenied', 'Operator obtuvo el panel técnico.');
    });

    for (const [route, context] of [...internalLinks.entries()].slice(0, 150)) {
      try {
        const response = await context.request.get(base + route, { timeout: 15000 });
        report.links.push({ path: new URL(route, base).pathname, status: response.status() });
        if (response.status() >= 400) report.failures.push({ name: 'enlace-interno', path: new URL(route, base).pathname, status: response.status() });
      } catch (error) { report.links.push({ path: new URL(route, base).pathname, error: clean(error.message) }); }
    }

    try {
      const { default: lighthouse } = await import('lighthouse');
      const chromeLauncher = await import('chrome-launcher');
      const executable = process.env.BROWSER_PATH || (process.platform === 'win32' ? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe' : undefined);
      const chrome = await chromeLauncher.launch({ chromePath: executable, chromeFlags: ['--headless=new', '--no-sandbox', '--ignore-certificate-errors', '--disable-dev-shm-usage'] });
      try {
        const result = await lighthouse(base + '/', { port: chrome.port, output: ['json', 'html'], logLevel: 'error', onlyCategories: ['performance', 'accessibility', 'best-practices', 'seo'], maxWaitForLoad: 45000 });
        await fs.writeFile(path.join(directory, 'lighthouse-mobile.json'), result.report[0]);
        await fs.writeFile(path.join(directory, 'lighthouse-mobile.html'), result.report[1]);
        report.lighthouse = { lighthouseVersion: result.lhr.lighthouseVersion, fetchTime: result.lhr.fetchTime, environment: result.lhr.environment, configSettings: result.lhr.configSettings, categories: Object.fromEntries(Object.entries(result.lhr.categories).map(([key, category]) => [key, category.score === null ? null : Math.round(category.score * 100)])), runtimeError: result.lhr.runtimeError || null };
      } finally { await chrome.kill(); }
    } catch (error) { report.lighthouse = { error: clean(error.message) }; report.failures.push({ name: 'lighthouse', reason: clean(error.message) }); }
  } finally { await browser.close(); }
}

main().catch(error => { report.failures.push({ name: 'harness', reason: clean(error.message) }); }).finally(async () => {
  await fs.mkdir(directory, { recursive: true });
  report.summary = {
    screenshots: report.pages.length,
    structuralFailures: report.failures.length,
    failedFlows: report.flows.filter(flow => !flow.passed).length,
    axeViolations: report.pages.reduce((count, page) => count + page.axe.length, 0),
    axeSeriousOrCritical: report.pages.reduce((count, page) => count + page.axe.filter(violation => ['serious', 'critical'].includes(violation.impact)).length, 0),
    overlapCandidates: report.pages.reduce((count, page) => count + page.overlappingControls.length, 0)
  };
  await fs.writeFile(path.join(directory, 'report.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ phase, ...report.summary, lighthouse: report.lighthouse?.categories || report.lighthouse }, null, 2));
  // Contraste y objetivos táctiles se reportan para revisión; no se ocultan ni se convierten en una certificación WCAG.
  if (report.failures.length) process.exitCode = 1;
});
