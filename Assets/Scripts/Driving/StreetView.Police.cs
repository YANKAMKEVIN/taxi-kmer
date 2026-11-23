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
    /// <summary>Police dans le monde : postes de contrôle et voiture de la poursuite. Partie de <see cref="StreetView"/> (idée n° 33 : fichier découpé).</summary>
    public sealed partial class StreetView
    {
        /// <summary>Voiture de police qui suit le taxi pendant une poursuite, sur la voie du taxi (primitives provisoires).</summary>
        private void UpdateChaseCar(TaxiOnRoute state)
        {
            var chase = _police.Chase;
            if (chase == null)
            {
                if (_chaseCar != null) _chaseCar.SetActive(false);
                return;
            }
            if (_chaseCar == null)
            {
                _chaseCar = new GameObject("Voiture de police");
                _chaseCar.transform.SetParent(transform, false);
                var blue = new Color(0.1f, 0.2f, 0.45f);
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 0.8f, 4.4f), blue);
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0f, 1.35f, -0.2f), new Vector3(1.6f, 0.55f, 2f), new Color(0.92f, 0.92f, 0.9f));
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(-0.3f, 1.7f, -0.2f), new Vector3(0.5f, 0.15f, 0.3f), new Color(0.9f, 0.1f, 0.1f));
                Part(_chaseCar.transform, PrimitiveType.Cube, new Vector3(0.3f, 1.7f, -0.2f), new Vector3(0.5f, 0.15f, 0.3f), new Color(0.1f, 0.3f, 1f));
            }
            _chaseCar.SetActive(true);
            WorldGeometry.SampleAlong(_xz, _cum, chase.PoliceAlong, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var at = new Vector3(x, 0f, z) + right * state.Lateral;
            // Sur le relief (idée n° 8) : hauteur sous les essieux, tangage dans la pente.
            var fx = dx * state.Direction;
            var fz = dz * state.Direction;
            at.y = Ground.Vehicle(world.Terrain, at.x, at.z, fx, fz, 4.4f, out var rise);
            _chaseCar.transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(fx * 4.4f, rise, fz * 4.4f), Vector3.up));
        }

        private (Core.Police.ControlPost, GameObject, Transform) MakePolicePost(Core.Police.ControlPost post)
        {
            WorldGeometry.SampleAlong(_xz, _cum, post.Along, out var x, out var z, out var dx, out var dz);
            var right = new Vector3(dz, 0f, -dx);
            var root = new GameObject(Core.Police.PoliceMarkers.Label(post) + " " + post.Id);
            root.transform.SetParent(transform, false);
            // Repère local : +x vers l'extérieur de la route, -x vers la chaussée, +z dans le sens de la marche.
            var at = new Vector3(x, 0f, z) + right * (sideOffset + 2.5f);
            root.transform.SetPositionAndRotation(OnGround(at.x, at.z), Quaternion.LookRotation(new Vector3(dx, 0f, dz), Vector3.up));
            var blue = new Color(0.1f, 0.2f, 0.45f);
            var white = new Color(0.92f, 0.92f, 0.9f);
            var red = new Color(0.8f, 0.12f, 0.1f);
            var orange = new Color(1f, 0.45f, 0.05f);
            if (!post.Flying)
            {
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.5f, 1.3f, 0f), new Vector3(2.4f, 2.6f, 2.4f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.5f, 2.75f, 0f), new Vector3(2.8f, 0.3f, 2.8f), white);
                Part(root.transform, PrimitiveType.Cube, new Vector3(0.6f, 0.6f, 3f), new Vector3(0.3f, 1.2f, 0.3f), white);
                for (var i = 0; i < 4; i++) // barrière rabattue le long du bord, bandes rouges et blanches
                    Part(root.transform, PrimitiveType.Cube, new Vector3(0.6f, 1.1f, 3.6f + i * 0.9f), new Vector3(0.15f, 0.15f, 0.9f), i % 2 == 0 ? red : white);
            }
            else
            {
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 0.8f, 0f), new Vector3(1.9f, 1f, 4.4f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 1.6f, 0.8f), new Vector3(1.8f, 0.7f, 1.8f), blue);
                Part(root.transform, PrimitiveType.Cube, new Vector3(2.2f, 2.05f, 0.8f), new Vector3(0.9f, 0.15f, 0.3f), red);
            }
            foreach (var cz in new[] { -6f, -3f, 3f }) // plots
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(-0.8f, 0.35f, cz), new Vector3(0.35f, 0.35f, 0.35f), orange);
            var officer = new GameObject("Policier").transform;
            officer.SetParent(root.transform, false);
            officer.localPosition = new Vector3(0f, 0f, -1f);
            Part(officer, PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.85f, 0.45f), new Color(0.25f, 0.3f, 0.2f));
            Part(officer, PrimitiveType.Sphere, new Vector3(0f, 1.82f, 0f), Vector3.one * 0.26f, new Color(0.35f, 0.22f, 0.14f));
            Part(officer, PrimitiveType.Cylinder, new Vector3(0f, 2f, 0f), new Vector3(0.32f, 0.05f, 0.32f), blue); // képi
            root.SetActive(false);
            return (post, root, officer);
        }

        /// <summary>
        /// Postes de contrôle à chaque image : visibles selon <see cref="Core.Police.PoliceMarkers.Visible"/>, policier
        /// sur la chaussée pendant qu'il arrête le taxi, texte « Contrôle » au-dessus quand le taxi approche. Rend
        /// l'index du prochain texte de repère libre.
        /// </summary>
        private int SyncPolicePosts(int labelIndex, float along)
        {
            if (_police == null) return labelIndex;
            var minute = Street.Shift.Clock.GameMinutes;
            var current = _police.Active ? _police.Police.CurrentPost : null;
            foreach (var (post, root, officer) in _postViews)
            {
                var visible = Core.Police.PoliceMarkers.Visible(post, minute);
                root.SetActive(visible);
                if (!visible) continue;
                officer.localPosition = new Vector3(Core.Police.PoliceMarkers.Holding(post, current) ? -3f : 0f, 0f, -1f);
                if (!Street.DayFinished && Core.Police.PoliceMarkers.LabelShown(post, along))
                    ShowMarkerLabel(labelIndex++, root.transform.position + Vector3.up * 3.6f, Core.Police.PoliceMarkers.Label(post));
            }
            return labelIndex;
        }
    }
}
