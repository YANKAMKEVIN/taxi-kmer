using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    /// <summary>
    /// Sérialiseur des tests : JsonUtility dans Unity (le même que le jeu), System.Text.Json hors Unity (dotnet),
    /// réglé comme JsonUtility (champs publics, pas de propriétés).
    /// </summary>
    public sealed class TestSaveSerializer : ISaveSerializer
    {
        public static readonly TestSaveSerializer Instance = new TestSaveSerializer();

#if UNITY_5_3_OR_NEWER
        public string ToJson(object value) => UnityEngine.JsonUtility.ToJson(value);
        public T FromJson<T>(string json) where T : class => UnityEngine.JsonUtility.FromJson<T>(json);
#else
        private static readonly System.Text.Json.JsonSerializerOptions Options =
            new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

        public string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), Options);
        public T FromJson<T>(string json) where T : class => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options);
#endif
    }
}
