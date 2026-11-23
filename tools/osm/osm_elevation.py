#!/usr/bin/env python3
"""Relief réel du monde (idée n° 8) : ajoute au JSON du monde une grille d'altitudes (champ « terrain »).

Source : Copernicus DEM GLO-30 (30 m, licence libre y compris usage commercial, attribution obligatoire :
« © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union
and ESA; all rights reserved »). Tuile de Yaoundé (téléchargement par curl, le Python de Xcode échoue en HTTPS) :

  curl -sS -f -o build/dem/N03_E011.tif https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_N03_00_E011_00_DEM/Copernicus_DSM_COG_10_N03_00_E011_00_DEM.tif
  python3 tools/osm/osm_elevation.py build/dem/N03_E011.tif Assets/Resources/Data/world_messassi_etoudi.json

Lecteur GeoTIFF minimal en Python pur (TIFF classique petit-boutiste, tuiles, DEFLATE, prédicteurs 1 et 3, float32
ou int16) : aucune dépendance. Les altitudes sont relatives à l'origine du monde (0 m au point originLat/originLon).
"""
import argparse
import json
import math
import struct
import sys
import zlib

EARTH_RADIUS_M = 6_371_000.0  # même rayon que osm_roads.project
ATTRIBUTION = ("Copernicus DEM GLO-30 : © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018 provided "
               "under COPERNICUS by the European Union and ESA; all rights reserved")

_TYPES = {1: ("B", 1), 2: ("c", 1), 3: ("H", 2), 4: ("I", 4), 5: ("II", 8), 11: ("f", 4), 12: ("d", 8), 16: ("Q", 8)}


def _read_tags(data):
    if data[:4] != b"II*\x00":
        raise ValueError("TIFF classique petit-boutiste attendu")
    (ifd,) = struct.unpack_from("<I", data, 4)
    (count,) = struct.unpack_from("<H", data, ifd)
    tags = {}
    for i in range(count):
        tag, typ, n, value = struct.unpack_from("<HHI4s", data, ifd + 2 + 12 * i)
        fmt, size = _TYPES.get(typ, ("B", 1))
        total = size * n
        raw = value if total <= 4 else data[struct.unpack("<I", value)[0]:struct.unpack("<I", value)[0] + total]
        if typ == 2:
            tags[tag] = raw[:n].decode("ascii", "replace")
        elif typ == 5:
            tags[tag] = struct.unpack("<" + "I" * (2 * n), raw[:total])
        else:
            tags[tag] = struct.unpack("<" + fmt * n, raw[:total])
    return tags


def float_predictor_decode(buf, width, bytes_per_sample):
    """Annule le prédicteur flottant TIFF (3) d'une ligne : différences d'octets puis plans d'octets poids fort d'abord."""
    row = bytearray(buf)
    for i in range(1, len(row)):
        row[i] = (row[i] + row[i - 1]) & 0xFF
    out = bytearray(len(row))
    for k in range(width):
        for b in range(bytes_per_sample):
            out[k * bytes_per_sample + b] = row[(bytes_per_sample - 1 - b) * width + k]
    return bytes(out)


def float_predictor_encode(values_le, width, bytes_per_sample):
    """Inverse de float_predictor_decode (pour les tests)."""
    planes = bytearray(len(values_le))
    for k in range(width):
        for b in range(bytes_per_sample):
            planes[(bytes_per_sample - 1 - b) * width + k] = values_le[k * bytes_per_sample + b]
    out = bytearray(planes)
    for i in range(len(out) - 1, 0, -1):
        out[i] = (planes[i] - planes[i - 1]) & 0xFF
    return bytes(out)


