using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Pfim;

namespace Launcher
{
    /// <summary>
    /// Данные одного юнита, распарсенные из игровых архивов.
    /// </summary>
    public class CreatureInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Faction { get; set; } = "";
        public int AttackSkill { get; set; }
        public int DefenceSkill { get; set; }
        public int Shots { get; set; }
        public int MinDamage { get; set; }
        public int MaxDamage { get; set; }
        public int Speed { get; set; }
        public int Initiative { get; set; }
        public bool Flying { get; set; }
        public int Health { get; set; }
        public string CreatureTown { get; set; } = "";
        public int WeeklyGrowth { get; set; }
        public int Gold { get; set; }
        public List<string> Upgrades { get; set; } = new();
        public List<string> Abilities { get; set; } = new();
        public Image? Icon { get; set; }
    }

    /// <summary>
    /// Запись виртуальной файловой системы — файл из конкретного архива с датой.
    /// </summary>
    internal class VfsEntry
    {
        public string ArchivePath { get; set; } = "";
        public string EntryName { get; set; } = "";
        public DateTimeOffset LastModified { get; set; }
    }

    /// <summary>
    /// Парсер игровых данных из .pak и .h5u архивов.
    /// Читает XDB файлы, извлекает характеристики юнитов и иконки (DDS).
    /// </summary>
    public class GameDataParser
    {
        private readonly string _gameRoot;
        private Dictionary<string, VfsEntry> _vfs = new();

        // Маппинг CreatureTown → человекочитаемое название фракции
        private static readonly Dictionary<string, string> TownToFaction = new()
        {
            { "TOWN_HEAVEN", "Haven" },
            { "TOWN_INFERNO", "Inferno" },
            { "TOWN_NECROMANCY", "Necropolis" },
            { "TOWN_PRESERVE", "Sylvan" },
            { "TOWN_ACADEMY", "Academy" },
            { "TOWN_DUNGEON", "Dungeon" },
            { "TOWN_FORTRESS", "Fortress" },
            { "TOWN_STRONGHOLD", "Stronghold" },
            { "TOWN_NO_TYPE", "Нейтралы" },
            { "TOWN_NONE", "Нейтралы" },
        };

        // Маппинг папки в Creatures.xdb → фракция (для определения фракции по пути)
        private static readonly Dictionary<string, string> PathToFaction = new()
        {
            { "Haven", "Haven" },
            { "Inferno", "Inferno" },
            { "Necropolis", "Necropolis" },
            { "Preserve", "Sylvan" },
            { "Academy", "Academy" },
            { "Dungeon", "Dungeon" },
            { "Dwarf", "Fortress" },
            { "Orcs", "Stronghold" },
            { "Neutrals", "Нейтралы" },
        };

        public static readonly string[] FactionOrder =
        {
            "Haven", "Inferno", "Necropolis", "Sylvan",
            "Academy", "Dungeon", "Fortress", "Stronghold", "Нейтралы"
        };

        public int VfsCount => _vfs.Count;

        public GameDataParser(string gameRoot)
        {
            _gameRoot = gameRoot;
        }

        /// <summary>
        /// Строит виртуальную ФС из всех архивов: data/*.pak + UserMods/*.h5u.
        /// Для каждого внутреннего пути хранит архив с самой новой датой.
        /// </summary>
        public void BuildVfs()
        {
            _vfs.Clear();

            var archives = new List<string>();

            string dataDir = Path.Combine(_gameRoot, "data");
            if (Directory.Exists(dataDir))
                archives.AddRange(Directory.GetFiles(dataDir, "*.pak"));

            string userModsDir = Path.Combine(_gameRoot, "UserMods");
            if (Directory.Exists(userModsDir))
                archives.AddRange(Directory.GetFiles(userModsDir, "*.h5u"));

            foreach (string archivePath in archives)
            {
                try
                {
                    using var zip = ZipFile.OpenRead(archivePath);
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        // Нормализуем путь: обратные слэши → прямые, всегда с / в начале
                        string normalizedPath = "/" + entry.FullName.Replace('\\', '/').TrimStart('/');

                        if (!_vfs.ContainsKey(normalizedPath) ||
                            entry.LastWriteTime > _vfs[normalizedPath].LastModified)
                        {
                            _vfs[normalizedPath] = new VfsEntry
                            {
                                ArchivePath = archivePath,
                                EntryName = entry.FullName,
                                LastModified = entry.LastWriteTime
                            };
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Читает файл из виртуальной ФС (из нужного архива).
        /// </summary>
        private byte[]? ReadFile(string virtualPath)
        {
            string normalized = "/" + virtualPath.Replace('\\', '/').TrimStart('/');
            if (!_vfs.TryGetValue(normalized, out var vfsEntry))
                return null;

            try
            {
                using var zip = ZipFile.OpenRead(vfsEntry.ArchivePath);
                var entry = zip.GetEntry(vfsEntry.EntryName);
                if (entry == null)
                    return null;

                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Читает XDB (XML) файл из виртуальной ФС.
        /// </summary>
        private XDocument? ReadXdb(string virtualPath)
        {
            var data = ReadFile(virtualPath);
            if (data == null)
                return null;

            try
            {
                using var ms = new MemoryStream(data);
                return XDocument.Load(ms);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Извлекает путь из href атрибута, убирая #xpointer(...).
        /// Пример: "/GameMechanics/Creature/Creatures/Haven/Peasant.xdb#xpointer(/Creature)" → "/GameMechanics/Creature/Creatures/Haven/Peasant.xdb"
        /// </summary>
        private static string ExtractPath(string href)
        {
            int idx = href.IndexOf('#');
            return idx >= 0 ? href.Substring(0, idx) : href;
        }

        /// <summary>
        /// Резолвит относительный путь относительно директории текущего файла.
        /// </summary>
        private static string ResolvePath(string basePath, string relativePath)
        {
            if (relativePath.StartsWith("/"))
                return relativePath;

            string baseDir = basePath.Substring(0, basePath.LastIndexOf('/') + 1);
            return baseDir + relativePath;
        }

        /// <summary>
        /// Определяет фракцию по пути к файлу юнита в Creatures.xdb.
        /// </summary>
        private static string DetectFactionFromPath(string path)
        {
            foreach (var kv in PathToFaction)
            {
                if (path.Contains("/" + kv.Key + "/"))
                    return kv.Value;
            }
            return "Нейтралы";
        }

        /// <summary>
        /// Парсит все юниты из игровых архивов.
        /// </summary>
        public List<CreatureInfo> ParseCreatures()
        {
            var result = new List<CreatureInfo>();

            var creaturesXdb = ReadXdb("/GameMechanics/RefTables/Creatures.xdb");
            if (creaturesXdb == null)
                return result;

            var items = creaturesXdb.Descendants("Item").ToList();

            foreach (var item in items)
            {
                string id = item.Element("ID")?.Value ?? "";
                if (id == "CREATURE_UNKNOWN")
                    continue;

                string objHref = item.Element("Obj")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(objHref))
                    continue;

                string creaturePath = ExtractPath(objHref);
                string factionFromPath = DetectFactionFromPath(creaturePath);

                var creatureXdb = ReadXdb(creaturePath);
                if (creatureXdb == null)
                    continue;

                var root = creatureXdb.Root;
                if (root == null)
                    continue;

                var creature = new CreatureInfo
                {
                    Id = id,
                    Faction = factionFromPath,
                    AttackSkill = ParseInt(root, "AttackSkill"),
                    DefenceSkill = ParseInt(root, "DefenceSkill"),
                    Shots = ParseInt(root, "Shots"),
                    MinDamage = ParseInt(root, "MinDamage"),
                    MaxDamage = ParseInt(root, "MaxDamage"),
                    Speed = ParseInt(root, "Speed"),
                    Initiative = ParseInt(root, "Initiative"),
                    Flying = root.Element("Flying")?.Value == "true",
                    Health = ParseInt(root, "Health"),
                    CreatureTown = root.Element("CreatureTown")?.Value ?? "",
                    WeeklyGrowth = ParseInt(root, "WeeklyGrowth"),
                    Gold = ParseInt(root.Element("Cost"), "Gold"),
                };

                // Фракция из CreatureTown (приоритетнее чем из пути)
                if (TownToFaction.TryGetValue(creature.CreatureTown, out string? factionName))
                    creature.Faction = factionName;

                // Upgrades
                var upgradesEl = root.Element("Upgrades");
                if (upgradesEl != null)
                {
                    foreach (var upItem in upgradesEl.Elements("Item"))
                    {
                        string val = upItem.Value.Trim();
                        if (!string.IsNullOrEmpty(val))
                            creature.Upgrades.Add(val);
                    }
                }

                // Abilities
                var abilitiesEl = root.Element("Abilities");
                if (abilitiesEl != null)
                {
                    foreach (var abItem in abilitiesEl.Elements("Item"))
                    {
                        string val = abItem.Value.Trim();
                        if (!string.IsNullOrEmpty(val))
                            creature.Abilities.Add(val);
                    }
                }

                // Visual → CreatureVisual → имя + иконка
                string visualHref = root.Element("Visual")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(visualHref))
                {
                    string visualPath = ExtractPath(visualHref);
                    var visualXdb = ReadXdb(visualPath);
                    if (visualXdb?.Root != null)
                    {
                        // Имя юнита
                        string nameHref = visualXdb.Root.Element("CreatureNameFileRef")?.Attribute("href")?.Value ?? "";
                        if (!string.IsNullOrEmpty(nameHref))
                        {
                            string namePath = ResolvePath(visualPath, ExtractPath(nameHref));
                            var nameData = ReadFile(namePath);
                            if (nameData != null)
                            {
                                creature.Name = System.Text.Encoding.UTF8.GetString(nameData).Trim().Trim('\uFEFF');
                            }
                        }

                        // Иконка 64x64
                        string icon64Href = visualXdb.Root.Element("Icon64")?.Attribute("href")?.Value ?? "";
                        if (!string.IsNullOrEmpty(icon64Href))
                        {
                            string iconXdbPath = ResolvePath(visualPath, ExtractPath(icon64Href));
                            creature.Icon = LoadDdsIcon(iconXdbPath);
                        }
                    }
                }

                if (string.IsNullOrEmpty(creature.Name))
                    creature.Name = id;

                result.Add(creature);
            }

            return result;
        }

        /// <summary>
        /// Загружает DDS иконку через цепочку: Texture.xdb → DestName (DDS файл рядом).
        /// </summary>
        private Image? LoadDdsIcon(string textureXdbPath)
        {
            try
            {
                var textureXdb = ReadXdb(textureXdbPath);
                if (textureXdb?.Root == null)
                    return null;

                string destName = textureXdb.Root.Element("DestName")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(destName))
                    return null;

                string ddsPath = ResolvePath(textureXdbPath, destName);
                var ddsData = ReadFile(ddsPath);
                if (ddsData == null)
                    return null;

                return DecodeDds(ddsData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Декодирует DDS байты в Bitmap через Pfim.
        /// </summary>
        private static Image? DecodeDds(byte[] ddsData)
        {
            try
            {
                using var ms = new MemoryStream(ddsData);
                var image = Pfimage.FromStream(ms);

                PixelFormat format;
                switch (image.Format)
                {
                    case Pfim.ImageFormat.Rgba32:
                        format = PixelFormat.Format32bppArgb;
                        break;
                    case Pfim.ImageFormat.Rgb24:
                        format = PixelFormat.Format24bppRgb;
                        break;
                    case Pfim.ImageFormat.Rgba16:
                        format = PixelFormat.Format16bppArgb1555;
                        break;
                    case Pfim.ImageFormat.R5g5b5:
                        format = PixelFormat.Format16bppRgb555;
                        break;
                    case Pfim.ImageFormat.R5g5b5a1:
                        format = PixelFormat.Format16bppArgb1555;
                        break;
                    case Pfim.ImageFormat.R5g6b5:
                        format = PixelFormat.Format16bppRgb565;
                        break;
                    default:
                        format = PixelFormat.Format32bppArgb;
                        break;
                }

                var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                try
                {
                    var ptr = handle.AddrOfPinnedObject();
                    var bitmap = new Bitmap(image.Width, image.Height, image.Stride, format, ptr);
                    // Создаём копию чтобы не зависеть от GCHandle
                    var copy = new Bitmap(bitmap);
                    bitmap.Dispose();
                    return copy;
                }
                finally
                {
                    handle.Free();
                }
            }
            catch
            {
                return null;
            }
        }

        private static int ParseInt(XElement? parent, string elementName)
        {
            if (parent == null)
                return 0;
            string? val = parent.Element(elementName)?.Value;
            if (val != null && int.TryParse(val, out int result))
                return result;
            return 0;
        }
    }
}
