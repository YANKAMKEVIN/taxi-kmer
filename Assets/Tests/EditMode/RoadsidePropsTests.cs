using System.Linq;
using NUnit.Framework;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class RoadsidePropsTests
    {
        private static readonly float[] Route = { 0, 0, 0, 400 }; // 400 m tout droit vers le nord

        [Test]
        public void Plan_PolesTreesStalls_OffTheRoad_AndReproducible()
        {
            var c = new RoadsideConfig();
            var a = RoadsideProps.Plan(c, Route, new[] { 200f }, (x, z) => false, (x, z) => false, 5);
            var b = RoadsideProps.Plan(c, Route, new[] { 200f }, (x, z) => false, (x, z) => false, 5);
            Assert.AreEqual(a.Count, b.Count, "Même graine, même décor");
            Assert.AreEqual(11, a.Count(p => p.Kind == PropKind.Pole), "Un poteau tous les 38 m sur 400 m (le premier à 19 m)");
            Assert.Greater(a.Count(p => p.Kind == PropKind.Mango || p.Kind == PropKind.Palm), 15);
            Assert.Greater(a.Count(p => p.Kind == PropKind.Stall), 0, "Étals autour du lieu à clients");
            Assert.IsTrue(a.All(p => System.Math.Abs(p.X) >= c.singleOffset - 1.6f), "Rien sur la chaussée");
            Assert.IsTrue(a.Where(p => p.Kind == PropKind.Pole).All(p => p.X < 0), "Poteaux d'un seul côté");
            Assert.IsTrue(a.Where(p => p.Kind == PropKind.Stall).All(p => System.Math.Abs(p.Z - 200f) <= c.stallRadius + 1f));
        }

        [Test]
        public void Plan_KeepsJunctionsClear_AndWiresJoinNeighbourPoles()
        {
            var c = new RoadsideConfig();
            var props = RoadsideProps.Plan(c, Route, new float[0], (x, z) => false, (x, z) => z > 100f && z < 160f, 5);
            Assert.IsFalse(props.Any(p => p.Z > 100f && p.Z < 160f), "Carrefour dégagé");
            var poles = props.Where(p => p.Kind == PropKind.Pole).OrderBy(p => p.Z).ToList();
            Assert.IsTrue(float.IsNaN(poles.Last().NextX), "Le dernier poteau n'a pas de fil vers l'avant");
            Assert.AreEqual(poles[1].Z, poles[0].NextZ, 1e-3f, "Le fil va au poteau suivant");
        }

        [Test]
        public void Build_EachKind_FillsItsSlots_WithUvs()
        {
            var set = new MeshSet();
            var c = new RoadsideConfig();
            PropBuilder.Build(set, new Prop { Kind = PropKind.Pole, DirZ = 1, Scale = 1, NextX = 0, NextZ = 38 }, c);
            PropBuilder.Build(set, new Prop { Kind = PropKind.Mango, X = 5, DirZ = 1, Scale = 1 }, c);
            PropBuilder.Build(set, new Prop { Kind = PropKind.Palm, X = 9, DirZ = 1, Scale = 1 }, c);
            PropBuilder.Build(set, new Prop { Kind = PropKind.Stall, X = 12, DirZ = 1, Scale = 1, Variant = 2 }, c);
            foreach (var slot in new[] { PropBuilder.Pole, PropBuilder.Wire, PropBuilder.Trunk, PropBuilder.Foliage, PropBuilder.Stall, PropBuilder.Umbrella0 + 2, PropBuilder.Goods })
                Assert.Greater(set.Get(slot).TriangleCount, 0, $"Emplacement {slot}");
            foreach (var kv in set.Slots) Assert.AreEqual(kv.Value.Vertices.Count, kv.Value.Uvs.Count);
        }
    }
}
