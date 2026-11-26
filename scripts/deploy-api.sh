#!/usr/bin/env bash
# Vubes Club API'yi SSH "vubes-test" sunucusuna yayınlar.
# Kullanım: bash apps/backend-api/scripts/deploy-api.sh
# Veritabanına ve sunucudaki gizli ayarlara (/etc/vubes/api.env) dokunmaz.
set -euo pipefail

API_DIR="$(cd "$(dirname "$0")/.." && pwd)"
HOST="${VUBES_HOST:-vubes-test}"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT
export PATH="$HOME/.dotnet:$PATH"

echo "→ Derleniyor (linux-x64)"
dotnet publish "$API_DIR/VubesClub.Api.csproj" -c Release -r linux-x64 --self-contained true \
  -p:InvariantGlobalization=true -o "$OUT" -v q --nologo >/dev/null

echo "→ Kod gönderiliyor"
rsync -rlt --delete --exclude 'wwwroot/media' "$OUT"/ "$HOST":/opt/vubes/api/

echo "→ Medya gönderiliyor (yalnızca eklenir, silinmez)"
rsync -rlt --exclude '.DS_Store' "$API_DIR/wwwroot/media/" "$HOST":/var/lib/vubes/media/

echo "→ Yeniden başlatılıyor"
ssh "$HOST" 'set -e
sudo chmod 755 /opt/vubes/api
sudo chmod +x /opt/vubes/api/VubesClub.Api
sudo ln -sfn /var/lib/vubes/media /opt/vubes/api/wwwroot/media
sudo chown -R opc:vubes /var/lib/vubes/media
sudo chmod -R u+rwX,g+rX,o+rX /var/lib/vubes/media
sudo chcon -R -t bin_t /opt/vubes/api
sudo chcon -R -t httpd_sys_content_t /var/lib/vubes/media
echo "  veritabanı migration"
sudo systemd-run --quiet --pipe --wait -p User=vubes -p EnvironmentFile=/etc/vubes/api.env \
  -p WorkingDirectory=/opt/vubes/api /opt/vubes/api/VubesClub.Api --migrate
sudo systemctl restart vubes-api'

echo "→ Sağlık kontrolü"
for i in $(seq 1 20); do
  if ssh "$HOST" 'curl -fsS http://127.0.0.1:5080/health/live >/dev/null'; then
    echo "✓ API yayında"
    exit 0
  fi
  sleep 2
done
echo "✗ API yanıt vermedi. Log: ssh $HOST 'sudo journalctl -u vubes-api -n 50 --no-pager'" >&2
exit 1
