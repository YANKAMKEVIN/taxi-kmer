using System;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Mise en scène du bilan du jour : les lignes (recettes, versement, carburant, puis les dépenses imprévues) tombent
    /// une à une avec leur montant qui défile, puis le total (l'épargne), puis un tampon (« VERSEMENT COUVERT »,
    /// « IL MANQUE … », « TAXI REPRIS »…) frappe le total. Un appui
    /// pendant l'animation la termine d'un coup ; « Nouvelle journée » n'est actif qu'ensuite.
    /// </summary>
    public sealed class SummaryReveal
    {
        /// <summary>Lignes d'un bilan sans dépense imprévue : recettes, versement, carburant.</summary>
        public const int DefaultLines = 3;
        public const float FirstLine = 0.3f;
        public const float LineGap = 0.45f;
        /// <summary>Toutes les lignes tombent en au plus … s : avec des dépenses imprévues, l'écart se resserre.</summary>
        public const float MaxLinesSeconds = 1.8f;
        public const float DropSeconds = 0.25f;
        public const float CountSeconds = 0.45f;
        public const float StampDelay = 0.75f;
        public const float StampSeconds = 0.22f;
        public const float NoteDelay = 0.35f;
        public const float ReadyDelay = 0.2f;

        /// <summary>Nombre de lignes du bilan (3 et plus : recettes, versement, carburant, puis les dépenses imprévues).</summary>
        public int Lines { get; }
        /// <summary>Écart entre deux lignes (s).</summary>
        public float Gap { get; }
        public float TotalAt => FirstLine + Lines * Gap + 0.15f;
        public float StampAt => TotalAt + StampDelay;
        public float NoteAt => StampAt + NoteDelay;
        public float ReadyAt => NoteAt + ReadyDelay;

        public SummaryReveal(int lines = DefaultLines)
        {
            Lines = Math.Max(1, lines);
            Gap = Math.Min(LineGap, MaxLinesSeconds / Lines);
        }

        public float Time { get; private set; }

        public void Advance(float dt) => Time += Math.Max(0f, dt);

        /// <summary>Appui pendant l'animation : tout s'affiche tout de suite.</summary>
        public void Finish() => Time = Math.Max(Time, ReadyAt);

        public bool Done => Time >= ReadyAt;

        /// <summary>Chute d'une ligne, de 0 (pas encore là) à 1 (posée).</summary>
        public float LineDrop(int i) => Ramp(Time, LineStart(i), DropSeconds);

        /// <summary>Défilement du montant d'une ligne, de 0 à 1.</summary>
        public float LineCount(int i) => Ease(Ramp(Time, LineStart(i) + DropSeconds * 0.5f, CountSeconds));

        public float TotalDrop => Ramp(Time, TotalAt, DropSeconds);
        public float TotalCount => Ease(Ramp(Time, TotalAt + DropSeconds * 0.5f, CountSeconds * 1.3f));

        /// <summary>Taille du tampon : 0 avant, de 2,2 à 1 pendant qu'il frappe, puis 1.</summary>
        public float StampScale
        {
            get
            {
                if (Time < StampAt) return 0f;
                var k = Ramp(Time, StampAt, StampSeconds);
                return 2.2f - 1.2f * Ease(k);
            }
        }

        public bool NoteVisible => Time >= NoteAt;

        /// <summary>Montant affiché pendant le défilement (arrondi à la dizaine tant qu'il défile, exact à la fin).</summary>
        public static int Partial(int amount, float fraction)
        {
            if (fraction >= 1f) return amount;
            if (fraction <= 0f) return 0;
            return (int)(Math.Round(amount * fraction / 10.0) * 10);
        }

        /// <summary>Texte du tampon d'après la seule journée : « VERSEMENT COUVERT » ou « IL MANQUE 2 000 FCFA ».</summary>
        public static string StampText(DaySummary s)
            => s.VersementReached ? "VERSEMENT COUVERT" : $"IL MANQUE {DaySummary.Money(s.VersementShortfall)}";

        /// <summary>
        /// Texte du tampon d'après la clôture de la carrière (épargne comprise) : « TAXI REPRIS » après un renvoi,
        /// « IL MANQUE … » si le versement reste incomplet, « COUVERT PAR L'ÉPARGNE » si l'épargne l'a complété,
        /// sinon « VERSEMENT COUVERT ».
        /// </summary>
        public static string StampText(DayClosure c)
        {
            if (c.Mood == OwnerMood.Fired) return "TAXI REPRIS";
            if (c.VersementMissing > 0) return $"IL MANQUE {DaySummary.Money(c.VersementMissing)}";
            return c.TakenFromSavings > 0 ? "COUVERT PAR L'ÉPARGNE" : "VERSEMENT COUVERT";
        }

        /// <summary>Tampon « bonne nouvelle » (vert) : le versement est remis en entier et le chauffeur garde son taxi.</summary>
        public static bool StampPositive(DayClosure c) => c.Mood != OwnerMood.Fired && c.VersementMissing == 0;

        private float LineStart(int i) => FirstLine + Math.Max(0, Math.Min(Lines - 1, i)) * Gap;

        private static float Ramp(float t, float start, float length) => Math.Max(0f, Math.Min(1f, (t - start) / length));

        private static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
