#!/usr/bin/env bash
# Чинит DNS/IPv6 на RUVDS, из‑за которых в Docker:
#   Network is unreachable (www.wildberries.ru:443)
#   Name or service not known (api.vk.com / id.vk.ru)
set -euo pipefail

echo "==> Prefer IPv4"
grep -q 'precedence :ffff:0:0/96' /etc/gai.conf 2>/dev/null || \
  echo 'precedence :ffff:0:0/96  100' >> /etc/gai.conf

echo "==> Disable IPv6 on host"
sysctl -w net.ipv6.conf.all.disable_ipv6=1 >/dev/null
sysctl -w net.ipv6.conf.default.disable_ipv6=1 >/dev/null

echo "==> Public DNS"
printf 'nameserver 8.8.8.8\nnameserver 1.1.1.1\n' > /etc/resolv.conf

echo "==> Docker daemon DNS"
mkdir -p /etc/docker
if [[ ! -f /etc/docker/daemon.json ]]; then
  cat > /etc/docker/daemon.json <<'EOF'
{
  "ipv6": false,
  "ip6tables": false,
  "dns": ["8.8.8.8", "1.1.1.1"]
}
EOF
else
  echo "    /etc/docker/daemon.json уже есть — проверьте dns/ipv6 вручную"
fi

systemctl restart docker
sleep 2

echo "==> Probe"
getent ahostsv4 www.wildberries.ru | head -n2 || true
getent ahostsv4 api.vk.com | head -n2 || true
curl -4 -I --max-time 10 https://www.wildberries.ru/ | head -n1 || true
curl -4 -I --max-time 10 https://api.vk.com/ | head -n1 || true

echo "==> Recreate app containers"
cd "$(dirname "$0")/.."
docker compose up -d --force-recreate server bots

echo "==> Done"
