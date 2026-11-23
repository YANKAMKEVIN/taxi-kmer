namespace TaxiKmer.Core.Ux
{
    /// <summary>
    /// Choix qui attendent la réponse du joueur (décision de Kevin, 23/11) : tant qu'il y en a un, « Terminer la journée »
    /// est refusé, sinon son effet (amende, dédommagement, course…) serait perdu.
    /// </summary>
    public struct PendingChoices
    {
        public bool PoliceControl;
        /// <summary>Accrochage : s'arrêter ou partir, poursuivre ou laisser filer.</summary>
        public bool Crash;
        /// <summary>Contre-offre d'un client (négociation).</summary>
        public bool Haggle;
        /// <summary>Passager qui refuse de payer.</summary>
        public bool Refusal;
        /// <summary>Panne sèche : pousser ou appeler le technicien.</summary>
        public bool DryTank;
        /// <summary>Crevaison : changer la roue.</summary>
        public bool FlatTyre;
        /// <summary>Panne : appeler un mécanicien.</summary>
        public bool BrokenDown;

        /// <summary>Choix en attente lus dans la logique du jeu (la présentation ajoute le contrôle de police et l'accrochage).</summary>
        public static PendingChoices Of(Rides.Street street, Rides.TaxiOnRoute taxi, bool policeControl = false,
            Road.CrashChoice crash = Road.CrashChoice.None, Road.VehicleCondition car = null)
        {
            var finished = street == null || street.DayFinished;
            return new PendingChoices
            {
                PoliceControl = policeControl && !finished,
                Crash = crash != Road.CrashChoice.None && !finished,
                Haggle = !finished && street.PendingHaggle != null,
                Refusal = !finished && street.PendingRefusal != null,
                DryTank = !finished && street.DryTankChoice(taxi),
                FlatTyre = !finished && car != null && car.FlatTyre && !car.BrokenDown,
                BrokenDown = !finished && car != null && car.BrokenDown,
            };
        }

        public bool Any => PoliceControl || Crash || Haggle || Refusal || DryTank || FlatTyre || BrokenDown;

        /// <summary>Ce qu'il faut régler d'abord (« le contrôle de police ») ; vide s'il n'y a rien.</summary>
        public string What =>
            PoliceControl ? "le contrôle de police"
            : Crash ? "l'accrochage"
            : Refusal ? "le passager qui refuse de payer"
            : Haggle ? "la négociation du client"
            : DryTank ? "la panne sèche"
            : BrokenDown ? "la panne"
            : FlatTyre ? "la crevaison"
            : "";
    }

    public enum MenuScreen
    {
        /// <summary>En jeu, aucun menu.</summary>
        None,
        Title,
        Pause,
        Settings,
        /// <summary>Mode photo (idée n° 27) : ouvert depuis la pause, HUD masqué, caméra libre, heure figée.</summary>
        Photo,
    }

    /// <summary>
    /// Écran titre, pause et réglages : quel écran est ouvert, et où revenir en quittant les réglages.
    /// Tant qu'un écran est ouvert, la journée est figée (<see cref="Frozen"/>).
    /// </summary>
    public sealed class GameMenu
    {
        private MenuScreen _settingsReturn = MenuScreen.Pause;

        public MenuScreen Screen { get; private set; }
        public bool Frozen => Screen != MenuScreen.None;

        /// <summary>
        /// Le temps du jeu est arrêté (physique, sons) : en pause et dans les réglages ouverts depuis la pause. Sur
        /// l'écran titre, le décor continue de vivre, seule l'horloge de la journée attend.
        /// </summary>
        public bool TimeStopped => Screen == MenuScreen.Pause || Screen == MenuScreen.Photo
            || (Screen == MenuScreen.Settings && _settingsReturn == MenuScreen.Pause);

        /// <summary>« Mode photo » dans la pause (idée n° 27).</summary>
        public bool OpenPhoto()
        {
            if (Screen != MenuScreen.Pause) return false;
            Screen = MenuScreen.Photo;
            return true;
        }

        /// <param name="showTitle">Ouvrir sur l'écran titre (premier lancement) plutôt que directement au volant.</param>
        public GameMenu(bool showTitle) => Screen = showTitle ? MenuScreen.Title : MenuScreen.None;

        /// <summary>« Commencer la journée » (ou « Continuer ») sur l'écran titre.</summary>
        public void StartDay()
        {
            if (Screen == MenuScreen.Title) Screen = MenuScreen.None;
            ConfirmingNewCareer = false;
        }

        /// <summary>« Nouvelle partie » attend sa confirmation (second appui) sur l'écran titre (idée n° 10).</summary>
        public bool ConfirmingNewCareer { get; private set; }

        /// <summary>
        /// « Nouvelle partie » : le premier appui demande confirmation, le second rend true (la présentation remet la
        /// carrière à zéro) et lance la journée.
        /// </summary>
        public bool RequestNewCareer()
        {
            if (Screen != MenuScreen.Title) return false;
            if (!ConfirmingNewCareer)
            {
                ConfirmingNewCareer = true;
                return false;
            }
            StartDay();
            return true;
        }

        /// <summary>Bouton pause, Échap ou retour Android, ou l'appli passe en arrière-plan : seulement en jeu.</summary>
        public bool Pause()
        {
            if (Screen != MenuScreen.None) return false;
            Screen = MenuScreen.Pause;
            return true;
        }

        public void Resume()
        {
            if (Screen == MenuScreen.Pause) Screen = MenuScreen.None;
        }

        /// <summary>Retour (Échap) : réglages → écran précédent, pause → jeu ; sans effet sur l'écran titre.</summary>
        public void Back()
        {
            if (Screen == MenuScreen.Settings) CloseSettings();
            else if (Screen == MenuScreen.Photo) Screen = MenuScreen.Pause;
            else Resume();
        }

        public void OpenSettings()
        {
            if (Screen != MenuScreen.Title && Screen != MenuScreen.Pause) return;
            ConfirmingNewCareer = false;
            _settingsReturn = Screen;
            Screen = MenuScreen.Settings;
        }

        public void CloseSettings()
        {
            if (Screen == MenuScreen.Settings) Screen = _settingsReturn;
        }

        /// <summary>
        /// « Terminer la journée » dans la pause : on quitte le menu pour le bilan. Refusé tant qu'un choix attend sa
        /// réponse (<paramref name="pending"/> : contrôle de police, accrochage, négociation… ; décision de Kevin du 23/11) :
        /// la pause reste ouverte et le bouton dit quoi régler d'abord (<see cref="EndDayLabel"/>).
        /// </summary>
        public bool EndDay(PendingChoices pending = default)
        {
            if (Screen != MenuScreen.Pause || pending.Any) return false;
            Screen = MenuScreen.None;
            return true;
        }

        /// <summary>Message quand la fin de journée est refusée (vide si elle est possible).</summary>
        public static string EndDayRefusal(PendingChoices pending) =>
            pending.Any ? $"Règle d'abord {pending.What} avant de terminer la journée" : "";

        /// <summary>
        /// Libellé du bouton de fin de journée, qui prévient si des passagers ne paieront pas, ou dit ce qu'il faut régler
        /// d'abord quand un choix attend (le bouton ne fait alors rien).
        /// </summary>
        public static string EndDayLabel(int passengersOnBoard, PendingChoices pending = default)
        {
            if (pending.Any) return EndDayRefusal(pending);
            if (passengersOnBoard <= 0) return "Terminer la journée";
            return passengersOnBoard == 1
                ? "Terminer la journée (1 passager ne paiera pas)"
                : $"Terminer la journée ({passengersOnBoard} passagers ne paieront pas)";
        }
    }
}
