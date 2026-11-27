#!/usr/bin/env python3
"""
Prépare les textures du monde (direction « réaliste stylisé poussé ») pour URP :
- matières Poly Haven (CC0) : albédo et normale recopiés, carte « ARM » (occlusion, rugosité, métal) convertie en carte
  métal/lissé d'URP (R = métal, A = 1 - rugosité) ;
- atlas des fenêtres, portes et rideaux de boutique, et atlas d'enseignes (noms de commerces inventés), dessinés ici.
Usage : python3 tools/art/prepare_textures.py <dossier Poly Haven téléchargé> Assets/Art/Textures
"""
import os, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

SRC, DST = sys.argv[1], sys.argv[2]
SIZE = 1024
MATERIALS = ["cracked_red_ground", "dry_ground_01", "asphalt_02", "asphalt_03", "painted_plaster_wall", "beige_wall_002",
             "blue_plaster_weathered", "clay_plaster", "grey_plaster_02", "damaged_plaster", "corrugated_iron",
             "green_metal_rust", "dirty_concrete", "concrete_pavement", "painted_metal_shutter",
             "bark_brown_02", "palm_tree_bark", "forest_leaves_03", "weathered_planks"]


def matter(name):
    out = os.path.join(DST, name)
    os.makedirs(out, exist_ok=True)
    Image.open(os.path.join(SRC, name, f"{name}_albedo.jpg")).convert("RGB").save(os.path.join(out, f"{name}_albedo.jpg"), quality=92)
    Image.open(os.path.join(SRC, name, f"{name}_normal.jpg")).convert("RGB").save(os.path.join(out, f"{name}_normal.jpg"), quality=95)
    ao, rough, metal = Image.open(os.path.join(SRC, name, f"{name}_arm.jpg")).convert("RGB").split()
    smooth = rough.point(lambda v: 255 - v)
    zero = Image.new("L", ao.size, 0)
    Image.merge("RGBA", (metal, zero, zero, smooth)).save(os.path.join(out, f"{name}_ms.png"))


