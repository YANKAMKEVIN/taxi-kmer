using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.World
{
    /// <summary>Réglages des bas-côtés (roadside.json) : distances en mètres, valeurs par défaut de Claude (27/11).</summary>
    [Serializable]
    public class RoadsideConfig
    {
        /// <summary>Écart au centre de l'itinéraire : chaussée simple, puis double voie (avec muret central).</summary>
        public float singleOffset = 7.5f;
        public float dualOffset = 12f;
        /// <summary>Poteaux électriques en bois, d'un seul côté, reliés par trois fils qui pendent.</summary>
        public float poleSpacing = 38f;
        public float poleHeight = 8.5f;
        public float wireSag = 0.9f;
        /// <summary>Arbres (manguiers, palmiers) : un tous les … m de chaque côté, en moyenne, part de palmiers.</summary>
        public float treeSpacing = 26f;
        public float palmShare = 0.3f;
        /// <summary>Étals sous parasol autour des lieux à clients (marchés, carrefours), et dans ce rayon.</summary>
        public int stallsPerStop = 6;
        public float stallRadius = 45f;
        /// <summary>Pas de décor à moins de … m d'une autre rue (carrefours dégagés).</summary>
        public float junctionClearance = 9f;
    }

    public enum PropKind { Pole, Mango, Palm, Stall }

    public struct Prop
    {
        public PropKind Kind;
        public float X, Z;
        /// <summary>Direction de la route à cet endroit (unitaire).</summary>
        public float DirX, DirZ;
        public float Scale;
        public int Variant;
        /// <summary>Pour un poteau : position du poteau suivant (fils), NaN pour le dernier.</summary>
        public float NextX, NextZ;
    }

    /// <summary>
    /// Bas-côtés de l'itinéraire (planche d'ambiance v1) : poteaux électriques en bois et leurs fils, manguiers et palmiers,
    /// étals sous parasol aux lieux à clients. Placement reproductible (graine), loin des carrefours ; logique pure.
    /// </summary>
    public static class RoadsideProps
    {
        public static List<Prop> Plan(RoadsideConfig c, float[] route, IReadOnlyList<float> stopsAlong,
            Func<float, float, bool> dualAt, Func<float, float, bool> nearOtherRoad, int seed)
        {
            var props = new List<Prop>();
            if (route == null || route.Length < 4) return props;
            var cum = WorldGeometry.CumulativeLengths(route);
            var length = cum[cum.Length - 1];
            var rnd = new Random(seed);

            void Side(float along, float side, float extra, out float x, out float z, out float dx, out float dz)
            {
                WorldGeometry.SampleAlong(route, cum, along, out var cx, out var cz, out dx, out dz);
                var offset = (dualAt(cx, cz) ? c.dualOffset : c.singleOffset) + extra;
                // À droite dans le sens de l'itinéraire : (dz, -dx).
                x = cx + dz * offset * side;
                z = cz - dx * offset * side;
            }

            // Poteaux : côté gauche, espacement régulier ; on saute ceux qui tombent dans un carrefour (le fil passe au-dessus).
            var poles = new List<Prop>();
            for (var d = c.poleSpacing * 0.5f; d < length; d += c.poleSpacing)
            {
                Side(d, -1f, 0.5f, out var x, out var z, out var dx, out var dz);
                if (nearOtherRoad(x, z)) continue;
                poles.Add(new Prop { Kind = PropKind.Pole, X = x, Z = z, DirX = dx, DirZ = dz, Scale = 0.95f + (float)rnd.NextDouble() * 0.1f, Variant = rnd.Next(3) });
            }
            for (var i = 0; i < poles.Count; i++)
            {
                var p = poles[i];
                var next = i + 1 < poles.Count ? poles[i + 1] : default;
                var close = i + 1 < poles.Count && Math.Abs(next.X - p.X) + Math.Abs(next.Z - p.Z) < c.poleSpacing * 2.6f;
                p.NextX = close ? next.X : float.NaN;
                p.NextZ = close ? next.Z : float.NaN;
                props.Add(p);
            }

            // Arbres des deux côtés, espacement irrégulier.
            foreach (var side in new[] { -1f, 1f })
                for (var d = (float)rnd.NextDouble() * c.treeSpacing; d < length; d += c.treeSpacing * (0.5f + (float)rnd.NextDouble()))
                {
                    Side(d, side, 2.5f + (float)rnd.NextDouble() * 5f, out var x, out var z, out var dx, out var dz);
                    if (nearOtherRoad(x, z)) continue;
                    var palm = rnd.NextDouble() < c.palmShare;
                    props.Add(new Prop
                    {
                        Kind = palm ? PropKind.Palm : PropKind.Mango, X = x, Z = z, DirX = dx, DirZ = dz,
                        Scale = 0.8f + (float)rnd.NextDouble() * 0.5f, Variant = rnd.Next(4),
                    });
                }

            // Étals sous parasol autour des lieux à clients, des deux côtés, au bord de la chaussée.
            if (stopsAlong != null)
                foreach (var stop in stopsAlong)
                    for (var k = 0; k < c.stallsPerStop; k++)
                    {
                        var d = stop + ((float)rnd.NextDouble() * 2f - 1f) * c.stallRadius;
                        if (d < 0f || d > length) continue;
                        var side = k % 2 == 0 ? 1f : -1f;
                        Side(d, side, -1.5f + (float)rnd.NextDouble() * 2.5f, out var x, out var z, out var dx, out var dz);
                        if (nearOtherRoad(x, z)) continue;
                        props.Add(new Prop { Kind = PropKind.Stall, X = x, Z = z, DirX = dx, DirZ = dz, Scale = 1f, Variant = rnd.Next(3) });
                    }
            return props;
        }
    }
}
