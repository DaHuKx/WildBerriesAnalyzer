#!/usr/bin/env python3
"""Проверка списка прокси на доступ к ozon.ru (stdlib, Windows/Linux).

Формат строк:
  user:pass@host:port
  host:port:user:pass
  http://user:pass@host:port

Пример:
  python scripts/check-ozon-proxies.py "E:\\Загрузки\\proxys_70199 (1).txt"
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from typing import Optional
from urllib.error import HTTPError, URLError
from urllib.request import ProxyHandler, Request, build_opener

URL = "https://www.ozon.ru/"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
)


def parse_proxy_line(line: str) -> Optional[str]:
    raw = line.strip()
    if not raw or raw.startswith("#"):
        return None
    if raw.startswith("http://") or raw.startswith("https://") or raw.startswith("socks"):
        return raw

    # user:pass@host:port
    if "@" in raw:
        return "http://" + raw

    # host:port:user:pass
    parts = raw.split(":")
    if len(parts) >= 4:
        host, port, user = parts[0], parts[1], parts[2]
        password = ":".join(parts[3:])
        return f"http://{user}:{password}@{host}:{port}"

    return None


def proxy_label(proxy_url: str) -> str:
    try:
        _, rest = proxy_url.split("://", 1)
        if "@" in rest:
            rest = rest.split("@", 1)[1]
        return rest
    except ValueError:
        return proxy_url


def classify(code: int, location: str, error: str) -> str:
    loc = (location or "").lower()
    err = (error or "").lower()
    if error:
        if "timed out" in err or "timeout" in err:
            return "timeout"
        if "407" in err:
            return "auth"
        return "fail"
    if code == 200:
        return "ok"
    if code in (301, 302, 307, 308) and "__rr=" in loc:
        return "challenge"
    if code in (301, 302, 307, 308):
        return "redirect"
    if code == 403:
        return "blocked"
    if code == 407:
        return "auth"
    return f"http{code}"


def check_one(proxy_url: str, timeout: float) -> dict:
    started = time.perf_counter()
    opener = build_opener(
        ProxyHandler({"http": proxy_url, "https": proxy_url}),
    )
    req = Request(
        URL,
        method="GET",
        headers={
            "User-Agent": USER_AGENT,
            "Accept": "text/html,application/xhtml+xml",
            "Accept-Language": "ru-RU,ru;q=0.9",
        },
    )
    code = 0
    location = ""
    error = ""
    try:
        with opener.open(req, timeout=timeout) as resp:
            code = getattr(resp, "status", None) or resp.getcode() or 0
            location = resp.headers.get("Location") or ""
    except HTTPError as ex:
        code = ex.code
        location = ex.headers.get("Location") if ex.headers else ""
        error = "" if code in (301, 302, 303, 307, 308, 403, 407) else str(ex)
    except Exception as ex:
        error = f"{type(ex).__name__}: {ex}"

    elapsed = time.perf_counter() - started
    verdict = classify(code, location, error)
    return {
        "proxy": proxy_label(proxy_url),
        "proxyUrl": proxy_url,
        "code": code,
        "time_s": round(elapsed, 3),
        "location": location[:180],
        "error": error[:200],
        "verdict": verdict,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Проверка прокси на ozon.ru")
    parser.add_argument("file", help="Текстовый список прокси")
    parser.add_argument("--workers", type=int, default=20)
    parser.add_argument("--timeout", type=float, default=12)
    parser.add_argument("--limit", type=int, default=0, help="Проверить только первые N (0 = все)")
    parser.add_argument(
        "--out",
        default="",
        help="CSV-отчёт (по умолчанию рядом со списком: ozon-proxy-check.csv)",
    )
    args = parser.parse_args()

    src = Path(args.file)
    if not src.is_file():
        print(f"Файл не найден: {src}", file=sys.stderr)
        return 1

    proxies: list[str] = []
    seen: set[str] = set()
    for line in src.read_text(encoding="utf-8", errors="ignore").splitlines():
        parsed = parse_proxy_line(line)
        if not parsed or parsed in seen:
            continue
        seen.add(parsed)
        proxies.append(parsed)

    if args.limit > 0:
        proxies = proxies[: args.limit]

    if not proxies:
        print("В файле нет прокси.", file=sys.stderr)
        return 1

    out_path = Path(args.out) if args.out else src.with_name("ozon-proxy-check.csv")
    print(f"Прокси: {len(proxies)}, workers={args.workers}, timeout={args.timeout}s")
    print(f"Цель: {URL}")
    print()

    results: list[dict] = []
    done = 0
    with ThreadPoolExecutor(max_workers=max(1, args.workers)) as pool:
        futs = {pool.submit(check_one, p, args.timeout): p for p in proxies}
        for fut in as_completed(futs):
            row = fut.result()
            results.append(row)
            done += 1
            mark = "OK" if row["verdict"] in ("ok", "challenge", "redirect") else row["verdict"]
            print(
                f"[{done}/{len(proxies)}] {row['proxy']:40}  "
                f"{mark:10}  http={row['code']:<3}  {row['time_s']:.2f}s"
            )

    results.sort(key=lambda r: (r["verdict"], r["proxy"]))
    with out_path.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.DictWriter(
            fh,
            fieldnames=["verdict", "code", "time_s", "proxy", "location", "error", "proxyUrl"],
        )
        writer.writeheader()
        writer.writerows(results)

    counts: dict[str, int] = {}
    for row in results:
        counts[row["verdict"]] = counts.get(row["verdict"], 0) + 1

    usable = [r for r in results if r["verdict"] in ("ok", "challenge", "redirect")]
    print()
    print("Итог:", json.dumps(counts, ensure_ascii=False))
    print(f"Живых (до Ozon достучались): {len(usable)} / {len(results)}")
    print(f"CSV: {out_path}")

    if usable:
        best = min(usable, key=lambda r: r["time_s"])
        print()
        print("Самый быстрый живой — вставьте в ozon-scraping-auth.json:")
        print(f'  "proxyUrl": "{best["proxyUrl"]}"')
        print()
        print("curl 307 / challenge — нормально для Ozon; fail/timeout/blocked — не используйте.")
    else:
        print()
        print("Ни один прокси не достучался до ozon.ru. Проверьте логин, HTTP vs SOCKS5, лимит потоков.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
