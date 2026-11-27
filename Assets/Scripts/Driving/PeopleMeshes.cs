using System;
using System.Collections.Generic;
using TaxiKmer.Core.World;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Une silhouette posée dans la scène : le corps (quatre matériaux) et les deux jambes qui se balancent.</summary>
    public sealed class PersonParts
    {
        public GameObject Root;
        public MeshFilter BodyFilter;
        public MeshRenderer Body;
        public Transform LegL, LegR;
        public MeshFilter LegFilterL, LegFilterR;
        public MeshRenderer LegRendererL, LegRendererR;
        public float Phase;
    }

    /// <summary>
    /// Silhouettes humaines (Core/World/PersonBuilder) : maillages construits une fois par corpulence et tenue, partagés ;
    /// habillage aux couleurs voulues ; balancement des jambes en marche. Sert aux piétons, aux clients et aux mototaxis.
    /// </summary>
    public static class PeopleMeshes
    {
        private static readonly Dictionary<int, Mesh> Bodies = new Dictionary<int, Mesh>();
        private static readonly Dictionary<int, Mesh> Legs = new Dictionary<int, Mesh>();

        public static readonly Color[] Skins =
        {
            new Color(0.33f, 0.2f, 0.13f), new Color(0.27f, 0.16f, 0.1f), new Color(0.42f, 0.27f, 0.17f), new Color(0.22f, 0.13f, 0.09f),
        };

        public static readonly Color[] Bottoms =
        {
            new Color(0.12f, 0.16f, 0.3f), new Color(0.1f, 0.1f, 0.11f), new Color(0.55f, 0.48f, 0.35f), new Color(0.35f, 0.36f, 0.38f),
        };

        /// <summary>Tissus de pagne (couleurs vives).</summary>
        public static readonly Color[] Wax =
        {
            new Color(0.9f, 0.45f, 0.1f), new Color(0.1f, 0.5f, 0.6f), new Color(0.75f, 0.15f, 0.3f), new Color(0.95f, 0.8f, 0.2f),
        };

        public static readonly Color Hair = new Color(0.06f, 0.05f, 0.05f);

        private static int Key(int corpulence, bool dress) => corpulence * 2 + (dress ? 1 : 0);

        public static Mesh Body(int corpulence, bool dress)
        {
            var k = Key(corpulence, dress);
            if (!Bodies.TryGetValue(k, out var m)) Bodies[k] = m = VehicleMeshes.ToMesh($"Silhouette {k}", PersonBuilder.Body(corpulence, dress), 4);
            return m;
        }

        public static Mesh Leg(int corpulence, bool dress)
        {
            var k = Key(corpulence, dress);
            if (!Legs.TryGetValue(k, out var m)) Legs[k] = m = VehicleMeshes.ToMesh($"Jambe {k}", PersonBuilder.Leg(corpulence, dress), 4);
            return m;
        }

        /// <summary>Construit une silhouette (corps et jambes) sous <paramref name="parent"/>, à l'origine, face à +z.</summary>
        public static PersonParts Build(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var p = new PersonParts { Root = root };
            var body = new GameObject("Corps");
            body.transform.SetParent(root.transform, false);
            p.BodyFilter = body.AddComponent<MeshFilter>();
            p.Body = body.AddComponent<MeshRenderer>();
            foreach (var left in new[] { true, false })
            {
                var leg = new GameObject(left ? "Jambe gauche" : "Jambe droite");
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(left ? -0.1f : 0.1f, PersonBuilder.HipHeight, 0f);
                var f = leg.AddComponent<MeshFilter>();
                var r = leg.AddComponent<MeshRenderer>();
                if (left) { p.LegL = leg.transform; p.LegFilterL = f; p.LegRendererL = r; }
                else { p.LegR = leg.transform; p.LegFilterR = f; p.LegRendererR = r; }
            }
            return p;
        }

        /// <summary>Habille une silhouette d'après son « look » (tiré par la logique) : corpulence, tenue, couleurs.</summary>
        public static void Dress(PersonParts p, int corpulence, int look, Color top, Func<Color, Material> material)
        {
            var dress = look % 3 == 0; // une personne sur trois en pagne ou en robe
            p.BodyFilter.sharedMesh = Body(corpulence, dress);
            p.LegFilterL.sharedMesh = p.LegFilterR.sharedMesh = Leg(corpulence, dress);
            var skin = material(Skins[(look / 3) % Skins.Length]);
            var bottom = material(dress ? Wax[(look / 2) % Wax.Length] : Bottoms[(look / 5) % Bottoms.Length]);
            var mats = new[] { skin, material(top), bottom, material(Hair) };
            p.Body.sharedMaterials = mats;
            p.LegRendererL.sharedMaterials = p.LegRendererR.sharedMaterials = mats;
            var legX = corpulence == 2 ? 0.13f : corpulence == 0 ? 0.08f : 0.1f;
            p.LegL.localPosition = new Vector3(-legX, PersonBuilder.HipHeight, 0f);
            p.LegR.localPosition = new Vector3(legX, PersonBuilder.HipHeight, 0f);
        }

        /// <summary>Balance les jambes selon le chemin parcouru (immobile : jambes droites).</summary>
        public static void Walk(PersonParts p, float metersMoved, float speed)
        {
            p.Phase += metersMoved * 4.2f;
            var a = PersonBuilder.Swing(p.Phase, speed, true);
            p.LegL.localRotation = Quaternion.Euler(a, 0f, 0f);
            p.LegR.localRotation = Quaternion.Euler(-a, 0f, 0f);
        }
    }
}
