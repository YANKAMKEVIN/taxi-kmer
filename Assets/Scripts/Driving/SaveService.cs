using System;
using System.Collections.Generic;
using System.IO;
using TaxiKmer.Core.Career;
using TaxiKmer.Core.Police;
using TaxiKmer.Core.Rides;
using TaxiKmer.Core.Save;
using UnityEngine;

namespace TaxiKmer.Driving
{
    /// <summary>Réglages de la sauvegarde (Assets/Resources/Data/save.json).</summary>
    [Serializable]
    public class SaveConfig
    {
        /// <summary>Sauvegarde automatique de la journée en cours toutes les … s (0 = jamais).</summary>
        public float autosaveSeconds = 30f;
        /// <summary>Sauvegarde quand l'appli passe en arrière-plan ou se ferme.</summary>
        public bool saveOnPause = true;
        /// <summary>
        /// Au lancement, reprendre la journée sauvegardée. Coupé tant que le jalon 3 n'est pas validé (système
        /// risqué, validé seul) : les sauvegardes s'écrivent, mais ne se relisent pas encore au lancement.
        /// </summary>
        public bool resumeOnStart;
        /// <summary>
        /// Enregistrer chaque journée pour la rejouer (idée n° 37) : fichier rejeux/rejeu-jour-N.json dans le dossier de
        /// l'appli, à joindre à un signalement de bug. Coupé par défaut (fichier de quelques Mo par journée).
        /// </summary>
        public bool recordReplay;
    }

