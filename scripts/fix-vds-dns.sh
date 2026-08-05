#!/usr/bin/env bash
# Чинит DNS/IPv6 на RUVDS, из‑за которых в Docker/.NET:
#   No IPv4 addresses for www.wildberries.ru (AAAA-only / broken DNS)
#   Network is unreachable / Name or service not known
set -euo pipefail

echo "==> Prefer IPv4 (gai.conf)"
grep -q 'precedence :ffff:0:0/96' /etc/gai.conf 2>/dev/null || \
  echo 'precedence :ffff:0:0/96  100' >> /etc/gai.conf

echo "==> Disable IPv6 on host"
sysctl -w net.ipv6.conf.all.disable_ipv6=1 >/dev/null
sysctl -w net.ipv6.conf.default.disable_ipv6=1 >/dev/null
# Persist across reboot if possible
if [[ -d /etc/sysctl.d ]]; then
  cat > /etc/sysctl.d/99-disable-ipv6.conf <<'EOF'
net.ipv6.conf.all.disable_ipv6 = 1
net.ipv6.conf.default.disable_ipv6 = 1
EOF
fi

echo "==> Host DNS → 8.8.8.8 / 1.1.1.1"
# systemd-resolved часто перетирает /etc/resolv.conf — правим и resolved, и файл.
if systemctl is-active --quiet systemd-resolved 2>/dev/null; then
  mkdir -p /etc/systemd/resolved.conf.d
  cat > /etc/systemd/resolved.conf.d/dns.conf <<'EOF'
[Resolve]
DNS=8.8.8.8 1.1.1.1
FallbackDNS=8.8.4.4 1.0.0.1
DNSStubListener=yes
EOF
  systemctl restart systemd-resolved || true
  # Симлинк на stub; дополнительно прописываем публичные NS на случай поломки stub.
  if [[ -L /etc/resolv.conf ]] || [[ -f /etc/resolv.conf ]]; then
    true
  fi
fi

# Надёжный fallback: прямой resolv.conf (если stub снова сломается — раскомментируйте unlink).
printf 'nameserver 8.8.8.8\nnameserver 1.1.1.1\noptions timeout:2 attempts:3\n' > /etc/resolv.conf
# Если resolved вернул stub-symlink — оставляем, но DNS уже в resolved.conf.d
if systemctl is-active --quiet systemd-resolved 2>/dev/null; then
  ln -sf /run/systemd/resolve/resolv.conf /etc/resolv.conf 2>/dev/null || true
  # И всё же дублируем публичные DNS в resolv.conf stub иногда игнорирует — force file:
  # На проблемных RUVDS stub часто ломает curl → пишем плоский файл.
  rm -f /etc/resolv.conf
  printf 'nameserver 8.8.8.8\nnameserver 1.1.1.1\noptions timeout:2 attempts:3\n' > /etc/resolv.conf
fi

echo "==> Docker daemon.json (ipv6 off + DNS)"
mkdir -p /etc/docker
python3 - <<'PY'
import json
from pathlib import Path
path = Path("/etc/docker/daemon.json")
data = {}
if path.exists():
    try:
        data = json.loads(path.read_text() or "{}")
    except Exception:
        data = {}
data["ipv6"] = False
data["ip6tables"] = False
data["dns"] = ["8.8.8.8", "1.1.1.1"]
path.write_text(json.dumps(data, indent=2) + "\n")
print(path.read_text())
PY

echo "==> Restart Docker"
systemctl restart docker
sleep 3

probe_https() {
  local url="$1"
  # -sS: без прогресс-бара (иначе | head обрывает curl до HTTP-заголовков)
  local code
  code="$(curl -4 -sS -o /dev/null -w '%{http_code}' --connect-timeout 5 --max-time 12 "$url" 2>/tmp/wba-curl-err || true)"
  if [[ -n "$code" && "$code" != "000" ]]; then
    echo "OK  $url → HTTP $code"
  else
    echo "FAIL $url → $(tr '\n' ' ' </tmp/wba-curl-err 2>/dev/null || echo timeout/unknown)"
  fi
}

echo "==> Host probe"
echo "--- getent ahostsv4 www.wildberries.ru"
getent ahostsv4 www.wildberries.ru | head -n3 || true
echo "--- /etc/resolv.conf"
cat /etc/resolv.conf || true
probe_https "https://www.wildberries.ru/"
probe_https "https://api.vk.com/"
probe_https "https://dns.google/resolve?name=www.wildberries.ru&type=A"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Recreate app containers (with rebuild — DoH in Ipv4Http)"
docker compose up -d --build --force-recreate server bots

echo "==> Container DNS probe (wba-server)"
sleep 3
docker exec wba-server cat /etc/resolv.conf || true
# В slim-образе curl/getent может не быть — это нормально.
if docker exec wba-server sh -c 'command -v curl >/dev/null 2>&1'; then
  docker exec wba-server sh -c 'curl -4 -sS -o /dev/null -w "WB HTTP %{http_code}\n" --connect-timeout 5 --max-time 12 https://www.wildberries.ru/' || true
else
  echo "(no curl in image — смотрите логи обновления цен / DoH в Ipv4Http)"
fi

echo "==> Done"
echo "Проверка: docker logs wba-server --tail 50"
echo "Ожидаем отсутствие 'AAAA-only / broken DNS'."
