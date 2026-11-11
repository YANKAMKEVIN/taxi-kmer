#!/usr/bin/env python3
"""Import des routes OpenStreetMap pour Taxi Kmer.

Deux étapes :
  fetch   : interroge l'API Overpass pour une zone (bbox) et enregistre le JSON brut.
  convert : transforme le JSON brut en routes en mètres locaux (x = est, z = nord),
            prêtes à être lues par Unity.

Exemples :
  python3 tools/osm/osm_roads.py fetch --bbox 3.905,11.505,3.952,11.535 -o build/osm/messassi_etoudi.osm.json
  python3 tools/osm/osm_roads.py convert build/osm/messassi_etoudi.osm.json -o build/osm/roads_full.json
  puis tools/osm/osm_route.py (itinéraire, lieux, couloir).

Sur macOS, le Python de Xcode échoue en HTTPS avec Overpass (LibreSSL) : télécharger avec curl
(voir CLAUDE.md) ou un Python récent.

Données © contributeurs OpenStreetMap, licence ODbL : le crédit doit apparaître dans le jeu.
Bbox du jalon 1 (Messassi → Étoudi) : 3.905,11.505,3.952,11.535 (sud, ouest, nord, est).
"""
from __future__ import annotations

import argparse
import json
import math
import sys
import urllib.parse
import urllib.request

OVERPASS_URL = "https://overpass-api.de/api/interpreter"
EARTH_RADIUS_M = 6_371_000.0

# Types de routes gardés, et largeur par défaut d'une voie (m) quand OSM ne la donne pas.
HIGHWAY_TYPES = {
    "trunk": 2, "primary": 2, "secondary": 2, "tertiary": 2,
    "unclassified": 2, "residential": 2, "service": 1,
    "trunk_link": 1, "primary_link": 1, "secondary_link": 1, "tertiary_link": 1,
    "living_street": 1, "track": 1,
}
LANE_WIDTH_M = 3.25


def overpass_query(bbox: tuple[float, float, float, float]) -> str:
    s, w, n, e = bbox
    types = "|".join(sorted(HIGHWAY_TYPES))
    return (
        "[out:json][timeout:120];"
        f'(way["highway"~"^({types})$"]({s},{w},{n},{e});'
        f'way["building"]({s},{w},{n},{e}););'
        "(._;>;);out body;"
    )


def fetch(bbox, out_path: str) -> None:
    data = urllib.parse.urlencode({"data": overpass_query(bbox)}).encode()
    req = urllib.request.Request(OVERPASS_URL, data=data, headers={"User-Agent": "taxi-kmer-osm-import"})
    with urllib.request.urlopen(req, timeout=180) as resp:
        raw = resp.read()
    with open(out_path, "wb") as f:
        f.write(raw)


def project(lat: float, lon: float, origin_lat: float, origin_lon: float) -> tuple[float, float]:
    """Projection équirectangulaire locale : précise au mètre près sur quelques kilomètres."""
    x = math.radians(lon - origin_lon) * EARTH_RADIUS_M * math.cos(math.radians(origin_lat))
    z = math.radians(lat - origin_lat) * EARTH_RADIUS_M
    return round(x, 2), round(z, 2)


def _lanes(tags: dict) -> int:
    try:
        return max(1, int(str(tags.get("lanes", "")).split(";")[0]))
    except ValueError:
        return HIGHWAY_TYPES.get(tags.get("highway", ""), 2)


MIN_WIDTH_M, MAX_WIDTH_M = 2.0, 25.0


def _width(tags: dict, lanes: int) -> float:
    """Largeur du tag OSM si elle est plausible (« 7 », « 7 m », « 7.5 »), sinon déduite des voies.
    Certaines saisies OSM sont fausses (ex. 1471 m sur une rue d'Étoudi)."""
    raw = str(tags.get("width", "")).replace(",", ".").replace("m", "").strip()
    try:
        w = float(raw)
        if MIN_WIDTH_M <= w <= MAX_WIDTH_M:
            return round(w, 2)
    except ValueError:
        pass
    return round(lanes * LANE_WIDTH_M, 2)


def convert(osm: dict) -> dict:
    nodes = {el["id"]: (el["lat"], el["lon"]) for el in osm.get("elements", []) if el.get("type") == "node"}
    ways = [el for el in osm.get("elements", []) if el.get("type") == "way"]
    if not nodes:
        raise ValueError("Aucun nœud dans les données OSM.")

    lats = [p[0] for p in nodes.values()]
    lons = [p[1] for p in nodes.values()]
    origin = ((min(lats) + max(lats)) / 2, (min(lons) + max(lons)) / 2)

    roads, buildings = [], []
    node_use: dict[int, int] = {}
    for way in ways:
        tags = way.get("tags", {})
        refs = [r for r in way.get("nodes", []) if r in nodes]
        if len(refs) < 2:
            continue
        pts = [list(project(*nodes[r], *origin)) for r in refs]
        if tags.get("highway") in HIGHWAY_TYPES:
            lanes = _lanes(tags)
            roads.append({
                "id": way["id"],
                "name": tags.get("name", ""),
                "highway": tags["highway"],
                "lanes": lanes,
                "width": _width(tags, lanes),
                "oneway": tags.get("oneway") in ("yes", "1", "true"),
                "nodes": refs,
                "points": pts,
            })
            for r in set(refs):
                node_use[r] = node_use.get(r, 0) + 1
        elif "building" in tags and refs[0] == refs[-1] and len(refs) >= 4:
            levels = tags.get("building:levels", "")
            buildings.append({
                "id": way["id"],
                "levels": int(levels) if str(levels).isdigit() else 1,
                "footprint": pts[:-1],
            })

    intersections = sorted(r for r, n in node_use.items() if n >= 2)
    return {
        "attribution": "© contributeurs OpenStreetMap (ODbL)",
        "origin": {"lat": round(origin[0], 7), "lon": round(origin[1], 7)},
        "roads": roads,
        "intersections": [{"node": r, "point": list(project(*nodes[r], *origin))} for r in intersections],
        "buildings": buildings,
    }


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    f = sub.add_parser("fetch")
    f.add_argument("--bbox", required=True, help="sud,ouest,nord,est en degrés")
    f.add_argument("-o", "--out", required=True)
    c = sub.add_parser("convert")
    c.add_argument("input")
    c.add_argument("-o", "--out", required=True)
    args = p.parse_args(argv)

    if args.cmd == "fetch":
        bbox = tuple(float(v) for v in args.bbox.split(","))
        if len(bbox) != 4 or not (bbox[0] < bbox[2] and bbox[1] < bbox[3]):
            p.error("bbox attendue : sud,ouest,nord,est")
        fetch(bbox, args.out)
    else:
        with open(args.input, encoding="utf-8") as fh:
            result = convert(json.load(fh))
        with open(args.out, "w", encoding="utf-8") as fh:
            json.dump(result, fh, ensure_ascii=False, indent=1)
        print(f"{len(result['roads'])} routes, {len(result['intersections'])} carrefours, "
              f"{len(result['buildings'])} bâtiments → {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
