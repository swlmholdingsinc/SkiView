#!/usr/bin/env python3
"""Pass 18: headless-Chrome check of the WebGL build (no extra packages: a tiny CDP websocket client).
Serves Builds/WebGL on a local port, opens index.html?<query>, records console lines and fetched bundle names, waits for a
console regex (default LAKE READY), optionally presses keys / clicks, then saves a screenshot.
Usage: webgl_check.py --query "lake=tallpine" [--wait "LAKE READY"] [--shot out.png] [--after 4] [--size 1920x1080] [--keys c]"""
import argparse, base64, json, os, re, socket, struct, subprocess, sys, threading, time, urllib.request, http.server, functools

class WS:
    def __init__(self, url):
        m = re.match(r'ws://([^:/]+):(\d+)(/.*)', url); host, port, path = m.group(1), int(m.group(2)), m.group(3)
        self.s = socket.create_connection((host, port)); key = base64.b64encode(os.urandom(16)).decode()
        self.s.sendall(('GET %s HTTP/1.1\r\nHost: %s:%d\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: %s\r\nSec-WebSocket-Version: 13\r\n\r\n' % (path, host, port, key)).encode())
        buf = b''
        while b'\r\n\r\n' not in buf: buf += self.s.recv(4096)
        self.rest = buf.split(b'\r\n\r\n', 1)[1]; self.lock = threading.Lock()
    def send(self, obj):
        data = json.dumps(obj).encode(); hdr = bytearray([0x81]); n = len(data); mask = os.urandom(4)
        if n < 126: hdr.append(0x80 | n)
        elif n < 65536: hdr.append(0x80 | 126); hdr += struct.pack('>H', n)
        else: hdr.append(0x80 | 127); hdr += struct.pack('>Q', n)
        with self.lock: self.s.sendall(bytes(hdr) + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(data)))
    def _read(self, n):
        while len(self.rest) < n:
            c = self.s.recv(1 << 20)
            if not c: raise EOFError
            self.rest += c
        out, self.rest = self.rest[:n], self.rest[n:]; return out
    def recv(self):
        msg = b''
        while True:
            b0, b1 = self._read(2); n = b1 & 0x7f
            if n == 126: n = struct.unpack('>H', self._read(2))[0]
            elif n == 127: n = struct.unpack('>Q', self._read(8))[0]
            payload = self._read(n); op = b0 & 0x0f
            if op in (0, 1, 2): msg += payload
            if b0 & 0x80 and op in (0, 1, 2): return json.loads(msg.decode('utf-8', 'replace'))

