#!/bin/bash
# Le agrega un hash de contenido al nombre de app.css en la salida de publish
# y actualiza la referencia en index.html, para que un app.css nuevo siempre
# tenga una URL nueva. Así se puede cachear ese archivo de forma agresiva e
# indefinida sin correr el riesgo de que un navegador se quede pegado con una
# versión vieja: si el contenido cambia, cambia el nombre, y el navegador
# nunca reutiliza la copia vieja porque nunca la vuelve a pedir con esa URL.
set -euo pipefail

WWWROOT="$1"
CSS_DIR="$WWWROOT/css"
INDEX_FILE="$WWWROOT/index.html"

# dotnet publish es incremental: si el .css fuente no cambió, puede que ni
# siquiera esté presente como "app.css" plano (ya quedó renombrado de una
# corrida anterior). Por eso buscamos cualquier variante ya existente
# (app.css o app.<hash-viejo>.css) en vez de asumir un nombre fijo.
CSS_FILE=$(find "$CSS_DIR" -maxdepth 1 -name "app.css" -o -name "app.*.css" 2>/dev/null | grep -v '\.gz$\|\.br$' | head -1)

if [ -z "$CSS_FILE" ] || [ ! -f "$INDEX_FILE" ]; then
  echo "fingerprint-css.sh: no se encontró app*.css o index.html en $WWWROOT, se omite." >&2
  exit 0
fi

HASH=$(shasum -a 256 "$CSS_FILE" | cut -c1-10)
NEW_NAME="app.$HASH.css"
NEW_FILE="$CSS_DIR/$NEW_NAME"

if [ "$CSS_FILE" != "$NEW_FILE" ]; then
  mv "$CSS_FILE" "$NEW_FILE"
  [ -f "$CSS_FILE.gz" ] && mv "$CSS_FILE.gz" "$NEW_FILE.gz"
  [ -f "$CSS_FILE.br" ] && mv "$CSS_FILE.br" "$NEW_FILE.br"
fi

# Reemplaza cualquier referencia previa (plana o con hash viejo) por la
# actual. Idempotente: corre bien tanto en un publish limpio como en uno
# incremental que ya traía un index.html reescrito de una corrida anterior.
sed -i.bak -E "s#css/app(\.[a-f0-9]+)?\.css#css/$NEW_NAME#g" "$INDEX_FILE"
rm -f "$INDEX_FILE.bak"

# Limpia hashes viejos que hayan quedado de publicaciones incrementales
# anteriores, para que no se acumulen archivos huérfanos sin referencia.
find "$CSS_DIR" -maxdepth 1 -name "app.*.css*" ! -name "$NEW_NAME" ! -name "$NEW_NAME.gz" ! -name "$NEW_NAME.br" -delete 2>/dev/null

echo "fingerprint-css.sh: app.css -> $NEW_NAME"
