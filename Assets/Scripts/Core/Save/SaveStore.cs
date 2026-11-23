using System;
using System.Text;

namespace TaxiKmer.Core.Save
{
    /// <summary>Conversion objet ↔ JSON (JsonUtility dans Unity, System.Text.Json dans les tests).</summary>
    public interface ISaveSerializer
    {
        string ToJson(object value);
        T FromJson<T>(string json) where T : class;
    }

    /// <summary>
    /// Fichiers de la sauvegarde, par nom (un dossier dans Unity, la mémoire dans les tests).
    /// <see cref="Write"/> doit avoir écrit sur le disque quand elle rend la main ; <see cref="Move"/> exige que la
    /// destination n'existe pas.
    /// </summary>
    public interface ISaveStorage
    {
        bool Exists(string name);
        string Read(string name);
        void Write(string name, string text);
        void Copy(string source, string destination);
        void Move(string source, string destination);
        void Delete(string name);
    }

    /// <summary>Enveloppe écrite sur le disque : contenu sérialisé + empreinte, pour détecter un fichier abîmé.</summary>
    [Serializable]
    public class SaveEnvelope
    {
        public string format = SaveStore.Format;
        public int version;
        /// <summary>Empreinte FNV-1a 64 bits du contenu, en hexadécimal.</summary>
        public string checksum = "";
        /// <summary>Le <see cref="SaveData"/> sérialisé (texte JSON).</summary>
        public string payload = "";
    }

    public enum LoadStatus
    {
        /// <summary>Aucune sauvegarde : nouvelle partie.</summary>
        NoSave,
        Loaded,
        /// <summary>Le fichier principal était abîmé ou incomplet : la copie précédente a été reprise.</summary>
        RecoveredFromBackup,
        /// <summary>Sauvegarde illisible et pas de copie valable : mise de côté, nouvelle partie.</summary>
        Corrupted,
        /// <summary>Écrite par une version plus récente du jeu : laissée intacte, rien n'est chargé ni écrasé.</summary>
        TooNew,
    }

    public struct LoadResult
    {
        public LoadStatus Status;
        /// <summary>Les données chargées (et mises à jour vers la version courante) ; null sans sauvegarde utilisable.</summary>
        public SaveData Data;
        /// <summary>Explication pour le journal (fichier mis de côté, version trouvée…).</summary>
        public string Detail;
    }

    /// <summary>
    /// Sauvegarde sûre (jalon 3, système « risqué » validé seul) : écriture dans un fichier temporaire, copie de
    /// secours de l'ancienne sauvegarde, puis remplacement ; à la lecture, contrôle de l'empreinte, reprise de la
    /// copie de secours ou du fichier temporaire si le principal est abîmé ou manque (coupure pendant l'écriture),
    /// mise de côté des fichiers illisibles (jamais effacés), refus d'écraser une sauvegarde d'un jeu plus récent.
    /// </summary>
    public sealed class SaveStore
    {
        public const string Format = "taxikmer-save";
        public const string MainFile = "partie.json";
        public const string TempFile = "partie.json.tmp";
        public const string BackupFile = "partie.json.bak";

        private readonly ISaveStorage _storage;
        private readonly ISaveSerializer _serializer;
        private bool _blockedByNewerSave;

        public SaveStore(ISaveStorage storage, ISaveSerializer serializer)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>Écrit la sauvegarde. Refusé (false) si le disque contient une sauvegarde d'une version plus récente.</summary>
        public bool Save(SaveData data, string savedAtUtc = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_blockedByNewerSave) return false;
            data.version = SaveMigrations.CurrentVersion;
            if (savedAtUtc != null) data.savedAtUtc = savedAtUtc;

            var payload = _serializer.ToJson(data);
            var envelope = new SaveEnvelope { version = data.version, checksum = Checksum(payload), payload = payload };
            _storage.Write(TempFile, _serializer.ToJson(envelope));

            // L'ancienne sauvegarde devient la copie de secours, puis le fichier temporaire la remplace.
            if (_storage.Exists(MainFile))
            {
                if (IsValid(MainFile)) _storage.Copy(MainFile, BackupFile);
                _storage.Delete(MainFile);
            }
            _storage.Move(TempFile, MainFile);
            return true;
        }

        public LoadResult Load()
        {
            var mainExists = _storage.Exists(MainFile);
            if (mainExists)
            {
                var main = TryRead(MainFile, out var detail);
                if (main.Status == LoadStatus.Loaded || main.Status == LoadStatus.TooNew) return Finish(main);
                SetAside(MainFile);
                var recovered = Recover();
                if (recovered.Data != null) return Finish(recovered);
                return Finish(new LoadResult { Status = LoadStatus.Corrupted, Detail = detail });
            }

            // Pas de fichier principal : coupure entre l'effacement et le remplacement, ou première partie.
            if (!_storage.Exists(TempFile) && !_storage.Exists(BackupFile)) return new LoadResult { Status = LoadStatus.NoSave };
            var fallback = Recover();
            if (fallback.Data != null) return Finish(fallback);
            return Finish(new LoadResult { Status = LoadStatus.Corrupted, Detail = "aucune copie lisible" });
        }