class H(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *a): pass
    def end_headers(self):
        p = self.path.split('?')[0]
        if p.endswith('.br'): self.send_header('Content-Encoding', 'br')
        elif p.endswith('.gz'): self.send_header('Content-Encoding', 'gzip')
        super().end_headers()
    def guess_type(self, path):
        p = path[:-3] if path.endswith(('.br', '.gz')) else path
        if p.endswith('.wasm'): return 'application/wasm'
        if p.endswith('.js'): return 'application/javascript'
        return super().guess_type(p)

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--query', default=''); ap.add_argument('--wait', default='LAKE READY'); ap.add_argument('--timeout', type=float, default=180)
    ap.add_argument('--shot'); ap.add_argument('--after', type=float, default=3); ap.add_argument('--size', default='1920x1080'); ap.add_argument('--keys', default=''); ap.add_argument('--fill', action='store_true', help='stretch the canvas over the whole window (review stills)')
    ap.add_argument('--root', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Builds', 'WebGL')); ap.add_argument('--port', type=int, default=8765)
    a = ap.parse_args(); W, Hh = map(int, a.size.split('x'))
    srv = http.server.ThreadingHTTPServer(('127.0.0.1', a.port), functools.partial(H, directory=os.path.abspath(a.root)))
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    prof = '/tmp/p18_chrome_%d' % os.getpid()
    ch = subprocess.Popen(['nice', '-n', '15', 'google-chrome', '--headless=new', '--remote-debugging-port=9333', '--user-data-dir=' + prof, '--no-first-run', '--use-angle=swiftshader',
                           '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--window-size=%d,%d' % (W, Hh), '--autoplay-policy=no-user-gesture-required', 'about:blank'],
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    res = dict(query=a.query, console=[], fetched=[], ready_s=None, errors=[])
    try:
        for _ in range(100):
            try: tabs = json.load(urllib.request.urlopen('http://127.0.0.1:9333/json')); break
            except Exception: time.sleep(0.2)
        page = [t for t in tabs if t.get('type') == 'page'][0]; ws = WS(page['webSocketDebuggerUrl'])
        mid = [0]
        def call(method, **params): mid[0] += 1; ws.send(dict(id=mid[0], method=method, params=params)); return mid[0]
        call('Runtime.enable'); call('Network.enable'); call('Page.enable'); call('Emulation.setDeviceMetricsOverride', width=W, height=Hh, deviceScaleFactor=1, mobile=False)
        FILL = "var k=document.querySelector('#unity-container'); if(k){k.style.cssText='position:absolute;left:0;top:0;width:%dpx;height:%dpx;transform:none;margin:0';} var c=document.querySelector('#unity-canvas'); if(c){c.style.width='%dpx';c.style.height='%dpx';} var f=document.querySelector('#unity-footer'); if(f) f.style.display='none';" % (W, Hh, W, Hh)
        t0 = time.time(); call('Page.navigate', url='http://127.0.0.1:%d/index.html%s' % (a.port, ('?' + a.query) if a.query else ''))
        pend = {}; shot_id = None; ready_at = None; done_actions = False
        ws.s.settimeout(1.0)
        while time.time() - t0 < a.timeout:
            try: m = ws.recv()
            except socket.timeout: m = None
            if m:
                me = m.get('method')
                if me == 'Runtime.consoleAPICalled':
                    txt = ' '.join(str(x.get('value', x.get('description', ''))) for x in m['params']['args'])
                    res['console'].append(round(time.time() - t0, 1).__str__() + ' ' + txt[:300])
                    if ready_at is None and re.search(a.wait, txt): ready_at = time.time(); res['ready_s'] = round(ready_at - t0, 1)
                elif me == 'Runtime.exceptionThrown': res['errors'].append(str(m['params']['exceptionDetails'].get('text'))[:300])
                elif me == 'Network.requestWillBeSent':
                    u = m['params']['request']['url'].split('?')[0]; res['fetched'].append(u.rsplit('/', 1)[-1])
                elif m.get('id') == shot_id and 'result' in m:
                    open(a.shot, 'wb').write(base64.b64decode(m['result']['data'])); res['shot'] = a.shot; break
            if a.fill and time.time() - t0 > 1.0 and not getattr(main, 'filled', False):
                main.filled = True; call('Runtime.evaluate', expression=FILL)
            if ready_at and not done_actions and time.time() - ready_at > 1.0:
                done_actions = True
                for k in a.keys:
                    call('Input.dispatchKeyEvent', type='keyDown', key=k, text=k, code='Key' + k.upper(), windowsVirtualKeyCode=ord(k.upper()))
                    call('Input.dispatchKeyEvent', type='keyUp', key=k, code='Key' + k.upper(), windowsVirtualKeyCode=ord(k.upper())); time.sleep(0.4)
            if ready_at and shot_id is None and time.time() - ready_at > a.after:
                if a.shot: shot_id = call('Page.captureScreenshot', format='png')
                else: break
    finally:
        ch.terminate(); srv.shutdown()
        try: ch.wait(5)
        except Exception: ch.kill()
        subprocess.call(['rm', '-rf', prof])
    res['bundles'] = sorted(set(f for f in res['fetched'] if f.endswith('.bundle')))
    print(json.dumps({k: v for k, v in res.items() if k != 'fetched'}, indent=1))

main()
