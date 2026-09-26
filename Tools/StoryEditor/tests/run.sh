#!/usr/bin/env bash
# Автотесты редактора сюжета.
#   tests/run.sh        — редактор в headless Chromium (режимы «браузер» и «db»), ~90 проверок
#   tests/run.sh app    — приложение для Mac целиком (Electron + диск), ~40 проверок; на Linux — через xvfb-run
#   tests/run.sh all    — всё, плюс тесты хранения приложения (npm test)
# Нужен playwright (npm i -g playwright или NODE_PATH на его node_modules). Свой Chromium — PW_CHROMIUM=/путь/к/chrome.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$HERE/.out"
mkdir -p "$OUT"

web() {
  python3 "$HERE/wrap.py" "$OUT/local.html"
  python3 "$HERE/wrap.py" "$OUT/db.html" "$HERE/mockdb.html"
  node "$HERE/web.test.js" "$OUT"
}

app() {
  (cd "$HERE/../app" && node scripts/copy-editor.js >/dev/null)
  if [ -z "${DISPLAY:-}" ] && command -v xvfb-run >/dev/null; then xvfb-run -a node "$HERE/app.e2e.js"; else node "$HERE/app.e2e.js"; fi
}

case "${1:-web}" in
  web) web ;;
  app) app ;;
  all) (cd "$HERE/../app" && npm test); web; app ;;
  *) echo "run.sh [web|app|all]"; exit 2 ;;
esac
