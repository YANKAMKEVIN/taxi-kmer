using System.Collections.Generic;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Fuel;
using TaxiKmer.Core.Incidents;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Traffic;
using TaxiKmer.Core.Ux;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEngine;
using UnityEngine.UI;

namespace TaxiKmer.Driving
{
    /// <summary>Silhouettes des clients au bord de la route et passagers qui descendent. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        private void SyncPeople()
        {
            var seen = new HashSet<RoadsideClient>(Street.Waiting);
            var gone = new List<RoadsideClient>();
            foreach (var kv in _people)
                if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var c in gone)
            {
                Destroy(_people[c].Root);
                _people.Remove(c);
            }

            var calling = new List<RoadsideClient>();
            foreach (var c in Street.Waiting)
            {
                if (!_people.TryGetValue(c, out var person)) _people[c] = person = MakePerson(c);
                var accepted = c.State == RoadsideState.Accepted;
                person.AcceptedMark.SetActive(accepted);
                if (accepted)
                {
                    var k = WorldMarkers.RingPulse(Time.time);
                    person.Ring.localScale = new Vector3(k, 1f, k);
                }
                if (c.Calling) calling.Add(c);
            }
            // Le plus proche d'abord : sa bulle reste à sa place, les suivantes montent si elles la chevauchent.
            var taxiPos = taxi.transform.position;
            calling.Sort((a, b) => (_people[a].Root.transform.position - taxiPos).sqrMagnitude
                .CompareTo((_people[b].Root.transform.position - taxiPos).sqrMagnitude));
            _placed.Clear();
            for (var i = 0; i < calling.Count; i++)
            {
                var c = calling[i];
                ShowBubble(i, _people[c].Root.transform.position + Vector3.up * 2.4f, c.Client);
            }
            for (var i = calling.Count; i < _bubbles.Count; i++) _bubbles[i].Root.gameObject.SetActive(false);
        }

        private Person MakePerson(RoadsideClient c)
        {
            // Sur le trottoir de la chaussée de son sens (CityLife), tourné vers les taxis qui arrivent.
            var place = City.Place(c);
            var pos = OnGround(place.X, place.Z); // sur le relief (idée n° 8)
            var root = new GameObject("Client " + c.Client.Id);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(place.FaceX, 0f, place.FaceZ), Vector3.up));

            var width = c.Client.Corpulence switch
            {
                Core.Rides.Corpulence.Mince => 0.42f,
                Core.Rides.Corpulence.Costaud => 0.78f,
                _ => 0.56f,
            };
            var shirt = Shirts[(c.Client.Id.GetHashCode() & 0x7fffffff) % Shirts.Length];
            Part(root.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(width, 0.8f, width * 0.75f), shirt);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            // Client accepté : anneau jaune qui pulse au sol sous lui, et zone d'arrêt dessinée sur la voie devant lui
            // (le taxi s'y arrête pour le faire monter ; local +z = vers la route).
            var mark = new GameObject("Client accepté");
            mark.transform.SetParent(root.transform, false);
            var yellow = new Color(1f, 0.8f, 0.05f);
            var ring = new GameObject("Anneau").transform;
            ring.SetParent(mark.transform, false);
            for (var i = 0; i < RingSegments; i++)
            {
                var a = i * 360f / RingSegments;
                var seg = Part(ring, PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.03f, 0.8f),
                    new Vector3(0.34f, 0.02f, 0.12f), yellow);
                seg.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            Part(mark.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, sideOffset - taxi.laneOffset), new Vector3(8f, 0.02f, 2.4f),
                new Color(1f, 0.85f, 0.2f));
            mark.SetActive(false);
            return new Person { Root = root, AcceptedMark = mark, Ring = ring };
        }

        private void SpawnLeavingPerson(Client c)
        {
            var t = taxi.transform;
            var go = new GameObject("Client déposé");
            var side = Flat(t.right);
            var at = t.position + side * 2.5f;
            go.transform.SetPositionAndRotation(OnGround(at.x, at.z), Quaternion.LookRotation(side, Vector3.up));
            var shirt = Shirts[(c.Id.GetHashCode() & 0x7fffffff) % Shirts.Length];
            Part(go.transform, PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.56f, 0.8f, 0.42f), shirt);
            Part(go.transform, PrimitiveType.Sphere, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            Destroy(go, 5f);
        }

        private GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (personMaterial != null) r.sharedMaterial = personMaterial;
            _block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(_block);
            return go;
        }
    }
}
