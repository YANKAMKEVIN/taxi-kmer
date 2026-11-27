using UnityEngine;

namespace TaxiKmer.World
{
    /// <summary>
    /// Matériaux du monde habillé, un par emplacement (Core.World.CitySlot), chargés depuis Resources/Art/Materials (créés
    /// par l'outil d'éditeur CityArtSetup). Si un matériau manque, le matériau de secours donné est utilisé.
    /// </summary>
    public static class CityMaterials
    {
        public static readonly string[] SlotNames =
        {
            "RoadPaved", "RoadDirt", "Median", "Lamp", "Wall0", "Wall1", "Wall2", "Wall3", "Wall4", "Wall5", "Wall6", "Wall7",
            "RoofTin", "RoofRust", "RoofConcrete", "Windows", "Signs",
            // Décors de bas-côté (Core.World.PropBuilder, emplacements 17 à 25).
            "Pole", "Wire", "Trunk", "Foliage", "Stall", "Umbrella0", "Umbrella1", "Umbrella2", "Goods",
        };

        public static Material Slot(int slot, Material fallback)
        {
            if (slot < 0 || slot >= SlotNames.Length) return fallback;
            return Named(SlotNames[slot], fallback);
        }

        public static Material Named(string name, Material fallback)
        {
            var m = Resources.Load<Material>("Art/Materials/" + name);
            return m != null ? m : fallback;
        }
    }
}
