using System;
using System.IO;
using UnityEngine;

namespace ShooterShop.Saves
{
    public sealed class JsonSaveStorage : ISaveStorage
    {
        private readonly string _path;

        public JsonSaveStorage(string fileName)
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public bool TryLoad(out SaveData data)
        {
            data = null;

            if (!File.Exists(_path))
                return false;

            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(_path));
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save file is corrupted and will be ignored: {_path}\n{exception.Message}");
                return false;
            }
        }

        public void Save(SaveData data)
        {
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
            File.Copy(temporaryPath, _path, true);
            File.Delete(temporaryPath);
        }

        public void Delete()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }
}
