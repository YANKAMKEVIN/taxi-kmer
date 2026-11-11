#!/usr/bin/env python3
"""Itinéraire du tronçon et découpe du monde autour de lui.

À partir des routes converties (osm_roads.py convert) et d'une liste ordonnée de lieux :
  1. calcule l'itinéraire le plus court sur le réseau, en préférant les grands axes ;
  2. mesure la distance de chaque lieu le long de l'itinéraire (→ fichier de lieux du jeu) ;
  3. ne garde que les routes et bâtiments dans un couloir autour de l'itinéraire.

Exemple :
  python3 tools/osm/osm_route.py build/osm/roads_full.json tools/osm/landmarks_messassi_etoudi.json \\
      --stops Assets/Resources/Data/stops_messassi_etoudi.json \\
      --world Assets/Resources/Data/world_messassi_etoudi.json --corridor 250
"""
from __future__ import annotations

import argparse
import heapq
import json
import math
import sys

import osm_roads

# Coût relatif par mètre : le taxi suit les grands axes plutôt que les ruelles.
ROAD_COST = {
    "trunk": 1.0, "primary": 1.0, "trunk_link": 1.1, "primary_link": 1.1,
    "secondary": 1.2, "secondary_link": 1.2, "tertiary": 1.4, "tertiary_link": 1.4,
    "unclassified": 2.0, "residential": 3.0, "living_street": 4.0, "service": 6.0, "track": 8.0,
}
# Un lieu se raccroche au grand axe le plus proche : les taxis attendent les clients sur les grandes routes.
SNAP_TYPES = {"trunk", "primary", "secondary", "tertiary"}


def _dist(a, b) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])


def build_graph(roads):
    """Graphe non orienté : nœud OSM → [(voisin, longueur, coût)] ; et position de chaque nœud."""
    graph: dict[int, list] = {}
    pos: dict[int, tuple] = {}
    snap_nodes: set[int] = set()
    for r in roads:
        cost = ROAD_COST.get(r["highway"], 3.0)
        for (n1, p1), (n2, p2) in zip(zip(r["nodes"], r["points"]), zip(r["nodes"][1:], r["points"][1:])):
            d = _dist(p1, p2)
            graph.setdefault(n1, []).append((n2, d, d * cost))
            graph.setdefault(n2, []).append((n1, d, d * cost))
            pos[n1], pos[n2] = tuple(p1), tuple(p2)
            if r["highway"] in SNAP_TYPES:
                snap_nodes.update((n1, n2))
    return graph, pos, snap_nodes


def nearest_node(point, pos, candidates) -> int:
    return min(candidates, key=lambda n: _dist(pos[n], point))


def shortest_path(graph, start: int, goal: int) -> list[int]:
    best = {start: 0.0}
    prev: dict[int, int] = {}
    heap = [(0.0, start)]
    while heap:
        cost, node = heapq.heappop(heap)
        if node == goal:
            break
        if cost > best.get(node, math.inf):
            continue
        for nxt, _, c in graph.get(node, ()):
            nc = cost + c
            if nc < best.get(nxt, math.inf):
                best[nxt] = nc
                prev[nxt] = node
                heapq.heappush(heap, (nc, nxt))
    if goal not in best:
        raise ValueError(f"Aucun chemin entre les nœuds {start} et {goal}.")
    path = [goal]
    while path[-1] != start:
        path.append(prev[path[-1]])
    return path[::-1]


def point_segment_distance(p, a, b) -> float:
    ax, az = a
    dx, dz = b[0] - ax, b[1] - az
    L2 = dx * dx + dz * dz
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - az) * dz) / L2))
    return math.hypot(p[0] - (ax + t * dx), p[1] - (az + t * dz))


def distance_to_polyline(p, line) -> float:
    return min(point_segment_distance(p, a, b) for a, b in zip(line, line[1:]))