class Dem:
    """Grille d'altitudes géoréférencée (lat / lon du coin haut-gauche, pas en degrés)."""

    def __init__(self, path):
        with open(path, "rb") as f:
            self._data = f.read()
        t = _read_tags(self._data)
        self.width, self.height = t[256][0], t[257][0]
        self.tile_w, self.tile_h = (t[322][0], t[323][0]) if 322 in t else (self.width, t.get(278, (self.height,))[0])
        self.offsets = t[324] if 324 in t else t[273]
        self.counts = t[325] if 325 in t else t[279]
        self.compression = t.get(259, (1,))[0]
        self.predictor = t.get(317, (1,))[0]
        self.bits = t.get(258, (16,))[0]
        self.float = t.get(339, (1,))[0] == 3
        scale, tie = t[33550], t[33922]
        self.dlon, self.dlat = scale[0], scale[1]
        self.lon0, self.lat0 = tie[3] - tie[0] * self.dlon, tie[4] + tie[1] * self.dlat
        self._tiles = {}

    def _tile(self, index):
        if index in self._tiles:
            return self._tiles[index]
        raw = self._data[self.offsets[index]:self.offsets[index] + self.counts[index]]
        if self.compression in (8, 32946):
            raw = zlib.decompress(raw)
        elif self.compression != 1:
            raise ValueError(f"compression TIFF {self.compression} non gérée")
        bps = self.bits // 8
        w = self.tile_w
        rows = []
        for r in range(len(raw) // (w * bps)):
            line = raw[r * w * bps:(r + 1) * w * bps]
            if self.predictor == 3:
                line = float_predictor_decode(line, w, bps)
            fmt = ("f" if bps == 4 else "d") if self.float else ("h" if bps == 2 else "i")
            vals = list(struct.unpack("<" + fmt * w, line))
            if self.predictor == 2:
                for i in range(1, w):
                    vals[i] += vals[i - 1]
            rows.append(vals)
        self._tiles[index] = rows
        return rows

    def pixel(self, col, row):
        col = min(max(col, 0), self.width - 1)
        row = min(max(row, 0), self.height - 1)
        per_row = -(-self.width // self.tile_w)
        tile = self._tile((row // self.tile_h) * per_row + col // self.tile_w)
        return tile[row % self.tile_h][col % self.tile_w]

    def height_at(self, lat, lon):
        """Altitude (m) au point, interpolée entre les 4 pixels voisins (centres de pixels)."""
        fx = (lon - self.lon0) / self.dlon - 0.5
        fy = (self.lat0 - lat) / self.dlat - 0.5
        x0, y0 = math.floor(fx), math.floor(fy)
        tx, ty = fx - x0, fy - y0
        a, b = self.pixel(x0, y0), self.pixel(x0 + 1, y0)
        c, d = self.pixel(x0, y0 + 1), self.pixel(x0 + 1, y0 + 1)
        return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def unproject(x, z, origin_lat, origin_lon):
    """Inverse de osm_roads.project."""
    lat = origin_lat + math.degrees(z / EARTH_RADIUS_M)
    lon = origin_lon + math.degrees(x / (EARTH_RADIUS_M * math.cos(math.radians(origin_lat))))
    return lat, lon


def terrain(world, height_at, cell=30.0, margin=300.0):
    """Grille d'altitudes couvrant routes et bâtiments (+ marge), relatives à l'origine du monde."""
    xs, zs = [], []
    for key in ("roads", "buildings"):
        for item in world.get(key, []):
            xz = item["xz"]
            xs.extend(xz[0::2])
            zs.extend(xz[1::2])
    min_x, max_x = min(xs) - margin, max(xs) + margin
    min_z, max_z = min(zs) - margin, max(zs) + margin
    w = int(math.ceil((max_x - min_x) / cell)) + 1
    h = int(math.ceil((max_z - min_z) / cell)) + 1
    lat0, lon0 = world["originLat"], world["originLon"]
    base = height_at(lat0, lon0)
    heights = []
    for j in range(h):
        for i in range(w):
            lat, lon = unproject(min_x + i * cell, min_z + j * cell, lat0, lon0)
            heights.append(round(height_at(lat, lon) - base, 1))
    return {"attribution": ATTRIBUTION, "originX": round(min_x, 2), "originZ": round(min_z, 2), "cellMeters": cell,
            "width": w, "height": h, "baseAltitude": round(base, 1), "heights": heights}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("dem", help="GeoTIFF Copernicus GLO-30 (ou tout GeoTIFF WGS 84 en tuiles)")
    ap.add_argument("world", help="JSON du monde Unity (Assets/Resources/Data/world_*.json), modifié sur place")
    ap.add_argument("--cell", type=float, default=30.0, help="pas de la grille (m)")
    args = ap.parse_args(argv)
    dem = Dem(args.dem)
    with open(args.world, encoding="utf-8") as f:
        world = json.load(f)
    world["terrain"] = terrain(world, dem.height_at, args.cell)
    t = world["terrain"]
    with open(args.world, "w", encoding="utf-8") as f:
        json.dump(world, f, ensure_ascii=False, separators=(",", ":"))
    print(f"Relief : {t['width']} × {t['height']} points tous les {t['cellMeters']:g} m, "
          f"de {min(t['heights']):g} à {max(t['heights']):g} m autour de l'origine ({t['baseAltitude']:g} m).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
