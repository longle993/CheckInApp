from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

artifact_root = Path(__file__).resolve().parent.parent / '.local' / 'android-download'
artifact_root.mkdir(parents=True, exist_ok=True)

class PreviewHandler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.apk': 'application/vnd.android.package-archive'}

    def do_GET(self):
        if urlsplit(self.path).path.rstrip('/').lower() == '/checkinapp':
            self.send_response(302)
            self.send_header('Location', '/')
            self.end_headers()
            return
        super().do_GET()

    def list_directory(self, path):
        self.send_error(403)
        return None

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        if self.path.endswith('.apk'):
            self.send_header('Content-Disposition', 'attachment; filename="CheckInApp-LAN.apk"')
        super().end_headers()

ThreadingHTTPServer(('0.0.0.0', 8765), partial(PreviewHandler, directory=str(artifact_root))).serve_forever()