    /// <summary>JsonUtility pour la sauvegarde.</summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        public string ToJson(object value) => JsonUtility.ToJson(value);
        public T FromJson<T>(string json) where T : class => JsonUtility.FromJson<T>(json);
    }

    /// <summary>Fichiers de sauvegarde dans un dossier (Application.persistentDataPath/sauvegarde sur le téléphone).</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _dir;

        public FileSaveStorage(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        private string P(string name) => Path.Combine(_dir, name);
        public bool Exists(string name) => File.Exists(P(name));
        public string Read(string name) => File.ReadAllText(P(name));

        public void Write(string name, string text)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            using (var fs = new FileStream(P(name), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // jusqu'au disque, pas seulement au cache
            }
        }

        public void Copy(string source, string destination) => File.Copy(P(source), P(destination), true);
        public void Move(string source, string destination) => File.Move(P(source), P(destination));
        public void Delete(string name) => File.Delete(P(name));
    }

    /// <summary>
    /// Sauvegarde dans le jeu (jalon 3, partie technique) : charge au lancement, sauvegarde la journée en cours
    /// régulièrement et quand l'appli passe en arrière-plan, enregistre le bilan à la fin de la journée.
    /// La reprise au lancement est derrière <see cref="SaveConfig.resumeOnStart"/> (coupée par défaut).
    /// </summary>
    public sealed class SaveService : MonoBehaviour
    {
        public StreetView street;
        public TaxiCar taxi;
        /// <summary>Dossier des fichiers (vide : Application.persistentDataPath/sauvegarde).</summary>
        public string directoryOverride = "";

        /// <summary>Tests : dossier et réglages imposés à toutes les sauvegardes créées ensuite (null : normal).</summary>
        public static string DirectoryForTests;
        public static SaveConfig ConfigForTests;

        public SaveConfig Config { get; private set; }
        public SaveStore Store { get; private set; }
        public SaveData Data { get; private set; }
        public LoadResult LastLoad { get; private set; }
        /// <summary>La journée a été reprise d'une sauvegarde au lancement.</summary>
        public bool Resumed { get; private set; }
        /// <summary>La carrière (jour, épargne, propriétaire), toujours chargée : l'argent passe d'un jour à l'autre.</summary>
        public DriverCareer Career { get; private set; }
        /// <summary>Clôture de la journée, une fois terminée (null avant).</summary>
        public DayClosure? Closure { get; private set; }
        /// <summary>Le joueur avait été renvoyé : une nouvelle carrière a commencé à ce lancement.</summary>
        public bool StartedNewCareer { get; private set; }

        /// <summary>Dernières données de la session : si le disque est inaccessible, la carrière continue en mémoire.</summary>
        private static SaveData s_lastData;

        private float _sinceSave;
        private int _seed;
        private OwnerConfig _owner;

        public string Directory => !string.IsNullOrEmpty(DirectoryForTests) ? DirectoryForTests
            : string.IsNullOrEmpty(directoryOverride) ? Path.Combine(Application.persistentDataPath, "sauvegarde")
            : directoryOverride;

        /// <summary>Appelé par StreetView quand la rue est créée : lit la sauvegarde (sans encore rien reprendre).</summary>
        public void Initialize(int seed, SaveConfig config, OwnerConfig owner = null)
        {
            _seed = seed;
            _owner = owner ?? new OwnerConfig();
            Config = ConfigForTests ?? config ?? new SaveConfig();
            try
            {
                Store = new SaveStore(new FileSaveStorage(Directory), new JsonUtilitySaveSerializer());
                LastLoad = Store.Load();
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaxiKmer] Sauvegarde inaccessible ({Directory}) : {e.Message}");
                LastLoad = new LoadResult { Status = LoadStatus.Corrupted, Detail = e.Message };
            }
            Data = LastLoad.Data ?? (DirectoryForTests == null ? s_lastData : null) ?? new SaveData();
            if (Data.career.fired)
            {
                // Renvoyé à la dernière partie : nouvelle carrière (le bilan de la précédente reste dans l'historique du fichier abandonné).
                Data = new SaveData();
                StartedNewCareer = true;
            }
            Career = SaveSnapshot.LoadCareer(Data, _owner);
            switch (LastLoad.Status)
            {
                case LoadStatus.Loaded: Debug.Log($"[TaxiKmer] Sauvegarde chargée : jour {Data.career.dayNumber}."); break;
                case LoadStatus.RecoveredFromBackup: Debug.LogWarning($"[TaxiKmer] Sauvegarde réparée : {LastLoad.Detail}."); break;
                case LoadStatus.Corrupted: Debug.LogWarning($"[TaxiKmer] Sauvegarde illisible, nouvelle partie : {LastLoad.Detail}."); break;
                case LoadStatus.TooNew: Debug.LogWarning($"[TaxiKmer] Sauvegarde d'une version plus récente, laissée intacte : {LastLoad.Detail}."); break;
            }
        }

        /// <summary>À la première image (le taxi est posé) : reprend la journée sauvegardée si c'est permis. Rend true si repris.</summary>
        public bool TryResume()
        {
            if (Config == null || !Config.resumeOnStart || !Data.hasDayInProgress || street == null || street.Street == null) return false;
            var lost = SaveSnapshot.RestoreDay(Data.day, street.Street);
            if (lost > 0) Debug.LogWarning($"[TaxiKmer] {lost} passager(s) de la sauvegarde n'ont pas pu être remis à bord.");
            if (taxi != null) taxi.PlaceOnRoute(Data.day.taxiAlong, Data.day.taxiDirection);
            Resumed = true;
            return true;
        }

        /// <summary>
        /// Clôt la journée terminée (une seule fois) : versement, épargne, humeur du propriétaire ; enregistre le bilan.
        /// </summary>
        public DayClosure CloseDay()
        {
            if (Closure.HasValue) return Closure.Value;
            var closure = Career.CloseDay(street.Street.Summary, street.Street.Reputation?.Config.effects);
            Closure = closure;
            SaveSnapshot.RecordDay(Data, street.Street.Summary, closure, Career);
            Write();
            return closure;
        }

        /// <summary>
        /// Début de journée : papiers de la carrière, et renouvellement automatique de ceux arrivés à échéance, payés sur
        /// l'épargne (décision de Kevin du 21/11 ; épargne insuffisante : le papier reste expiré). Écrit la sauvegarde si
        /// quelque chose a été renouvelé. Rend les papiers et les messages à afficher.
        /// </summary>
        public PaperSet StartDayPapers(PoliceConfig police, List<string> messages)
        {
            var papers = SaveSnapshot.LoadPapers(Data, police);
            var done = SaveSnapshot.RenewPapersAtDayStart(Data, papers, Career, police);
            foreach (var r in done) messages?.Add(PoliceTexts.Renewal(r));
            if (done.Exists(r => r.Renewed)) Write();
            return papers;
        }

        /// <summary>Nouvelle carrière après un renvoi : la sauvegarde repart de zéro (l'ancienne reste en copie de secours).</summary>
        public void StartNewCareer()
        {
            Data = new SaveData();
            Career = SaveSnapshot.LoadCareer(Data, _owner);
            Write();
        }

        private void Update()
        {
            if (street == null || street.Street == null || street.Street.DayFinished) return;
            if (Config.autosaveSeconds <= 0f) return;
            _sinceSave += Time.unscaledDeltaTime;
            if (_sinceSave >= Config.autosaveSeconds) SaveDayNow();
        }

        /// <summary>Sauvegarde la journée en cours maintenant.</summary>
        public void SaveDayNow()
        {
            _sinceSave = 0f;
            if (street == null || street.Street == null || street.Street.DayFinished) return;
            var state = street.TaxiOnRouteNow();
            SaveSnapshot.CaptureDay(Data, street.Street, state.Along, state.Direction, _seed);
            Write();
        }

        private void Write()
        {
            Data.gameVersion = Application.version;
            if (DirectoryForTests == null) s_lastData = Data;
            if (Store == null) return;
            try
            {
                if (!Store.Save(Data, DateTime.UtcNow.ToString("o")))
                    Debug.LogWarning("[TaxiKmer] Sauvegarde non écrite : une sauvegarde plus récente est sur le téléphone.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TaxiKmer] Échec de la sauvegarde : {e.Message}");
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Config != null && Config.saveOnPause) SaveDayNow();
        }

        private void OnApplicationQuit()
        {
            if (Config != null && Config.saveOnPause) SaveDayNow();
        }
    }
}
