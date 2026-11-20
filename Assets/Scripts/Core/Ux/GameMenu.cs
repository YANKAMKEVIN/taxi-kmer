namespace TaxiKmer.Core.Ux
{
    public enum MenuScreen
    {
        /// <summary>En jeu, aucun menu.</summary>
        None,
        Title,
        Pause,
        Settings,
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
        public bool TimeStopped => Screen == MenuScreen.Pause || (Screen == MenuScreen.Settings && _settingsReturn == MenuScreen.Pause);

        /// <param name="showTitle">Ouvrir sur l'écran titre (premier lancement) plutôt que directement au volant.</param>
        public GameMenu(bool showTitle) => Screen = showTitle ? MenuScreen.Title : MenuScreen.None;

        /// <summary>« Commencer la journée » sur l'écran titre.</summary>
        public void StartDay()
        {
            if (Screen == MenuScreen.Title) Screen = MenuScreen.None;
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
            else Resume();
        }

        public void OpenSettings()
        {
            if (Screen != MenuScreen.Title && Screen != MenuScreen.Pause) return;
            _settingsReturn = Screen;
            Screen = MenuScreen.Settings;
        }

        public void CloseSettings()
        {
            if (Screen == MenuScreen.Settings) Screen = _settingsReturn;
        }

        /// <summary>« Terminer la journée » dans la pause : on quitte le menu pour le bilan.</summary>
        public bool EndDay()
        {
            if (Screen != MenuScreen.Pause) return false;
            Screen = MenuScreen.None;
            return true;
        }

        /// <summary>Libellé du bouton de fin de journée, qui prévient si des passagers ne paieront pas.</summary>
        public static string EndDayLabel(int passengersOnBoard)
        {
            if (passengersOnBoard <= 0) return "Terminer la journée";
            return passengersOnBoard == 1
                ? "Terminer la journée (1 passager ne paiera pas)"
                : $"Terminer la journée ({passengersOnBoard} passagers ne paieront pas)";
        }
    }
}
