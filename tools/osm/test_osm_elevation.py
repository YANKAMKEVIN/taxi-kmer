import math
import os
import struct
import sys
import tempfile
import unittest
import zlib

sys.path.insert(0, os.path.dirname(__file__))
import osm_elevation  # noqa: E402
import osm_roads  # noqa: E402


def write_tiff(path, values, width, height, tile=16, lon0=11.0, lat0=4.0, step=0.001, predictor=3):
    """Petit GeoTIFF float32 en tuiles, DEFLATE, prédicteur flottant (comme Copernicus GLO-30)."""
    tiles = []
    for ty in range(0, height, tile):
        for tx in range(0, width, tile):
            raw = b""
            for r in range(tile):
                row = [values[min(ty + r, height - 1)][min(tx + c, width - 1)] for c in range(tile)]
                line = struct.pack("<" + "f" * tile, *row)
                raw += osm_elevation.float_predictor_encode(line, tile, 4) if predictor == 3 else line
            tiles.append(zlib.compress(raw))
    entries = []
    extra = b""
    base = 8

    def add(tag, typ, values_, fmt):
        entries.append((tag, typ, values_, fmt))

    n = len(tiles)
    add(256, 3, [width], "H"); add(257, 3, [height], "H"); add(258, 3, [32], "H"); add(259, 3, [8], "H")
    add(317, 3, [predictor], "H"); add(322, 3, [tile], "H"); add(323, 3, [tile], "H"); add(339, 3, [3], "H")
    add(324, 4, [0] * n, "I"); add(325, 4, [len(t) for t in tiles], "I")
    add(33550, 12, [step, step, 0.0], "d"); add(33922, 12, [0.0, 0.0, 0.0, lon0, lat0, 0.0], "d")
    ifd_size = 2 + 12 * len(entries) + 4
    data_start = base + ifd_size
    blobs = []
    offset = data_start
    payload = {}
    for tag, typ, vals, fmt in entries:
        blob = struct.pack("<" + fmt * len(vals), *vals)
        if len(blob) > 4:
            payload[tag] = offset
            blobs.append(blob)
            offset += len(blob)
    tile_offsets = []
    for t in tiles:
        tile_offsets.append(offset)
        offset += len(t)
    out = bytearray(b"II*\x00" + struct.pack("<I", base) + struct.pack("<H", len(entries)))
    for tag, typ, vals, fmt in entries:
        if tag == 324:
            vals = tile_offsets
        blob = struct.pack("<" + fmt * len(vals), *vals)
        if len(blob) > 4:
            out += struct.pack("<HHII", tag, typ, len(vals), payload[tag])
        else:
            out += struct.pack("<HHI", tag, typ, len(vals)) + blob.ljust(4, b"\0")
    out += struct.pack("<I", 0)
    for tag, typ, vals, fmt in entries:
        if tag == 324:
            vals = tile_offsets
        blob = struct.pack("<" + fmt * len(vals), *vals)
        if len(blob) > 4:
            out += blob
    for t in tiles:
        out += t
    with open(path, "wb") as f:
        f.write(out)


class ElevationTests(unittest.TestCase):
    def test_float_predictor_round_trip(self):
        line = struct.pack("<4f", 700.5, 701.25, 699.0, 1100.75)
        enc = osm_elevation.float_predictor_encode(line, 4, 4)
        self.assertNotEqual(enc, line)
        self.assertEqual(line, osm_elevation.float_predictor_decode(enc, 4, 4))

    def test_reads_tiled_geotiff_and_interpolates(self):
        w, h = 40, 30  # plusieurs tuiles de 16
        values = [[700.0 + c + 100.0 * r for c in range(w)] for r in range(h)]
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "dem.tif")
            write_tiff(path, values, w, h)
            dem = osm_elevation.Dem(path)
            self.assertEqual((40, 30), (dem.width, dem.height))
            self.assertEqual(values[20][33], dem.pixel(33, 20))
            # Centre du pixel (col 33, ligne 20) : lon = 11 + 33,5 × 0,001, lat = 4 − 20,5 × 0,001.
            self.assertAlmostEqual(values[20][33], dem.height_at(4.0 - 0.0205, 11.0 + 0.0335), places=3)
            self.assertAlmostEqual(values[20][33] + 0.5, dem.height_at(4.0 - 0.0205, 11.0 + 0.034), places=3)

    def test_unproject_inverts_osm_roads_project(self):
        x, z = osm_roads.project(3.93, 11.52, 3.92, 11.51)
        lat, lon = osm_elevation.unproject(x, z, 3.92, 11.51)
        self.assertAlmostEqual(3.93, lat, places=6)
        self.assertAlmostEqual(11.52, lon, places=6)

    def test_terrain_grid_is_relative_to_the_origin(self):
        world = {"originLat": 3.92, "originLon": 11.51,
                 "roads": [{"xz": [0.0, 0.0, 100.0, 50.0]}], "buildings": []}
        t = osm_elevation.terrain(world, lambda lat, lon: 700.0 + (lat - 3.92) * 1000.0, cell=25.0, margin=0.0)
        self.assertEqual((5, 3), (t["width"], t["height"]))
        self.assertEqual(700.0, t["baseAltitude"])
        self.assertEqual(0.0, t["heights"][0])
        self.assertEqual(len(t["heights"]), t["width"] * t["height"])
        self.assertAlmostEqual(math.degrees(50 / 6_371_000.0) * 1000.0, t["heights"][-1], places=1)


if __name__ == "__main__":
    unittest.main()
