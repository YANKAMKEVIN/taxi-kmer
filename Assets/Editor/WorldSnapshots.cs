using System.IO;
using TaxiKmer.Core.World;
using TaxiKmer.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TaxiKmer.EditorTools
{
    /// <summary>
    /// Captures de contrôle du monde, sans téléphone : vue de dessus et quelques vues à hauteur de taxi le long
    /// de l'itinéraire. Sortie : build/snapshots/*.png. En ligne de commande (sans -nographics, il faut le GPU) :
    /// Unity -batchmode -projectPath . -executeMethod TaxiKmer.EditorTools.WorldSnapshots.Run -quit
    /// </summary>
    public static class WorldSnapshots
    {
        private const string OutDir = "build/snapshots";

        [MenuItem("Taxi Kmer/Captures du monde")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(WorldSceneSetup.ScenePath, OpenSceneMode.Single);
            var builder = Object.FindFirstObjectByType<WorldBuilder>();
            builder.BuildNow();
            var cam = Camera.main;
            cam.GetComponent<RouteFlyCamera>().enabled = false;
            Directory.CreateDirectory(OutDir);

            var xz = builder.Data.route.xz;
            var cum = WorldGeometry.CumulativeLengths(xz);
            var total = cum[cum.Length - 1];

            var fog = RenderSettings.fog;
            RenderSettings.fog = false;
            // Vues de dessus (~170 m de côté) en trois points du trajet.
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 300f;
            foreach (var f in new[] { 0.1f, 0.5f, 0.9f })
            {
                WorldGeometry.SampleAlong(xz, cum, total * f, out var px, out var pz, out _, out _);
                cam.transform.SetPositionAndRotation(new Vector3(px, 150f, pz), Quaternion.Euler(90f, 0f, 0f));
                Capture(cam, $"00-dessus-{Mathf.RoundToInt(total * f)}m");
            }
            RenderSettings.fog = fog;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 800f;

            // Vues à hauteur de taxi : 5 points répartis sur l'itinéraire.
            for (var k = 0; k < 5; k++)
            {
                var d = total * (0.05f + 0.9f * k / 4f);
                WorldGeometry.SampleAlong(xz, cum, d - 12f, out var cx, out var cz, out _, out _);
                WorldGeometry.SampleAlong(xz, cum, d + 25f, out var tx, out var tz, out _, out _);
                var pos = new Vector3(cx, 5f, cz);
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(tx, 1.5f, tz) - pos, Vector3.up));
                Capture(cam, $"{k + 1:00}-{Mathf.RoundToInt(d)}m");
            }
            Debug.Log("[TaxiKmer] Captures écrites dans " + OutDir);
        }

        private static void Capture(Camera cam, string name)
        {
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
