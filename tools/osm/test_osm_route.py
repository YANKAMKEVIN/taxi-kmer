import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import osm_route  # noqa: E402


def road(rid, highway, nodes, points):
    return {"id": rid, "name": "", "highway": highway, "lanes": 2, "width": 6.5, "oneway": False,
            "nodes": nodes, "points": points}


def world():
    # Grand axe direct 1-2-3 (0 → 1000 m vers le nord), détour résidentiel 1-4-3.
    roads = [
        road(10, "trunk", [1, 2, 3], [[0, 0], [0, 500], [0, 1000]]),
        road(11, "residential", [1, 4, 3], [[0, 0], [-100, 480], [0, 1000]]),
        road(12, "residential", [5, 6], [[2000, 0], [2000, 50]]),  # loin : hors couloir
    ]
    buildings = [
        {"id": 20, "levels": 1, "footprint": [[10, 490], [20, 490], [20, 500], [10, 500]]},
        {"id": 21, "levels": 1, "footprint": [[2000, 900], [2010, 900], [2010, 910], [2000, 910]]},
    ]
    intersections = [{"node": 1, "point": [0, 0]}, {"node": 3, "point": [0, 1000]}]
    return {"attribution": "© contributeurs OpenStreetMap (ODbL)", "origin": {"lat": 0.0, "lon": 0.0},
            "roads": roads, "buildings": buildings, "intersections": intersections}


def landmark(lid, z):
    # 1 m de latitude ≈ 1 / 111 195 degré
    return {"id": lid, "name": lid, "lat": z / 111195.0, "lon": 0.0}


class RouteTests(unittest.TestCase):
    def setUp(self):
        self.stops, self.world = osm_route.plan(world(), [landmark("a", 0), landmark("b", 500), landmark("c", 1000)], 100)

    def test_prefers_main_road(self):
        self.assertEqual(self.world["route"]["nodes"], [1, 2, 3])

    def test_stop_distances_along_route(self):
        self.assertEqual([s["distanceAlongRouteMeters"] for s in self.stops], [0, 500, 1000])
        self.assertEqual(self.world["route"]["lengthMeters"], 1000)

    def test_corridor_keeps_only_nearby(self):
        self.assertEqual(sorted(r["id"] for r in self.world["roads"]), [10, 11])
        self.assertEqual([b["id"] for b in self.world["buildings"]], [20])

    def test_unity_format_is_flat(self):
        u = osm_route.to_unity(self.world)
        self.assertEqual(u["route"]["xz"], [0, 0, 0, 500, 0, 1000])
        self.assertEqual(len(u["buildings"][0]["xz"]), 8)
        self.assertIn("OpenStreetMap", u["attribution"])


if __name__ == "__main__":
    unittest.main()
