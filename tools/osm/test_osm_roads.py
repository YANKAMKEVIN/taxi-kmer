import json
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import osm_roads  # noqa: E402

FIXTURE = os.path.join(os.path.dirname(__file__), "fixtures", "mini.osm.json")


class ConvertTests(unittest.TestCase):
    def setUp(self):
        with open(FIXTURE, encoding="utf-8") as f:
            self.result = osm_roads.convert(json.load(f))

    def test_keeps_drivable_roads_only(self):
        ids = sorted(r["id"] for r in self.result["roads"])
        self.assertEqual(ids, [100, 101])

    def test_projection_in_meters(self):
        road = next(r for r in self.result["roads"] if r["id"] == 100)
        (x1, z1), (_, z2) = road["points"][0], road["points"][1]
        self.assertAlmostEqual(z2 - z1, 111.19, delta=0.1)  # 0,001° de latitude ≈ 111 m
        self.assertAlmostEqual(x1, road["points"][2][0], delta=0.01)

    def test_lanes_width_oneway(self):
        main = next(r for r in self.result["roads"] if r["id"] == 100)
        side = next(r for r in self.result["roads"] if r["id"] == 101)
        self.assertEqual(main["lanes"], 2)
        self.assertAlmostEqual(main["width"], 6.5)
        self.assertTrue(side["oneway"])
        self.assertEqual(side["name"], "")

    def test_width_tag_sanity(self):
        self.assertEqual(osm_roads._width({"width": "7 m"}, 2), 7.0)
        self.assertEqual(osm_roads._width({"width": "1471.06"}, 2), 6.5)
        self.assertEqual(osm_roads._width({"width": "abc"}, 1), 3.25)

    def test_intersections(self):
        self.assertEqual([i["node"] for i in self.result["intersections"]], [2])

    def test_buildings(self):
        self.assertEqual(len(self.result["buildings"]), 1)
        b = self.result["buildings"][0]
        self.assertEqual(b["levels"], 2)
        self.assertEqual(len(b["footprint"]), 4)

    def test_attribution(self):
        self.assertIn("OpenStreetMap", self.result["attribution"])

    def test_query_contains_bbox(self):
        q = osm_roads.overpass_query((3.88, 11.49, 3.93, 11.53))
        self.assertIn("3.88,11.49,3.93,11.53", q)


if __name__ == "__main__":
    unittest.main()
