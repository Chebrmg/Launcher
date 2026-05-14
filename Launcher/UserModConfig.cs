using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Launcher
{
    public class UserModConfig
    {
        public string Name { get; set; } = "";
        public string ShortDescription { get; set; } = "";
        public string FullDescription { get; set; } = "";
        public bool ForChebovka { get; set; }
        public bool Install { get; set; }

        private const string ConfigEntryName = "Mod_Config";
        private const string PreviewEntryName = "preview.png";

        public static UserModConfig ReadFromArchive(string h5uPath)
        {
            var config = new UserModConfig();
            config.Name = Path.GetFileNameWithoutExtension(h5uPath);

            try
            {
                using var zip = ZipFile.OpenRead(h5uPath);
                var entry = zip.GetEntry(ConfigEntryName);
                if (entry != null)
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();
                    var loaded = JsonSerializer.Deserialize<UserModConfig>(json);
                    if (loaded != null)
                    {
                        config = loaded;
                        if (string.IsNullOrEmpty(config.Name))
                            config.Name = Path.GetFileNameWithoutExtension(h5uPath);
                    }
                }
            }
            catch { }

            return config;
        }

        public void SaveToArchive(string h5uPath)
        {
            try
            {
                using var zip = ZipFile.Open(h5uPath, ZipArchiveMode.Update);

                var existing = zip.GetEntry(ConfigEntryName);
                existing?.Delete();

                var entry = zip.CreateEntry(ConfigEntryName);
                using var stream = entry.Open();
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            catch { }
        }

        public static Image? ReadPreviewFromArchive(string h5uPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(h5uPath);
                var entry = zip.GetEntry(PreviewEntryName);
                if (entry == null)
                    return null;

                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                return Image.FromStream(ms);
            }
            catch
            {
                return null;
            }
        }

        public static void SavePreviewToArchive(string h5uPath, string imagePath)
        {
            try
            {
                using var zip = ZipFile.Open(h5uPath, ZipArchiveMode.Update);

                var existing = zip.GetEntry(PreviewEntryName);
                existing?.Delete();

                var entry = zip.CreateEntry(PreviewEntryName, CompressionLevel.Optimal);
                using var output = entry.Open();
                using var input = File.OpenRead(imagePath);
                input.CopyTo(output);
            }
            catch { }
        }

        /// <summary>
        /// Возвращает имя файла-маркера в зависимости от флага ForChebovka.
        /// </summary>
        public string GetPatchFlagName()
        {
            return ForChebovka ? "_patched_chebovka.flag" : "_patched_usermod.flag";
        }

        /// <summary>
        /// Дата для установки в записях архива.
        /// 2029 год для обычных модов, 2031 для модов Chebovka.
        /// </summary>
        public DateTime GetPatchDate()
        {
            return ForChebovka
                ? new DateTime(2031, 1, 1)
                : new DateTime(2029, 1, 1);
        }

        /// <summary>
        /// Проверяет, был ли мод уже пропатчен с правильным маркером.
        /// </summary>
        public static bool IsPatched(string h5uPath, bool forChebovka)
        {
            try
            {
                if (!File.Exists(h5uPath))
                    return false;

                string flagName = forChebovka ? "_patched_chebovka.flag" : "_patched_usermod.flag";

                using var zip = ZipFile.OpenRead(h5uPath);
                return zip.Entries.Any(e => e.FullName == flagName);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Обновляет даты файлов в архиве и ставит маркер.
        /// Удаляет старый маркер если тип (Chebovka/обычный) изменился.
        /// </summary>
        public static void PatchArchive(string h5uPath, bool forChebovka)
        {
            try
            {
                if (!File.Exists(h5uPath))
                    return;

                string flagName = forChebovka ? "_patched_chebovka.flag" : "_patched_usermod.flag";
                string otherFlagName = forChebovka ? "_patched_usermod.flag" : "_patched_chebovka.flag";
                DateTime patchDate = forChebovka ? new DateTime(2031, 1, 1) : new DateTime(2029, 1, 1);

                string tempPath = h5uPath + "_temp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using (var source = ZipFile.OpenRead(h5uPath))
                using (var target = ZipFile.Open(tempPath, ZipArchiveMode.Create))
                {
                    foreach (var entry in source.Entries)
                    {
                        if (entry.FullName == "_patched_usermod.flag" ||
                            entry.FullName == "_patched_chebovka.flag")
                            continue;

                        var newEntry = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);

                        // Mod_Config и preview.png сохраняем без изменения даты
                        if (entry.FullName == ConfigEntryName || entry.FullName == PreviewEntryName)
                            newEntry.LastWriteTime = entry.LastWriteTime;
                        else
                            newEntry.LastWriteTime = patchDate;

                        using var input = entry.Open();
                        using var output = newEntry.Open();
                        input.CopyTo(output);
                    }

                    // Записываем маркер
                    var flag = target.CreateEntry(flagName);
                    using (var writer = new StreamWriter(flag.Open()))
                    {
                        writer.Write(forChebovka ? "patched_chebovka" : "patched_usermod");
                    }
                }

                File.Delete(h5uPath);
                File.Move(tempPath, h5uPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("UserMod patch error: " + ex.Message);
            }
        }
    }
}
