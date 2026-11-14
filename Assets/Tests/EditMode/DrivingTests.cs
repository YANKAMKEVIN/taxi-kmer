using NUnit.Framework;
using TaxiKmer.Core.Driving;
using TaxiKmer.Core.World;

namespace TaxiKmer.Core.Tests
{
    public class DriveLogicTests
    {
        private const float Dt = 0.02f;
        private static DriveInput Accel => new DriveInput { Accelerate = true };
        private static DriveInput BrakePedal => new DriveInput { Brake = true };

        private static DriveLogic NewLogic() => new DriveLogic(new DriveConfig());

        [Test]
        public void Accelerate_FromRest_FullThrottleForward()
        {
            var cmd = NewLogic().Step(Accel, 0f, Dt);
            Assert.AreEqual(1f, cmd.Throttle);
            Assert.AreEqual(0f, cmd.Brake);
            Assert.AreEqual(Gear.Forward, cmd.Gear);
        }

        [Test]
        public void Accelerate_Limiter_CutsThrottleAtMaxSpeed()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            var max = cfg.pedals.maxForwardKmh / 3.6f;
            Assert.AreEqual(0f, logic.Step(Accel, max, Dt).Throttle, 1e-4);
            var mid = logic.Step(Accel, max * (1f - cfg.pedals.limiterBand / 2f), Dt).Throttle;
            Assert.AreEqual(0.5f, mid, 1e-3);
        }

        [Test]
        public void Brake_WhileMoving_Brakes()
        {
            var cmd = NewLogic().Step(BrakePedal, 10f, Dt);
            Assert.AreEqual(1f, cmd.Brake);
            Assert.AreEqual(0f, cmd.Throttle);
        }