def font(size, bold=True):
    for p in ("/System/Library/Fonts/Supplemental/Arial Bold.ttf" if bold else "/System/Library/Fonts/Supplemental/Arial.ttf",
              "/System/Library/Fonts/Supplemental/Arial Narrow Bold.ttf", "/Library/Fonts/Arial.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def window_atlas():
    """Atlas 4 × 4 de 256 px : fenêtres (cadres vert, blanc, bleu, alu ; avec ou sans barreaux, nacos), portes, rideaux."""
    # Fond opaque = encadrement crème autour de l'ouverture (pas de transparence : moins coûteux sur téléphone).
    img = Image.new("RGBA", (SIZE, SIZE), (214, 206, 190, 255))
    d = ImageDraw.Draw(img)
    frames = [(46, 140, 96), (236, 236, 228), (52, 96, 160), (170, 172, 176)]
    rnd = random.Random(7)
    for cell in range(16):
        cx, cy = (cell % 4) * 256, (cell // 4) * 256
        kind = cell // 4  # rangée 0 : fenêtres simples ; 1 : avec barreaux ; 2 : nacos ; 3 : portes et rideaux
        frame = frames[cell % 4]
        if kind < 3:
            d.rectangle([cx + 16, cy + 16, cx + 240, cy + 240], fill=frame + (255,))
            glass = (38 + rnd.randint(0, 20), 52 + rnd.randint(0, 20), 62 + rnd.randint(0, 20), 255)
            for gx in (0, 1):
                x0 = cx + 30 + gx * 102
                d.rectangle([x0, cy + 30, x0 + 94, cy + 226], fill=glass)
                d.polygon([(x0 + 10, cy + 226), (x0 + 60, cy + 30), (x0 + 80, cy + 30), (x0 + 30, cy + 226)], fill=(110, 130, 145, 255))
            if kind == 1:
                for b in range(7):
                    d.rectangle([cx + 34 + b * 30, cy + 24, cx + 40 + b * 30, cy + 232], fill=(30, 30, 30, 255))
                d.rectangle([cx + 24, cy + 126, cx + 232, cy + 132], fill=(30, 30, 30, 255))
            if kind == 2:
                for b in range(14):
                    d.rectangle([cx + 30, cy + 34 + b * 14, cx + 226, cy + 40 + b * 14], fill=(160, 175, 180, 255))
            d.rectangle([cx + 10, cy + 236, cx + 246, cy + 248], fill=(205, 200, 190, 255))  # appui de fenêtre
        else:
            if cell % 4 < 2:  # portes en métal peint ou bois
                col = [(120, 40, 30), (40, 80, 120)][cell % 4]
                d.rectangle([cx + 40, cy + 8, cx + 216, cy + 250], fill=col + (255,))
                for p in range(3):
                    d.rectangle([cx + 56, cy + 24 + p * 76, cx + 200, cy + 84 + p * 76], outline=(0, 0, 0, 120), width=4)
                d.ellipse([cx + 180, cy + 130, cx + 192, cy + 142], fill=(200, 190, 120, 255))
            else:  # rideau métallique de boutique (ouvert : fond sombre de la boutique)
                d.rectangle([cx, cy, cx + 255, cy + 255], fill=(36, 32, 30, 255))
                d.rectangle([cx, cy, cx + 255, cy + 34], fill=(150, 152, 156, 255))
                for s in range(8):
                    d.line([cx, cy + 4 + s * 4, cx + 255, cy + 4 + s * 4], fill=(110, 112, 116, 255), width=1)
                for s in range(5):  # étagères et marchandise
                    y = cy + 70 + s * 36
                    d.rectangle([cx + 12, y, cx + 244, y + 4], fill=(90, 70, 50, 255))
                    for k in range(9):
                        c = rnd.choice([(200, 60, 40), (240, 190, 40), (40, 120, 200), (60, 160, 80), (230, 230, 230)])
                        d.rectangle([cx + 16 + k * 26, y - 22, cx + 34 + k * 26, y - 1], fill=c + (255,))
    img.save(os.path.join(DST, "fenetres_atlas.png"))


def sign_atlas():
    """Atlas 2 × 8 d'enseignes (512 × 128) : commerces inventés, couleurs vives, comme sur les photos de Kevin."""
    names = [("CHEZ TANTINE ROSE", "Beignets · Haricots"), ("SALON PRESTIGE", "Coiffure hommes"), ("QUINCAILLERIE", "du Carrefour"),
             ("CALL-BOX", "Transfert · Crédit"), ("PHARMACIE", "de la Borne"), ("BOULANGERIE", "Le Bon Pain"),
             ("ALIMENTATION", "Générale"), ("COUTURE MODERNE", "Pagnes · Retouches"), ("GARAGE", "Mécanique · Vulcanisation"),
             ("MAQUIS", "Le Coin des Amis"), ("TÉLÉPHONIE", "Réparation · Accessoires"), ("PRESSING", "Netto Express"),
             ("CABARET", "Chez Mbarga"), ("CENTRE DE SANTÉ", "Soins · Consultations"), ("PAPETERIE", "Photocopies"),
             ("MOTO PIÈCES", "Neuves · Occasion")]
    palette = [((230, 120, 20), (255, 255, 255)), ((30, 110, 180), (255, 255, 255)), ((250, 210, 30), (30, 30, 30)),
               ((200, 30, 40), (255, 255, 255)), ((30, 140, 70), (255, 255, 255)), ((240, 240, 235), (40, 60, 140)),
               ((120, 40, 130), (255, 230, 120)), ((20, 20, 24), (250, 200, 40))]
    img = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, (title, sub) in enumerate(names):
        cx, cy = (i % 2) * 512, (i // 2) * 128
        bg, fg = palette[i % len(palette)]
        d.rectangle([cx, cy, cx + 511, cy + 127], fill=bg)
        d.rectangle([cx + 4, cy + 4, cx + 507, cy + 123], outline=fg, width=3)
        f = font(46)
        w = d.textlength(title, font=f)
        while w > 470:
            f = font(f.size - 2); w = d.textlength(title, font=f)
        d.text((cx + 256 - w / 2, cy + 16), title, font=f, fill=fg)
        f2 = font(26, bold=False)
        w2 = d.textlength(sub, font=f2)
        d.text((cx + 256 - w2 / 2, cy + 80), sub, font=f2, fill=fg)
    img = img.filter(ImageFilter.GaussianBlur(0.6))  # pas de crénelage à distance
    img.save(os.path.join(DST, "enseignes_atlas.png"))


os.makedirs(DST, exist_ok=True)
for m in MATERIALS:
    matter(m)
window_atlas()
sign_atlas()
with open(os.path.join(DST, "LICENCES.md"), "w") as f:
    f.write("# Licences des textures\n\n")
    f.write("- Matières (dossiers) : [Poly Haven](https://polyhaven.com), licence CC0 (domaine public), sans attribution obligatoire. "
            "Recopiées en 1K ; cartes `_ms` dérivées de leur carte ARM.\n")
    f.write("- `fenetres_atlas.png`, `enseignes_atlas.png` : dessinées par tools/art/prepare_textures.py pour Taxi Kmer "
            "(commerces inventés).\n")
print("ok")
