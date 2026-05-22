using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Pfim;

namespace Launcher
{
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
        public int CreatureTier { get; set; }
        public string BaseCreature { get; set; } = "";
        public List<string> Upgrades { get; set; } = new();
        public List<string> Abilities { get; set; } = new();
        public Image? Icon { get; set; }

        public bool IsBase => BaseCreature == "CREATURE_UNKNOWN";
    }

    public class ArtifactInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string Slot { get; set; } = "";
        public int CostOfGold { get; set; }
        public Image? Icon { get; set; }

        public string TypeDisplay => Type switch
        {
            "ARTF_CLASS_MINOR" => "Минорный",
            "ARTF_CLASS_MAJOR" => "Мажорный",
            "ARTF_CLASS_RELIC" => "Реликвия",
            _ => Type,
        };

        private static readonly Dictionary<string, string> SlotDisplayMap = new(StringComparer.Ordinal)
        {
            { "PRIMARY",   "Меч"      },
            { "SECONDARY", "Щит"      },
            { "HEAD",      "Корона"   },
            { "CHEST",     "Кираса"   },
            { "NECK",      "Ожерелье" },
            { "SHOULDERS", "Плащ"     },
            { "FINGER",    "Кольцо"   },
            { "FEET",      "Сапоги"   },
            { "MISCSLOT1", "Карман"   },
        };

        private static readonly Dictionary<string, string> SlotMap = new(StringComparer.Ordinal)
        {
            { "ARTF_SLOT_SWORD",  "PRIMARY"   },
            { "ARTF_SLOT_SHIELD", "SECONDARY" },
            { "ARTF_SLOT_HELM",   "HEAD"      },
            { "ARTF_SLOT_ARMOR",  "CHEST"     },
            { "ARTF_SLOT_NECK",   "NECK"      },
            { "ARTF_SLOT_RING",   "FINGER"    },
            { "ARTF_SLOT_FEET",   "FEET"      },
            { "ARTF_SLOT_CAPE",   "SHOULDERS" },
            { "ARTF_SLOT_MISC",   "MISCSLOT1" },
        };

        public string SlotDisplay => SlotMap.TryGetValue(Slot, out var s) ? s : Slot;
        public string SlotDisplayRu => SlotDisplayMap.TryGetValue(SlotDisplay, out var r) ? r : SlotDisplay;
    }

    internal readonly struct VfsEntry
    {
        public string ArchivePath { get; init; }
        public string EntryName { get; init; }
        public DateTimeOffset LastModified { get; init; }
    }

    public class GameDataParser : IDisposable
    {
        private readonly string _gameRoot;
        private readonly Dictionary<string, VfsEntry> _vfs = new(8192, StringComparer.OrdinalIgnoreCase);

        // Пул открытых архивов, чтобы не тратить время на постоянный I/O при открытии файлов
        private readonly ConcurrentDictionary<string, ZipArchive> _openedArchives = new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, byte[]> _fileCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Image?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _abilityNames = new(StringComparer.Ordinal);

        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

        static GameDataParser()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private static readonly Dictionary<string, string> TownToFaction = new(StringComparer.Ordinal)
        {
            { "TOWN_HEAVEN",     "Орден Света"    },
            { "TOWN_INFERNO",    "Инферно"         },
            { "TOWN_NECROMANCY", "Некрополис"      },
            { "TOWN_PRESERVE",   "Лесной Союз"     },
            { "TOWN_ACADEMY",    "Академия"        },
            { "TOWN_DUNGEON",    "Лига Теней"      },
            { "TOWN_FORTRESS",   "Северные Кланы"  },
            { "TOWN_STRONGHOLD", "Великая Орда"    },
            { "TOWN_NO_TYPE",    "Нейтралы"        },
            { "TOWN_NONE",       "Нейтралы"        },
        };

        private static readonly Dictionary<string, string> PathToFaction = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Haven",     "Орден Света"   },
            { "Inferno",   "Инферно"        },
            { "Necropolis","Некрополис"     },
            { "Preserve",  "Лесной Союз"   },
            { "Academy",   "Академия"      },
            { "Dungeon",   "Лига Теней"    },
            { "Dwarf",     "Северные Кланы"},
            { "Orcs",      "Великая Орда"  },
            { "Neutrals",  "Нейтралы"      },
        };

        public static readonly string[] SelectableFactions =
        {
            "Орден Света", "Инферно", "Некрополис", "Лесной Союз",
            "Академия", "Лига Теней", "Северные Кланы", "Великая Орда"
        };

        public static readonly Dictionary<string, string[]> FactionPathSegments = new(StringComparer.Ordinal)
        {
            { "Орден Света",   new[] { "/Haven/"     } },
            { "Инферно",       new[] { "/Inferno/"   } },
            { "Некрополис",    new[] { "/Necropolis/"} },
            { "Лесной Союз",   new[] { "/Preserve/"  } },
            { "Академия",      new[] { "/Academy/"   } },
            { "Лига Теней",    new[] { "/Dungeon/"   } },
            { "Северные Кланы",new[] { "/Dwarf/"     } },
            { "Великая Орда",  new[] { "/Orcs/"      } },
        };

        public int VfsCount { get; private set; }
        public string DiagInfo { get; private set; } = "";

        public GameDataParser(string gameRoot) => _gameRoot = gameRoot;

        public void BuildVfs()
        {
            _vfs.Clear();
            _fileCache.Clear();
            _iconCache.Clear();

            foreach (var zip in _openedArchives.Values) zip.Dispose();
            _openedArchives.Clear();

            var archives = new List<string>();
            string dataDir = Path.Combine(_gameRoot, "data");
            string userModDir = Path.Combine(_gameRoot, "UserMods");

            if (Directory.Exists(dataDir)) archives.AddRange(Directory.GetFiles(dataDir, "*.pak"));
            if (Directory.Exists(userModDir)) archives.AddRange(Directory.GetFiles(userModDir, "*.h5u"));

            var diagLines = new ConcurrentBag<string>
            {
                $"Путь: {_gameRoot}",
                $"data/: {(Directory.Exists(dataDir) ? "есть" : "НЕТ")}",
                $"Архивов найдено: {archives.Count}"
            };

            var localVfsMaps = new Dictionary<string, VfsEntry>[archives.Count];

            Parallel.For(0, archives.Count, i =>
            {
                string archivePath = archives[i];
                string archiveName = Path.GetFileName(archivePath);
                var local = new Dictionary<string, VfsEntry>(2048, StringComparer.OrdinalIgnoreCase);
                try
                {
                    // Открываем архив один раз и сохраняем его в пул
                    var zip = ZipFile.OpenRead(archivePath);
                    _openedArchives.TryAdd(archivePath, zip);

                    int count = 0;
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
                            continue;

                        count++;
                        string norm = NormalizePath(entry.FullName);

                        if (!local.TryGetValue(norm, out var existing) || entry.LastWriteTime > existing.LastModified)
                        {
                            local[norm] = new VfsEntry
                            {
                                ArchivePath = archivePath,
                                EntryName = entry.FullName,
                                LastModified = entry.LastWriteTime,
                            };
                        }
                    }
                    diagLines.Add($"{archiveName}: {count} файлов");
                }
                catch (Exception ex)
                {
                    diagLines.Add($"{archiveName}: ОШИБКА — {ex.Message}");
                }
                localVfsMaps[i] = local;
            });

            foreach (var local in localVfsMaps)
            {
                if (local == null) continue;
                foreach (var kv in local)
                {
                    if (!_vfs.TryGetValue(kv.Key, out var existing) || kv.Value.LastModified > existing.LastModified)
                        _vfs[kv.Key] = kv.Value;
                }
            }

            VfsCount = _vfs.Count;
            bool hasCreatures = _vfs.ContainsKey("/GameMechanics/RefTables/Creatures.xdb");
            diagLines.Add($"Creatures.xdb: {(hasCreatures ? "найден" : "НЕ НАЙДЕН")}");
            diagLines.Add($"Всего записей VFS: {VfsCount}");

            DiagInfo = string.Join("\n", diagLines.OrderBy(x => x));
        }

        private void PreloadFiles(IEnumerable<string> paths)
        {
            var byArchive = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                string norm = NormalizePath(path);
                if (_fileCache.ContainsKey(norm) || !_vfs.TryGetValue(norm, out var vfsEntry))
                    continue;

                if (!byArchive.TryGetValue(vfsEntry.ArchivePath, out var list))
                    byArchive[vfsEntry.ArchivePath] = list = new List<KeyValuePair<string, string>>();

                list.Add(new KeyValuePair<string, string>(vfsEntry.EntryName, norm));
            }

            Parallel.ForEach(byArchive, kv =>
            {
                string archivePath = kv.Key;
                var needed = kv.Value;

                if (!_openedArchives.TryGetValue(archivePath, out var zip))
                    return;

                lock (zip) // Защищаем обращение к структурам zip-файла из параллельных потоков
                {
                    foreach (var pair in needed)
                    {
                        var entry = zip.GetEntry(pair.Key);
                        if (entry == null) continue;

                        using var stream = entry.Open();
                        using var ms = new MemoryStream((int)entry.Length);
                        stream.CopyTo(ms);
                        _fileCache.TryAdd(pair.Value, ms.ToArray());
                    }
                }
            });
        }

        private byte[]? ReadFile(string virtualPath)
        {
            string norm = NormalizePath(virtualPath);

            if (_fileCache.TryGetValue(norm, out var cached))
                return cached;

            if (!_vfs.TryGetValue(norm, out var vfsEntry))
                return null;

            try
            {
                if (!_openedArchives.TryGetValue(vfsEntry.ArchivePath, out var zip))
                    return null;

                lock (zip)
                {
                    var entry = zip.GetEntry(vfsEntry.EntryName);
                    if (entry == null) return null;

                    using var stream = entry.Open();
                    using var ms = new MemoryStream((int)entry.Length);
                    stream.CopyTo(ms);
                    var data = ms.ToArray();
                    _fileCache.TryAdd(norm, data);
                    return data;
                }
            }
            catch { return null; }
        }

        private XDocument? ReadXdb(string virtualPath)
        {
            var data = ReadFile(virtualPath);
            if (data == null) return null;
            try
            {
                using var ms = new MemoryStream(data);
                return XDocument.Load(ms);
            }
            catch { return null; }
        }

        private static string NormalizePath(string path) =>
            path.StartsWith('/') ? path.Replace('\\', '/') : "/" + path.Replace('\\', '/');

        private static string ExtractPath(string href)
        {
            int idx = href.IndexOf('#');
            return idx >= 0 ? href[..idx] : href;
        }

        private static string ResolvePath(string basePath, string relativePath)
        {
            if (relativePath.StartsWith('/'))
                return relativePath;

            string baseDir = basePath[..(basePath.LastIndexOf('/') + 1)];
            string combined = baseDir + relativePath;

            var parts = combined.Split('/');
            var stack = new Stack<string>(parts.Length);
            foreach (string part in parts)
            {
                if (part == "..")
                {
                    if (stack.Count > 0) stack.Pop();
                }
                else if (part != "." && part != "")
                {
                    stack.Push(part);
                }
            }
            return "/" + string.Join("/", stack.Reverse());
        }

        private void BuildAbilityNames()
        {
            _abilityNames.Clear();

            var abilitiesXdb = ReadXdb("/GameMechanics/RefTables/CombatAbilities.xdb");
            if (abilitiesXdb == null) return;

            var items = abilitiesXdb.Root?.Element("objects")?.Elements("Item") ?? abilitiesXdb.Descendants("Item");
            var namePaths = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

            Parallel.ForEach(items, item =>
            {
                string id = item.Element("ID")?.Value ?? "";
                if (string.IsNullOrEmpty(id) || id == "ABILITY_NONE") return;

                string nameHref = item.Element("obj")?.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                    namePaths[id] = ExtractPath(nameHref);
            });

            PreloadFiles(namePaths.Values);

            Parallel.ForEach(namePaths, kv =>
            {
                var data = ReadFile(kv.Value);
                _abilityNames[kv.Key] = data != null ? DetectAndDecode(data) : kv.Key;
            });
        }

        public List<CreatureInfo> ParseCreatures(List<string>? factions = null)
        {
            var creaturesXdb = ReadXdb("/GameMechanics/RefTables/Creatures.xdb");
            if (creaturesXdb == null) return new();

            if (_abilityNames.Count == 0)
                BuildAbilityNames();

            var items = creaturesXdb.Root?.Element("objects")?.Elements("Item") ?? creaturesXdb.Descendants("Item");
            var creatureMeta = new ConcurrentBag<(string id, string creaturePath)>();
            var allCreaturePaths = new ConcurrentBag<string>();

            Parallel.ForEach(items, item =>
            {
                string id = item.Element("ID")?.Value ?? "";
                if (id == "CREATURE_UNKNOWN") return;

                string href = item.Element("Obj")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(href)) return;

                if (factions != null && !MatchesFactions(href, factions)) return;

                string cp = ExtractPath(href);
                creatureMeta.Add((id, cp));
                allCreaturePaths.Add(cp);
            });

            PreloadFiles(allCreaturePaths);

            var visualPaths = new ConcurrentBag<string>();
            Parallel.ForEach(creatureMeta, meta =>
            {
                var cXdb = ReadXdb(meta.creaturePath);
                string vHref = cXdb?.Root?.Element("Visual")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(vHref))
                    visualPaths.Add(ExtractPath(vHref));
            });

            PreloadFiles(visualPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var namePaths = new ConcurrentBag<string>();
            var iconXdbPaths = new ConcurrentBag<string>();

            Parallel.ForEach(visualPaths.Distinct(StringComparer.OrdinalIgnoreCase), vp =>
            {
                var vXdb = ReadXdb(vp);
                if (vXdb?.Root == null) return;

                string nameHref = vXdb.Root.Element("CreatureNameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                    namePaths.Add(ResolvePath(vp, ExtractPath(nameHref)));

                string iconHref = vXdb.Root.Element("Icon64")?.Attribute("href")?.Value
                               ?? vXdb.Root.Element("Icon128")?.Attribute("href")?.Value
                               ?? "";
                if (!string.IsNullOrEmpty(iconHref))
                    iconXdbPaths.Add(ResolvePath(vp, ExtractPath(iconHref)));
            });

            PreloadFiles(namePaths.Distinct(StringComparer.OrdinalIgnoreCase));
            PreloadFiles(iconXdbPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var ddsPaths = new ConcurrentBag<string>();
            Parallel.ForEach(iconXdbPaths.Distinct(StringComparer.OrdinalIgnoreCase), ixp =>
            {
                var texXdb = ReadXdb(ixp);
                string destName = texXdb?.Root?.Element("DestName")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(destName))
                    ddsPaths.Add(ResolvePath(ixp, destName));
            });
            PreloadFiles(ddsPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var result = new ConcurrentBag<CreatureInfo>();

            Parallel.ForEach(creatureMeta, meta =>
            {
                var cXdb = ReadXdb(meta.creaturePath);
                if (cXdb?.Root == null) return;

                var root = cXdb.Root;
                var creature = new CreatureInfo
                {
                    Id = meta.id,
                    Faction = DetectFactionFromPath(meta.creaturePath),
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
                    CreatureTier = ParseInt(root, "CreatureTier"),
                    BaseCreature = root.Element("BaseCreature")?.Value ?? "CREATURE_UNKNOWN",
                };

                if (TownToFaction.TryGetValue(creature.CreatureTown, out string? fn))
                    creature.Faction = fn;

                var upgradesNode = root.Element("Upgrades");
                if (upgradesNode != null)
                {
                    foreach (var upItem in upgradesNode.Elements("Item"))
                    {
                        string val = upItem.Value.Trim();
                        if (val.Length > 0) creature.Upgrades.Add(val);
                    }
                }

                var abilitiesNode = root.Element("Abilities");
                if (abilitiesNode != null)
                {
                    foreach (var abItem in abilitiesNode.Elements("Item"))
                    {
                        string val = abItem.Value.Trim();
                        if (val.Length == 0) continue;
                        creature.Abilities.Add(_abilityNames.TryGetValue(val, out string? n) ? n : val);
                    }
                }

                string visualHref = root.Element("Visual")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(visualHref))
                {
                    string vp = ExtractPath(visualHref);
                    var vXdb = ReadXdb(vp);
                    if (vXdb?.Root != null)
                    {
                        string nameHref = vXdb.Root.Element("CreatureNameFileRef")?.Attribute("href")?.Value ?? "";
                        if (!string.IsNullOrEmpty(nameHref))
                        {
                            string np = ResolvePath(vp, ExtractPath(nameHref));
                            var nd = ReadFile(np);
                            if (nd != null) creature.Name = DetectAndDecode(nd);
                        }

                        string iconHref = vXdb.Root.Element("Icon64")?.Attribute("href")?.Value
                                       ?? vXdb.Root.Element("Icon128")?.Attribute("href")?.Value
                                       ?? "";
                        if (!string.IsNullOrEmpty(iconHref))
                        {
                            string iconXdbPath = ResolvePath(vp, ExtractPath(iconHref));
                            creature.Icon = LoadDdsIcon(iconXdbPath);
                        }
                    }
                }

                if (string.IsNullOrEmpty(creature.Name))
                    creature.Name = meta.id;

                result.Add(creature);
            });

            return result.ToList();
        }

        public List<ArtifactInfo> ParseArtifacts()
        {
            var artifactsXdb = ReadXdb("/GameMechanics/RefTables/Artifacts.xdb");
            if (artifactsXdb == null) return new();

            var items = artifactsXdb.Root?.Element("objects")?.Elements("Item") ?? artifactsXdb.Descendants("Item");
            var validItems = new ConcurrentBag<(XElement item, XElement data)>();
            var preloadPaths = new ConcurrentBag<string>();

            Parallel.ForEach(items, item =>
            {
                string id = item.Element("ID")?.Value ?? "";
                if (string.IsNullOrEmpty(id)) return;

                var data = item.Element("obj") ?? item;
                if (data.Element("CanBeGeneratedToSell")?.Value != "true") return;

                validItems.Add((item, data));

                CollectPath(data, "NameFileRef", null, preloadPaths);
                CollectPath(data, "DescriptionFileRef", null, preloadPaths);
                CollectPath(data, "Icon", null, preloadPaths);
            });

            PreloadFiles(preloadPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var ddsPaths = new ConcurrentBag<string>();
            Parallel.ForEach(validItems, pair =>
            {
                string iconHref = pair.data.Element("Icon")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(iconHref)) return;

                string iconXdbPath = ExtractPath(iconHref);
                var texXdb = ReadXdb(iconXdbPath);
                string destName = texXdb?.Root?.Element("DestName")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(destName))
                    ddsPaths.Add(ResolvePath(iconXdbPath, destName));
            });
            PreloadFiles(ddsPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var result = new ConcurrentBag<ArtifactInfo>();

            Parallel.ForEach(validItems, pair =>
            {
                string id = pair.item.Element("ID")!.Value;

                var artifact = new ArtifactInfo
                {
                    Id = id,
                    Type = pair.data.Element("Type")?.Value ?? "",
                    Slot = pair.data.Element("Slot")?.Value ?? "",
                    CostOfGold = ParseInt(pair.data, "CostOfGold"),
                };

                string nameHref = pair.data.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                {
                    var nd = ReadFile(ExtractPath(nameHref));
                    if (nd != null) artifact.Name = StripTags(DetectAndDecode(nd));
                }
                if (string.IsNullOrEmpty(artifact.Name)) artifact.Name = id;

                string descHref = pair.data.Element("DescriptionFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(descHref))
                {
                    var dd = ReadFile(ExtractPath(descHref));
                    if (dd != null) artifact.Description = StripTags(DetectAndDecode(dd));
                }

                string iconHref = pair.data.Element("Icon")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(iconHref))
                    artifact.Icon = LoadDdsIcon(ExtractPath(iconHref));

                result.Add(artifact);
            });

            return result.ToList();
        }

        private static void CollectPath(XElement data, string elementName, string? basePath, ConcurrentBag<string> target)
        {
            string href = data.Element(elementName)?.Attribute("href")?.Value ?? "";
            if (string.IsNullOrEmpty(href)) return;
            string path = ExtractPath(href);
            target.Add(basePath != null ? ResolvePath(basePath, path) : path);
        }

        private static bool MatchesFactions(string href, List<string> factions)
        {
            foreach (string faction in factions)
                if (FactionPathSegments.TryGetValue(faction, out var segs))
                    foreach (string seg in segs)
                        if (href.Contains(seg, StringComparison.OrdinalIgnoreCase))
                            return true;
            return false;
        }

        private static string DetectFactionFromPath(string path)
        {
            foreach (var kv in PathToFaction)
                if (path.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            return "Нейтралы";
        }

        private Image? LoadDdsIcon(string textureXdbPath)
        {
            if (_iconCache.TryGetValue(textureXdbPath, out var cached))
                return cached;

            Image? icon = null;
            try
            {
                var texXdb = ReadXdb(textureXdbPath);
                string destName = texXdb?.Root?.Element("DestName")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(destName))
                {
                    string ddsPath = ResolvePath(textureXdbPath, destName);
                    var ddsData = ReadFile(ddsPath);
                    if (ddsData != null) icon = DecodeDds(ddsData);
                }
            }
            catch { }

            _iconCache.TryAdd(textureXdbPath, icon);
            return icon;
        }

        private static Image? DecodeDds(byte[] ddsData)
        {
            try
            {
                using var ms = new MemoryStream(ddsData);
                using var image = Pfimage.FromStream(ms);

                PixelFormat format = image.Format switch
                {
                    Pfim.ImageFormat.Rgba32 => PixelFormat.Format32bppArgb,
                    Pfim.ImageFormat.Rgb24 => PixelFormat.Format24bppRgb,
                    Pfim.ImageFormat.Rgba16 => PixelFormat.Format16bppArgb1555,
                    Pfim.ImageFormat.R5g5b5 => PixelFormat.Format16bppRgb555,
                    Pfim.ImageFormat.R5g5b5a1 => PixelFormat.Format16bppArgb1555,
                    Pfim.ImageFormat.R5g6b5 => PixelFormat.Format16bppRgb565,
                    _ => PixelFormat.Format32bppArgb,
                };

                var handle = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
                try
                {
                    var ptr = handle.AddrOfPinnedObject();
                    using var bitmap = new Bitmap(image.Width, image.Height, image.Stride, format, ptr);

                    var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(copy))
                    {
                        g.DrawImage(bitmap, 0, 0, image.Width, image.Height);
                    }
                    return copy;
                }
                finally { handle.Free(); }
            }
            catch { return null; }
        }

        private static string DetectAndDecode(byte[] data)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return Encoding.Unicode.GetString(data).Trim().TrimStart('\uFEFF');

            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(data).Trim().TrimStart('\uFEFF');

            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return Encoding.UTF8.GetString(data).Trim().TrimStart('\uFEFF');

            if (data.Length >= 8)
            {
                int nullsOdd = 0, nullsEven = 0;
                int check = Math.Min(data.Length & ~1, 40);
                for (int i = 0; i < check; i += 2)
                {
                    if (data[i] == 0) nullsEven++;
                    if (data[i + 1] == 0) nullsOdd++;
                }
                if (nullsOdd >= check / 4)
                    return Encoding.Unicode.GetString(data).Trim();
                if (nullsEven >= check / 4)
                    return Encoding.BigEndianUnicode.GetString(data).Trim();
            }

            try
            {
                var decoder = Encoding.UTF8.GetDecoder();
                decoder.Fallback = DecoderFallback.ExceptionFallback;
                int charCount = decoder.GetCharCount(data, 0, data.Length);
                var chars = new char[charCount];
                decoder.GetChars(data, 0, data.Length, chars, 0);
                return new string(chars).Trim();
            }
            catch
            {
                try
                {
                    return Encoding.GetEncoding(1251).GetString(data).Trim();
                }
                catch
                {
                    return Encoding.UTF8.GetString(data).Trim();
                }
            }
        }

        private static string StripTags(string text) => TagRegex.Replace(text, "").Trim();

        private static int ParseInt(XElement? parent, string elementName)
        {
            if (parent == null) return 0;
            string? val = parent.Element(elementName)?.Value;
            return val != null && int.TryParse(val, out int r) ? r : 0;
        }

        public void Dispose()
        {
            foreach (var zip in _openedArchives.Values)
            {
                try { zip.Dispose(); } catch { }
            }
            _openedArchives.Clear();
        }
    }
}