using System.Collections.Generic;
using TaxiKmer.Core.World;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>
    /// Maillages des véhicules (Core/World/CarBodyBuilder), construits une fois et partagés : carrosserie de berline pour le
    /// taxi du joueur (ses roues tournent à part) et berline avec roues pour le trafic. Sous-maillages dans l'ordre
    /// peinture, vitres, garnitures, phares, feux (, pneus).
    /// </summary>
    public static class VehicleMeshes
    {
        private static Mesh s_body, s_traffic;

        public static Mesh PlayerBody => s_body != null ? s_body : s_body = ToMesh("Carrosserie de berline", CarBodyBuilder.Sedan(), 5);
        public static Mesh TrafficSedan => s_traffic != null ? s_traffic : s_traffic = ToMesh("Berline du trafic", CarBodyBuilder.Sedan(4.3f, 1.74f, true), 6);

        public static Mesh ToMesh(string name, MeshSet set, int slots)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var mesh = new Mesh { name = name, subMeshCount = slots };
            var tris = new List<int[]>();
            for (var s = 0; s < slots; s++)
            {
                var data = set.Get(s);
                var offset = verts.Count;
                for (var i = 0; i < data.Vertices.Count; i++)
                {
                    var v = data.Vertices[i];
                    verts.Add(new Vector3(v.X, v.Y, v.Z));
                    uvs.Add(new Vector2(data.Uvs[i].U, data.Uvs[i].V));
                }
                var t = new int[data.Triangles.Count];
                for (var i = 0; i < t.Length; i++) t[i] = data.Triangles[i] + offset;
                tris.Add(t);
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            for (var s = 0; s < slots; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>
        /// Habille le taxi du joueur (construit en blocs par TaxiFactory) : les blocs de caisse sont cachés et remplacés par
        /// la carrosserie, avec leurs propres matériaux (peinture jaune, vitres, garnitures, phares, feux).
        /// </summary>
        public static void DressPlayerTaxi(Transform root)
        {
            if (root.Find("Carrosserie") != null) return;
            Material Hide(string block)
            {
                Material m = null;
                foreach (Transform child in root)
                {
                    if (child.name != block) continue;
                    var r = child.GetComponent<MeshRenderer>();
                    if (r == null) continue;
                    m = r.sharedMaterial;
                    r.enabled = false;
                }
                return m;
            }
            var paint = Hide("Caisse");
            Hide("Toit");
            var glass = Hide("Vitres");
            var trim = Hide("Pare-chocs avant");
            Hide("Pare-chocs arrière");
            var head = Hide("Phare");
            var tail = Hide("Feu arrière");
            if (paint == null) return; // pas un taxi de TaxiFactory
            var go = new GameObject("Carrosserie");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = PlayerBody;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { paint, glass ?? paint, trim ?? paint, head ?? paint, tail ?? paint };
        }
    }
}
