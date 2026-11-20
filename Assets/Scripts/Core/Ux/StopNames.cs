using System;

namespace TaxiKmer.Core.Ux
{
    /// <summary>Noms courts des lieux pour l'interface : « Étoudi (devant le palais…) » → « Étoudi ».</summary>
    public static class StopNames
    {
        /// <summary>Coupe le nom avant une parenthèse ou un « / » : « Carrefour / marché de Messassi » → « Carrefour ».</summary>
        public static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return name ?? "";
            var cut = name.IndexOf(" (", StringComparison.Ordinal);
            if (cut > 0) name = name.Substring(0, cut);
            cut = name.IndexOf(" / ", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        /// <summary>Nom de la frise du trajet : le nom court, sans « de Messassi » quand il suit un premier mot.</summary>
        public static string Strip(string name)
        {
            var s = Short(name);
            var cut = s.IndexOf(" de ", StringComparison.Ordinal);
            return cut > 0 ? s.Substring(0, cut) : s;
        }
    }
}
