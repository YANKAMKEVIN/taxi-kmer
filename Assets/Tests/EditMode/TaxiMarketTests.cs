using NUnit.Framework;
using TaxiKmer.Core.Career;

namespace TaxiKmer.Core.Tests
{
    public class TaxiMarketTests
    {
        [Test]
        public void Loan_DownPaymentAndDailyRepayment()
        {
            var loan = new LoanData();
            Assert.AreEqual(600000, TaxiPurchase.DownPayment(loan, 2000000));
            Assert.AreEqual(28000, TaxiPurchase.DailyRepayment(loan, 1400000), "1 400 000 + 20 % sur 60 jours.");
            Assert.AreEqual(0, TaxiPurchase.DailyRepayment(loan, 0));
            Assert.IsTrue(TaxiPurchase.CanBuyWithLoan(loan, 600000, 2000000));
            Assert.IsFalse(TaxiPurchase.CanBuyWithLoan(loan, 599999, 2000000));
            Assert.IsFalse(TaxiPurchase.CanBuyCash(600000, 2000000));
        }

        [Test]
        public void DaysToSave()
        {
            Assert.AreEqual(0, TaxiPurchase.DaysToSave(5000, 4000, 100));
            Assert.AreEqual(10, TaxiPurchase.DaysToSave(0, 20000, 2000));
            Assert.AreEqual(11, TaxiPurchase.DaysToSave(0, 20001, 2000));
            Assert.AreEqual(-1, TaxiPurchase.DaysToSave(0, 20000, 0), "Sans épargne quotidienne, jamais.");
        }

        [Test]
        public void OwnerDay_NoVersement_ButChargesAreHis()
        {
            var market = new TaxiMarketConfig { maintenancePerKm = 50f };
            Assert.AreEqual(3000 - 800 - 300 - 1000 - 200, TaxiPurchase.OwnerDayNet(market, 3000, 800, 6f, 1000, 200));
        }

        [Test]
        public void Offers_SortedByPrice()
        {
            var market = new TaxiMarketConfig
            {
                offers = new[]
                {
                    new VehicleOfferData { id = "neuf", price = 9000000 },
                    new VehicleOfferData { id = "occasion", used = true, condition = 55, price = 2500000 },
                },
            };
            Assert.AreEqual("occasion", TaxiPurchase.Sorted(market)[0].id);
            Assert.IsEmpty(TaxiPurchase.Sorted(new TaxiMarketConfig()));
        }
    }
}
