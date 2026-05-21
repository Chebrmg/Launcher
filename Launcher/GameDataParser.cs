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
        private Dictionary<string, byte[]> _fileCache = new();
        private Dictionary<string, string> _abilityNames = new();

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

        // Фракции для выбора (без нейтралов)
        public static readonly string[] SelectableFactions =
        {
            "Haven", "Inferno", "Necropolis", "Sylvan",
            "Academy", "Dungeon", "Fortress", "Stronghold"
        };

        // Фракция → сегменты пути для фильтрации в Creatures.xdb
        public static readonly Dictionary<string, string[]> FactionPathSegments = new()
        {
            { "Haven", new[] { "/Haven/" } },
            { "Inferno", new[] { "/Inferno/" } },
            { "Necropolis", new[] { "/Necropolis/" } },
            { "Sylvan", new[] { "/Preserve/" } },
            { "Academy", new[] { "/Academy/" } },
            { "Dungeon", new[] { "/Dungeon/" } },
            { "Fortress", new[] { "/Dwarf/" } },
            { "Stronghold", new[] { "/Orcs/" } },
        };

        public int VfsCount => _vfs.Count;
        public string DiagInfo { get; private set; } = "";

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

            var diagLines = new List<string>();
            diagLines.Add($"Путь: {_gameRoot}");
            diagLines.Add($"data/: {(Directory.Exists(Path.Combine(_gameRoot, "data")) ? "есть" : "НЕТ")}");
            diagLines.Add($"Архивов найдено: {archives.Count}");

            foreach (string archivePath in archives)
            {
                string archiveName = Path.GetFileName(archivePath);
                try
                {
                    using var zip = ZipFile.OpenRead(archivePath);
                    int entryCount = 0;
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        entryCount++;
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
                    diagLines.Add($"{archiveName}: {entryCount} файлов");
                }
                catch (Exception ex)
                {
                    diagLines.Add($"{archiveName}: ОШИБКА — {ex.Message}");
                }
            }

            bool hasCreatures = _vfs.ContainsKey("/GameMechanics/RefTables/Creatures.xdb");
            diagLines.Add($"Creatures.xdb: {(hasCreatures ? "найден" : "НЕ НАЙДЕН")}");

            DiagInfo = string.Join("\n", diagLines);
        }

        /// <summary>
        /// Предзагружает нужные файлы из архивов в кэш за один проход.
        /// </summary>
        private void PreloadFiles(IEnumerable<string> paths)
        {
            // Группируем запрашиваемые пути по архивам
            var byArchive = new Dictionary<string, List<(string normalized, string entryName)>>();
            foreach (string path in paths)
            {
                string normalized = "/" + path.Replace('\\', '/').TrimStart('/');
                if (_fileCache.ContainsKey(normalized))
                    continue;
                if (!_vfs.TryGetValue(normalized, out var vfsEntry))
                    continue;

                if (!byArchive.ContainsKey(vfsEntry.ArchivePath))
                    byArchive[vfsEntry.ArchivePath] = new();
                byArchive[vfsEntry.ArchivePath].Add((normalized, vfsEntry.EntryName));
            }

            // Читаем каждый архив один раз
            foreach (var kv in byArchive)
            {
                try
                {
                    var needed = new HashSet<string>(kv.Value.Select(x => x.entryName));
                    using var zip = ZipFile.OpenRead(kv.Key);
                    foreach (var entry in zip.Entries)
                    {
                        if (needed.Contains(entry.FullName))
                        {
                            string norm = kv.Value.First(x => x.entryName == entry.FullName).normalized;
                            using var stream = entry.Open();
                            using var ms = new MemoryStream();
                            stream.CopyTo(ms);
                            _fileCache[norm] = ms.ToArray();
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Читает файл из кэша или из архива.
        /// </summary>
        private byte[]? ReadFile(string virtualPath)
        {
            string normalized = "/" + virtualPath.Replace('\\', '/').TrimStart('/');

            if (_fileCache.TryGetValue(normalized, out var cached))
                return cached;

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
                var data = ms.ToArray();
                _fileCache[normalized] = data;
                return data;
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
        /// Парсит CombatAbilities.xdb и загружает имена способностей из .txt файлов.
        /// </summary>
        private void BuildAbilityNames()
        {
            _abilityNames.Clear();

            var abilitiesXdb = ReadXdb("/GameMechanics/RefTables/CombatAbilities.xdb");
            if (abilitiesXdb == null)
                return;

            // Собираем пути к .txt файлам имён
            var namePaths = new Dictionary<string, string>(); // ID → path
            foreach (var item in abilitiesXdb.Descendants("Item"))
            {
                string id = item.Element("ID")?.Value ?? "";
                if (string.IsNullOrEmpty(id) || id == "ABILITY_NONE")
                    continue;

                string nameHref = item.Element("obj")?.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(nameHref))
                    continue;

                namePaths[id] = ExtractPath(nameHref);
            }

            // Предзагружаем все .txt файлы одним проходом
            PreloadFiles(namePaths.Values);

            // Читаем имена
            foreach (var kv in namePaths)
            {
                var data = ReadFile(kv.Value);
                if (data != null)
                    _abilityNames[kv.Key] = DetectAndDecode(data);
                else
                    _abilityNames[kv.Key] = kv.Key; // fallback на ID
            }
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
        /// Проверяет, принадлежит ли путь к юниту указанным фракциям.
        /// </summary>
        private static bool MatchesFactions(string href, List<string> factions)
        {
            foreach (string faction in factions)
            {
                if (FactionPathSegments.TryGetValue(faction, out var segments))
                {
                    foreach (string seg in segments)
                    {
                        if (href.Contains(seg))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Парсит юнитов из игровых архивов.
        /// factions — список фракций для фильтрации (null = все).
        /// </summary>
        public List<CreatureInfo> ParseCreatures(List<string>? factions = null)
        {
            var result = new List<CreatureInfo>();

            var creaturesXdb = ReadXdb("/GameMechanics/RefTables/Creatures.xdb");
            if (creaturesXdb == null)
                return result;

            var items = creaturesXdb.Descendants("Item").ToList();

            // Загружаем словарь имён способностей
            if (_abilityNames.Count == 0)
                BuildAbilityNames();

            // Собираем пути файлов Creature.xdb (с фильтром по фракциям)
            var creaturePaths = new List<string>();
            foreach (var item in items)
            {
                string id = item.Element("ID")?.Value ?? "";
                if (id == "CREATURE_UNKNOWN") continue;
                string href = item.Element("Obj")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(href)) continue;

                // Фильтр по фракциям через путь
                if (factions != null && !MatchesFactions(href, factions))
                    continue;

                creaturePaths.Add(ExtractPath(href));
            }

            // Предзагружаем Creature.xdb файлы одним проходом
            PreloadFiles(creaturePaths);

            // Собираем Visual пути из загруженных Creature.xdb для второй предзагрузки
            var visualPaths = new List<string>();
            foreach (string cp in creaturePaths)
            {
                var cXdb = ReadXdb(cp);
                string vHref = cXdb?.Root?.Element("Visual")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(vHref))
                    visualPaths.Add(ExtractPath(vHref));
            }
            PreloadFiles(visualPaths);

            // Собираем пути к именам и иконкам из Visual.xdb
            var extraPaths = new List<string>();
            foreach (string vp in visualPaths)
            {
                var vXdb = ReadXdb(vp);
                if (vXdb?.Root == null) continue;

                string nameHref = vXdb.Root.Element("CreatureNameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                    extraPaths.Add(ResolvePath(vp, ExtractPath(nameHref)));

                string iconHref = vXdb.Root.Element("Icon64")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(iconHref))
                {
                    string iconXdbPath = ResolvePath(vp, ExtractPath(iconHref));
                    extraPaths.Add(iconXdbPath);
                    // Попробуем прочитать Texture.xdb чтобы узнать путь к DDS
                    var texXdb = ReadXdb(iconXdbPath);
                    string destName = texXdb?.Root?.Element("DestName")?.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(destName))
                        extraPaths.Add(ResolvePath(iconXdbPath, destName));
                }
            }
            PreloadFiles(extraPaths);

            foreach (var item in items)
            {
                string id = item.Element("ID")?.Value ?? "";
                if (id == "CREATURE_UNKNOWN")
                    continue;

                string objHref = item.Element("Obj")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(objHref))
                    continue;

                if (factions != null && !MatchesFactions(objHref, factions))
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

                // Abilities (с подтягиванием имён из CombatAbilities.xdb)
                var abilitiesEl = root.Element("Abilities");
                if (abilitiesEl != null)
                {
                    foreach (var abItem in abilitiesEl.Elements("Item"))
                    {
                        string val = abItem.Value.Trim();
                        if (!string.IsNullOrEmpty(val))
                        {
                            string displayName = _abilityNames.TryGetValue(val, out string? name)
                                ? name : val;
                            creature.Abilities.Add(displayName);
                        }
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
                                creature.Name = DetectAndDecode(nameData);
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

        /// <summary>
        /// Определяет кодировку текстового файла по BOM и декодирует.
        /// </summary>
        private static string DetectAndDecode(byte[] data)
        {
            // UTF-16 LE BOM: FF FE
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return System.Text.Encoding.Unicode.GetString(data).Trim().Trim('\uFEFF');

            // UTF-16 BE BOM: FE FF
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                return System.Text.Encoding.BigEndianUnicode.GetString(data).Trim().Trim('\uFEFF');

            // UTF-8 BOM: EF BB BF
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return System.Text.Encoding.UTF8.GetString(data).Trim().Trim('\uFEFF');

            // Нет BOM — проверяем на UTF-16 LE (частые нулевые байты)
            if (data.Length >= 4)
            {
                int nullCount = 0;
                for (int i = 1; i < Math.Min(data.Length, 20); i += 2)
                    if (data[i] == 0) nullCount++;
                if (nullCount > 2)
                    return System.Text.Encoding.Unicode.GetString(data).Trim();
            }

            // Fallback: UTF-8
            return System.Text.Encoding.UTF8.GetString(data).Trim();
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
