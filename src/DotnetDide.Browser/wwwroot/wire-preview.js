// Interactive HTML lives in an opaque iframe. Only virtual HTTP messages cross
// into the host; no session storage, model keys, DOM handles or worker references.
export async function createWirePreview({ iframe, request, port, path = '/', onNavigate = () => {} }) {
  if (!(iframe instanceof HTMLIFrameElement)) throw new TypeError('An iframe is required.');
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new TypeError('Invalid virtual port.');
  iframe.setAttribute('sandbox', 'allow-scripts allow-forms'); iframe.referrerPolicy = 'no-referrer';
  const hostUrl = new URL("./wire-preview-host.html", import.meta.url);
  let closed = false, generation = 0, channel = null, currentPath = path, detach = () => {};
  const origin = `http://wire.invalid:${port}`;
  const local = (value, base) => {
    const url = new URL(value, origin + base);
    if (url.origin !== origin || url.username || url.password) throw new Error('Preview only supports this virtual site.');
    return url.pathname + url.search;
  };
  const get = async value => {
    const result = await request({ port, method: 'GET', path: value });
    if (result.status < 200 || result.status >= 300) throw new Error(`${value}: ${result.error || result.status}`);
    return result;
  };
  const text = data => new TextDecoder().decode(Uint8Array.from(atob(data), c => c.charCodeAt(0)));
  const contentType = response => Object.entries(response.headers).find(([key]) => key.toLowerCase() === 'content-type')?.[1]?.[0] ?? 'application/octet-stream';
  const dataUrl = async (value, base) => {
    if (value.startsWith('data:')) return value;
    const response = await get(local(value, base)); return `data:${contentType(response)};base64,${response.body}`;
  };
  const css = async (source, base) => {
    const pattern = /url\(\s*(['"]?)([^'"\s)]+)\1\s*\)/g;
    const matches = [...source.matchAll(pattern)];
    for (const match of matches) if (!match[2].startsWith('#')) source = source.replace(match[0], `url("${await dataUrl(match[2], base)}")`);
    if (/@import\b/i.test(source)) throw new Error('Inline CSS imports or use separate stylesheet links in this preview.');
    return source;
  };
  async function render(nextPath) {
    if (closed) throw new Error('Preview is closed.');
    const mine = ++generation; currentPath = local(nextPath, currentPath);
    detach(); channel?.close(); channel = null;
    const page = await get(currentPath);
    if (!contentType(page).includes('text/html')) throw new Error('Choose an HTML page for the interactive preview.');
    const template = document.createElement('template'); template.innerHTML = text(page.body);
    for (const node of template.content.querySelectorAll('meta,base,iframe,object,embed')) node.remove();
    for (const script of template.content.querySelectorAll('script')) {
      if (script.type === 'module') throw new Error('This preview supports classic scripts. Module imports require the separate-origin service-worker host.');
      if (script.src) {
        const source = text((await get(local(script.getAttribute('src'), currentPath))).body);
        script.removeAttribute('src'); script.textContent = source.replace(/<\/script/gi, '<\\/script');
      }
      script.removeAttribute('integrity'); script.removeAttribute('crossorigin');
    }
    for (const link of template.content.querySelectorAll('link')) {
      if (link.rel !== 'stylesheet') { link.remove(); continue; }
      const asset = local(link.getAttribute('href'), currentPath);
      const style = document.createElement('style'); style.textContent = await css(text((await get(asset)).body), asset); link.replaceWith(style);
    }
    for (const style of template.content.querySelectorAll('style')) style.textContent = await css(style.textContent, currentPath);
    for (const element of template.content.querySelectorAll('[style]')) element.setAttribute('style', await css(element.getAttribute('style'), currentPath));
    for (const element of template.content.querySelectorAll('img,audio,video,source')) {
      element.removeAttribute('srcset');
      if (element.hasAttribute('src')) element.setAttribute('src', await dataUrl(element.getAttribute('src'), currentPath));
      if (element.hasAttribute('poster')) element.setAttribute('poster', await dataUrl(element.getAttribute('poster'), currentPath));
    }
    const token = crypto.randomUUID();
    let html = '';
    const receive = event => {
      if (!closed && mine === generation && event.source === iframe.contentWindow && event.data?.type === 'dotnetdide-wire-host-ready' && event.data.token === token) {
        iframe.contentWindow.postMessage({type:'dotnetdide-wire-document',token,html}, '*'); return;
      }
      if (closed || mine !== generation || event.source !== iframe.contentWindow || event.data?.type !== 'dotnetdide-wire-ready' || event.data.token !== token) return;
      globalThis.removeEventListener('message', receive);
      const pair = new MessageChannel(); channel = pair.port1;
      channel.onmessage = async ({ data }) => {
        if (closed || mine !== generation || !Number.isSafeInteger(data?.id)) return;
        const respond = message => { if (!closed && mine === generation) pair.port1.postMessage({id:data.id,...message}); };
        try {
          if (data.navigate) { await render(local(data.navigate, currentPath)); return; }
          const value = data.request;
          if (!value || typeof value.path !== 'string' || typeof value.method !== 'string' || (value.body?.length ?? 0) > 2_800_000) throw new Error('Invalid preview request.');
          const response = await request({ port, method:value.method, path:local(value.path,currentPath), headers:value.headers, body:value.body });
          respond({response});
        } catch(error) { respond({error:String(error?.message ?? error)}); }
      };
      iframe.contentWindow.postMessage({type:'dotnetdide-wire-connect',token}, '*', [pair.port2]);
    };
    globalThis.addEventListener('message', receive); detach = () => globalThis.removeEventListener('message', receive);
    const bootstrap = `(${guest.toString()})(${JSON.stringify(token)},${JSON.stringify(origin + currentPath)});`;
    const policy = "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data:; media-src data:; font-src data:; connect-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'; object-src 'none'";
    if (closed || mine !== generation) { detach(); return; }
    html = `<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="${policy}"><script>${bootstrap}</script></head><body>${template.innerHTML}</body></html>`;
    iframe.removeAttribute('srcdoc');
    iframe.src = hostUrl.href + '#' + token;
    onNavigate(currentPath);
  }
  try { await render(path); } catch(error) { closed=true;detach();channel?.close();iframe.removeAttribute('src');iframe.removeAttribute('srcdoc');throw error; }
  return { reload: () => render(currentPath), navigate: render, destroy() { closed=true;generation++;detach();channel?.close();iframe.removeAttribute('src');iframe.removeAttribute('srcdoc'); } };
}

function guest(token, base) {
  let port, sequence = 0; const pending = new Map();
  const connected = new Promise(resolve => {
    const connect = event => {
      if (event.source !== parent || event.data?.type !== 'dotnetdide-wire-connect' || event.data.token !== token || !event.ports[0]) return;
      removeEventListener('message', connect);port=event.ports[0];
      port.onmessage = ({data}) => { const item=pending.get(data.id);if(!item)return;pending.delete(data.id);clearTimeout(item.timer);data.error?item.reject(new Error(data.error)):item.resolve(data.response); };
      resolve();
    };
    addEventListener('message',connect);parent.postMessage({type:'dotnetdide-wire-ready',token},'*');
  });
  const invoke = async message => {
    await connected;if(pending.size>=32)throw new Error('Too many preview requests.');
    return new Promise((resolve,reject)=>{const id=++sequence;const timer=setTimeout(()=>{pending.delete(id);reject(new Error('Preview request timed out.'));},25000);pending.set(id,{resolve,reject,timer});port.postMessage({id,...message});});
  };
  globalThis.fetch = async (input,init={}) => {
    const url=new URL(typeof input==='string'||input instanceof URL?input:input.url,base);
    if(url.origin!==new URL(base).origin)throw new TypeError('External networking is disabled in interactive previews.');
    const req=new Request(input instanceof Request?input:url,init);req.signal.throwIfAborted();
    let body='';if(!['GET','HEAD'].includes(req.method)){const bytes=new Uint8Array(await req.arrayBuffer());for(let i=0;i<bytes.length;i+=8192)body+=String.fromCharCode(...bytes.subarray(i,i+8192));body=btoa(body);}
    const response=await invoke({request:{path:url.pathname+url.search,method:req.method,headers:Object.fromEntries([...req.headers].map(([key,value])=>[key,[value]])),body}});
    req.signal.throwIfAborted();
    const bytes=Uint8Array.from(atob(response.body),c=>c.charCodeAt(0));
    const headers=new Headers();for(const [key,values] of Object.entries(response.headers))for(const value of values)headers.append(key,value);
    return new Response([204,205,304].includes(response.status)||req.method==='HEAD'?null:bytes,{status:response.status,headers});
  };
  addEventListener('click',event=>{
    const anchor=event.target?.closest?.('a[href]');if(!anchor || anchor.getAttribute('href').startsWith('#'))return;
    event.preventDefault();void invoke({navigate:new URL(anchor.getAttribute('href'),base).href}).catch(console.error);
  });
}
