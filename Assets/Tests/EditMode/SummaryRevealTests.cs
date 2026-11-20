using NUnit.Framework;
using TaxiKmer.Core.Day;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class SummaryRevealTests
    {
        [Test]
        public void LinesFallOneByOne_ThenTotal_ThenStamp()
        {
            var r = new SummaryReveal();
            Assert.AreEqual(0f, r.LineDrop(0));
            r.Advance(SummaryReveal.FirstLine + SummaryReveal.DropSeconds);
            Assert.AreEqual(1f, r.LineDrop(0), "Première ligne posée.");
            Assert.AreEqual(0f, r.LineDrop(1), "La deuxième attend.");
            Assert.Less(r.LineCount(0), 1f, "Son montant défile encore.");
            Assert.AreEqual(0f, r.TotalDrop);
            Assert.AreEqual(0f, r.StampScale);

            r.Advance(SummaryReveal.TotalAt);
            Assert.AreEqual(1f, r.LineDrop(2));
            Assert.AreEqual(1f, r.LineCount(2), 1e-6f);
            Assert.Greater(r.TotalDrop, 0f);
            Assert.IsFalse(r.Done);

            r.Advance(SummaryReveal.StampAt - r.Time + 0.01f);
            Assert.Greater(r.StampScale, 1.5f, "Le tampon arrive en grand…");
            r.Advance(SummaryReveal.StampSeconds);
            Assert.AreEqual(1f, r.StampScale, 1e-6f, "… et se pose.");
            r.Advance(1f);
            Assert.IsTrue(r.NoteVisible);
            Assert.IsTrue(r.Done);
        }

        [Test]
        public void Finish_ShowsEverythingAtOnce()
        {
            var r = new SummaryReveal();
            r.Advance(0.1f);
            r.Finish();
            Assert.IsTrue(r.Done);
            Assert.AreEqual(1f, r.LineCount(0), 1e-6f);
            Assert.AreEqual(1f, r.TotalCount, 1e-6f);
            Assert.AreEqual(1f, r.StampScale, 1e-6f);
        }

        [Test]
        public void Partial_CountsUpToTheExactAmount()
        {
            Assert.AreEqual(0, SummaryReveal.Partial(3360, 0f));
            Assert.AreEqual(1680, SummaryReveal.Partial(3360, 0.5f));
            Assert.AreEqual(3360, SummaryReveal.Partial(3360, 1f));
        }

        [Test]
        public void Stamp_SaysCoveredOrHowMuchIsMissing()
        {
            var ledger = new DayLedger(new DayConfig());
            ledger.AddFare(8000);
            Assert.AreEqual("IL MANQUE 2 000 FCFA", SummaryReveal.StampText(DaySummary.From(ledger)));
            ledger.AddFare(2500);
            Assert.AreEqual("VERSEMENT COUVERT", SummaryReveal.StampText(DaySummary.From(ledger)));
        }
    }
}
