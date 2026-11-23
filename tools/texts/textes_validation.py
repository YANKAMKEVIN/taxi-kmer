#!/usr/bin/env python3
"""Page de validation des textes du jeu (répliques des passagers et textes de la radio).

Usage :
  python3 tools/texts/textes_validation.py page  [-o build/validation/textes.html]
  python3 tools/texts/textes_validation.py apply decisions.json

« page » écrit une page HTML autonome (aucun réseau) qui liste les 195 répliques de dialogues.json et les textes
de radio.json (annonces, publicités, flashs). Kevin coche « validée » ou « à revoir », corrige une tournure et note
une remarque ; ses choix restent dans le navigateur (localStorage) et le bouton « Exporter » télécharge un fichier
decisions.json.

« apply » réinjecte ce fichier dans les JSON du jeu : statut (« validée » / « à revoir »), texte corrigé et
remarque (champ « remarque », ignoré par le jeu). Le format des JSON (indentation 4) est conservé.
"""
import argparse
import html
import json
import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DATA = os.path.join(ROOT, "Assets", "Resources", "Data")
STATUSES = ("proposée", "validée", "à revoir")


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    with open(path, "w", encoding="utf-8") as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=4) + "\n")


def collect(dialogues, radio):
    """Liste à plat des textes à valider : clé unique, groupe, contexte, texte, statut."""
    items = []
    chars = {c["id"]: c["name"] for c in dialogues.get("characters", [])}
    for line in dialogues.get("lines", []):
        context = [line.get("trigger", ""), line.get("register", "")]
        if line.get("character"):
            context.append(chars.get(line["character"], line["character"]))
        if line.get("fromTime") or line.get("untilTime"):
            context.append(f"{line.get('fromTime', '')}–{line.get('untilTime', '')}")
        items.append({
            "key": "dialogues/" + line["id"],
            "group": "Répliques : " + line.get("trigger", ""),
            "context": " · ".join(c for c in context if c),
            "text": line.get("text", ""),
            "replies": [r.get("text", "") for r in line.get("replies", [])],
            "statut": line.get("statut", ""),
            "remarque": line.get("remarque", ""),
            "traduction": line.get("traduction", ""),
        })
    for station in radio.get("stations", []):
        for p in station.get("programmes", []):
            if not p.get("text"):
                continue
            items.append({
                "key": f"radio/{station['id']}/{p['id']}",
                "group": f"Radio : {station.get('name', station['id'])}",
                "context": " · ".join(x for x in [p.get("kind", ""), p.get("register", "")] if x),
                "text": p["text"],
                "replies": [],
                "statut": p.get("statut", ""),
                "remarque": p.get("remarque", ""),
                "traduction": p.get("traduction", ""),
            })
    for f in radio.get("flashes", []):
        items.append({
            "key": "flash/" + f["id"],
            "group": "Radio : flashs info",
            "context": " · ".join(x for x in [f.get("event", ""), f.get("register", "")] if x),
            "text": f.get("text", ""),
            "replies": [],
            "statut": f.get("statut", ""),
            "remarque": f.get("remarque", ""),
            "traduction": f.get("traduction", ""),
        })
    return items


def _target(dialogues, radio, key):
    parts = key.split("/")
    if parts[0] == "dialogues" and len(parts) == 2:
        return next((l for l in dialogues.get("lines", []) if l["id"] == parts[1]), None)
    if parts[0] == "radio" and len(parts) == 3:
        st = next((s for s in radio.get("stations", []) if s["id"] == parts[1]), None)
        return st and next((p for p in st.get("programmes", []) if p["id"] == parts[2]), None)
    if parts[0] == "flash" and len(parts) == 2:
        return next((f for f in radio.get("flashes", []) if f["id"] == parts[1]), None)
    return None


def apply_decisions(dialogues, radio, decisions):
    """Applique les décisions ; renvoie (nombre appliqué, clés inconnues). Lève ValueError sur un statut inconnu."""
    applied, unknown = 0, []
    for key, d in decisions.get("items", {}).items():
        target = _target(dialogues, radio, key)
        if target is None:
            unknown.append(key)
            continue
        statut = d.get("statut", target.get("statut", ""))
        if statut not in STATUSES:
            raise ValueError(f"{key} : statut inconnu « {statut} »")
        target["statut"] = statut
        text = (d.get("text") or "").strip()
        if text:
            target["text"] = text
        remark = (d.get("remarque") or "").strip()
        if remark:
            target["remarque"] = remark
        elif "remarque" in target:
            del target["remarque"]
        applied += 1
    return applied, unknown


