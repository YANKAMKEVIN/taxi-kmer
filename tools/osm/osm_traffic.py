#!/usr/bin/env python3
"""Trajectoires du trafic sur l'itinéraire, une par sens, en respectant les sens uniques (jalon 2).

L'itinéraire du joueur (route.xz) est calculé sans tenir compte des sens uniques : sur les tronçons en double voie
(deux chaussées à sens unique à ~11 m l'une de l'autre), il suit tantôt l'une, tantôt l'autre. Le trafic, lui,
doit rouler sur la bonne chaussée. Cet outil relit le monde déjà découpé (world_*.json), construit le graphe
orienté de ses routes (une route à sens unique ne se parcourt que dans l'ordre de ses points) et calcule :
  - forward  : du début de l'itinéraire (dispensaire) à sa fin (Étoudi) ;
  - backward : de la fin au début.
Le chemin reste près de l'itinéraire (fort surcoût au-delà de --near mètres).
Il relève aussi, pour chaque sens, les carrefours avec une rue latérale **à droite** (on entre et on sort en tournant
à droite, sans couper le trafic d'en face) : position le long de la trajectoire, début de la rue (≤ 40 m), et si l'on
peut en sortir (entrer sur l'axe) ou y entrer (quitter l'axe) selon son sens unique. Chaque segment porte twoWay = 1 si la
route est à double sens (le trafic d'en face roule sur la même chaussée), 0 si elle est à sens unique. Quand la
chaussée d'en face manque dans les données, le trafic remonte la route à sens unique (surcoût) en la partageant.

Avec --lights (fichier fait main : feux repérés par leur distance le long de l'itinéraire du joueur), il place aussi
les feux tricolores : position dans le monde (traffic.lights) et, pour chaque sens, distance le long de sa trajectoire.

Exemple :
  python3 tools/osm/osm_traffic.py Assets/Resources/Data/world_messassi_etoudi.json \
      --lights tools/osm/traffic_lights_messassi_etoudi.json
"""
from __future__ import annotations

import argparse
import heapq
import json
import math
import sys

from osm_route import ROAD_COST, point_segment_distance

NODE_PRECISION = 1  # décimales : deux routes qui partagent un nœud OSM ont les mêmes coordonnées arrondies
FAR_PENALTY = 20.0  # coût multiplié loin de l'itinéraire
# Une route à sens unique peut se remonter avec ce surcoût, comme si elle était à double sens : sert quand la chaussée
# d'en face manque dans les données du couloir (sinon le trafic ferait un détour par les rues voisines).
WRONG_WAY_PENALTY = 6.0


def _key(x: float, z: float):
    return (round(x, NODE_PRECISION), round(z, NODE_PRECISION))


def _points(xz):
    return [(xz[i], xz[i + 1]) for i in range(0, len(xz) - 1, 2)]


def _dist(a, b) -> float:
    return math.hypot(a[0] - b[0], a[1] - b[1])


