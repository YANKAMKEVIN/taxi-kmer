using NUnit.Framework;
using TaxiKmer.Core.Ux;

namespace TaxiKmer.Core.Tests
{
    public class PhotoModeTests
    {
        [Test]
        public void Menu_PhotoFromPauseOnly_TimeStopped_BackToPause()
        {
            var menu = new GameMenu(false);
            Assert.IsFalse(menu.OpenPhoto(), "seulement depuis la pause");
            menu.Pause();
            Assert.IsTrue(menu.OpenPhoto());
            Assert.AreEqual(MenuScreen.Photo, menu.Screen);
            Assert.IsTrue(menu.Frozen);
            Assert.IsTrue(menu.TimeStopped, "heure figée");
            menu.Back();
            Assert.AreEqual(MenuScreen.Pause, menu.Screen);
        }

        [Test]
        public void Camera_MovesWhereItLooks_StaysNearTheTaxiAndAboveGround()
        {
            var cam = new PhotoCamera(0f, 3f, 0f, 90f, 10f, 0f, 0f); // regarde vers l'est
            cam.Move(1f, 0f, 0f, 1f);
            Assert.AreEqual(PhotoCamera.SpeedMs, cam.X, 1e-3f);
            Assert.AreEqual(0f, cam.Z, 1e-3f);
            cam.Move(0f, 0f, -1f, 10f);
            Assert.AreEqual(PhotoCamera.MinHeight, cam.Y, "pas sous le sol");
            for (var i = 0; i < 100; i++) cam.Move(1f, 0f, 0f, 1f);
            Assert.AreEqual(PhotoCamera.MaxDistance, cam.X, 1e-2f, "reste dans la bulle autour du taxi");
        }

        [Test]
        public void Camera_LookClampsPitch_AndNamesPhotos()
        {
            var cam = new PhotoCamera(0f, 3f, 0f, 0f, 0f, 0f, 0f);
            cam.Look(0.5f, 0f);
            Assert.AreEqual(90f, cam.YawDeg, 1e-3f);
            cam.Look(0f, -5f);
            Assert.AreEqual(80f, cam.PitchDeg, 1e-3f);
            Assert.AreEqual("taxi-kmer-jour-12-0715.png", PhotoCamera.FileName(12, "07:15", 0));
            Assert.AreEqual("taxi-kmer-jour-12-0715-2.png", PhotoCamera.FileName(12, "07:15", 2));
        }
    }
}
