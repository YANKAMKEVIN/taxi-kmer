using System;
using NUnit.Framework;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Phone;
using TaxiKmer.Core.Save;

namespace TaxiKmer.Core.Tests
{
    public class PhoneTests
    {
        private static SaveData After(int day, int missing, bool seized = false, params int[] missedDays)
        {
            var d = new SaveData();
            d.career.dayNumber = day + 1;
            d.career.missedVersementDays = missedDays;
            d.career.history = new[] { new DayRecordData { dayNumber = day, versementMissing = missing, vehicleSeized = seized } };
            return d;
        }

        [Test]
        public void FirstDay_NoMessage()
        {
            CollectionAssert.IsEmpty(PhoneMessages.Morning(new PhoneConfig(), new SaveData(), new OwnerConfig(), new Random(1)));
        }

        [Test]
        public void MissedVersement_OwnerThreatens_WithAmountAndMissesLeft()
        {
            var phone = new PhoneConfig { ownerThreat = new[] { "Manque {manque}, encore {reste}." } };
            var m = PhoneMessages.Morning(phone, After(4, 1500, false, 4), new OwnerConfig(), new Random(1));
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(PhoneSender.Owner, m[0].From);
            Assert.AreEqual("Manque 1 500 FCFA, encore 2.", m[0].Text);
            Assert.AreEqual(5, m[0].Day);
        }

        [Test]
        public void SecondMiss_IsTheLastWarning_AndImpoundHasItsOwnMessage()
        {
            var phone = new PhoneConfig();
            var last = PhoneMessages.Morning(phone, After(6, 500, false, 2, 6), new OwnerConfig(), new Random(1));
            Assert.AreEqual(phone.ownerLastWarning[0], last[0].Text);
            var impound = PhoneMessages.Morning(phone, After(6, 0, true), new OwnerConfig(), new Random(1));
            Assert.AreEqual(phone.ownerImpound[0], impound[0].Text);
        }

        [Test]
        public void PaidVersement_ThanksOnlySometimes_AndNothingWhenDisabled()
        {
            var phone = new PhoneConfig { thanksEveryDays = 3 };
            Assert.AreEqual(1, PhoneMessages.Morning(phone, After(3, 0), new OwnerConfig(), new Random(1)).Count);
            Assert.AreEqual(0, PhoneMessages.Morning(phone, After(4, 0), new OwnerConfig(), new Random(1)).Count);
            phone.enabled = false;
            Assert.AreEqual(0, PhoneMessages.Morning(phone, After(4, 900, false, 4), new OwnerConfig(), new Random(1)).Count);
        }

        [Test]
        public void Inbox_CountsUnread_NewestFirst()
        {
            var inbox = new PhoneInbox();
            Assert.AreEqual("Téléphone", inbox.ButtonLabel);
            inbox.Receive(new PhoneMessage { Text = "a" });
            inbox.Receive(new PhoneMessage { Text = "b" });
            inbox.Receive(new PhoneMessage { Text = "" });
            Assert.AreEqual(2, inbox.Unread);
            Assert.AreEqual("b", inbox.Messages[0].Text);
            Assert.AreEqual("Téléphone (2)", inbox.ButtonLabel);
            inbox.MarkAllRead();
            Assert.AreEqual(0, inbox.Unread);
        }
    }
}