class _RouteIndex:
    """Distance approximative d'un point à l'itinéraire (grille de 50 m pour aller vite)."""

    def __init__(self, line, cell=50.0):
        self.cell = cell
        self.grid: dict[tuple, list] = {}
        for a, b in zip(line, line[1:]):
            steps = max(1, int(_dist(a, b) // cell) + 1)
            for k in range(steps + 1):
                p = (a[0] + (b[0] - a[0]) * k / steps, a[1] + (b[1] - a[1]) * k / steps)
                self.grid.setdefault(self._cell(p), []).append((a, b))

    def _cell(self, p):
        return (int(math.floor(p[0] / self.cell)), int(math.floor(p[1] / self.cell)))

    def distance(self, p) -> float:
        cx, cz = self._cell(p)
        best = math.inf
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for a, b in self.grid.get((cx + dx, cz + dz), ()):
                    best = min(best, point_segment_distance(p, a, b))
        return best


def build_directed_graph(roads, route_line, near: float):
    """Graphe orienté : clé de nœud → [(voisin, coût, double_sens)] ; et position de chaque nœud."""
    index = _RouteIndex(route_line)
    graph: dict[tuple, list] = {}
    pos: dict[tuple, tuple] = {}
    for r in roads:
        cost_per_m = ROAD_COST.get(r["highway"], 3.0)
        pts = _points(r["xz"])
        two_way = not r["oneway"]
        for a, b in zip(pts, pts[1:]):
            ka, kb = _key(*a), _key(*b)
            if ka == kb:
                continue
            pos[ka], pos[kb] = a, b
            mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            far = index.distance(mid) > near
            c = _dist(a, b) * cost_per_m * (FAR_PENALTY if far else 1.0)
            graph.setdefault(ka, []).append((kb, c, two_way))
            # Sens interdit : permis avec surcoût, et alors partagé avec le trafic d'en face (double sens).
            graph.setdefault(kb, []).append((ka, c if two_way else c * WRONG_WAY_PENALTY, True))
    return graph, pos


def shortest(graph, pos, start_point, goal_point, radius: float):
    """Plus court chemin entre les nœuds proches (≤ radius) du départ et de l'arrivée. → (clés, double_sens par segment)."""
    starts = [k for k in graph if _dist(pos[k], start_point) <= radius]
    goals = {k for k in graph if _dist(pos[k], goal_point) <= radius}
    if not starts or not goals:
        raise ValueError("Aucun nœud près du départ ou de l'arrivée.")
    best = {}
    prev = {}
    heap = []
    for s in starts:
        c = _dist(pos[s], start_point) * 5.0  # départ au plus près du point voulu
        best[s] = c
        heapq.heappush(heap, (c, s))
    reached = None
    while heap:
        cost, node = heapq.heappop(heap)
        if cost > best.get(node, math.inf):
            continue
        if node in goals:
            reached = node
            break
        for nxt, c, two_way in graph[node]:
            nc = cost + c
            if nc < best.get(nxt, math.inf):
                best[nxt] = nc
                prev[nxt] = (node, two_way)
                heapq.heappush(heap, (nc, nxt))
    if reached is None:
        raise ValueError("Aucun chemin orienté entre le départ et l'arrivée.")
    keys = [reached]
    flags = []
    while keys[-1] in prev:
        node, two_way = prev[keys[-1]]
        keys.append(node)
        flags.append(two_way)
    return keys[::-1], flags[::-1]


JUNCTION_TYPES = {"trunk_link", "primary", "primary_link", "secondary", "secondary_link", "tertiary", "tertiary_link",
                  "unclassified", "residential", "living_street", "service"}
BRANCH_LENGTH = 40.0     # longueur de rue latérale gardée (m)
MIN_JUNCTION_SPACING = 25.0
END_MARGIN = 40.0         # pas de carrefour près des bouts de la trajectoire


def _project(line, cum, p):
    """(distance le long, écart signé à droite) du point p sur la polyligne."""
    best = (math.inf, 0.0, 0.0)
    for i, (a, b) in enumerate(zip(line, line[1:])):
        ex, ez = b[0] - a[0], b[1] - a[1]
        L2 = ex * ex + ez * ez
        t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * ex + (p[1] - a[1]) * ez) / L2))
        px, pz = p[0] - (a[0] + ex * t), p[1] - (a[1] + ez * t)
        d = math.hypot(px, pz)
        if d < best[0]:
            L = math.sqrt(L2) or 1.0
            lateral = -(ex * pz - ez * px) / L  # à droite du sens de marche = positif
            best = (d, cum[i] + (cum[i + 1] - cum[i]) * t, lateral)
    return best


def _truncate(points, length):
    out = [points[0]]
    total = 0.0
    for a, b in zip(points, points[1:]):
        d = _dist(a, b)
        if total + d >= length:
            t = (length - total) / d if d > 0 else 0.0
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
            return out
        out.append(b)
        total += d
    return out


def junctions(roads, path_points) -> list[dict]:
    """Carrefours avec une rue latérale à droite de la trajectoire."""
    cum = [0.0]
    for a, b in zip(path_points, path_points[1:]):
        cum.append(cum[-1] + _dist(a, b))
    on_path = {_key(*p) for p in path_points}
    found = []
    for r in roads:
        if r["highway"] not in JUNCTION_TYPES:
            continue
        pts = _points(r["xz"])
        for i, p in enumerate(pts):
            if _key(*p) not in on_path:
                continue
            # Deux branches possibles depuis le nœud : vers la fin de la route (sortie dans le sens des points)
            # et vers son début.
            for branch, forward in ((pts[i:], True), (pts[i::-1], False)):
                if len(branch) < 2:
                    continue
                d1, along, lateral = _project(path_points, cum, branch[1])
                if d1 < 4.0 or lateral <= 0:  # la branche suit l'axe, ou part à gauche
                    continue
                if along < END_MARGIN or along > cum[-1] - END_MARGIN:
                    continue
                _, j_along, _ = _project(path_points, cum, p)
                oneway = r["oneway"]
                found.append({
                    "along": round(j_along, 1),
                    # Rue décrite depuis le carrefour vers l'extérieur.
                    "xz": [round(v, 2) for q in _truncate(branch, BRANCH_LENGTH) for v in q],
                    # Sortir de l'axe = rouler dans la rue en s'éloignant ; entrer = rouler vers le carrefour.
                    "canExit": 1 if (not oneway or forward) else 0,
                    "canEnter": 1 if (not oneway or not forward) else 0,
                    "highway": r["highway"],
                })
    found.sort(key=lambda j: j["along"])
    kept = []
    for j in found:
        if kept and j["along"] - kept[-1]["along"] < MIN_JUNCTION_SPACING:
            continue
        kept.append(j)
    return kept