        /// <summary>Efface la sauvegarde (nouvelle partie demandée par le joueur). La copie de secours est gardée.</summary>
        public void DeleteSave()
        {
            if (_storage.Exists(MainFile))
            {
                if (IsValid(MainFile)) _storage.Copy(MainFile, BackupFile);
                _storage.Delete(MainFile);
            }
            if (_storage.Exists(TempFile)) _storage.Delete(TempFile);
            _blockedByNewerSave = false;
        }

        private LoadResult Finish(LoadResult r)
        {
            _blockedByNewerSave = r.Status == LoadStatus.TooNew;
            return r;
        }

        /// <summary>Fichier temporaire complet (écrit juste avant la coupure), sinon copie de secours ; remis en principal.</summary>
        private LoadResult Recover()
        {
            foreach (var name in new[] { TempFile, BackupFile })
            {
                if (!_storage.Exists(name)) continue;
                var r = TryRead(name, out _);
                if (r.Status != LoadStatus.Loaded) continue;
                if (_storage.Exists(MainFile)) _storage.Delete(MainFile);
                _storage.Copy(name, MainFile);
                if (name == TempFile) _storage.Delete(TempFile);
                r.Status = LoadStatus.RecoveredFromBackup;
                r.Detail = name == TempFile ? "repris du fichier temporaire" : "repris de la copie de secours";
                return r;
            }
            return new LoadResult { Status = LoadStatus.Corrupted };
        }

        private bool IsValid(string name) => TryRead(name, out _).Status == LoadStatus.Loaded;

        private LoadResult TryRead(string name, out string detail)
        {
            detail = "";
            string text;
            try { text = _storage.Read(name); }
            catch (Exception e) { detail = $"{name} illisible : {e.Message}"; return new LoadResult { Status = LoadStatus.Corrupted, Detail = detail }; }

            SaveEnvelope envelope;
            try { envelope = string.IsNullOrWhiteSpace(text) ? null : _serializer.FromJson<SaveEnvelope>(text); }
            catch (Exception) { envelope = null; }
            if (envelope == null || envelope.format != Format || envelope.payload == null)
            {
                detail = $"{name} : format inconnu ou fichier tronqué";
                return new LoadResult { Status = LoadStatus.Corrupted, Detail = detail };
            }
            if (envelope.checksum != Checksum(envelope.payload))
            {
                detail = $"{name} : empreinte fausse (fichier abîmé)";
                return new LoadResult { Status = LoadStatus.Corrupted, Detail = detail };
            }
            if (envelope.version > SaveMigrations.CurrentVersion)
            {
                // Le numéro de l'enveloppe n'est pas couvert par l'empreinte (idée n° 34) : le contenu, lui, l'est. S'il
                // se lit et porte une version connue, c'est l'enveloppe qui est abîmée ; on se fie au contenu.
                SaveData inner = null;
                try { inner = _serializer.FromJson<SaveData>(envelope.payload); }
                catch (Exception) { inner = null; }
                if (inner == null || inner.version < 1 || inner.version > SaveMigrations.CurrentVersion)
                {
                    detail = $"{name} : version {envelope.version}, plus récente que ce jeu ({SaveMigrations.CurrentVersion})";
                    return new LoadResult { Status = LoadStatus.TooNew, Detail = detail };
                }
                envelope.version = inner.version;
            }

            SaveData data;
            try { data = _serializer.FromJson<SaveData>(envelope.payload); }
            catch (Exception) { data = null; }
            if (data == null)
            {
                detail = $"{name} : contenu illisible";
                return new LoadResult { Status = LoadStatus.Corrupted, Detail = detail };
            }
            SaveMigrations.Upgrade(data, envelope.version);
            return new LoadResult { Status = LoadStatus.Loaded, Data = data };
        }

        /// <summary>Met un fichier abîmé de côté sous un nom libre (jamais effacé : utile pour comprendre un bug).</summary>
        private void SetAside(string name)
        {
            for (var i = 1; i < 1000; i++)
            {
                var target = $"partie-abimee-{i}.json";
                if (_storage.Exists(target)) continue;
                _storage.Move(name, target);
                return;
            }
            _storage.Delete(name);
        }

        /// <summary>FNV-1a 64 bits sur l'UTF-8 du texte, en hexadécimal (16 caractères).</summary>
        public static string Checksum(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            foreach (var b in Encoding.UTF8.GetBytes(text ?? ""))
            {
                hash ^= b;
                hash *= prime;
            }
            return hash.ToString("x16");
        }
    }
}
