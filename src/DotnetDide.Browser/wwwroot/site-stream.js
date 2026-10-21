// Pull-based binary transport. One 64 KiB chunk is requested at a time.
export function openSiteStream(worker, id, value, maximumBytes, options, pending, observed, output, terminate) {
  const {signal, timeoutMs = 30000} = options;
  const rejectEarly = error => { observed.finish(error); return Promise.reject(error); };
  if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) return rejectEarly(new TypeError('Invalid stream idle timeout.'));
  if (signal?.aborted) return rejectEarly(signal.reason ?? new DOMException('Aborted.', 'AbortError'));
  return new Promise((resolve, reject) => {
    const pair = new MessageChannel(); let controller, ended = false, timer, grace, pulling = false;
    let complete, failComplete, response;
    const completed = new Promise((yes, no) => { complete = yes; failComplete = no; });
    completed.catch(() => {}); // The body also reports failures; consumers may only use that API.
    const cleanup = (close = true) => { clearTimeout(timer); signal?.removeEventListener('abort', abort); pending.delete(id); if (close) pair.port1.close(); };
    const fail = reason => {
      if (ended) return; ended = true;
      const error = reason instanceof Error ? reason : new Error(String(reason));
      pair.port1.postMessage({type:'cancel'}); cleanup(false); observed.finish(error);
      grace = setTimeout(() => {pair.port1.close(); terminate(new Error('Site handler ignored cancellation; worker terminated.'));}, 1000);
      controller?.error(error); failComplete(error); reject(error);
    };
    const abort = () => fail(signal.reason ?? new DOMException('Aborted.', 'AbortError'));
    const touch = () => { clearTimeout(timer); timer = setTimeout(() => fail(new Error('Site stream idle timeout.')), timeoutMs); };
    const body = new ReadableStream({
      start(value) { controller = value; },
      pull() { if (!ended && !pulling) { pulling = true; touch(); pair.port1.postMessage({type:'pull'}); } },
      cancel(reason) { fail(reason ?? new DOMException('Response cancelled.', 'AbortError')); },
    }, {highWaterMark:0});
    pair.port1.onmessage = ({data}) => {
      if (ended) { if (data.type === 'closed') { clearTimeout(grace); pair.port1.close(); } return; } touch();
      if (data.type === 'headers') {
        observed.response(data.value);
        response = {...data.value, body, completed, cancel: fail}; resolve(response);
      } else if (data.type === 'chunk') {
        pulling = false; const chunk = data.bytes;
        if (!(chunk instanceof Uint8Array) || chunk.length > 65536) { fail(new Error('Invalid stream chunk.')); return; }
        observed.chunk(chunk); controller.enqueue(chunk);
      } else if (data.type === 'end') {
        ended = true; cleanup(); Object.assign(response, {stdout:data.value.stdout,stderr:data.value.stderr,error:data.value.error}); output(data.value); observed.finish(); controller.close(); complete();
      } else if (data.type === 'error') fail(new Error(data.error));
    };
    pending.set(id, {cleanup, reject:fail});
    signal?.addEventListener('abort', abort, {once:true}); touch();
    try { worker.postMessage({id, operation:'site-stream', payload:{request:value,maximumBytes}, port:pair.port2}, [pair.port2]); }
    catch (error) { fail(error); }
  });
}

/** Compatibility adapter for callers which still need a complete base64 body. */
export async function collectSiteStream(response) {
  const reader = response.body.getReader(); const chunks = [];
  try {
    while (true) { const {value,done} = await reader.read(); if (done) break; chunks.push(value); }
    await response.completed;
  } catch (error) { response.cancel(error); throw error; }
  finally { reader.releaseLock(); }
  const parts = [];
  for (const chunk of chunks) for (let offset = 0; offset < chunk.length; offset += 8192)
    parts.push(String.fromCharCode(...chunk.subarray(offset, offset + 8192)));
  return {status:response.status,headers:response.headers,body:btoa(parts.join('')),error:response.error ?? null,stdout:response.stdout ?? "",stderr:response.stderr ?? ""};
}