PAGE = """<!doctype html>
<html lang="fr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Taxi Kmer · textes à valider</title>
<style>
body{font:15px/1.45 system-ui,sans-serif;margin:0;background:#f6f4ef;color:#222}
header{position:sticky;top:0;background:#1f1f1f;color:#fff;padding:10px 16px;display:flex;gap:12px;flex-wrap:wrap;align-items:center}
header button,header select{font:inherit;padding:4px 10px}
main{max-width:900px;margin:0 auto;padding:8px 16px 60px}
h2{margin:24px 0 8px;font-size:17px}
.card{background:#fff;border:1px solid #ddd;border-radius:8px;padding:10px 12px;margin:8px 0}
.card.validée{border-left:6px solid #2e7d32}.card.à\\ revoir{border-left:6px solid #c62828}
.ctx{color:#666;font-size:13px}.rep{color:#444;font-size:13px;margin:2px 0 0 12px}
textarea{width:100%;box-sizing:border-box;font:inherit;margin-top:6px}
.row{display:flex;gap:14px;flex-wrap:wrap;margin-top:6px}
</style></head><body>
<header><b>Textes à valider</b><span id="count"></span>
<select id="filter"><option value="">Tout</option><option>proposée</option><option>validée</option><option>à revoir</option></select>
<button id="export">Exporter decisions.json</button></header>
<main id="list"></main>
<script id="data" type="application/json">__DATA__</script>
<script>
const items = JSON.parse(document.getElementById('data').textContent);
const KEY = 'taxi-kmer-validation';
let state = {};
try { state = JSON.parse(localStorage.getItem(KEY) || '{}'); } catch (e) {}
function save() { try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) {} count(); }
function cur(it) { return Object.assign({statut: it.statut, text: it.text, remarque: it.remarque}, state[it.key] || {}); }
function count() {
  const n = items.filter(it => cur(it).statut === 'validée').length;
  const r = items.filter(it => cur(it).statut === 'à revoir').length;
  document.getElementById('count').textContent = `${n} validées · ${r} à revoir · ${items.length} textes`;
}
function render() {
  const f = document.getElementById('filter').value, list = document.getElementById('list');
  list.innerHTML = ''; let group = '';
  for (const it of items) {
    const c = cur(it); if (f && c.statut !== f) continue;
    if (it.group !== group) { group = it.group; const h = document.createElement('h2'); h.textContent = group; list.appendChild(h); }
    const d = document.createElement('div'); d.className = 'card ' + c.statut;
    d.innerHTML = `<div class="ctx"></div><textarea rows="2"></textarea><div class="reps"></div>
      <div class="row"><label><input type="radio" name="${it.key}" value="validée"> validée</label>
      <label><input type="radio" name="${it.key}" value="à revoir"> à revoir</label></div>
      <textarea rows="1" placeholder="Remarque (facultatif)"></textarea>`;
    d.querySelector('.ctx').textContent = it.key + ' · ' + it.context + (it.traduction ? ' · ' + it.traduction : '');
    const [ta, note] = d.querySelectorAll('textarea'); ta.value = c.text; note.value = c.remarque || '';
    for (const r of it.replies) { const p = document.createElement('div'); p.className = 'rep'; p.textContent = '→ ' + r; d.querySelector('.reps').appendChild(p); }
    d.querySelectorAll('input').forEach(i => { i.checked = i.value === c.statut;
      i.onchange = () => { state[it.key] = Object.assign(cur(it), {statut: i.value}); d.className = 'card ' + i.value; save(); }; });
    ta.oninput = () => { state[it.key] = Object.assign(cur(it), {text: ta.value}); save(); };
    note.oninput = () => { state[it.key] = Object.assign(cur(it), {remarque: note.value}); save(); };
    list.appendChild(d);
  }
  count();
}
document.getElementById('filter').onchange = render;
document.getElementById('export').onclick = () => {
  const out = {version: 1, items: {}};
  for (const it of items) if (state[it.key]) { const c = cur(it); out.items[it.key] = {statut: c.statut, text: c.text !== it.text ? c.text : '', remarque: c.remarque || ''}; }
  const a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([JSON.stringify(out, null, 1)], {type: 'application/json'}));
  a.download = 'decisions.json'; a.click();
};
render();
</script></body></html>
"""


def page_html(items):
    data = json.dumps(items, ensure_ascii=False).replace("</", "<\\/")
    return PAGE.replace("__DATA__", data)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("page")
    p.add_argument("-o", "--output", default=os.path.join(ROOT, "build", "validation", "textes.html"))
    p.add_argument("--data", default=DATA)
    a = sub.add_parser("apply")
    a.add_argument("decisions")
    a.add_argument("--data", default=DATA)
    args = ap.parse_args(argv)
    dpath, rpath = os.path.join(args.data, "dialogues.json"), os.path.join(args.data, "radio.json")
    dialogues, radio = load(dpath), load(rpath)
    if args.cmd == "page":
        items = collect(dialogues, radio)
        os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
        with open(args.output, "w", encoding="utf-8") as f:
            f.write(page_html(items))
        print(f"{len(items)} textes → {args.output}")
        return 0
    applied, unknown = apply_decisions(dialogues, radio, load(args.decisions))
    save(dpath, dialogues)
    save(rpath, radio)
    print(f"{applied} décisions appliquées" + (f" ; clés inconnues : {', '.join(unknown)}" if unknown else ""))
    return 1 if unknown else 0


if __name__ == "__main__":
    sys.exit(main())
