#!/usr/bin/env python3
"""Imports GreyHaven's game-compatibility data from two open datasets.

    python3 -I tools/apps/import.py anticheat            # Are We Anti-Cheat Yet (MIT)
    python3 -I tools/apps/import.py protondb <dump.tar.gz> [--date YYYY-MM-DD]   # ProtonDB (ODbL)

anticheat: downloads games.json from github.com/AreWeAntiCheatYet/AreWeAntiCheatYet (MIT) and
    keeps name, status, anti-cheats, store IDs and the reference link, in data/apps/anticheat.json.
protondb: summarises a ProtonDB report dump from github.com/bdefore/protondb-data (released
    under the Open Database License) into data/apps/protondb-summary.json.gz: per Steam app ID,
    how many reports from the two years before the dump, the share that say it works, and the
    share that worked without tweaks. The summary is a derived database, so it stays ODbL
    (data/apps/LICENSE-protondb.md). protondb.com itself is never fetched.

Downloaded files are untrusted: they're only parsed as JSON, never run.
"""
import argparse, datetime, gzip, io, json, sys, tarfile, urllib.request
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "data" / "apps"
AWACY = "https://raw.githubusercontent.com/AreWeAntiCheatYet/AreWeAntiCheatYet/HEAD/games.json"


def anticheat():
    with urllib.request.urlopen(AWACY, timeout=60) as r:
        games = json.load(r)
    keep = []
    for g in games:
        ids = g.get("storeIds") or {}
        keep.append({
            "name": g["name"],
            "status": g["status"],                      # Supported | Running | Planned | Broken | Denied
            "native": bool(g.get("native")),
            "anticheats": g.get("anticheats") or [],
            "steam": ids.get("steam"),
            "epic": (ids.get("epic") or {}).get("slug"),
            "reference": g.get("reference") or None,
            "changed": (g.get("dateChanged") or "")[:10] or None,
        })
    keep.sort(key=lambda x: x["name"].lower())
    doc = {
        "source": "Are We Anti-Cheat Yet (https://areweanticheatyet.com), github.com/AreWeAntiCheatYet/AreWeAntiCheatYet, MIT License",
        "imported": datetime.date.today().isoformat(),
        "games": keep,
    }
    (OUT / "anticheat.json").write_text(json.dumps(doc, indent=0, ensure_ascii=False) + "\n")
    print(f"{len(keep)} games -> data/apps/anticheat.json")


def protondb(dump, date):
    with tarfile.open(dump, "r:gz") as tar:
        member = next(m for m in tar.getmembers() if m.isfile() and m.name.endswith(".json"))
        reports = json.load(tar.extractfile(member))
    cutoff = (date - datetime.timedelta(days=730))
    per = defaultdict(lambda: [0, 0, 0])  # reports, works, works without tweaks
    for r in reports:
        try:
            appid = str(r["app"]["steam"]["appId"])
            when = datetime.date.fromtimestamp(int(r["timestamp"]))
            resp = r["responses"]
        except (KeyError, TypeError, ValueError):
            continue
        if not appid.isdigit() or when < cutoff or when > date: continue
        verdict = resp.get("verdict")
        if verdict not in ("yes", "no"): continue
        c = per[appid]
        c[0] += 1
        if verdict == "yes": c[1] += 1
        if resp.get("verdictOob") == "yes" or (verdict == "yes" and resp.get("triedOob") != "yes" and not resp.get("launchOptions")): c[2] += 1
    games = {a: [n, round(100 * w / n), round(100 * o / n)] for a, (n, w, o) in sorted(per.items(), key=lambda kv: int(kv[0]))}
    doc = {
        "source": "ProtonDB report dump (https://github.com/bdefore/protondb-data), Open Database License 1.0",
        "dump": Path(dump).name,
        "dumpDate": date.isoformat(),
        "window": "reports from the 730 days before dumpDate",
        "fields": ["reports", "percentWorks", "percentWithoutTweaks"],
        "games": games,
    }
    with gzip.open(OUT / "protondb-summary.json.gz", "wt", encoding="utf-8", compresslevel=9) as f:
        json.dump(doc, f, separators=(",", ":"))
    print(f"{len(games)} Steam games from {len(reports)} reports -> data/apps/protondb-summary.json.gz")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("anticheat")
    p = sub.add_parser("protondb")
    p.add_argument("dump")
    p.add_argument("--date", help="the dump's date (default: from its file name, e.g. reports_oct1_2026)")
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    if args.cmd == "anticheat":
        anticheat()
    else:
        date = datetime.date.fromisoformat(args.date) if args.date else datetime.datetime.strptime(
            Path(args.dump).name.split("_", 1)[1].split(".")[0].replace("_", " "), "%b%d %Y").date()
        protondb(args.dump, date)


if __name__ == "__main__":
    main()
