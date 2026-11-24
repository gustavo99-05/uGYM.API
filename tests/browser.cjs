const fs = require('node:fs/promises');
const path = require('node:path');
const assert = require('node:assert/strict');
const puppeteer = require(process.env.UGYM_PUPPETEER_MODULE || 'puppeteer-core');
const frontend = process.env.UGYM_FRONTEND_PATH;
const output = path.resolve('artifacts/screenshots');
(async () => {
 const browser = await puppeteer.launch({headless:true,executablePath:process.env.UGYM_BROWSER_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe',args:['--no-first-run','--disable-background-networking']});
 try {
  const page = await browser.newPage();
  if(process.argv.includes('--icons')) {
    assert(frontend,'UGYM_FRONTEND_PATH required');
    const svg = await fs.readFile(path.join(frontend,'mobile/assets/icon.svg'),'utf8');
    for(const [file,size] of [['icon-192.png',192],['icon-512.png',512],['icon-maskable.png',512]]){
      await page.setViewport({width:size,height:size,deviceScaleFactor:1});
      await page.setContent('<style>*{margin:0}svg{display:block;width:100vw;height:100vh}</style>'+svg);
      await page.screenshot({path:path.join(frontend,'mobile/assets',file),omitBackground:false});
    }
    console.log('OK PNG icons 192, 512 and maskable'); return;
  }
  const url=process.env.UGYM_TEST_URL;
  assert(url && process.env.UGYM_TEST_PASSWORD,'Test server and ephemeral credentials required');
  await fs.mkdir(output,{recursive:true});
  let checks=0;
  const errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const check=(v,label)=>{assert(v,label);checks++;console.log('OK UI '+label);};
  const wait = selector => page.waitForSelector(selector,{visible:true,timeout:15000});
  const clickText=async(selector,text)=>{const handles=await page.$$(selector);for(const h of handles){if((await h.evaluate(e=>e.textContent)).includes(text)){await h.click();return;}}throw Error('Button not found: '+text);};
  const noOverflow=async(label)=>{
    const metrics=await page.evaluate(()=>({viewport:innerWidth,doc:document.documentElement.scrollWidth,body:document.body.scrollWidth}));
    check(metrics.doc<=metrics.viewport && metrics.body<=metrics.viewport,label+' sem rolagem horizontal');
  };
  async function screenshot(label){
    await page.evaluate(async()=>{await Promise.all(document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).map(a=>a.finished.catch(()=>{})));});
    await page.screenshot({path:path.join(output,label+'.png'),fullPage:false});
  }
  await page.setViewport({width:390,height:844,isMobile:true,hasTouch:true,deviceScaleFactor:1});
  await page.goto(url+'/mobile/',{waitUntil:'networkidle0'});
  await wait('#login-form');
  for(const width of [360,390,430]){
    await page.setViewport({width,height:844,isMobile:true,hasTouch:true,deviceScaleFactor:1});
    await noOverflow('login '+width);await screenshot('login-'+width);
  }
  await page.type('#username',process.env.UGYM_TEST_USER);
  await page.type('#password','senha-incorreta');
  await page.click('#login-submit'); await wait('#login-error:not([hidden])');
  check((await page.$eval('#login-error',e=>e.textContent)).includes('inválidos'),'credenciais inválidas');
  await page.$eval('#password',e=>e.value='');
  await page.type('#password',process.env.UGYM_TEST_PASSWORD);
  await page.click('#show-password');
  check(await page.$eval('#password',e=>e.type==='text'),'mostrar senha');
  await page.click('#show-password');
  await page.click('#login-submit'); await wait('.hero-card');
  check(await page.$eval('.stats-grid',e=>e.textContent.includes('4')),'dashboard com contagem real');
  for(const width of [360,390,430]){
    await page.setViewport({width,height:844,isMobile:true,hasTouch:true,deviceScaleFactor:1});
    await noOverflow('dashboard '+width);await screenshot('dashboard-'+width);
    await page.click('[data-page="alunos"]');await wait('.student-card');
    await noOverflow('alunos '+width);await screenshot('alunos-'+width);
    const name=await page.$eval('.student-info h3',e=>e.textContent);
    await page.click('[data-detail]');await wait('[data-action="register"]');
    check(await page.$eval('#detail-title',e=>!!e.textContent),'detalhes em '+width);
    await noOverflow('detalhes '+width);await screenshot('detalhes-'+width);
    await page.click('[data-action="close"]');
    await page.click('[data-page="checkin"]');await wait('.student-card');
    await noOverflow('check-in '+width);await screenshot('checkin-'+width);
    await page.click('[data-page="conta"]');await wait('.account-card');
    await noOverflow('conta '+width);await screenshot('conta-'+width);
    await page.click('[data-page="inicio"]');await wait('.hero-card');
  }
  await page.click('[data-page="alunos"]');await wait('.student-card');
  await page.type('#student-search','inexistente');
  await wait('.empty-state');check(await page.$eval('#list-count',e=>e.textContent==='0 alunos'),'pesquisa vazia');
  await page.$eval('#student-search',e=>{e.value='';e.dispatchEvent(new Event('input',{bubbles:true}));});
  await page.click('[data-filter="inativos"]');
  await page.waitForFunction(()=>document.querySelectorAll('.student-card').length===1 && document.querySelector('.student-info h3')?.textContent==='Bruno Inativo');
  check(await page.$$eval('.student-card',es=>es.length===1),'filtro de inativos');
  await page.click('[data-filter="todos"]');
  await page.type('#student-search','Carla');
  await page.waitForFunction(()=>document.querySelectorAll('.student-card').length===1 && document.querySelector('.student-info h3')?.textContent==='Carla Treino');
  check(await page.$$eval('.student-card',es=>es.length===1),'pesquisa por nome');
  await page.click('[data-detail]');await wait('[data-action="status"]');
  await page.click('[data-action="status"]');await wait('#confirm-dialog[open]');
  await page.click('#confirm-dialog [value="confirm"]');
  await page.waitForFunction(()=>document.querySelector('#detail-content .badge')?.textContent==='Inativo');
  check(await page.$eval('[data-action="register"]',e=>e.disabled),'edição desativa check-in');
  await page.click('[data-action="status"]');await wait('#confirm-dialog[open]');await page.click('#confirm-dialog [value="confirm"]');
  await page.waitForFunction(()=>document.querySelector('#detail-content .badge')?.textContent==='Ativo');
  await page.click('[data-action="history"]');await wait('.history-stats');
  check(await page.$eval('#detail-content',e=>e.textContent.includes('O próximo treino espera')),'histórico vazio');
  await page.click('[data-reopen]');await wait('[data-action="register"]:not(:disabled)');
  await page.click('[data-action="register"]');await wait('.success-panel');
  check(await page.$eval('.success-panel',e=>e.textContent.includes('Carla Treino')),'check-in confirmado com nome e hora');
  await screenshot('checkin-sucesso');
  await page.click('.success-panel [data-action="close"]');
  await page.click('[data-detail]');await wait('[data-action="history"]');
  await page.click('[data-action="history"]');await wait('.timeline li');
  await page.click('[data-period="hoje"]');await wait('.timeline li');
  check(await page.$$eval('.timeline li',es=>es.length===1),'histórico atualizado e filtro hoje');
  for(const width of [360,390,430]){
    await page.setViewport({width,height:844,isMobile:true,hasTouch:true,deviceScaleFactor:1});
    await noOverflow('histórico '+width);await screenshot('historico-'+width);
  }
  await page.click('[data-action="close"]');
  await page.click('[data-page="checkin"]');await wait('.student-card');await noOverflow('check-in 430');await screenshot('checkin-430');
  // Slow the real API to verify visible loading; no simulated student data is supplied.
  await page.setRequestInterception(true);
  const slow = req => {if(req.url().includes('/api/Aluno?')) setTimeout(()=>req.continue().catch(()=>{}),700);else req.continue().catch(()=>{});};
  page.on('request',slow);
  await page.click('[data-page="alunos"]');await wait('.skeleton');check(true,'skeleton durante requisição real');
  await wait('.student-card');page.off('request',slow);await page.setRequestInterception(false);
  // A real HTTP error verifies recovery UI without touching stored records.
  await page.setRequestInterception(true);
  const fail=req=>{if(req.url().includes('/api/Aluno?'))req.respond({status:503,contentType:'application/json',body:'{"mensagem":"Serviço temporariamente indisponível."}'});else req.continue();};
  page.on('request',fail);
  await page.click('[data-page="checkin"]');await wait('.empty-state');
  check(await page.$eval('.empty-state',e=>e.textContent.includes('indisponível')),'tratamento de HTTP 503');
  page.off('request',fail);await page.setRequestInterception(false);
  await page.click('[data-action="retry"]');await wait('.student-card');
  await page.setOfflineMode(true);await wait('#connection:not([hidden])');
  check(await page.$eval('#main',e=>e.textContent.includes('Sem conexão')),'modo offline remove dados e bloqueia operações');
  await screenshot('offline-430');
  await page.setOfflineMode(false);await wait('.student-card');
  await page.click('[data-page="conta"]');await wait('.account-card');await noOverflow('conta 430');await screenshot('conta-430');
  await page.emulateMediaFeatures([{name:'prefers-reduced-motion',value:'reduce'}]);
  check(await page.evaluate(()=>matchMedia('(prefers-reduced-motion: reduce)').matches),'preferência de movimento reduzido');
  await page.click('[data-action="logout"]');await wait('#login:not([hidden])');
  check(await page.evaluate(()=>document.querySelector('#main').textContent===''),'logout limpa dados da interface');
  const cachePaths=await page.evaluate(async()=>{await navigator.serviceWorker.ready;return (await Promise.all((await caches.keys()).map(async name=>(await(await caches.open(name)).keys()).map(r=>new URL(r.url).pathname)))).flat();});
  check(cachePaths.length>0 && cachePaths.every(p=>!p.startsWith('/api/')),'cache PWA contém somente arquivos estáticos');
  await page.reload({waitUntil:'networkidle0'});await wait('#login:not([hidden])');
  check(true,'sessão permanece encerrada após recarregar');
  // Desktop uses the same real backend session and existing workflows.
  await page.setViewport({width:1366,height:900});
  await page.goto(url+'/',{waitUntil:'networkidle0'});
  await page.evaluate(()=>entrarNoSistema());await wait('#desktop-login[open]');
  await page.type('#desktop-user',process.env.UGYM_TEST_USER);await page.type('#desktop-password',process.env.UGYM_TEST_PASSWORD);
  await page.click('#desktop-login-submit');await wait('#app');
  await page.waitForFunction(()=>document.querySelectorAll('#tabela-alunos tr').length===4);
  check(true,'desktop autentica e carrega alunos existentes');
  // Add enough fictitious registrations through the real API to exercise both UI pagers.
  await page.evaluate(async()=>{
    function cpf(numero){
      let value=String(numero);
      for(let digit=9;digit<=10;digit++){
        let sum=0;for(let i=0;i<digit;i++)sum+=Number(value[i])*(digit+1-i);
        value+=String(sum%11<2?0:11-sum%11);
      }
      return value;
    }
    for(let i=0;i<21;i++)await UGymApi.request('/api/Aluno',{method:'POST',body:JSON.stringify({nome:'QA Paginação '+String(i).padStart(2,'0'),cpf:cpf(930000000+i),plano:'Mensal'})});
    go('alunos',null);await carregarAlunos();
  });
  await page.waitForFunction(()=>document.querySelectorAll('#tabela-alunos tr').length===20);
  check(await page.$eval('#alunos-status',e=>e.textContent.includes('25 alunos')),'desktop recebe somente vinte dos 25 alunos');
  await page.click('#alunos-proxima');
  await page.waitForFunction(()=>document.querySelector('#alunos-status').textContent.includes('Página 2')&&document.querySelectorAll('#tabela-alunos tr').length===5);
  check(true,'desktop navega para a segunda página');
  await page.type('#alunos-busca','qa paginacao 20');
  await page.waitForFunction(()=>document.querySelector('#alunos-status').textContent.includes('1 aluno encontrado')&&document.querySelector('#tabela-alunos').textContent.includes('QA Paginação 20'));
  check(true,'desktop busca no servidor e volta à primeira página');
  await page.$eval('#alunos-busca',e=>{e.value='';e.dispatchEvent(new Event('input',{bubbles:true}));});
  await page.select('#alunos-filtro','false');
  await page.waitForFunction(()=>document.querySelector('#tabela-alunos').textContent.includes('Bruno Inativo')&&document.querySelectorAll('#tabela-alunos tr').length===1);
  check(true,'desktop combina paginação com filtro de inativos');
  await page.evaluate(()=>sairDaConta());await wait('#landing');
  await page.goto(url+'/mobile/#alunos',{waitUntil:'networkidle0'});
  await page.type('#username',process.env.UGYM_TEST_USER);await page.type('#password',process.env.UGYM_TEST_PASSWORD);
  await page.click('#login-submit');await wait('.student-card');
  await page.waitForFunction(()=>document.querySelectorAll('.student-card').length===20);
  check(await page.$eval('#list-count',e=>e.textContent==='25 alunos'),'mobile recebe somente vinte dos 25 alunos');
  await page.click('#student-pagination [data-list-page="2"]');
  await page.waitForFunction(()=>document.querySelectorAll('.student-card').length===5&&document.querySelector('#student-pagination').textContent.includes('Página 2'));
  check(true,'mobile navega para a segunda página');
  await page.type('#student-search','qa paginacao 00');
  await page.waitForFunction(()=>document.querySelectorAll('.student-card').length===1&&document.querySelector('#list-count').textContent==='1 aluno');
  check(await page.$eval('#student-pagination',e=>e.textContent.includes('Página 1')),'mobile busca sem acentos e reinicia a página');
  await page.click('[data-filter="ativos"]');await wait('.student-card');
  await page.click('[data-detail]');await wait('[data-action="status"]');
  await page.click('[data-action="status"]');await wait('#confirm-dialog[open]');
  await page.click('#confirm-dialog [value="confirm"]');
  await page.waitForFunction(()=>document.querySelector('#detail-content .badge')?.textContent==='Inativo'&&document.querySelector('#list-count').textContent==='0 alunos');
  check(true,'mobile remove aluno do filtro ativo após inativação');
  await page.click('[data-action="close"]');
  await page.evaluate(()=>UGymApi.logout());
  await page.reload({waitUntil:'networkidle0'});await wait('#login:not([hidden])');
  check(errors.length===0,'nenhum erro JavaScript: '+errors.join('; '));
  console.log('Interface: '+checks+' verificações passaram. Capturas: '+output);
 } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exitCode=1;});
