import json
import os
import shutil
import tempfile
import unittest

import textes_validation as tv


class TextesValidationTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        for f in ("dialogues.json", "radio.json"):
            shutil.copy(os.path.join(tv.DATA, f), self.dir)

    def tearDown(self):
        shutil.rmtree(self.dir)

    def test_collect_lists_every_line_and_radio_text(self):
        d, r = tv.load(os.path.join(tv.DATA, "dialogues.json")), tv.load(os.path.join(tv.DATA, "radio.json"))
        items = tv.collect(d, r)
        radio_texts = sum(1 for s in r["stations"] for p in s["programmes"] if p.get("text")) + len(r["flashes"])
        self.assertEqual(len(d["lines"]) + radio_texts, len(items))
        self.assertEqual(len(items), len({i["key"] for i in items}), "clés uniques")

    def test_page_is_standalone_html(self):
        out = os.path.join(self.dir, "p.html")
        self.assertEqual(0, tv.main(["page", "-o", out, "--data", self.dir]))
        page = open(out, encoding="utf-8").read()
        self.assertIn("montee-commercante", page)
        self.assertNotIn("http://", page.replace("http://www.w3.org", ""))
        self.assertNotIn("https://", page)

    def test_apply_round_trip_keeps_format(self):
        path = os.path.join(self.dir, "dialogues.json")
        before = open(path, encoding="utf-8").read()
        dec = os.path.join(self.dir, "dec.json")
        json.dump({"version": 1, "items": {}}, open(dec, "w"))
        self.assertEqual(0, tv.main(["apply", dec, "--data", self.dir]))
        self.assertEqual(before, open(path, encoding="utf-8").read(), "aucune décision = fichier identique")

    def test_apply_sets_status_text_and_remark(self):
        d = tv.load(os.path.join(self.dir, "dialogues.json"))
        r = tv.load(os.path.join(self.dir, "radio.json"))
        line = d["lines"][0]["id"]
        flash = r["flashes"][0]["id"]
        decisions = {"items": {
            "dialogues/" + line: {"statut": "validée", "text": "Bonjour !", "remarque": ""},
            "flash/" + flash: {"statut": "à revoir", "text": "", "remarque": "trop long"},
        }}
        n, unknown = tv.apply_decisions(d, r, decisions)
        self.assertEqual((2, []), (n, unknown))
        self.assertEqual("validée", d["lines"][0]["statut"])
        self.assertEqual("Bonjour !", d["lines"][0]["text"])
        self.assertNotIn("remarque", d["lines"][0])
        self.assertEqual("à revoir", r["flashes"][0]["statut"])
        self.assertEqual("trop long", r["flashes"][0]["remarque"])

    def test_apply_reports_unknown_keys_and_rejects_bad_status(self):
        d = tv.load(os.path.join(self.dir, "dialogues.json"))
        r = tv.load(os.path.join(self.dir, "radio.json"))
        _, unknown = tv.apply_decisions(d, r, {"items": {"dialogues/nope": {"statut": "validée"}}})
        self.assertEqual(["dialogues/nope"], unknown)
        with self.assertRaises(ValueError):
            tv.apply_decisions(d, r, {"items": {"dialogues/" + d["lines"][0]["id"]: {"statut": "bof"}}})


if __name__ == "__main__":
    unittest.main()
