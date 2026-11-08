const $ = id => document.getElementById(id);
let catalog = [];
const modes = ['tasa', 'precio_limpio', 'valor_transado'];
const number = (value, digits=2) => new Intl.NumberFormat('es-DO', {minimumFractionDigits:digits, maximumFractionDigits:digits}).format(value);
const date = value => new Date(value).toLocaleDateString('es-DO', {day:'2-digit',month:'short',year:'numeric',timeZone:'UTC'});
function selectBond() {
  const b = catalog.find(e => e.id === $('titulo').value).bond;
  const nominal = $('valorNominalInput');
  nominal.min = nominal.step = b.denomination;
  nominal.value = Math.ceil(Math.max(Number(nominal.value)||100000,b.denomination)/b.denomination)*b.denomination;
  const start=new Date(b.issueDate), end=new Date(b.maturityDate);
  start.setUTCDate(start.getUTCDate()+1); end.setUTCDate(end.getUTCDate()-1);
  const field=$('fechaPicker'); field.min=start.toISOString().slice(0,10); field.max=end.toISOString().slice(0,10);
  field.value = '2017-09-01';
  if(field.value<field.min) field.value=field.min;
  if(field.value>field.max) field.value=field.min;
  $('ResultsContent').hidden=$('tableContent').hidden=true;
  $('status').textContent='';
}
function selectIssuer() {
  $('titulo').replaceChildren();
  for(const entry of catalog.filter(e=>e.issuer===$('emisorDrop').value)) {
    const option=document.createElement('option'); option.value=entry.id;option.textContent=entry.bond.alias;$('titulo').append(option);
  }
  selectBond();
}
function selectMode() {
  for(const name of modes) { const input=$(name+'Input'); input.disabled=!$(name+'Radio').checked;input.required=!input.disabled; }
}
$('emisorDrop').addEventListener('change',selectIssuer);
$('titulo').addEventListener('change',selectBond);
for(const name of modes) {
  $(name+'Radio').addEventListener('change',selectMode);
  $(name+'Input').min='0.000001'; $(name+'Input').step='any';
}
$('tasaInput').max='100'; $('precio_limpioInput').removeAttribute('max');
$('tasaInput').value='10';$('precio_limpioInput').value='100';$('valor_transadoInput').value='100000';
$('fechaPicker').required=$('valorNominalInput').required=true;
selectMode();
function table(id, rows, header) {
  const table=document.createElement('table');table.id=id;
  if(header){const thead=table.createTHead(),tr=thead.insertRow();for(const value of header){const th=document.createElement('th');th.textContent=value;tr.append(th);}}
  const tbody=table.createTBody();
  for(const row of rows){const tr=tbody.insertRow();row.forEach((value,i)=>{const td=tr.insertCell();td.textContent=value;if(!header&&i===0)td.className='propertyLabel';});}
  return table;
}
$('calcularButton').addEventListener('click',async()=>{
  const mode=modes.findIndex(name=>$(name+'Radio').checked)+1;
  for(const id of ['titulo','fechaPicker','valorNominalInput',modes[mode-1]+'Input']) if(!$(id).reportValidity())return;
  $('calcularButton').disabled=true;$('loaderDiv').hidden=false;$('ResultsContent').hidden=$('tableContent').hidden=true;$('status').textContent='';
  try {
    const response=await fetch('/api/calculate',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({id:$('titulo').value,settlement:$('fechaPicker').value,nominal:Number($('valorNominalInput').value),mode,value:Number($(modes[mode-1]+'Input').value)})});
    const data=await response.json();if(!response.ok)throw Error(data.error||'No se pudo calcular.');const r=data.result;
    const rows=[['Emisor',r.emisor],['Título',r.alias],['Fecha Emisión',date(r.fechaInicio)],['Fecha Vencimiento',date(r.fechaVencimiento)],['Código ISIN',r.isin],['Moneda',r.moneda],['Tipo',r.tipo],['Base',r.metodo],['Tasa Cupón',number(r.tasaCupon*100,4)+'%'],['Días a Vencimiento',r.diasAVencimiento],['Valor Nominal',number(r.valorNominal)],['Valor Compra',number(r.valorCompra)],['Valor Transado',number(r.valorTransado)],['Rendimiento',number(r.yield*100,8)+'%'],['Precio Limpio',number(r.precioLimpio*100,8)+'%'],['Precio Sucio',number(r.precioSucio*100,8)+'%'],['Cupón Corrido',number(r.cuponCorrido)],['Fecha Valoración',date(r.fechaLiquidacion)],['Ratio Cupón',number(r.ratioCupon,5)],['Días Acumulados',r.diasAcumulados],['Duración Macaulay',number(r.macaulayDuration,6)]];
    $('ResultsDiv').replaceChildren(table('ResultsTable',rows));$('TableDiv').replaceChildren(table('tableOfPayments',data.payments,data.columns));
    $('ResultsContent').hidden=$('tableContent').hidden=$('pagosButton').hidden=false;
    document.querySelectorAll('.returnButton').forEach(b=>b.hidden=false);
    $('ResultsSection').scrollIntoView({behavior:'smooth'});
  } catch(error){$('status').textContent=error.message;}
  finally{$('calcularButton').disabled=false;$('loaderDiv').hidden=true;}
});
$('pagosButton').addEventListener('click',()=>$('tableSection').scrollIntoView({behavior:'smooth'}));
document.querySelectorAll('.returnButton').forEach(b=>b.addEventListener('click',()=>$('mainSection').scrollIntoView({behavior:'smooth'})));
document.querySelector('.navbar-toggler').addEventListener('click',e=>{const expanded=$('navbarResponsive').classList.toggle('show');e.currentTarget.setAttribute('aria-expanded',String(expanded));});
$('contactLink').addEventListener('click',e=>{e.preventDefault();alert('ValoresRD · Proyecto histórico de 2017. Los datos de contacto originales no están disponibles en esta demostración.');});
fetch('/api/bonds').then(r=>{if(!r.ok)throw Error('No se pudo cargar el catálogo.');return r.json();}).then(data=>{
  catalog=data;$('emisorDrop').replaceChildren();
  for(const issuer of [...new Set(data.map(e=>e.issuer))]) {const option=document.createElement('option');option.textContent=option.value=issuer;$('emisorDrop').append(option);}
  $('emisorDrop').disabled=$('titulo').disabled=$('calcularButton').disabled=false;
  selectIssuer();
}).catch(error=>{$('status').textContent=error.message;}).finally(()=>{
  $('splashscreen').hidden=true;document.querySelectorAll('.initialHidden').forEach(e=>e.hidden=false);
});