def traffic_paths(world: dict, near: float = 40.0, radius: float = 30.0) -> dict:
    route = _points(world["route"]["xz"])
    graph, pos = build_directed_graph(world["roads"], route, near)
    out = {}
    for name, a, b in (("forward", route[0], route[-1]), ("backward", route[-1], route[0])):
        try:
            keys, flags = shortest(graph, pos, a, b, radius)
            pts = [pos[k] for k in keys]
        except ValueError:
            # Repli : l'itinéraire lui-même, considéré à double sens.
            pts = route if name == "forward" else route[::-1]
            flags = [True] * (len(pts) - 1)
        out[name] = {
            "xz": [round(v, 2) for p in pts for v in p],
            "twoWay": [1 if f else 0 for f in flags],
            "lengthMeters": int(round(sum(_dist(p, q) for p, q in zip(pts, pts[1:])))),
            "junctions": junctions(world["roads"], pts),
        }
    return out


def place_lights(world: dict, paths: dict, lights: list[dict]) -> list[dict]:
    """Feux : point de l'itinéraire → position dans le monde, et distance le long de chaque trajectoire (paths[...]["lights"])."""
    route = _points(world["route"]["xz"])
    cum = [0.0]
    for a, b in zip(route, route[1:]):
        cum.append(cum[-1] + _dist(a, b))
    placed = []
    for name in paths:
        paths[name]["lights"] = []
    for lt in lights:
        d = max(0.0, min(cum[-1], float(lt["routeDistanceMeters"])))
        i = next((k for k in range(1, len(cum)) if cum[k] >= d), len(cum) - 1)
        seg = cum[i] - cum[i - 1]
        t = (d - cum[i - 1]) / seg if seg > 0 else 0.0
        a, b = route[i - 1], route[i]
        point = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
        placed.append({"id": int(lt["id"]), "name": lt.get("name", ""), "x": round(point[0], 2), "z": round(point[1], 2)})
        for name, path in paths.items():
            pts = _points(path["xz"])
            pcum = [0.0]
            for p, q in zip(pts, pts[1:]):
                pcum.append(pcum[-1] + _dist(p, q))
            dist, along, _ = _project(pts, pcum, point)
            if dist <= 25.0:
                path["lights"].append({"id": int(lt["id"]), "along": round(along, 1)})
        for path in paths.values():
            path["lights"].sort(key=lambda x: x["along"])
    return placed


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("world", help="monde découpé (sortie de osm_route.py), complété sur place")
    p.add_argument("--near", type=float, default=40.0, help="au-delà de cet écart à l'itinéraire (m), fort surcoût")
    p.add_argument("--lights", help="feux tricolores faits main (id, name, routeDistanceMeters)")
    args = p.parse_args(argv)
    with open(args.world, encoding="utf-8") as f:
        world = json.load(f)
    world["traffic"] = traffic_paths(world, args.near)
    lights = []
    if args.lights:
        with open(args.lights, encoding="utf-8") as f:
            lights = json.load(f)["lights"]
    world["traffic"]["lights"] = place_lights(world, {k: world["traffic"][k] for k in ("forward", "backward")}, lights)
    with open(args.world, "w", encoding="utf-8") as f:
        json.dump(world, f, ensure_ascii=False, separators=(",", ":"))
    for name, path in ((k, world["traffic"][k]) for k in ("forward", "backward")):
        one = path["twoWay"].count(0)
        print(f"{name:8} {path['lengthMeters']:>6} m, {len(path['twoWay'])} segments dont {one} à sens unique, "
              f"{len(path['junctions'])} carrefours à droite")
    for lt in world["traffic"]["lights"]:
        alongs = {k: [x["along"] for x in world["traffic"][k]["lights"] if x["id"] == lt["id"]] for k in ("forward", "backward")}
        print(f"feu {lt['id']} {lt['name']} : {alongs}")
    print(f"Itinéraire du joueur : {world['route']['lengthMeters']} m")
    return 0


if __name__ == "__main__":
    sys.exit(main())