        [Test]
        public void Brake_HeldAtStop_SwitchesToReverseAfterDelay()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            var cmd = logic.Step(BrakePedal, 0f, Dt);
            Assert.AreEqual(1f, cmd.Brake, "D'abord on tient le frein.");
            Assert.AreEqual(Gear.Forward, cmd.Gear);
            for (var t = 0f; t < cfg.pedals.reverseHoldSeconds; t += Dt) cmd = logic.Step(BrakePedal, 0f, Dt);
            Assert.AreEqual(Gear.Reverse, cmd.Gear);
            Assert.AreEqual(-1f, cmd.Throttle);
            Assert.AreEqual(0f, cmd.Brake);
        }

        [Test]
        public void Reverse_LimitedToMaxReverseSpeed()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            for (var i = 0; i < 50; i++) logic.Step(BrakePedal, 0f, Dt);
            var cmd = logic.Step(BrakePedal, -cfg.pedals.maxReverseKmh / 3.6f, Dt);
            Assert.AreEqual(0f, cmd.Throttle, 1e-4);
        }

        [Test]
        public void Accelerate_WhileRollingBackward_BrakesFirstThenForward()
        {
            var logic = NewLogic();
            for (var i = 0; i < 50; i++) logic.Step(BrakePedal, 0f, Dt);
            var cmd = logic.Step(Accel, -3f, Dt);
            Assert.AreEqual(1f, cmd.Brake);
            Assert.AreEqual(Gear.Forward, cmd.Gear);
            cmd = logic.Step(Accel, 0f, Dt);
            Assert.AreEqual(1f, cmd.Throttle);
        }

        [Test]
        public void BrakeTap_ThenRelease_DoesNotReverse()
        {
            var logic = NewLogic();
            logic.Step(BrakePedal, 0f, Dt);
            var cmd = logic.Step(default, 0f, Dt);
            Assert.AreEqual(Gear.Forward, cmd.Gear);
            cmd = logic.Step(BrakePedal, 0f, Dt);
            Assert.AreEqual(1f, cmd.Brake, "Le chrono de marche arrière repart de zéro.");
        }

        [Test]
        public void NoPedal_AtStop_HoldsParkingBrake_AndCoastsWhenMoving()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            Assert.AreEqual(cfg.pedals.parkingBrake, logic.Step(default, 0f, Dt).Brake);
            var rolling = logic.Step(default, 10f, Dt);
            Assert.AreEqual(0f, rolling.Brake);
            Assert.AreEqual(0f, rolling.Throttle);
        }

        [Test]
        public void BothPedals_Brakes()
        {
            var cmd = NewLogic().Step(new DriveInput { Accelerate = true, Brake = true }, 5f, Dt);
            Assert.AreEqual(1f, cmd.Brake);
            Assert.AreEqual(0f, cmd.Throttle);
        }

        [Test]
        public void Steering_RampsUp_AndReturnsToCenter()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            var right = new DriveInput { Right = true };
            var cmd = logic.Step(right, 0f, 0.1f);
            Assert.AreEqual(cfg.steering.riseRate * 0.1f, cmd.Steer, 1e-4);
            for (var i = 0; i < 20; i++) cmd = logic.Step(right, 0f, 0.1f);
            Assert.AreEqual(1f, cmd.Steer);
            Assert.AreEqual(cfg.steering.maxAngleDeg, cmd.SteerAngleDeg, 1e-3);
            cmd = logic.Step(default, 0f, 0.1f);
            Assert.AreEqual(1f - cfg.steering.returnRate * 0.1f, cmd.Steer, 1e-4);
            for (var i = 0; i < 20; i++) cmd = logic.Step(default, 0f, 0.1f);
            Assert.AreEqual(0f, cmd.Steer);
        }

        [Test]
        public void Steering_LeftIsNegative_BothArrowsCancel()
        {
            var logic = NewLogic();
            Assert.Less(logic.Step(new DriveInput { Left = true }, 0f, 0.1f).Steer, 0f);
            var both = NewLogic().Step(new DriveInput { Left = true, Right = true }, 0f, 0.1f);
            Assert.AreEqual(0f, both.Steer);
        }

        [Test]
        public void Steering_AngleShrinksWithSpeed()
        {
            var cfg = new DriveConfig();
            var logic = new DriveLogic(cfg);
            var right = new DriveInput { Right = true };
            for (var i = 0; i < 20; i++) logic.Step(right, 0f, 0.1f);
            var fast = logic.Step(right, cfg.steering.highSpeedKmh / 3.6f, 0.1f);
            Assert.AreEqual(cfg.steering.highSpeedAngleDeg, fast.SteerAngleDeg, 1e-3);
            var faster = logic.Step(right, 2f * cfg.steering.highSpeedKmh / 3.6f, 0.1f);
            Assert.AreEqual(cfg.steering.highSpeedAngleDeg, faster.SteerAngleDeg, 1e-3, "Borné au-delà.");
        }
    }

    public class ControlLayoutTests
    {
        // Galaxy S24 en paysage.
        private const float W = 2340f, H = 1080f;

        private static (float x, float y) Center(ControlZone z)
        {
            var r = z.Resolve(W, H);
            return (r.X + r.Width / 2f, r.Y + r.Height / 2f);
        }

        [Test]
        public void Default_EachZoneCenter_MapsToItsControl()
        {
            var layout = ControlLayout.Default();
            Assert.AreEqual(7, layout.zones.Length);
            foreach (var z in layout.zones)
            {
                var (x, y) = Center(z);
                Assert.AreEqual(z.control, layout.ControlAt(x, y, W, H), z.control.ToString());
            }
        }

        [Test]
        public void Default_ArrowsLeft_PedalsRight_HornTopRight()
        {
            var layout = ControlLayout.Default();
            Assert.AreEqual(DriveControl.SteerLeft, layout.ControlAt(0.15f * H, 0.2f * H, W, H));
            Assert.AreEqual(DriveControl.Accelerate, layout.ControlAt(W - 0.1f * H, 0.3f * H, W, H));
            Assert.AreEqual(DriveControl.Horn, layout.ControlAt(W - 0.1f * H, H - 0.15f * H, W, H));
            Assert.AreEqual(DriveControl.None, layout.ControlAt(W / 2f, H / 2f, W, H), "Le centre sert à tourner la caméra.");
            Assert.AreEqual(DriveControl.Action, layout.ControlAt(W / 2f, H * 0.85f, W, H), "Bouton contextuel en haut au centre.");
            Assert.AreEqual(DriveControl.None, layout.ControlAt(W / 2f, H * 0.85f, W, H, actionVisible: false), "Caché : on peut y glisser pour la caméra.");
            Assert.AreEqual(DriveControl.Quality, layout.ControlAt(0.2f * H, H - 0.4f * H, W, H), "Qualité à gauche, sous le texte d'état.");
        }

        [Test]
        public void Default_ZonesDoNotOverlap_AndStayOnScreen()
        {
            foreach (var (w, h) in new[] { (W, H), (1920f, 1080f), (1280f, 800f) })
            {
                var zones = ControlLayout.Default().zones;
                for (var i = 0; i < zones.Length; i++)
                {
                    var a = zones[i].Resolve(w, h);
                    Assert.GreaterOrEqual(a.X, 0f); Assert.GreaterOrEqual(a.Y, 0f);
                    Assert.LessOrEqual(a.X + a.Width, w); Assert.LessOrEqual(a.Y + a.Height, h);
                    for (var j = i + 1; j < zones.Length; j++)
                    {
                        var b = zones[j].Resolve(w, h);
                        var overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                        Assert.IsFalse(overlap, $"{zones[i].control} / {zones[j].control} à {w}×{h}");
                    }
                }
            }
        }

        [Test]
        public void Zone_KeepsSquareShape_OnWideScreens()
        {
            var z = new ControlZone { corner = ScreenCorner.TopRight, side = 0.1f, edge = 0.1f, width = 0.2f, height = 0.2f };
            var r = z.Resolve(2000f, 1000f);
            Assert.AreEqual(r.Width, r.Height, 1e-3);
            Assert.AreEqual(2000f - 100f - 200f, r.X, 1e-3);
            Assert.AreEqual(1000f - 100f - 200f, r.Y, 1e-3);
        }
    }

    public class ChaseCameraLogicTests
    {
        [Test]
        public void Drag_RotatesYawAndPitch_PitchClamped()
        {
            var cfg = new ChaseCameraConfig();
            var cam = new ChaseCameraLogic(cfg);
            cam.Drag(0.1f, 0f);
            Assert.AreEqual(0.1f * cfg.dragDegreesPerScreen, cam.YawOffsetDeg, 1e-3);
            cam.Drag(0f, -10f);
            Assert.AreEqual(cfg.maxPitchDeg, cam.PitchDeg);
            cam.Drag(0f, 10f);
            Assert.AreEqual(cfg.minPitchDeg, cam.PitchDeg);
        }

        [Test]
        public void Yaw_WrapsAround()
        {
            var cfg = new ChaseCameraConfig { dragDegreesPerScreen = 100f };
            var cam = new ChaseCameraLogic(cfg);
            cam.Drag(2f, 0f); // 200° → -160°
            Assert.AreEqual(-160f, cam.YawOffsetDeg, 1e-3);
        }

        [Test]
        public void Recenters_OnlyAfterDelay()
        {
            var cfg = new ChaseCameraConfig();
            var cam = new ChaseCameraLogic(cfg);
            cam.Drag(0.2f, 0.05f);
            var yaw = cam.YawOffsetDeg;
            cam.Update(cfg.recenterDelaySeconds * 0.5f, false);
            Assert.AreEqual(yaw, cam.YawOffsetDeg, "Pas encore.");
            cam.Update(cfg.recenterDelaySeconds, false);
            for (var i = 0; i < 200; i++) cam.Update(0.05f, false);
            Assert.AreEqual(0f, cam.YawOffsetDeg);
            Assert.AreEqual(cfg.defaultPitchDeg, cam.PitchDeg);
        }

        [Test]
        public void Dragging_PreventsRecenter()
        {
            var cfg = new ChaseCameraConfig();
            var cam = new ChaseCameraLogic(cfg);
            cam.Drag(0.2f, 0f);
            for (var i = 0; i < 200; i++) cam.Update(0.05f, true);
            Assert.AreEqual(0.2f * cfg.dragDegreesPerScreen, cam.YawOffsetDeg, 1e-3);
        }
    }

    public class RouteProjectionTests
    {
        [Test]
        public void DistanceAlong_ProjectsOntoNearestSegment()
        {
            var xz = new float[] { 0, 0, 100, 0, 100, 50 };
            var cum = WorldGeometry.CumulativeLengths(xz);
            Assert.AreEqual(30f, WorldGeometry.DistanceAlong(xz, cum, 30f, 5f), 1e-3);
            Assert.AreEqual(120f, WorldGeometry.DistanceAlong(xz, cum, 104f, 20f), 1e-3);
            Assert.AreEqual(0f, WorldGeometry.DistanceAlong(xz, cum, -20f, 3f), 1e-3);
            Assert.AreEqual(150f, WorldGeometry.DistanceAlong(xz, cum, 100f, 80f), 1e-3);
        }
    }
}
