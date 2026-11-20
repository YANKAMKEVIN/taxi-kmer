using System;
using TaxiKmer.Core.Day;

namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Mise en scène du bilan du jour : les lignes (recettes, versement, carburant) tombent une à une avec leur montant
    /// qui défile, puis le reste, puis un tampon « VERSEMENT COUVERT » ou « IL MANQUE … » frappe le total. Un appui
    /// pendant l'animation la termine d'un coup ; « Nouvelle journée » n'est actif qu'ensuite.
    /// </summary>
    public sealed class SummaryReveal
    {
        public const int Lines = 3;
        public const float FirstLine = 0.3f;
        public const float LineGap = 0.45f;
        public const float DropSeconds = 0.25f;
        public const float CountSeconds = 0.45f;
        public const float TotalAt = FirstLine + Lines * LineGap + 0.15f;
        public const float StampAt = TotalAt + 0.75f;
        public const float StampSeconds = 0.22f;
        public const float NoteAt = StampAt + 0.35f;
        public const float ReadyAt = NoteAt + 0.2f;

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

        /// <summary>Texte du tampon : « VERSEMENT COUVERT » ou « IL MANQUE 2 000 FCFA ».</summary>
        public static string StampText(DaySummary s)
            => s.VersementReached ? "VERSEMENT COUVERT" : $"IL MANQUE {DaySummary.Money(s.VersementShortfall)}";

        private static float LineStart(int i) => FirstLine + Math.Max(0, Math.Min(Lines - 1, i)) * LineGap;

        private static float Ramp(float t, float start, float length) => Math.Max(0f, Math.Min(1f, (t - start) / length));

        private static float Ease(float x) => 1f - (1f - x) * (1f - x);
    }
}
