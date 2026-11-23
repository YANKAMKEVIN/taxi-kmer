#!/usr/bin/env python3
"""Fabrique les sauvegardes de référence v1 à v6 (idée n° 35) : une par version du format, avec exactement les
champs que SaveData avait à cette version (relevés dans l'historique git de Assets/Scripts/Core/Save/SaveData.cs :
51a37c2 v1, aadafb8 v2, 0962430 v3, bcc944c v4, baedd5a v5, 34fd760 v6), dans l'enveloppe de SaveStore
(format, version, empreinte FNV-1a 64 bits du contenu). Usage : python3 tools/dotnet/SaveFixtures/fabriquer.py
Les fichiers sont gardés tels quels dans le dépôt : ne les refaire que pour ajouter une version."""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))


def fnv(text):
    h = 14695981039346656037
    for b in text.encode("utf-8"):
        h ^= b
        h = (h * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return format(h, "016x")


def record(day, version):
    r = {"dayNumber": day, "revenue": 9800, "rideCount": 11, "versement": 7000, "kilometres": 23.4, "fuelLitres": 2.3,
         "fuelCost": 1900, "expensesTotal": 0, "remaining": 900, "unpaidPassengers": 0, "unpaidFares": 0,
         "arrested": False, "vehicleSeized": False}
    if version >= 2:
        r.update({"versementPaid": 7000, "versementMissing": 0, "savingsAfter": 6000 + day * 500, "ownerMood": 0})
    if version >= 4:
        r.update({"reputation": 61, "reputationChange": 2})
    return r


def save(version):
    career = {"dayNumber": 9, "history": [record(7, version), record(8, version)]}
    if version >= 2:
        career.update({"savings": 12000, "missedVersementDays": [4], "fired": False})
    if version >= 3:
        career["fuelLitres"] = 21.5
    if version >= 4:
        career["reputation"] = 63
    if version >= 5:
        career["impounds"] = 1
    if version >= 6:
        career.update({"vehicleDamage": 35.0, "papers": [{"id": "assurance", "lastValidDay": 40}]})
    day = {"elapsedRealSeconds": 812.5, "revenue": 3600, "rideCount": 4, "metersDriven": 7300.0,
           "expenses": [{"label": "Contrôle de police", "amount": 1000}],
           "passengers": [{"id": "c12", "destinationStopId": "emana", "distanceMeters": 2154, "proposedFare": 1000,
                           "corpulence": 1, "bench": 1}],
           "leftUnpaid": 0, "leftUnpaidFares": 0, "taxiAlong": 1520.0, "taxiDirection": 1, "seed": 4242}
    if version >= 3:
        day.update({"tankLitres": 18.0, "fuelBoughtLitres": 10.0, "fuelBoughtCost": 8400})
    if version >= 4:
        day.update({"reputationDayStart": 63, "reputation": 64})
    return {"version": version, "savedAtUtc": "2026-11-20T18:00:00Z", "gameVersion": "0.1.0", "career": career,
            "hasDayInProgress": True, "day": day}


def main():
    for v in range(1, 7):
        payload = json.dumps(save(v), ensure_ascii=False, separators=(",", ":"))
        envelope = {"format": "taxikmer-save", "version": v, "checksum": fnv(payload), "payload": payload}
        with open(os.path.join(HERE, f"partie-v{v}.json"), "w", encoding="utf-8") as f:
            f.write(json.dumps(envelope, ensure_ascii=False, separators=(",", ":")))


if __name__ == "__main__":
    main()
