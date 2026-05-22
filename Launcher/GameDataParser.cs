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

    public class SkillPrerequisite
    {
        public string HeroClass { get; set; } = "";
        public List<string> DependencyIds { get; set; } = new();
    }

    public class SkillInfo
    {
        public string Id { get; set; } = "";
        public string SkillType { get; set; } = "";
        public string HeroClass { get; set; } = "";
        public string BasicSkillId { get; set; } = "";
        public List<string> Names { get; set; } = new();
        public List<string> Descriptions { get; set; } = new();
        public List<Image?> Icons { get; set; } = new();
        public List<SkillPrerequisite> Prerequisites { get; set; } = new();

        public bool IsSkill => SkillType == "SKILLTYPE_SKILL";
        public bool IsPerk => !IsSkill;
        public bool IsStandardPerk => SkillType == "SKILLTYPE_STANDART_PERK";
        public bool IsClassPerk => SkillType == "SKILLTYPE_CLASS_PERK";
        public bool IsSpecialPerk => SkillType == "SKILLTYPE_SPECIAL_PERK";
        public bool IsUniquePerk => SkillType == "SKILLTYPE_UINQUE_PERK";
        public bool IsRacial => HeroClass != "HERO_CLASS_NONE" && HeroClass != "";

        public bool IsPrimaryPerk => IsPerk && Prerequisites.Count == 0;
        public bool IsSecondaryPerk => IsPerk && Prerequisites.Count > 0;

        public int MasteryLevels => IsSkill ? Names.Count : 0;

        public string GetName(int mastery = 0)
        {
            if (IsSkill && mastery >= 0 && mastery < Names.Count) return Names[mastery];
            if (Names.Count > 0) return Names[0];
            return Id;
        }

        public string GetDescription(int mastery = 0)
        {
            if (IsSkill && mastery >= 0 && mastery < Descriptions.Count) return Descriptions[mastery];
            if (Descriptions.Count > 0) return Descriptions[0];
            return "";
        }

        public Image? GetIcon(int mastery = 0)
        {
            if (IsSkill)
            {
                int idx = mastery + 1;
                return idx >= 0 && idx < Icons.Count ? Icons[idx] : null;
            }
            return Icons.Count > 1 ? Icons[1] : (Icons.Count > 0 ? Icons[0] : null);
        }
    }

    public class HeroClassInfo
    {
        public string Id { get; set; } = "";
        public Dictionary<string, int> SkillProbs { get; set; } = new();
        public int OffenceProb { get; set; }
        public int DefenceProb { get; set; }
        public int SpellpowerProb { get; set; }
        public int KnowledgeProb { get; set; }
    }

    public class HeroInfo
    {
        public string InternalName { get; set; } = "";
        public string Name { get; set; } = "";
        public string HeroClass { get; set; } = "";
        public string TownType { get; set; } = "";
        public string PrimarySkillId { get; set; } = "";
        public string PrimarySkillMastery { get; set; } = "";
        public string SpecializationName { get; set; } = "";
        public string SpecializationDesc { get; set; } = "";
        public Image? FaceIcon { get; set; }
        public int Offence { get; set; }
        public int Defence { get; set; }
        public int Spellpower { get; set; }
        public int Knowledge { get; set; }
        public List<(string SkillId, string Mastery)> Skills { get; set; } = new();
        public List<string> PerkIds { get; set; } = new();
        public List<string> SpellIds { get; set; } = new();

        public string Faction => GameDataParser.TownToFactionPublic(TownType);
    }

    public class SpellResourceCost
    {
        public int Wood { get; set; }
        public int Ore { get; set; }
        public int Mercury { get; set; }
        public int Crystal { get; set; }
        public int Sulfur { get; set; }
        public int Gem { get; set; }
        public int Gold { get; set; }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Wood > 0) parts.Add($"Дерево: {Wood}");
            if (Ore > 0) parts.Add($"Руда: {Ore}");
            if (Mercury > 0) parts.Add($"Ртуть: {Mercury}");
            if (Crystal > 0) parts.Add($"Кристалл: {Crystal}");
            if (Sulfur > 0) parts.Add($"Сера: {Sulfur}");
            if (Gem > 0) parts.Add($"Самоцветы: {Gem}");
            if (Gold > 0) parts.Add($"Золото: {Gold}");
            return parts.Count > 0 ? string.Join(", ", parts) : "0";
        }
    }

    public class SpellInfo
    {
        public string Id { get; set; } = "";
        public string GameId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Image? Icon { get; set; }
        public int Level { get; set; }
        public string MagicSchool { get; set; } = "";
        public int ManaCost { get; set; }
        public SpellResourceCost? ResourceCost { get; set; }
        public string SourcePath { get; set; } = "";
        public string ObjHref { get; set; } = "";

        public bool IsRunic => MagicSchool == "MAGIC_SCHOOL_RUNIC";
        public bool IsWarcry => MagicSchool == "MAGIC_SCHOOL_WARCRIES";

        public string SchoolDisplayName => MagicSchool switch
        {
            "MAGIC_SCHOOL_DARK" => "Тёмная магия",
            "MAGIC_SCHOOL_DESTRUCTIVE" => "Магия Хаоса",
            "MAGIC_SCHOOL_LIGHT" => "Магия Света",
            "MAGIC_SCHOOL_SUMMONING" => "Магия Призыва",
            "MAGIC_SCHOOL_RUNIC" => "Рунная магия",
            "MAGIC_SCHOOL_WARCRIES" => "Боевые кличи",
            _ => MagicSchool
        };
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

        public static readonly Dictionary<string, string> FactionToHeroClass = new(StringComparer.Ordinal)
        {
            { "Орден Света",    "HERO_CLASS_KNIGHT"      },
            { "Инферно",        "HERO_CLASS_DEMON_LORD"   },
            { "Некрополис",     "HERO_CLASS_NECROMANCER"  },
            { "Лесной Союз",    "HERO_CLASS_RANGER"       },
            { "Академия",       "HERO_CLASS_WIZARD"       },
            { "Лига Теней",     "HERO_CLASS_WARLOCK"      },
            { "Северные Кланы", "HERO_CLASS_RUNEMAGE"     },
            { "Великая Орда",   "HERO_CLASS_BARBARIAN"    },
        };

        public static string TownToFactionPublic(string town) =>
            TownToFaction.TryGetValue(town, out var f) ? f : "Нейтралы";

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

        public List<SkillInfo> ParseSkills()
        {
            var skillsXdb = ReadXdb("/GameMechanics/RefTables/Skills.xdb");
            if (skillsXdb == null) return new();

            var items = skillsXdb.Root?.Element("objects")?.Elements("Item") ?? skillsXdb.Descendants("Item");
            var validItems = new ConcurrentBag<(XElement item, XElement data)>();

            foreach (var item in items)
            {
                string id = item.Element("ID")?.Value ?? "";
                if (string.IsNullOrEmpty(id) || id == "HERO_SKILL_NONE") continue;
                var data = item.Element("obj") ?? item;
                validItems.Add((item, data));
            }

            var textPaths = new ConcurrentBag<string>();
            var iconXdbPaths = new ConcurrentBag<string>();

            Parallel.ForEach(validItems, pair =>
            {
                var data = pair.data;

                foreach (var nameItem in data.Element("NameFileRef")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = nameItem.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(href)) textPaths.Add(ExtractPath(href));
                }

                foreach (var descItem in data.Element("DescriptionFileRef")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = descItem.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(href)) textPaths.Add(ExtractPath(href));
                }

                foreach (var texItem in data.Element("Texture")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = texItem.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(href)) iconXdbPaths.Add(ExtractPath(href));
                }
            });

            PreloadFiles(textPaths.Distinct(StringComparer.OrdinalIgnoreCase));
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

            var result = new ConcurrentBag<SkillInfo>();

            Parallel.ForEach(validItems, pair =>
            {
                string id = pair.item.Element("ID")!.Value;
                var data = pair.data;

                var skill = new SkillInfo
                {
                    Id = id,
                    SkillType = data.Element("SkillType")?.Value ?? "",
                    HeroClass = data.Element("HeroClass")?.Value ?? "",
                    BasicSkillId = data.Element("BasicSkillID")?.Value ?? "",
                };

                foreach (var nameItem in data.Element("NameFileRef")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = nameItem.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(href))
                    {
                        var nd = ReadFile(ExtractPath(href));
                        skill.Names.Add(nd != null ? StripTags(DetectAndDecode(nd)) : id);
                    }
                }

                foreach (var descItem in data.Element("DescriptionFileRef")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = descItem.Attribute("href")?.Value ?? "";
                    if (!string.IsNullOrEmpty(href))
                    {
                        var dd = ReadFile(ExtractPath(href));
                        skill.Descriptions.Add(dd != null ? StripTags(DetectAndDecode(dd)) : "");
                    }
                }

                foreach (var texItem in data.Element("Texture")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string href = texItem.Attribute("href")?.Value ?? "";
                    if (string.IsNullOrEmpty(href))
                    {
                        skill.Icons.Add(null);
                    }
                    else
                    {
                        skill.Icons.Add(LoadDdsIcon(ExtractPath(href)));
                    }
                }

                var prereqs = data.Element("SkillPrerequisites")?.Elements("Item");
                if (prereqs != null)
                {
                    foreach (var prereqItem in prereqs)
                    {
                        var prereq = new SkillPrerequisite
                        {
                            HeroClass = prereqItem.Element("Class")?.Value ?? "",
                        };
                        foreach (var depItem in prereqItem.Element("dependenciesIDs")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                        {
                            string val = depItem.Value.Trim();
                            if (!string.IsNullOrEmpty(val)) prereq.DependencyIds.Add(val);
                        }
                        skill.Prerequisites.Add(prereq);
                    }
                }

                if (skill.Names.Count == 0) skill.Names.Add(id);

                result.Add(skill);
            });

            return result.ToList();
        }

        public List<HeroClassInfo> ParseHeroClasses()
        {
            var classXdb = ReadXdb("/GameMechanics/RefTables/HeroClass.xdb");
            if (classXdb == null) return new();

            var items = classXdb.Root?.Element("objects")?.Elements("Item") ?? classXdb.Descendants("Item");
            var result = new List<HeroClassInfo>();

            foreach (var item in items)
            {
                string id = item.Element("ID")?.Value ?? "";
                if (string.IsNullOrEmpty(id) || id == "HERO_CLASS_NONE") continue;

                var data = item.Element("obj") ?? item;
                var hci = new HeroClassInfo { Id = id };

                foreach (var sp in data.Element("SkillsProbs")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string skillId = sp.Element("SkillID")?.Value ?? "";
                    int prob = ParseInt(sp, "Prob");
                    if (!string.IsNullOrEmpty(skillId)) hci.SkillProbs[skillId] = prob;
                }

                var ap = data.Element("AttributeProbs");
                if (ap != null)
                {
                    hci.OffenceProb = ParseInt(ap, "OffenceProb");
                    hci.DefenceProb = ParseInt(ap, "DefenceProb");
                    hci.SpellpowerProb = ParseInt(ap, "SpellpowerProb");
                    hci.KnowledgeProb = ParseInt(ap, "KnowledgeProb");
                }

                result.Add(hci);
            }

            return result;
        }

        public List<HeroInfo> ParseHeroes()
        {
            var anyXdb = ReadXdb("/MapObjects/_(AdvMapSharedGroup)/Heroes/Any.xdb");
            if (anyXdb == null) return new();

            var links = anyXdb.Root?.Element("links")?.Elements("Item") ?? anyXdb.Descendants("Item");
            var heroPaths = new ConcurrentBag<string>();
            foreach (var link in links)
            {
                string href = link.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(href)) heroPaths.Add(ExtractPath(href));
            }

            PreloadFiles(heroPaths.Distinct(StringComparer.OrdinalIgnoreCase));

            var validHeroes = new ConcurrentBag<(string path, XDocument doc)>();
            Parallel.ForEach(heroPaths.Distinct(StringComparer.OrdinalIgnoreCase), hp =>
            {
                var doc = ReadXdb(hp);
                if (doc?.Root == null) return;
                string scenario = doc.Root.Element("ScenarioHero")?.Value ?? "false";
                if (scenario == "true") return;
                validHeroes.Add((hp, doc));
            });

            var textPaths = new ConcurrentBag<string>();
            var iconXdbPaths = new ConcurrentBag<string>();

            foreach (var (path, doc) in validHeroes)
            {
                var root = doc.Root!;
                var editable = root.Element("Editable");

                string nameHref = editable?.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref)) textPaths.Add(ResolvePath(path, ExtractPath(nameHref)));

                string specNameHref = root.Element("SpecializationNameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(specNameHref)) textPaths.Add(ResolvePath(path, ExtractPath(specNameHref)));

                string specDescHref = root.Element("SpecializationDescFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(specDescHref)) textPaths.Add(ResolvePath(path, ExtractPath(specDescHref)));

                string faceHref = root.Element("FaceTexture")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(faceHref)) iconXdbPaths.Add(ResolvePath(path, ExtractPath(faceHref)));
            }

            PreloadFiles(textPaths.Distinct(StringComparer.OrdinalIgnoreCase));
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

            var result = new ConcurrentBag<HeroInfo>();

            Parallel.ForEach(validHeroes, hero =>
            {
                var (heroPath, doc) = hero;
                var root = doc.Root!;
                var editable = root.Element("Editable");

                var info = new HeroInfo
                {
                    InternalName = root.Element("InternalName")?.Value ?? "",
                    HeroClass = root.Element("Class")?.Value ?? "",
                    TownType = root.Element("TownType")?.Value ?? "",
                    PrimarySkillId = root.Element("PrimarySkill")?.Element("SkillID")?.Value ?? "",
                    PrimarySkillMastery = root.Element("PrimarySkill")?.Element("Mastery")?.Value ?? "",
                    Offence = ParseInt(editable, "Offence"),
                    Defence = ParseInt(editable, "Defence"),
                    Spellpower = ParseInt(editable, "Spellpower"),
                    Knowledge = ParseInt(editable, "Knowledge"),
                };

                string nameHref = editable?.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                {
                    var nd = ReadFile(ResolvePath(heroPath, ExtractPath(nameHref)));
                    if (nd != null) info.Name = StripTags(DetectAndDecode(nd));
                }
                if (string.IsNullOrEmpty(info.Name)) info.Name = info.InternalName;

                string specNameHref = root.Element("SpecializationNameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(specNameHref))
                {
                    var nd = ReadFile(ResolvePath(heroPath, ExtractPath(specNameHref)));
                    if (nd != null) info.SpecializationName = StripTags(DetectAndDecode(nd));
                }

                string specDescHref = root.Element("SpecializationDescFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(specDescHref))
                {
                    var nd = ReadFile(ResolvePath(heroPath, ExtractPath(specDescHref)));
                    if (nd != null) info.SpecializationDesc = StripTags(DetectAndDecode(nd));
                }

                string faceHref = root.Element("FaceTexture")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(faceHref))
                    info.FaceIcon = LoadDdsIcon(ResolvePath(heroPath, ExtractPath(faceHref)));

                foreach (var skillItem in editable?.Element("skills")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string sid = skillItem.Element("SkillID")?.Value ?? "";
                    string mastery = skillItem.Element("Mastery")?.Value ?? "";
                    if (!string.IsNullOrEmpty(sid)) info.Skills.Add((sid, mastery));
                }

                foreach (var perkItem in editable?.Element("perkIDs")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string val = perkItem.Value.Trim();
                    if (!string.IsNullOrEmpty(val)) info.PerkIds.Add(val);
                }

                foreach (var spellItem in editable?.Element("spellIDs")?.Elements("Item") ?? Enumerable.Empty<XElement>())
                {
                    string val = spellItem.Value.Trim();
                    if (!string.IsNullOrEmpty(val)) info.SpellIds.Add(val);
                }

                result.Add(info);
            });

            return result.ToList();
        }

        public List<SpellInfo> ParseSpells()
        {
            var spellDirs = new[]
            {
                "/GameMechanics/Spell/Combat_Spells/",
                "/GameMechanics/Spell/Hero_Abilities/Barbarian/",
                "/GameMechanics/Spell/RunicMagic/",
            };

            var spellPaths = new ConcurrentBag<string>();
            foreach (var dir in spellDirs)
            {
                foreach (var kv in _vfs)
                {
                    string path = kv.Key;
                    if (!path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!path.EndsWith(".xdb", StringComparison.OrdinalIgnoreCase)) continue;

                    string fileName = path.Substring(path.LastIndexOf('/') + 1);
                    if (fileName.Contains("SpellVisual", StringComparison.OrdinalIgnoreCase)) continue;
                    if (fileName.StartsWith("Mass_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (fileName.StartsWith("Empowered_", StringComparison.OrdinalIgnoreCase)) continue;

                    spellPaths.Add(path);
                }
            }

            PreloadFiles(spellPaths);

            var textPaths = new ConcurrentBag<string>();
            var iconXdbPaths = new ConcurrentBag<string>();
            var validSpells = new ConcurrentBag<(string path, XDocument doc)>();

            Parallel.ForEach(spellPaths, sp =>
            {
                var doc = ReadXdb(sp);
                if (doc?.Root == null) return;
                string rootName = doc.Root.Name.LocalName;
                if (rootName == "SpellVisual" || rootName == "Effect" || rootName == "Sound") return;

                string school = doc.Root.Element("MagicSchool")?.Value ?? "";
                if (string.IsNullOrEmpty(school)) return;

                int level = ParseInt(doc.Root, "Level");
                if (school != "MAGIC_SCHOOL_WARCRIES" && school != "MAGIC_SCHOOL_RUNIC" && (level < 1 || level > 5)) return;

                validSpells.Add((sp, doc));

                string nameHref = doc.Root.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref)) textPaths.Add(ResolvePath(sp, ExtractPath(nameHref)));

                string descHref = doc.Root.Element("LongDescriptionFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(descHref)) textPaths.Add(ResolvePath(sp, ExtractPath(descHref)));

                string texHref = doc.Root.Element("Texture")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(texHref)) iconXdbPaths.Add(ResolvePath(sp, ExtractPath(texHref)));
            });

            PreloadFiles(textPaths.Distinct(StringComparer.OrdinalIgnoreCase));
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

            var result = new ConcurrentBag<SpellInfo>();

            Parallel.ForEach(validSpells, pair =>
            {
                var root = pair.doc.Root!;
                string fileName = pair.path.Substring(pair.path.LastIndexOf('/') + 1);
                string id = System.IO.Path.GetFileNameWithoutExtension(fileName);

                var spell = new SpellInfo
                {
                    Id = id,
                    SourcePath = pair.path,
                    Level = ParseInt(root, "Level"),
                    MagicSchool = root.Element("MagicSchool")?.Value ?? "",
                    ManaCost = ParseInt(root, "TrainedCost"),
                };

                string nameHref = root.Element("NameFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(nameHref))
                {
                    var nd = ReadFile(ResolvePath(pair.path, ExtractPath(nameHref)));
                    if (nd != null) spell.Name = StripTags(DetectAndDecode(nd));
                }

                string descHref = root.Element("LongDescriptionFileRef")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(descHref))
                {
                    var nd = ReadFile(ResolvePath(pair.path, ExtractPath(descHref)));
                    if (nd != null) spell.Description = StripTags(DetectAndDecode(nd));
                }

                string texHref = root.Element("Texture")?.Attribute("href")?.Value ?? "";
                if (!string.IsNullOrEmpty(texHref))
                    spell.Icon = LoadDdsIcon(ResolvePath(pair.path, ExtractPath(texHref)));

                var costEl = root.Element("sSpellCost");
                if (costEl != null)
                {
                    var cost = new SpellResourceCost
                    {
                        Wood = ParseInt(costEl, "Wood"),
                        Ore = ParseInt(costEl, "Ore"),
                        Mercury = ParseInt(costEl, "Mercury"),
                        Crystal = ParseInt(costEl, "Crystal"),
                        Sulfur = ParseInt(costEl, "Sulfur"),
                        Gem = ParseInt(costEl, "Gem"),
                        Gold = ParseInt(costEl, "Gold"),
                    };
                    if (cost.Wood + cost.Ore + cost.Mercury + cost.Crystal + cost.Sulfur + cost.Gem + cost.Gold > 0)
                        spell.ResourceCost = cost;
                }

                if (string.IsNullOrEmpty(spell.Name)) spell.Name = id;

                result.Add(spell);
            });

            return result.ToList();
        }

        public void MapSpellGameIds(List<SpellInfo> spells)
        {
            var doc = ReadXdb("/GameMechanics/RefTables/UndividedSpells.xdb");
            if (doc?.Root == null) return;

            var lookup = new Dictionary<string, (string gameId, string objHref)>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in doc.Root.Element("objects")?.Elements("Item") ?? Enumerable.Empty<XElement>())
            {
                string gameId = item.Element("ID")?.Value ?? "";
                string objHref = item.Element("Obj")?.Attribute("href")?.Value ?? "";
                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(objHref)) continue;

                int hashIdx = objHref.IndexOf('#');
                string path = NormalizePath(hashIdx >= 0 ? objHref.Substring(0, hashIdx) : objHref);
                lookup[path] = (gameId, objHref);
            }

            foreach (var spell in spells)
            {
                string normPath = NormalizePath(spell.SourcePath);
                if (lookup.TryGetValue(normPath, out var entry))
                {
                    spell.GameId = entry.gameId;
                    spell.ObjHref = entry.objHref;
                }
            }
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