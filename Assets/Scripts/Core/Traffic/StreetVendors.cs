using System;
using System.Collections.Generic;

namespace TaxiKmer.Core.Traffic
{
    [Serializable]
    public class VendorGoodsData
    {
        public string id = "";
        /// <summary>Ce que dit le vendeur et que reprend le passager (« de l'eau fraîche »).</summary>
        public string label = "";
        public int price;
    }

    /// <summary>
    /// Vendeurs ambulants aux feux (idée n° 24, à valider par Kevin ; données : Assets/Resources/Data/vendors.json) :
    /// quand le taxi attend arrêté juste avant un feu de Messassi ou de Borne Fontaine, un vendeur s'approche (eau,
    /// arachides, crédit de téléphone) ; un passager peut acheter et en parler. Valeurs par défaut de Claude.
    /// </summary>
    [Serializable]
    public class VendorsConfig
    {
        public bool enabled = true;
        /// <summary>Feux où se tiennent les vendeurs (ids de tools/osm/traffic_lights_messassi_etoudi.json).</summary>
        public int[] lightIds = { 1, 4 };
        /// <summary>Le taxi compte comme « au feu » à moins de cette distance avant lui (m, le long de l'itinéraire).</summary>
        public float approachMeters = 35f;
        /// <summary>Arrêté : sous cette vitesse (km/h), depuis waitSeconds secondes.</summary>
        public float stoppedKmh = 3f;
        public float waitSeconds = 2.5f;
        /// <summary>Le vendeur reste à la fenêtre au plus tant de secondes (ou jusqu'au départ du taxi).</summary>
        public float staySeconds = 8f;
        /// <summary>Chance qu'un passager achète (au moins un passager à bord).</summary>
        public float buyChance = 0.35f;
        /// <summary>Un même feu n'envoie pas de vendeur plus souvent que ça (s).</summary>
        public float cooldownSeconds = 90f;
        public VendorGoodsData[] goods =
        {
            new VendorGoodsData { id = "eau", label = "de l'eau fraîche", price = 100 },
            new VendorGoodsData { id = "arachides", label = "des arachides grillées", price = 100 },
            new VendorGoodsData { id = "credit", label = "du crédit de téléphone", price = 500 },
            new VendorGoodsData { id = "beignets", label = "des beignets", price = 50 },
        };
    }

    public struct VendorEvent
    {
        /// <summary>Un vendeur arrive à la fenêtre à cette image.</summary>
        public bool Arrived;
        /// <summary>Le vendeur repart (le taxi démarre, ou il a attendu assez).</summary>
        public bool Left;
        /// <summary>Un passager achète (à l'arrivée du vendeur).</summary>
        public bool Bought;
        public VendorGoodsData Goods;
        public int LightId;
    }

    /// <summary>Le vendeur du feu où attend le taxi (pur, testable).</summary>
    public sealed class StreetVendors
    {
        private readonly VendorsConfig _config;
        private readonly Random _random;
        private readonly Dictionary<int, float> _lastVisit = new Dictionary<int, float>();
        private float _time, _stopped, _atWindow;

        /// <summary>Feu dont le vendeur est à la fenêtre (-1 : aucun).</summary>
        public int VendorAt { get; private set; } = -1;
        public VendorGoodsData Offer { get; private set; }

        public StreetVendors(VendorsConfig config, Random random)
        {
            _config = config ?? new VendorsConfig();
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <param name="lights">Feux du tronçon : id et distance le long de l'itinéraire.</param>
        /// <param name="direction">Sens du taxi (+1 vers Étoudi, -1 vers Messassi).</param>
        public VendorEvent Update(float dt, float along, int direction, float speedKmh, IReadOnlyList<(int id, float along)> lights, int passengers)
        {
            var e = new VendorEvent { LightId = -1 };
            if (!_config.enabled || dt <= 0f) return e;
            _time += dt;
            var stopped = Math.Abs(speedKmh) < _config.stoppedKmh;
            _stopped = stopped ? _stopped + dt : 0f;

            if (VendorAt >= 0)
            {
                _atWindow += dt;
                if (!stopped || _atWindow >= _config.staySeconds)
                {
                    e.Left = true;
                    e.LightId = VendorAt;
                    VendorAt = -1;
                    Offer = null;
                }
                return e;
            }
            if (_stopped < _config.waitSeconds) return e;
            var light = LightAhead(along, direction, lights);
            if (light < 0 || Array.IndexOf(_config.lightIds ?? Array.Empty<int>(), light) < 0) return e;
            if (_lastVisit.TryGetValue(light, out var last) && _time - last < _config.cooldownSeconds) return e;
            var goods = _config.goods ?? Array.Empty<VendorGoodsData>();
            if (goods.Length == 0) return e;
            _lastVisit[light] = _time;
            VendorAt = light;
            _atWindow = 0f;
            Offer = goods[_random.Next(goods.Length)];
            e.Arrived = true;
            e.LightId = light;
            e.Goods = Offer;
            e.Bought = passengers > 0 && _random.NextDouble() < _config.buyChance;
            return e;
        }

        /// <summary>Feu juste devant le taxi dans son sens, à moins de approachMeters ; -1 sinon.</summary>
        public int LightAhead(float along, int direction, IReadOnlyList<(int id, float along)> lights)
        {
            var best = -1;
            var bestGap = float.MaxValue;
            foreach (var (id, at) in lights ?? Array.Empty<(int, float)>())
            {
                var gap = (at - along) * (direction >= 0 ? 1 : -1);
                if (gap < 0f || gap > _config.approachMeters || gap >= bestGap) continue;
                best = id;
                bestGap = gap;
            }
            return best;
        }

        /// <summary>Message à l'écran quand le vendeur arrive.</summary>
        public static string Message(VendorEvent e) =>
            e.Goods == null ? "" : $"Un vendeur propose {e.Goods.label} à {Day.DaySummary.Money(e.Goods.price)}"
                + (e.Bought ? " : un passager achète" : "");
    }
}
