using System;

namespace TaxiKmer.Core.Save
{
    /// <summary>
    /// Mise à jour d'une sauvegarde écrite par une version plus ancienne du jeu. Un champ ajouté prend tout seul sa
    /// valeur par défaut à la lecture (JsonUtility et System.Text.Json ignorent ce qui manque) ; une étape n'est
    /// nécessaire que si le sens d'un champ change. Chaque étape fait passer de la version n à n + 1.
    /// </summary>
    public static class SaveMigrations
    {
        /// <summary>Version écrite par ce jeu. À augmenter à chaque changement du contenu, avec son étape ci-dessous.</summary>
        public const int CurrentVersion = 3;

        public static void Upgrade(SaveData data, int fromVersion)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (fromVersion > CurrentVersion) throw new InvalidOperationException($"Sauvegarde version {fromVersion} plus récente que le jeu ({CurrentVersion}).");
            for (var v = Math.Max(fromVersion, 0); v < CurrentVersion; v++)
                Step(data, v);
            data.version = CurrentVersion;
            Repair(data);
        }

        /// <summary>Étape v → v + 1. Version 0 = fichier sans numéro (n'existe pas encore en vrai).</summary>
        private static void Step(SaveData data, int from)
        {
            switch (from)
            {
                case 0:
                    break; // version 1 : premier format, rien à transformer
                case 1:
                    // Version 2 : épargne, versements manqués, renvoi. Les anciens bilans comptaient le versement
                    // comme payé : on le reporte, et l'épargne repart de zéro (l'argent ne passait pas d'un jour à l'autre).
                    if (data.career?.history != null)
                        foreach (var r in data.career.history)
                            if (r.versementPaid == 0 && r.versementMissing == 0) r.versementPaid = r.versement;
                    break;
                case 2:
                    // Version 3 : réservoir réel. Niveau inconnu → réservoir de départ (fuel.json) ; le carburant de la
                    // journée en cours se comptait à la distance : rien d'acheté.
                    if (data.career != null) data.career.fuelLitres = -1f;
                    if (data.day != null) { data.day.tankLitres = -1f; data.day.fuelBoughtLitres = 0f; data.day.fuelBoughtCost = 0; }
                    break;
            }
        }

        /// <summary>Remet d'aplomb ce qu'un fichier, même valide, pourrait avoir d'incohérent (tableaux absents, valeurs hors bornes).</summary>
        private static void Repair(SaveData data)
        {
            if (data.career == null) data.career = new CareerData();
            if (data.career.history == null) data.career.history = Array.Empty<DayRecordData>();
            if (data.career.missedVersementDays == null) data.career.missedVersementDays = Array.Empty<int>();
            if (data.career.dayNumber < 1) data.career.dayNumber = 1;
            if (data.day == null) { data.day = new DayProgressData(); data.hasDayInProgress = false; }
            if (data.day.expenses == null) data.day.expenses = Array.Empty<ExpenseData>();
            if (data.day.passengers == null) data.day.passengers = Array.Empty<PassengerData>();
            if (data.day.elapsedRealSeconds < 0f) data.day.elapsedRealSeconds = 0f;
            if (data.day.taxiDirection != -1) data.day.taxiDirection = 1;
        }
    }
}