def plan(world: dict, landmarks: list[dict], corridor: float):
    origin = world["origin"]
    graph, pos, snap_nodes = build_graph(world["roads"])
    waypoints = []
    for lm in landmarks:
        p = osm_roads.project(lm["lat"], lm["lon"], origin["lat"], origin["lon"])
        waypoints.append(nearest_node(p, pos, snap_nodes))

    nodes: list[int] = [waypoints[0]]
    for a, b in zip(waypoints, waypoints[1:]):
        nodes += shortest_path(graph, a, b)[1:]
    line = [pos[n] for n in nodes]

    cumulative = [0.0]
    for a, b in zip(line, line[1:]):
        cumulative.append(cumulative[-1] + _dist(a, b))

    stops = []
    search_from = 0
    for lm, wp in zip(landmarks, waypoints):
        i = nodes.index(wp, search_from)
        search_from = i
        stops.append({"id": lm["id"], "name": lm["name"], "distanceAlongRouteMeters": int(round(cumulative[i]))})

    # Couloir : un point tous les ~25 m de l'itinéraire, pour tester vite la proximité.
    samples = [line[0]]
    for a, b in zip(line, line[1:]):
        steps = max(1, int(_dist(a, b) // 25))
        samples += [(a[0] + (b[0] - a[0]) * k / steps, a[1] + (b[1] - a[1]) * k / steps) for k in range(1, steps + 1)]

    def near(p) -> bool:
        return any(abs(p[0] - s[0]) <= corridor and abs(p[1] - s[1]) <= corridor and _dist(p, s) <= corridor
                   for s in samples)

    roads = [r for r in world["roads"] if any(near(p) for p in r["points"])]
    buildings = [b for b in world["buildings"]
                 if near((sum(p[0] for p in b["footprint"]) / len(b["footprint"]),
                          sum(p[1] for p in b["footprint"]) / len(b["footprint"])))]
    kept = {n for r in roads for n in r["nodes"]}
    intersections = [i for i in world["intersections"] if i["node"] in kept]

    route = {"nodes": nodes, "points": [list(p) for p in line], "lengthMeters": int(round(cumulative[-1]))}
    out_world = dict(world, roads=roads, buildings=buildings, intersections=intersections,
                     route=route, corridorMeters=corridor)
    return stops, out_world


def _flat(points) -> list[float]:
    return [round(v, 2) for p in points for v in p]


def to_unity(world: dict) -> dict:
    """Format lisible par JsonUtility (pas de tableaux imbriqués) : coordonnées aplaties en xz = [x0, z0, x1, z1, ...]."""
    return {
        "attribution": world["attribution"],
        "originLat": world["origin"]["lat"],
        "originLon": world["origin"]["lon"],
        "corridorMeters": world["corridorMeters"],
        "roads": [{"id": r["id"], "name": r["name"], "highway": r["highway"], "lanes": r["lanes"],
                   "width": r["width"], "oneway": r["oneway"], "xz": _flat(r["points"])} for r in world["roads"]],
        "buildings": [{"id": b["id"], "levels": b["levels"], "xz": _flat(b["footprint"])} for b in world["buildings"]],
        "intersections": [{"node": i["node"], "x": i["point"][0], "z": i["point"][1]} for i in world["intersections"]],
        "route": {"lengthMeters": world["route"]["lengthMeters"], "xz": _flat(world["route"]["points"])},
    }


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("roads", help="sortie de osm_roads.py convert")
    p.add_argument("landmarks", help="lieux ordonnés (id, name, lat, lon)")
    p.add_argument("--stops", required=True, help="fichier de lieux du jeu à écrire")
    p.add_argument("--world", required=True, help="monde découpé à écrire (routes, bâtiments, itinéraire)")
    p.add_argument("--corridor", type=float, default=250.0, help="demi-largeur du couloir gardé (m)")
    p.add_argument("--weights", default="30,50,20", help="répartition mince,normal,costaud")
    args = p.parse_args(argv)

    with open(args.roads, encoding="utf-8") as f:
        world = json.load(f)
    with open(args.landmarks, encoding="utf-8") as f:
        landmarks = json.load(f)["landmarks"]

    stops, out_world = plan(world, landmarks, args.corridor)
    mince, normal, costaud = (int(v) for v in args.weights.split(","))
    with open(args.stops, "w", encoding="utf-8") as f:
        json.dump({"_note": "Généré par tools/osm/osm_route.py : distances mesurées le long de l'itinéraire OSM.",
                   "stops": stops, "minceWeight": mince, "normalWeight": normal, "costaudWeight": costaud},
                  f, ensure_ascii=False, indent=4)
    with open(args.world, "w", encoding="utf-8") as f:
        json.dump(to_unity(out_world), f, ensure_ascii=False, separators=(",", ":"))
    for s in stops:
        print(f"{s['distanceAlongRouteMeters']:>6} m  {s['name']}")
    print(f"Itinéraire {out_world['route']['lengthMeters']} m ; gardés : {len(out_world['roads'])} routes, "
          f"{len(out_world['buildings'])} bâtiments (couloir {args.corridor:g} m).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
