import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(__file__))
import osm_traffic  # noqa: E402


def road(rid, oneway, xz, highway="trunk"):
    return {"id": rid, "name": "", "highway": highway, "lanes": 2, "width": 6.5, "oneway": oneway, "xz": xz}


class TrafficPathsTests(unittest.TestCase):
    def test_dual_carriageway_each_direction_on_its_own_side(self):
        # Double voie nord-sud : chaussée vers le nord à x = 0, vers le sud à x = -12 ; raccords à double sens aux bouts.
        roads = [
            road(1, True, [0, 0, 0, 500, 0, 1000]),
            road(2, True, [-12, 1000, -12, 500, -12, 0]),
            road(3, False, [0, 0, -12, 0], "secondary"),
            road(4, False, [0, 1000, -12, 1000], "secondary"),
        ]
        # L'itinéraire du joueur suit la chaussée sud sur la première moitié (il ignore les sens uniques).
        world = {"roads": roads, "route": {"xz": [-12, 0, -12, 500, 0, 500, 0, 1000], "lengthMeters": 1012}}
        paths = osm_traffic.traffic_paths(world)
        fwd = paths["forward"]["xz"]
        back = paths["backward"]["xz"]
        self.assertTrue(all(abs(x) < 1e-6 for x in fwd[0::2][1:-1]), f"vers le nord sur x = 0 : {fwd}")
        self.assertTrue(all(abs(x + 12) < 1e-6 for x in back[0::2][1:-1]), f"vers le sud sur x = -12 : {back}")
        self.assertIn(0, paths["forward"]["twoWay"])
        self.assertEqual(len(paths["forward"]["twoWay"]), len(fwd) // 2 - 1)

    def test_two_way_road_same_path_both_directions(self):
        world = {"roads": [road(1, False, [0, 0, 0, 300, 50, 600], "residential")],
                 "route": {"xz": [0, 0, 0, 300, 50, 600], "lengthMeters": 604}}
        paths = osm_traffic.traffic_paths(world)
        self.assertEqual(paths["forward"]["xz"], [0, 0, 0, 300, 50, 600])
        self.assertEqual(paths["backward"]["xz"], [50, 600, 0, 300, 0, 0])
        self.assertEqual(paths["backward"]["twoWay"], [1, 1])

    def test_missing_return_carriageway_shares_the_oneway(self):
        # Sens unique vers le nord, pas de chaussée retour proche : le retour la partage plutôt que de faire un détour.
        roads = [road(1, True, [0, 0, 0, 300]),
                 road(2, False, [0, 300, 400, 300, 400, 0, 0, 0], "residential")]
        world = {"roads": roads, "route": {"xz": [0, 0, 0, 300], "lengthMeters": 300}}
        paths = osm_traffic.traffic_paths(world)
        self.assertEqual(paths["backward"]["xz"], [0, 300, 0, 0])
        self.assertEqual(paths["backward"]["twoWay"], [1])
        self.assertEqual(paths["forward"]["twoWay"], [0])

    def test_unreachable_falls_back_to_route(self):
        world = {"roads": [road(1, True, [0, 0, 0, 300]), road(2, True, [900, 0, 900, 300])],
                 "route": {"xz": [0, 0, 0, 300, 900, 300], "lengthMeters": 1200}}
        paths = osm_traffic.traffic_paths(world)
        self.assertEqual(paths["backward"]["xz"], [900, 300, 0, 300, 0, 0])
        self.assertEqual(paths["backward"]["twoWay"], [1, 1])


    def test_junctions_only_on_the_right_with_oneway_rules(self):
        # Axe vers le nord (x = 0) ; rues à l'est (droite) et à l'ouest (gauche) au même carrefour (0, 500),
        # et une rue à sens unique à droite en (0, 300) qui ne fait que s'éloigner de l'axe.
        roads = [
            road(1, False, [0, 0, 0, 300, 0, 500, 0, 1000], "secondary"),
            road(2, False, [0, 500, 60, 500, 120, 520], "residential"),
            road(3, False, [-80, 500, 0, 500], "residential"),
            road(4, True, [0, 300, 50, 300], "residential"),
        ]
        world = {"roads": roads, "route": {"xz": [0, 0, 0, 1000], "lengthMeters": 1000}}
        fwd = osm_traffic.traffic_paths(world)["forward"]["junctions"]
        self.assertEqual([j["along"] for j in fwd], [300.0, 500.0])
        one, two = fwd
        self.assertEqual((one["canExit"], one["canEnter"]), (1, 0), "sens unique qui s'éloigne : sortie seulement")
        self.assertEqual((two["canExit"], two["canEnter"]), (1, 1))
        self.assertEqual(two["xz"][:4], [0, 500, 40.0, 500], "décrite depuis le carrefour, tronquée à 40 m")
        back = osm_traffic.traffic_paths(world)["backward"]["junctions"]
        self.assertEqual([j["along"] for j in back], [500.0], "vers le sud, seule la rue de l'ouest est à droite")


if __name__ == "__main__":
    unittest.main()
