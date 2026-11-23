using System;
using System.Collections.Generic;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Phone
{
    /// <summary>
    /// Téléphone du chauffeur (idée n° 11 ; données : Assets/Resources/Data/phone.json) : un seul point d'entrée pour
    /// les messages du propriétaire (menaces avant renvoi), de la famille (jalon 7), de l'appli fictive (jalon 8) et les
    /// contacts à appeler au contrôle (crochet du jalon 4). Première tranche : messages du propriétaire du matin.
    /// Textes proposés par Claude, contenu et nom de l'appli à trancher par Kevin.
    /// </summary>
    [Serializable]
    public class PhoneConfig
    {
        public bool enabled = true;
        public string ownerName = "Le propriétaire";
        /// <summary>Nom de l'appli de courses fictive (jalon 8) : vide tant que Kevin ne l'a pas choisi.</summary>
        public string appName = "";
        /// <summary>Versement payé la veille : un de ces messages, une fois sur <see cref="thanksEveryDays"/>.</summary>
        public string[] ownerThanks =
        {
            "Bien reçu le versement d'hier. Continue comme ça.",
            "Versement reçu. Fais attention au taxi, hein.",
        };
        public int thanksEveryDays = 3;
        /// <summary>Versement manqué : {manque} = ce qui manquait, {reste} = manques encore tolérés cette semaine.</summary>
        public string[] ownerThreat =
        {
            "Il manquait {manque} hier. Encore {reste} comme ça cette semaine et je reprends mon taxi.",
            "Tu me dois {manque}. Je ne suis pas une banque : encore {reste} et c'est fini.",
        };
        /// <summary>Dernier avertissement (plus aucun manque toléré).</summary>
        public string[] ownerLastWarning =
        {
            "Dernier avertissement. Le prochain versement manqué, tu me rends les clés.",
        };
        /// <summary>Le taxi est allé à la fourrière la veille (le propriétaire renvoie à la 2ᵉ fois).</summary>
        public string[] ownerImpound =
        {
            "Mon taxi à la fourrière ! La prochaine fois, tu cherches un autre travail.",
        };
    }

    public enum PhoneSender
    {
        Owner,
        Family,
        App,
    }

    public sealed class PhoneMessage
    {
        public PhoneSender From;
        public string FromName = "";
        public string Text = "";
        public int Day;
        public bool Read;
    }

    /// <summary>Boîte de réception de la journée (pure, testable).</summary>
    public sealed class PhoneInbox
    {
        private readonly List<PhoneMessage> _messages = new List<PhoneMessage>();

        public IReadOnlyList<PhoneMessage> Messages => _messages;

        public int Unread
        {
            get
            {
                var n = 0;
                foreach (var m in _messages) if (!m.Read) n++;
                return n;
            }
        }

        public void Receive(PhoneMessage message)
        {
            if (message != null && !string.IsNullOrEmpty(message.Text)) _messages.Insert(0, message); // le plus récent en haut
        }

        public void MarkAllRead()
        {
            foreach (var m in _messages) m.Read = true;
        }

        /// <summary>Libellé du bouton : « Téléphone » ou « Téléphone (2) ».</summary>
        public string ButtonLabel => Unread > 0 ? $"Téléphone ({Unread})" : "Téléphone";
    }

    /// <summary>Messages qui arrivent le matin, tirés de la sauvegarde (le bilan de la veille) : aucune donnée de plus à sauvegarder.</summary>
    public static class PhoneMessages
    {
        public static List<PhoneMessage> Morning(PhoneConfig phone, SaveData data, OwnerConfig owner, Random random)
        {
            var list = new List<PhoneMessage>();
            if (phone == null || !phone.enabled || data?.career?.history == null || data.career.history.Length == 0) return list;
            var last = data.career.history[data.career.history.Length - 1];
            var day = data.career.dayNumber;
            string text = null;
            if (last.vehicleSeized && !data.career.fired) text = Pick(phone.ownerImpound, random);
            else if (last.versementMissing > 0)
            {
                owner = owner ?? new OwnerConfig();
                var recent = 0;
                foreach (var d in data.career.missedVersementDays ?? Array.Empty<int>())
                    if (d <= last.dayNumber && d > last.dayNumber - Math.Max(1, owner.windowDays)) recent++;
                // Comme le bilan : encore « left » manques et il reprend le taxi (sans la tolérance liée à la réputation).
                var left = Math.Max(1, owner.missesBeforeFired - recent);
                text = left <= 1 ? Pick(phone.ownerLastWarning, random) : Pick(phone.ownerThreat, random);
                text = text?.Replace("{manque}", Day.DaySummary.Money(last.versementMissing)).Replace("{reste}", left.ToString());
            }
            else if (phone.thanksEveryDays > 0 && last.dayNumber % phone.thanksEveryDays == 0) text = Pick(phone.ownerThanks, random);
            if (!string.IsNullOrEmpty(text))
                list.Add(new PhoneMessage { From = PhoneSender.Owner, FromName = phone.ownerName, Text = text, Day = day });
            return list;
        }

        private static string Pick(string[] options, Random random) =>
            options == null || options.Length == 0 ? null : options[random.Next(options.Length)];
    }
}
