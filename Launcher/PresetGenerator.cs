using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Launcher
{
    // ── Конфиг спец-модификаций (duel_settings.json в корне мода) ──────────────
    public class DuelSettings
    {
        public string? FileDate { get; set; }
        public List<HeroSpec>? Heroes { get; set; }
    }

    // ── Конфиг режимов игры (game_modes.json в корне мода) ─────────────────────
    public class GameModesConfig
    {
        public List<GameMode>? Modes { get; set; }
    }

    // Один режим игры. Имя/описание — inline-текст. SettingsFile — путь к конфигу спек
    // (типа duel_settings.json), который редактирует игрок; пусто → "/duel_settings.json".
    public class GameMode
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string? SettingsFile { get; set; }

        public string FactionMode { get; set; } = "PICK";   // PICK | RANDOM | BAN
        public int BanPoolSize { get; set; } = 6;           // BAN: сколько фракций предлагается (баним до 2)

        public bool AllowNonNativeHero { get; set; }        // показать 1 неродного героя (случайный)

        public int MaxLevel { get; set; } = 20;
        public bool AllowExtraLevel { get; set; } = true;
        public int StartGold { get; set; } = 120000;

        public int MageGuildFloors { get; set; } = 5;       // макс. уровень заклов (магия)
        public int WarcryFloors { get; set; } = 3;          // этажи кличей (орк)
        public int RuneFloors { get; set; } = 5;            // этажи рун (гном)

        public int SpellRerollCost { get; set; } = 5000;
        public bool AllowSpellReroll { get; set; } = true;
        public int ArtifactRerollCost { get; set; } = 5000;
        public bool AllowArtifactReroll { get; set; } = true;

        // Пул покупки тира = WeeklyGrowth * (ArmyGrowth[tier-1] + FactionGrowthBonus[faction][tier-1]).
        public List<int>? ArmyGrowth { get; set; }                          // T1..T7
        public Dictionary<string, List<int>>? FactionGrowthBonus { get; set; }

        public List<string>? BannedArtifacts { get; set; }  // id артов, убираемых из магазина (оба игрока)
    }

    public class HeroSpec
    {
        public string InternalName { get; set; } = "";
        public string? Specialization { get; set; }    // новое значение <Specialization> (напр. HERO_SPEC_...)
        public string? SpecNameFileRef { get; set; }
        public string? SpecDescFileRef { get; set; }
        public List<SpecModification>? Modifications { get; set; }
        public List<HeroPerk>? Perks { get; set; }
        public List<AbilityMod>? Abilities { get; set; }
        public List<SpellGrant>? GrantSpells { get; set; }      // выдача заклинаний герою (всегда, если герой выбран)
        public List<CustomArtifact>? Artifacts { get; set; }    // кастомные арты героя
    }

    // Выдача заклинаний по тегам school/level.
    //   ALL — все заклинания под теги; RANDOM — Count случайных под теги; SPECIFIC — конкретный SpellId.
    //   School: MAGIC_SCHOOL_* или ANY/пусто (любая школа). Level: 1..5 или 0 (любой).
    public class SpellGrant
    {
        public string Mode { get; set; } = "SPECIFIC";   // ALL | RANDOM | SPECIFIC
        public string? School { get; set; }              // MAGIC_SCHOOL_* | ANY
        public int Level { get; set; }                   // 1..5 | 0 = любой
        public int Count { get; set; } = 1;              // для RANDOM
        public string? SpellId { get; set; }             // для SPECIFIC (Id или GameId)
    }

    // Кастомный артефакт мода: привязан к существующему ID артефакта из Artifacts.xdb.
    // Имя/описание грузятся при парсинге (показ в магазине/слоте) + override в Artifacts.xdb.
    // Золото — при экипировке; армия и заклинания — при сборке пресета; 6 статов — SET в Artifacts.xdb.
    public class CustomArtifact
    {
        public string Id { get; set; } = "";
        public string? NameFileRef { get; set; }
        public string? DescFileRef { get; set; }
        public ArtifactStats? Stats { get; set; }        // 6 статов HeroStatsModif (SET) в копии Artifacts.xdb
        public List<PerkArmyMod>? Army { get; set; }     // армия при сборке (если арт надет)
        public List<SpellGrant>? GrantSpells { get; set; } // заклинания при сборке (если арт надет)
        public int Gold { get; set; }                    // +внутреннее золото лаунчера при экипировке
    }

    // 6 статов артефакта (HeroStatsModif). null = не менять.
    public class ArtifactStats
    {
        public int? Attack { get; set; }
        public int? Defence { get; set; }
        public int? Knowledge { get; set; }
        public int? SpellPower { get; set; }
        public int? Morale { get; set; }
        public int? Luck { get; set; }
    }

    // Добавление/удаление абилок (<Abilities><Item>...</Item></Abilities>) в копии xdb юнита.
    public class AbilityMod
    {
        public string File { get; set; } = "";
        public List<string>? Add { get; set; }
        public List<string>? Remove { get; set; }
    }

    // Кастомный перк мода: привязан к существующему перку из Skills.xdb (PerkId).
    // Описание грузится из txt при парсинге (показ в лаунчере) + переопределяется в копии Skills.xdb.
    // Золото и статы применяются при взятии перка на лвлапе; армия — при сборке дуэль-пресета.
    public class HeroPerk
    {
        public string PerkId { get; set; } = "";
        public string? NameFileRef { get; set; }
        public string? DescFileRef { get; set; }
        public int Gold { get; set; }                 // +внутреннее золото лаунчера при взятии
        public List<PerkStatMod>? Stats { get; set; }
        public List<PerkArmyMod>? Army { get; set; }
        public List<SpellGrant>? GrantSpells { get; set; } // выдача заклинаний при сборке (если перк взят)
    }

    // Прибавка к стату героя: amount = base + coef*source; ADD/MULT/SET.
    public class PerkStatMod
    {
        public string Stat { get; set; } = "";        // OFFENCE | DEFENCE | SPELLPOWER | KNOWLEDGE
        public string Operation { get; set; } = "ADD";
        public double Base { get; set; }
        public double Coef { get; set; }
        public string Source { get; set; } = "NONE";   // NONE | LEVEL | OFFENCE | DEFENCE | SPELLPOWER | KNOWLEDGE
    }

    // Изменение армии по тиру: amount = base + coef*source [*WeeklyGrowth], затем ADD/MULT/SET к Count.
    public class PerkArmyMod
    {
        public int Tier { get; set; }                  // 1..7
        public string Operation { get; set; } = "ADD";
        public double Base { get; set; }
        public double Coef { get; set; }
        public string Source { get; set; } = "NONE";
        public bool UseGrowth { get; set; }            // умножать amount на WeeklyGrowth юнита слота
    }

    public class SpecModification
    {
        public string File { get; set; } = "";
        public string Path { get; set; } = "";
        public string Operation { get; set; } = "ADD";   // ADD | MULT | SET
        public string Source { get; set; } = "NONE";     // NONE|LEVEL|OFFENCE|DEFENCE|SPELLPOWER|KNOWLEDGE
        public double Base { get; set; }
        public double Coef { get; set; }
    }

    public class PlayerPreset
    {
        public HeroInfo Hero { get; set; } = null!;
        public ArmySlot[] ArmySlots { get; set; } = Array.Empty<ArmySlot>();
        public Dictionary<string, ArtifactInfo?> EquippedArtifacts { get; set; } = new();
        public int HeroLevel { get; set; } = 1;
        public int TotalOffence { get; set; }
        public int TotalDefence { get; set; }
        public int TotalSpellpower { get; set; }
        public int TotalKnowledge { get; set; }
        public List<(string SkillId, int Mastery)> Skills { get; set; } = new();
        public List<string> Perks { get; set; } = new();
        public int RacialMastery { get; set; }
        public List<SpellInfo> Spells { get; set; } = new();
        public List<SpellInfo> Runes { get; set; } = new();
        public int GoldSpent { get; set; }
    }

    public static class PresetGenerator
    {
        private static readonly int[] ExpTable =
        {
            0, 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700,
            17500, 20600, 24320, 28784, 34140, 40567, 48279, 57533, 68637,
            81961, 97949, 117134, 140156, 167782, 200933, 244029, 304363,
            394864, 539665, 785826, 1228915, 2070784
        };

        private static readonly Dictionary<string, string> TownToFolder = new(StringComparer.Ordinal)
        {
            { "TOWN_HEAVEN",     "Haven"     },
            { "TOWN_INFERNO",    "Inferno"   },
            { "TOWN_NECROMANCY", "Necropolis" },
            { "TOWN_PRESERVE",   "Preserve"  },
            { "TOWN_ACADEMY",    "Academy"   },
            { "TOWN_DUNGEON",    "Dungeon"   },
            { "TOWN_FORTRESS",   "Dwarf"     },
            { "TOWN_STRONGHOLD", "Orcs"      },
        };

        public static readonly Dictionary<string, string> FactionToTown = new(StringComparer.Ordinal)
        {
            { "Орден Порядка",    "TOWN_HEAVEN"     },
            { "Инферно",        "TOWN_INFERNO"    },
            { "Некрополис",     "TOWN_NECROMANCY" },
            { "Лесной Союз",    "TOWN_PRESERVE"   },
            { "Академия Волшебства",       "TOWN_ACADEMY"    },
            { "Лига Теней",     "TOWN_DUNGEON"    },
            { "Северные Кланы", "TOWN_FORTRESS"   },
            { "Великая Орда",   "TOWN_STRONGHOLD" },
        };

        public static int GetExperience(int level)
        {
            int idx = Math.Clamp(level - 1, 0, ExpTable.Length - 1);
            return ExpTable[idx];
        }

        public static string Generate(string outputDir, PlayerPreset p1, PlayerPreset p2,
            string faction1, string faction2, GameDataParser? vfs = null,
            IReadOnlyList<SpellInfo>? spellPool = null)
        {
            string fileName = "ER_presets_ru.h5u";
            string outputPath = Path.Combine(outputDir, fileName);

            Directory.CreateDirectory(outputDir);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            string town1 = FactionToTown.TryGetValue(faction1, out var t1) ? t1 : "TOWN_HEAVEN";
            string town2 = FactionToTown.TryGetValue(faction2, out var t2) ? t2 : "TOWN_INFERNO";

            // Армейские бонусы (перки + кастомные арты) применяются к копии армии при сборке пресета.
            if (vfs != null)
            {
                try { ApplyPerkArmy(vfs, p1); ApplyPerkArmy(vfs, p2); }
                catch { /* армейские перки не критичны для формирования пресета */ }
                try { ApplyArtifactArmy(vfs, p1); ApplyArtifactArmy(vfs, p2); }
                catch { /* армейские арты не критичны для формирования пресета */ }
            }

            // Гейтинг заклинаний/рун по навыкам героя (исходя из взятых навыков).
            try { ApplySpellSkillGating(p1); ApplySpellSkillGating(p2); }
            catch { /* гейтинг не критичен для формирования пресета */ }

            // Выдача заклинаний (спека/перки/арты) — после гейтинга, без проверки навыков.
            if (vfs != null)
            {
                try { ApplyGrantSpells(vfs, p1, spellPool); ApplyGrantSpells(vfs, p2, spellPool); }
                catch { /* выдача заклинаний не критична для формирования пресета */ }
            }

            AddEntry(zip, "UI/MPDMLobby/presets.(DuelPresets).xdb", BuildPresetsXdb());
            AddEntry(zip, "Maps/DuelMode/PresetMap/map.xdb", BuildMapXdb(town1, town2));
            AddEntry(zip, "Maps/DuelMode/Heroes/AdvMapHero1.xdb", BuildHeroXdb(p1));
            AddEntry(zip, "Maps/DuelMode/Heroes/AdvMapHero2.xdb", BuildHeroXdb(p2));

            if (vfs != null)
            {
                try { ApplySpecs(zip, vfs, p1, p2); }
                catch { /* спец-модификации не критичны для формирования пресета */ }
            }

            return outputPath;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

        // Путь к активному конфигу спек (задаётся выбранным режимом). Пусто → "/duel_settings.json".
        public static string ActiveSettingsFile { get; set; } = "/duel_settings.json";

        // Читает и парсит конфиг спек активного режима (или null, если нет/битый)
        public static DuelSettings? LoadSettings(GameDataParser vfs)
        {
            string path = string.IsNullOrWhiteSpace(ActiveSettingsFile) ? "/duel_settings.json" : ActiveSettingsFile;
            string? cfgText = vfs.ReadText(path);
            if (string.IsNullOrWhiteSpace(cfgText)) return null;
            try { return JsonSerializer.Deserialize<DuelSettings>(cfgText, JsonOpts); }
            catch { return null; }
        }

        // Читает список режимов игры из game_modes.json в корне мода (или null).
        public static List<GameMode>? LoadGameModes(GameDataParser vfs)
        {
            string? cfgText = vfs.ReadText("/game_modes.json");
            if (string.IsNullOrWhiteSpace(cfgText)) return null;
            try
            {
                var cfg = JsonSerializer.Deserialize<GameModesConfig>(cfgText, JsonOpts);
                return cfg?.Modes;
            }
            catch { return null; }
        }

        // Применяет армейские перки взятого героя к копии его армии при сборке пресета.
        // Источник чисел (LEVEL/характеристики) берётся из финального состояния пресета.
        // Слоты копируются — исходная армия не мутируется (повторная генерация не складывает бонусы).
        private static void ApplyPerkArmy(GameDataParser vfs, PlayerPreset p)
        {
            if (p?.Hero == null || p.ArmySlots.Length == 0) return;
            var perks = GetHeroPerks(vfs, p.Hero.InternalName);
            if (perks == null || perks.Count == 0) return;

            var taken = new HashSet<string>(p.Perks ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var active = perks.Where(pk => !string.IsNullOrWhiteSpace(pk.PerkId)
                && pk.Army != null && pk.Army.Count > 0
                && taken.Contains(pk.PerkId)).ToList();
            if (active.Count == 0) return;

            var slots = p.ArmySlots
                .Select(s => s == null ? null : new ArmySlot { Creature = s.Creature, Count = s.Count })
                .ToArray();

            foreach (var perk in active)
                foreach (var am in perk.Army!)
                    ApplyArmyMod(slots, am, p);

            p.ArmySlots = slots!;
        }

        // Армейские бонусы от надетых кастомных артефактов (аналогично ApplyPerkArmy).
        private static void ApplyArtifactArmy(GameDataParser vfs, PlayerPreset p)
        {
            if (p?.Hero == null || p.ArmySlots.Length == 0) return;
            var arts = GetHeroArtifacts(vfs, p.Hero.InternalName);
            if (arts == null || arts.Count == 0) return;

            var equipped = new HashSet<string>(
                p.EquippedArtifacts.Values.Where(a => a != null).Select(a => a!.Id),
                StringComparer.OrdinalIgnoreCase);
            var active = arts.Where(a => !string.IsNullOrWhiteSpace(a.Id)
                && a.Army != null && a.Army.Count > 0
                && equipped.Contains(a.Id)).ToList();
            if (active.Count == 0) return;

            var slots = p.ArmySlots
                .Select(s => s == null ? null : new ArmySlot { Creature = s.Creature, Count = s.Count })
                .ToArray();

            foreach (var art in active)
                foreach (var am in art.Army!)
                    ApplyArmyMod(slots, am, p);

            p.ArmySlots = slots!;
        }

        // Применяет один армейский модификатор к копии слотов: amount = base + coef*source [*growth], ADD/MULT/SET.
        private static void ApplyArmyMod(ArmySlot?[] slots, PerkArmyMod am, PlayerPreset p)
        {
            double amount = am.Base + am.Coef * PerkSourceValue(am.Source, p);
            string op = (am.Operation ?? "ADD").Trim().ToUpperInvariant();
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot?.Creature == null || slot.Creature.CreatureTier != am.Tier) continue;

                double a = am.UseGrowth ? amount * slot.Creature.WeeklyGrowth : amount;
                int newCount = op switch
                {
                    "MULT" => (int)Math.Round(slot.Count * a, MidpointRounding.AwayFromZero),
                    "SET"  => (int)Math.Round(a, MidpointRounding.AwayFromZero),
                    _      => slot.Count + (int)Math.Round(a, MidpointRounding.AwayFromZero),
                };
                slot.Count = Math.Max(0, newCount);
            }
        }

        private static readonly Random GrantRng = new();

        // Выдача заклинаний герою при сборке пресета: спека героя + взятые перки + надетые кастомные арты.
        // Без проверки навыков; дубли (по GameId/Id) пропускаются.
        private static void ApplyGrantSpells(GameDataParser vfs, PlayerPreset p, IReadOnlyList<SpellInfo>? pool)
        {
            if (p?.Hero == null || pool == null || pool.Count == 0) return;
            var cfg = LoadSettings(vfs);
            var spec = cfg?.Heroes?.FirstOrDefault(h =>
                string.Equals(h.InternalName, p.Hero.InternalName, StringComparison.OrdinalIgnoreCase));
            if (spec == null) return;

            var grants = new List<SpellGrant>();
            if (spec.GrantSpells != null) grants.AddRange(spec.GrantSpells);

            if (spec.Perks != null)
            {
                var taken = new HashSet<string>(p.Perks ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                foreach (var perk in spec.Perks)
                    if (perk.GrantSpells != null && !string.IsNullOrWhiteSpace(perk.PerkId) && taken.Contains(perk.PerkId))
                        grants.AddRange(perk.GrantSpells);
            }

            if (spec.Artifacts != null)
            {
                var equipped = new HashSet<string>(
                    p.EquippedArtifacts.Values.Where(a => a != null).Select(a => a!.Id),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var art in spec.Artifacts)
                    if (art.GrantSpells != null && !string.IsNullOrWhiteSpace(art.Id) && equipped.Contains(art.Id))
                        grants.AddRange(art.GrantSpells);
            }

            if (grants.Count == 0) return;

            var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in p.Spells) { if (!string.IsNullOrEmpty(s.GameId)) have.Add(s.GameId); if (!string.IsNullOrEmpty(s.Id)) have.Add(s.Id); }
            foreach (var s in p.Runes)  { if (!string.IsNullOrEmpty(s.GameId)) have.Add(s.GameId); if (!string.IsNullOrEmpty(s.Id)) have.Add(s.Id); }

            foreach (var g in grants)
            {
                foreach (var sp in ResolveGrant(g, pool))
                {
                    if (sp == null) continue;
                    string gid = !string.IsNullOrEmpty(sp.GameId) ? sp.GameId : sp.Id;
                    if (string.IsNullOrEmpty(gid)) continue;
                    if (have.Contains(gid) || (!string.IsNullOrEmpty(sp.Id) && have.Contains(sp.Id))) continue;
                    p.Spells.Add(sp);
                    have.Add(gid);
                    if (!string.IsNullOrEmpty(sp.Id)) have.Add(sp.Id);
                }
            }
        }

        // Разрешает один SpellGrant в список заклинаний из пула.
        private static List<SpellInfo> ResolveGrant(SpellGrant g, IReadOnlyList<SpellInfo> pool)
        {
            string mode = (g.Mode ?? "SPECIFIC").Trim().ToUpperInvariant();
            if (mode == "SPECIFIC")
            {
                if (string.IsNullOrWhiteSpace(g.SpellId)) return new List<SpellInfo>();
                var sp = pool.FirstOrDefault(s =>
                    string.Equals(s.GameId, g.SpellId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Id, g.SpellId, StringComparison.OrdinalIgnoreCase));
                return sp != null ? new List<SpellInfo> { sp } : new List<SpellInfo>();
            }

            bool anySchool = string.IsNullOrWhiteSpace(g.School)
                || string.Equals(g.School, "ANY", StringComparison.OrdinalIgnoreCase);
            var matches = pool.Where(s =>
                (anySchool || string.Equals(s.MagicSchool, g.School, StringComparison.OrdinalIgnoreCase)) &&
                (g.Level <= 0 || s.Level == g.Level)).ToList();

            if (mode == "ALL") return matches;

            int count = Math.Max(1, g.Count);
            return matches.OrderBy(_ => GrantRng.Next()).Take(Math.Min(count, matches.Count)).ToList();
        }

        private static double PerkSourceValue(string source, PlayerPreset p) =>
            (source ?? "NONE").Trim().ToUpperInvariant() switch
            {
                "LEVEL" => p.HeroLevel,
                "OFFENCE" => p.TotalOffence,
                "DEFENCE" => p.TotalDefence,
                "SPELLPOWER" => p.TotalSpellpower,
                "KNOWLEDGE" => p.TotalKnowledge,
                _ => 0,
            };

        // Школа заклинания → навык школы магии (mastery: BASIC=0, ADVANCED=1, EXPERT=2).
        private static readonly Dictionary<string, string> SchoolSkill = new(StringComparer.Ordinal)
        {
            { "MAGIC_SCHOOL_LIGHT",       "HERO_SKILL_LIGHT_MAGIC" },
            { "MAGIC_SCHOOL_DARK",        "HERO_SKILL_DARK_MAGIC" },
            { "MAGIC_SCHOOL_DESTRUCTIVE", "HERO_SKILL_DESTRUCTIVE_MAGIC" },
            { "MAGIC_SCHOOL_SUMMONING",   "HERO_SKILL_SUMMONING_MAGIC" },
        };

        private const string WisdomSkillId = "HERO_SKILL_WISDOM";

        // Отсекает заклинания/руны, требующие навыков, которых у героя нет.
        //   Заклинания: 1–2 всегда; 3 — школа Basic+ ИЛИ Wisdom;
        //               4 — школа Advanced+ ИЛИ (Wisdom и герой 20 лвл); 5 — школа Expert.
        //   Руны:       1–2 всегда; 3–4 — расовый Advanced+; 5 — расовый Expert.
        // Кличи орков и прочие не-боевые школы не гейтятся.
        private static void ApplySpellSkillGating(PlayerPreset p)
        {
            if (p == null) return;
            var skills = p.Skills ?? new List<(string SkillId, int Mastery)>();

            int SchoolMastery(string magicSchool)
            {
                if (!SchoolSkill.TryGetValue(magicSchool ?? "", out var skillId)) return -1;
                int best = -1;
                foreach (var (sid, m) in skills)
                    if (string.Equals(sid, skillId, StringComparison.OrdinalIgnoreCase) && m > best) best = m;
                return best;
            }

            bool hasWisdom = skills.Any(s =>
                string.Equals(s.SkillId, WisdomSkillId, StringComparison.OrdinalIgnoreCase));

            bool SpellAllowed(SpellInfo sp)
            {
                if (sp == null) return false;
                // Гейтим только 4 боевые школы; кличи/прочее — без ограничений.
                if (!SchoolSkill.ContainsKey(sp.MagicSchool)) return true;
                if (sp.Level <= 2) return true;
                int sm = SchoolMastery(sp.MagicSchool);
                return sp.Level switch
                {
                    3 => sm >= 0 || hasWisdom,
                    4 => sm >= 1 || (hasWisdom && p.HeroLevel >= 20),
                    5 => sm >= 2,
                    _ => true,
                };
            }

            bool RuneAllowed(SpellInfo rn)
            {
                if (rn == null) return false;
                if (rn.Level <= 2) return true;
                return rn.Level switch
                {
                    3 or 4 => p.RacialMastery >= 1,
                    5 => p.RacialMastery >= 2,
                    _ => true,
                };
            }

            if (p.Spells != null) p.Spells = p.Spells.Where(SpellAllowed).ToList();
            if (p.Runes != null) p.Runes = p.Runes.Where(RuneAllowed).ToList();
        }

        // Подменяет имя/описание спеца у распарсенных героев, чтобы окно выбора героя
        // сразу показывало модовую специализацию из duel_settings.json.
        public static void ApplyHeroSpecDisplay(GameDataParser vfs, List<HeroInfo> heroes)
        {
            var cfg = LoadSettings(vfs);
            if (cfg?.Heroes == null || cfg.Heroes.Count == 0) return;

            foreach (var hero in heroes)
            {
                var spec = cfg.Heroes.FirstOrDefault(h =>
                    string.Equals(h.InternalName, hero.InternalName, StringComparison.OrdinalIgnoreCase));
                if (spec == null) continue;

                if (!string.IsNullOrWhiteSpace(spec.SpecNameFileRef))
                {
                    var t = vfs.ReadText(spec.SpecNameFileRef!);
                    if (!string.IsNullOrWhiteSpace(t)) hero.SpecializationName = StripTagsPublic(t!);
                }
                if (!string.IsNullOrWhiteSpace(spec.SpecDescFileRef))
                {
                    var t = vfs.ReadText(spec.SpecDescFileRef!);
                    if (!string.IsNullOrWhiteSpace(t)) hero.SpecializationDesc = StripTagsPublic(t!);
                }
            }
        }

        private static string StripTagsPublic(string s) =>
            System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "").Trim();

        // Кастомные перки героя из конфига (или null, если героя/перков нет).
        public static List<HeroPerk>? GetHeroPerks(GameDataParser vfs, string internalName)
        {
            var cfg = LoadSettings(vfs);
            var spec = cfg?.Heroes?.FirstOrDefault(h =>
                string.Equals(h.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
            return spec?.Perks;
        }

        // Кастомные артефакты героя из конфига (или null, если героя/артов нет).
        public static List<CustomArtifact>? GetHeroArtifacts(GameDataParser vfs, string internalName)
        {
            var cfg = LoadSettings(vfs);
            var spec = cfg?.Heroes?.FirstOrDefault(h =>
                string.Equals(h.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
            return spec?.Artifacts;
        }

        // Подменяет имя/описание у артефактов (ArtifactInfo) на тексты из duel_settings.json,
        // чтобы при парсинге кастомный текст сразу был виден в магазине/слоте.
        public static void ApplyArtifactDisplay(GameDataParser vfs, List<ArtifactInfo> artifacts)
        {
            var cfg = LoadSettings(vfs);
            if (cfg?.Heroes == null || cfg.Heroes.Count == 0) return;

            foreach (var hero in cfg.Heroes)
            {
                if (hero.Artifacts == null) continue;
                foreach (var art in hero.Artifacts)
                {
                    if (string.IsNullOrWhiteSpace(art.Id)) continue;
                    var ai = artifacts.FirstOrDefault(a =>
                        string.Equals(a.Id, art.Id, StringComparison.OrdinalIgnoreCase));
                    if (ai == null) continue;

                    if (!string.IsNullOrWhiteSpace(art.NameFileRef))
                    {
                        var t = vfs.ReadText(art.NameFileRef!);
                        if (!string.IsNullOrWhiteSpace(t)) ai.Name = StripTagsPublic(t!);
                    }
                    if (!string.IsNullOrWhiteSpace(art.DescFileRef))
                    {
                        var t = vfs.ReadText(art.DescFileRef!);
                        if (!string.IsNullOrWhiteSpace(t)) ai.Description = StripTagsPublic(t!);
                    }
                }
            }
        }

        // Подменяет имя/описание у перков (SkillInfo) на тексты из duel_settings.json,
        // чтобы при парсинге кастомное описание сразу было видно в лаунчере.
        public static void ApplyPerkDisplay(GameDataParser vfs, List<SkillInfo> skills)
        {
            var cfg = LoadSettings(vfs);
            if (cfg?.Heroes == null || cfg.Heroes.Count == 0) return;

            foreach (var hero in cfg.Heroes)
            {
                if (hero.Perks == null) continue;
                foreach (var perk in hero.Perks)
                {
                    if (string.IsNullOrWhiteSpace(perk.PerkId)) continue;
                    var sk = skills.FirstOrDefault(s =>
                        string.Equals(s.Id, perk.PerkId, StringComparison.OrdinalIgnoreCase));
                    if (sk == null) continue;

                    if (!string.IsNullOrWhiteSpace(perk.NameFileRef))
                    {
                        var t = vfs.ReadText(perk.NameFileRef!);
                        if (!string.IsNullOrWhiteSpace(t)) OverrideAll(sk.Names, StripTagsPublic(t!));
                    }
                    if (!string.IsNullOrWhiteSpace(perk.DescFileRef))
                    {
                        var t = vfs.ReadText(perk.DescFileRef!);
                        if (!string.IsNullOrWhiteSpace(t)) OverrideAll(sk.Descriptions, StripTagsPublic(t!));
                    }
                }
            }
        }

        private static void OverrideAll(List<string> list, string value)
        {
            if (list.Count == 0) list.Add(value);
            else for (int i = 0; i < list.Count; i++) list[i] = value;
        }

        // ── Спец-модификации: подкладываем изменённые копии игровых файлов ─────────
        private static void ApplySpecs(ZipArchive zip, GameDataParser vfs,
            PlayerPreset p1, PlayerPreset p2)
        {
            var cfg = LoadSettings(vfs);
            if (cfg?.Heroes == null || cfg.Heroes.Count == 0) return;

            DateTime fileDate = new DateTime(2032, 1, 1);
            if (!string.IsNullOrWhiteSpace(cfg.FileDate) &&
                DateTime.TryParse(cfg.FileDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                fileDate = parsedDate;

            var players = new[] { p1, p2 };
            var selected = new List<(PlayerPreset preset, HeroSpec spec)>();
            foreach (var preset in players)
            {
                if (preset?.Hero == null) continue;
                var spec = cfg.Heroes.FirstOrDefault(h =>
                    string.Equals(h.InternalName, preset.Hero.InternalName, StringComparison.OrdinalIgnoreCase));
                if (spec != null) selected.Add((preset, spec));
            }
            if (selected.Count == 0) return;

            // (A) Изменения игровых файлов — группируем по файлу (файл глобальный → одна копия).
            // В одной копии файла последовательно применяются и числовые модификации, и абилки.
            var modsByFile = new Dictionary<string, List<(SpecModification mod, PlayerPreset preset)>>(StringComparer.OrdinalIgnoreCase);
            var abilitiesByFile = new Dictionary<string, List<AbilityMod>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (preset, spec) in selected)
            {
                if (spec.Modifications != null)
                    foreach (var mod in spec.Modifications)
                    {
                        if (string.IsNullOrWhiteSpace(mod.File) || string.IsNullOrWhiteSpace(mod.Path)) continue;
                        if (!modsByFile.TryGetValue(mod.File, out var list))
                            modsByFile[mod.File] = list = new List<(SpecModification, PlayerPreset)>();
                        list.Add((mod, preset));
                    }

                if (spec.Abilities != null)
                    foreach (var am in spec.Abilities)
                    {
                        if (string.IsNullOrWhiteSpace(am.File)) continue;
                        if ((am.Add == null || am.Add.Count == 0) && (am.Remove == null || am.Remove.Count == 0)) continue;
                        if (!abilitiesByFile.TryGetValue(am.File, out var list))
                            abilitiesByFile[am.File] = list = new List<AbilityMod>();
                        list.Add(am);
                    }
            }

            var allFiles = new HashSet<string>(modsByFile.Keys, StringComparer.OrdinalIgnoreCase);
            allFiles.UnionWith(abilitiesByFile.Keys);
            foreach (var file in allFiles)
            {
                var doc = vfs.ReadXdb(file);
                if (doc?.Root == null) continue;
                if (modsByFile.TryGetValue(file, out var numMods))
                    foreach (var (mod, preset) in numMods)
                        ApplyNumericMod(doc, mod, preset);
                if (abilitiesByFile.TryGetValue(file, out var abMods))
                    foreach (var am in abMods)
                        ApplyAbilityMod(doc, am);
                AddXdbEntry(zip, file, doc, fileDate);
            }

            // (B) Спека в копию xdb самого героя (Specialization + Name/DescFileRef)
            foreach (var (preset, spec) in selected)
            {
                if (string.IsNullOrWhiteSpace(spec.Specialization) &&
                    string.IsNullOrWhiteSpace(spec.SpecNameFileRef) &&
                    string.IsNullOrWhiteSpace(spec.SpecDescFileRef))
                    continue;

                string folder = TownToFolder.TryGetValue(preset.Hero.TownType, out var f) ? f : "Haven";
                string sharedPath = $"/MapObjects/{folder}/{preset.Hero.InternalName}.(AdvMapHeroShared).xdb";

                var hdoc = vfs.ReadXdb(sharedPath);
                if (hdoc?.Root == null) continue;

                if (!string.IsNullOrWhiteSpace(spec.Specialization))
                    SetElementValue(hdoc.Root, "Specialization", spec.Specialization!);
                if (!string.IsNullOrWhiteSpace(spec.SpecNameFileRef))
                    SetHrefChild(hdoc.Root, "SpecializationNameFileRef", spec.SpecNameFileRef!);
                if (!string.IsNullOrWhiteSpace(spec.SpecDescFileRef))
                    SetHrefChild(hdoc.Root, "SpecializationDescFileRef", spec.SpecDescFileRef!);

                AddXdbEntry(zip, sharedPath, hdoc, fileDate);
            }

            // (C) Переопределение имени/описания кастомных перков в глобальной копии Skills.xdb
            var perkRefs = selected
                .Where(s => s.spec.Perks != null)
                .SelectMany(s => s.spec.Perks!)
                .Where(p => !string.IsNullOrWhiteSpace(p.PerkId) &&
                            (!string.IsNullOrWhiteSpace(p.NameFileRef) || !string.IsNullOrWhiteSpace(p.DescFileRef)))
                .ToList();

            if (perkRefs.Count > 0)
            {
                const string skillsPath = "/GameMechanics/RefTables/Skills.xdb";
                var sdoc = vfs.ReadXdb(skillsPath);
                if (sdoc?.Root != null)
                {
                    var items = sdoc.Root.Element("objects")?.Elements("Item") ?? sdoc.Descendants("Item");
                    var byId = items
                        .Where(it => !string.IsNullOrEmpty(it.Element("ID")?.Value))
                        .GroupBy(it => it.Element("ID")!.Value, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    bool changed = false;
                    foreach (var perk in perkRefs)
                    {
                        if (!byId.TryGetValue(perk.PerkId, out var item)) continue;
                        var data = item.Element("obj") ?? item;
                        if (!string.IsNullOrWhiteSpace(perk.NameFileRef))
                        { SetListItemHref(data, "NameFileRef", perk.NameFileRef!); changed = true; }
                        if (!string.IsNullOrWhiteSpace(perk.DescFileRef))
                        { SetListItemHref(data, "DescriptionFileRef", perk.DescFileRef!); changed = true; }
                    }
                    if (changed) AddXdbEntry(zip, skillsPath, sdoc, fileDate);
                }
            }

            // (D) Кастомные арты → глобальная копия Artifacts.xdb (6 статов SET + name/desc override).
            // Применяем только для надетых артов выбранных героев; изменения складываются в одну копию.
            var artMods = new List<CustomArtifact>();
            foreach (var (preset, spec) in selected)
            {
                if (spec.Artifacts == null) continue;
                var equipped = new HashSet<string>(
                    preset.EquippedArtifacts.Values.Where(a => a != null).Select(a => a!.Id),
                    StringComparer.OrdinalIgnoreCase);
                foreach (var art in spec.Artifacts)
                    if (!string.IsNullOrWhiteSpace(art.Id) && equipped.Contains(art.Id))
                        artMods.Add(art);
            }

            if (artMods.Count > 0)
            {
                const string artPath = "/GameMechanics/RefTables/Artifacts.xdb";
                var adoc = vfs.ReadXdb(artPath);
                if (adoc?.Root != null)
                {
                    var items = adoc.Root.Element("objects")?.Elements("Item") ?? adoc.Descendants("Item");
                    var byId = items
                        .Where(it => !string.IsNullOrEmpty(it.Element("ID")?.Value))
                        .GroupBy(it => it.Element("ID")!.Value, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                    bool changed = false;
                    foreach (var art in artMods)
                    {
                        if (!byId.TryGetValue(art.Id, out var item)) continue;
                        var data = item.Element("obj") ?? item;
                        if (!string.IsNullOrWhiteSpace(art.NameFileRef))
                        { SetHrefChild(data, "NameFileRef", art.NameFileRef!); changed = true; }
                        if (!string.IsNullOrWhiteSpace(art.DescFileRef))
                        { SetHrefChild(data, "DescriptionFileRef", art.DescFileRef!); changed = true; }
                        if (art.Stats != null)
                        { ApplyArtifactStats(data, art.Stats); changed = true; }
                    }
                    if (changed) AddXdbEntry(zip, artPath, adoc, fileDate);
                }
            }
        }

        // Записывает 6 статов артефакта (SET) в <HeroStatsModif> (создаётся при необходимости).
        private static void ApplyArtifactStats(XElement data, ArtifactStats st)
        {
            var mod = data.Element("HeroStatsModif");
            if (mod == null)
            {
                mod = new XElement("HeroStatsModif");
                data.Add(mod);
            }
            if (st.Attack.HasValue)     SetElementValue(mod, "Attack",     st.Attack.Value.ToString(CultureInfo.InvariantCulture));
            if (st.Defence.HasValue)    SetElementValue(mod, "Defence",    st.Defence.Value.ToString(CultureInfo.InvariantCulture));
            if (st.Knowledge.HasValue)  SetElementValue(mod, "Knowledge",  st.Knowledge.Value.ToString(CultureInfo.InvariantCulture));
            if (st.SpellPower.HasValue) SetElementValue(mod, "SpellPower", st.SpellPower.Value.ToString(CultureInfo.InvariantCulture));
            if (st.Morale.HasValue)     SetElementValue(mod, "Morale",     st.Morale.Value.ToString(CultureInfo.InvariantCulture));
            if (st.Luck.HasValue)       SetElementValue(mod, "Luck",       st.Luck.Value.ToString(CultureInfo.InvariantCulture));
        }

        // Устанавливает href первого <Item> в списочном элементе (NameFileRef/DescriptionFileRef).
        private static void SetListItemHref(XElement data, string listName, string href)
        {
            var list = data.Element(listName);
            if (list == null)
            {
                list = new XElement(listName);
                data.Add(list);
            }
            var item = list.Element("Item");
            if (item == null)
            {
                item = new XElement("Item");
                list.Add(item);
            }
            item.SetAttributeValue("href", href);
        }

        private static void ApplyNumericMod(XDocument doc, SpecModification mod, PlayerPreset preset)
        {
            var segs = mod.Path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segs.Length == 0) return;

            string? attr = null;
            if (segs[^1].StartsWith("@", StringComparison.Ordinal))
            {
                attr = segs[^1][1..];
                segs = segs[..^1];
            }

            var el = NavigateElement(doc, segs);
            if (el == null) return;

            string origStr = attr != null ? (el.Attribute(attr)?.Value ?? "") : el.Value;
            double orig = 0;
            double.TryParse(origStr.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out orig);

            double src = ResolveSource(mod.Source, preset);
            double amount = mod.Base + mod.Coef * src;
            double newVal = (mod.Operation ?? "ADD").Trim().ToUpperInvariant() switch
            {
                "ADD" => orig + amount,
                "MULT" => orig * amount,
                "SET" => amount,
                _ => orig,
            };

            string formatted = FormatLikeOriginal(origStr, newVal);
            if (attr != null) el.SetAttributeValue(attr, formatted);
            else el.Value = formatted;
        }

        // Добавляет/удаляет абилки в <Abilities> юнита (корневой элемент xdb).
        // add — без дублей; remove — все совпадения; секция <Abilities> создаётся при необходимости.
        private static void ApplyAbilityMod(XDocument doc, AbilityMod am)
        {
            var root = doc.Root;
            if (root == null) return;

            var abilities = root.Element("Abilities") ?? root.Descendants("Abilities").FirstOrDefault();
            if (abilities == null)
            {
                abilities = new XElement("Abilities");
                root.Add(abilities);
            }

            if (am.Remove != null)
            {
                foreach (var ab in am.Remove)
                {
                    if (string.IsNullOrWhiteSpace(ab)) continue;
                    abilities.Elements("Item")
                        .Where(it => string.Equals(it.Value.Trim(), ab.Trim(), StringComparison.OrdinalIgnoreCase))
                        .ToList()
                        .ForEach(it => it.Remove());
                }
            }

            if (am.Add != null)
            {
                foreach (var ab in am.Add)
                {
                    if (string.IsNullOrWhiteSpace(ab)) continue;
                    bool exists = abilities.Elements("Item")
                        .Any(it => string.Equals(it.Value.Trim(), ab.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (!exists) abilities.Add(new XElement("Item", ab.Trim()));
                }
            }
        }

        private static XElement? NavigateElement(XDocument doc, string[] segments)
        {
            XElement? cur = doc.Root;
            for (int i = 0; i < segments.Length; i++)
            {
                if (cur == null) return null;
                if (i == 0 && string.Equals(cur.Name.LocalName, segments[i], StringComparison.Ordinal))
                    continue; // первый сегмент совпал с корнем — остаёмся на корне
                cur = cur.Element(segments[i]);
            }
            return cur;
        }

        private static double ResolveSource(string source, PlayerPreset p) =>
            (source ?? "NONE").Trim().ToUpperInvariant() switch
            {
                "LEVEL" => p.HeroLevel,
                "OFFENCE" => p.TotalOffence,
                "DEFENCE" => p.TotalDefence,
                "SPELLPOWER" => p.TotalSpellpower,
                "KNOWLEDGE" => p.TotalKnowledge,
                _ => 0,
            };

        private static string FormatLikeOriginal(string orig, double val)
        {
            bool isInt = !orig.Contains('.') && !orig.Contains(',');
            return isInt
                ? ((long)Math.Round(val, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)
                : val.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static void SetHrefChild(XElement root, string childName, string href)
        {
            var el = root.Element(childName);
            if (el == null)
            {
                el = new XElement(childName);
                root.Add(el);
            }
            el.SetAttributeValue("href", href);
        }

        private static void SetElementValue(XElement root, string childName, string value)
        {
            var el = root.Element(childName);
            if (el == null)
            {
                el = new XElement(childName);
                root.Add(el);
            }
            el.Value = value;
        }

        private static void AddXdbEntry(ZipArchive zip, string entryPath, XDocument doc, DateTime date)
        {
            var entry = zip.CreateEntry(entryPath.TrimStart('/'), CompressionLevel.Optimal);
            entry.LastWriteTime = date;
            using var stream = entry.Open();
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // UTF-8 без BOM, как у остальных записей
                Indent = false,
                OmitXmlDeclaration = false,
            };
            using var xw = System.Xml.XmlWriter.Create(stream, settings);
            doc.Save(xw);
        }

        private static void AddEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string MasteryToString(int m) => m switch
        {
            0 => "MASTERY_BASIC",
            1 => "MASTERY_ADVANCED",
            2 => "MASTERY_EXPERT",
            _ => "MASTERY_BASIC",
        };

        private static string BuildHeroXdb(PlayerPreset p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<AdvMapHero>");

            sb.AppendLine("<Pos><x>0</x><y>0</y><z>0</z></Pos>");
            sb.AppendLine("<Rot>0</Rot>");
            sb.AppendLine("<Floor>0</Floor>");
            sb.AppendLine("<Name/>");
            sb.AppendLine("<CombatScript/>");
            sb.AppendLine("<pointLights/>");

            string folder = TownToFolder.TryGetValue(p.Hero.TownType, out var f) ? f : "Haven";
            string heroName = p.Hero.InternalName;
            sb.AppendLine($"<Shared href=\"/MapObjects/{folder}/{heroName}.(AdvMapHeroShared).xdb#xpointer(/AdvMapHeroShared)\"/>");

            sb.AppendLine("<PlayerID>PLAYER_NONE</PlayerID>");
            sb.AppendLine($"<Experience>{GetExperience(p.HeroLevel)}</Experience>");

            // Army slots
            sb.AppendLine("<armySlots>");
            foreach (var slot in p.ArmySlots)
            {
                if (slot?.Creature != null && slot.Count > 0)
                {
                    sb.AppendLine("<Item>");
                    sb.AppendLine($"<Creature>{slot.Creature.Id}</Creature>");
                    sb.AppendLine($"<Count>{slot.Count}</Count>");
                    sb.AppendLine("</Item>");
                }
            }
            sb.AppendLine("</armySlots>");

            // Artifacts
            var artifacts = p.EquippedArtifacts.Values
                .Where(a => a != null)
                .Select(a => a!.Id)
                .ToList();

            sb.AppendLine("<artifactIDs>");
            foreach (var artId in artifacts)
                sb.AppendLine($"<Item>{artId}</Item>");
            sb.AppendLine("</artifactIDs>");

            sb.AppendLine("<isUntransferable>");
            foreach (var _ in artifacts)
                sb.AppendLine("<Item>0</Item>");
            sb.AppendLine("</isUntransferable>");

            // Editable
            sb.AppendLine("<Editable>");
            sb.AppendLine("<NameFileRef href=\"\"/>");
            sb.AppendLine("<BiographyFileRef href=\"\"/>");
            sb.AppendLine($"<Offence>{p.TotalOffence}</Offence>");
            sb.AppendLine($"<Defence>{p.TotalDefence}</Defence>");
            sb.AppendLine($"<Spellpower>{p.TotalSpellpower}</Spellpower>");
            sb.AppendLine($"<Knowledge>{p.TotalKnowledge}</Knowledge>");

            sb.AppendLine("<skills>");
            foreach (var (skillId, mastery) in p.Skills)
            {
                sb.AppendLine("<Item>");
                sb.AppendLine($"<Mastery>{MasteryToString(mastery)}</Mastery>");
                sb.AppendLine($"<SkillID>{skillId}</SkillID>");
                sb.AppendLine("</Item>");
            }
            sb.AppendLine("</skills>");

            sb.AppendLine("<perkIDs>");
            foreach (var perkId in p.Perks)
                sb.AppendLine($"<Item>{perkId}</Item>");
            sb.AppendLine("</perkIDs>");

            // Spells + runes combined
            sb.AppendLine("<spellIDs>");
            foreach (var spell in p.Spells)
            {
                string sid = !string.IsNullOrEmpty(spell.GameId) ? spell.GameId : spell.Id;
                sb.AppendLine($"<Item>{sid}</Item>");
            }
            foreach (var rune in p.Runes)
            {
                string sid = !string.IsNullOrEmpty(rune.GameId) ? rune.GameId : rune.Id;
                sb.AppendLine($"<Item>{sid}</Item>");
            }
            sb.AppendLine("</spellIDs>");

            sb.AppendLine("<Ballista>true</Ballista>");
            sb.AppendLine("<FirstAidTent>true</FirstAidTent>");
            sb.AppendLine("<AmmoCart>true</AmmoCart>");
            sb.AppendLine("<FavoriteEnemies/>");
            sb.AppendLine("<TalismanLevel>0</TalismanLevel>");
            sb.AppendLine("</Editable>");

            sb.AppendLine("<OverrideMask>123</OverrideMask>");
            sb.AppendLine($"<PrimarySkillMastery>{MasteryToString(p.RacialMastery)}</PrimarySkillMastery>");

            sb.AppendLine("<LossTrigger><Action><FunctionName/></Action></LossTrigger>");
            sb.AppendLine("<AllowQuickCombat>true</AllowQuickCombat>");
            sb.AppendLine("<Textures><Icon128x128/><Icon64x64/><RoundedFace/><LeftFace/><RightFace/></Textures>");
            sb.AppendLine($"<PresetPrice>{p.GoldSpent}</PresetPrice>");
            sb.AppendLine("<BannedRaces/>");
            sb.AppendLine("</AdvMapHero>");

            return sb.ToString();
        }

        private static string BuildPresetsXdb()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<DuelPresets ObjectRecordID=""1000001"">
<presets>
<Item>
<PresetNameFileRef href=""""/>
<LeftFace href=""""/>
<RightFace href=""""/>
<RoundedFace href=""""/>
<PresetHero href=""/Maps/DuelMode/Heroes/AdvMapHero1.xdb#xpointer(/AdvMapHero)""/>
</Item>
<Item>
<PresetNameFileRef href=""""/>
<LeftFace href=""""/>
<RightFace href=""""/>
<RoundedFace href=""""/>
<PresetHero href=""/Maps/DuelMode/Heroes/AdvMapHero2.xdb#xpointer(/AdvMapHero)""/>
</Item>
</presets>
</DuelPresets>";
        }

        private static string BuildMapXdb(string town1, string town2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<AdvMapDesc ObjectRecordID=\"1001204\">");
            sb.AppendLine("<CustomGameMap>false</CustomGameMap>");
            sb.AppendLine("<Version>3</Version>");
            sb.AppendLine("<TileX>72</TileX>");
            sb.AppendLine("<TileY>72</TileY>");
            sb.AppendLine("<HasUnderground>false</HasUnderground>");
            sb.AppendLine("<HasSurface>true</HasSurface>");
            sb.AppendLine("<InitialFloor>0</InitialFloor>");
            sb.AppendLine("<objects>");
            sb.AppendLine("<Item href=\"/Maps/DuelMode/Heroes/AdvMapHero1.xdb#xpointer(/AdvMapHero)\"/>");
            sb.AppendLine("<Item href=\"/Maps/DuelMode/Heroes/AdvMapHero2.xdb#xpointer(/AdvMapHero)\"/>");
            sb.AppendLine("</objects>");
            sb.AppendLine("<Resources><PointLights/><SavesFilenames/></Resources>");
            sb.AppendLine("<AmbientLight/>");
            sb.AppendLine("<UndergroundAmbientLight/>");
            sb.AppendLine("<GroundAmbientLights>");
            sb.AppendLine("<Item href=\"/Lights/_(AmbientLight)/0_Default_AmbientLight.xdb#xpointer(/AmbientLight)\"/>");
            sb.AppendLine("</GroundAmbientLights>");
            sb.AppendLine("<UndergroundAmbientLights><Item/></UndergroundAmbientLights>");
            sb.AppendLine("<ReflectiveWater>false</ReflectiveWater>");
            sb.AppendLine("<tiles>");
            sb.AppendLine("<Item href=\"/MapObjects/_(AdvMapTile)/Grass/Grass.xdb#xpointer(/AdvMapTile)\"/>");
            sb.AppendLine("</tiles>");
            sb.AppendLine("<regions/>");
            sb.AppendLine("<GroundTerrainFileName href=\"GroundTerrain.bin\"/>");
            sb.AppendLine("<UndergroundTerrainFileName href=\"\"/>");
            sb.AppendLine("<NameFileRef href=\"\"/>");
            sb.AppendLine("<DescriptionFileRef href=\"\"/>");
            sb.AppendLine("<moons>");
            for (int i = 0; i < 3; i++)
                sb.AppendLine("<Item><State>0</State><RotationRate>0</RotationRate></Item>");
            sb.AppendLine("</moons>");
            sb.AppendLine("<RandomMoons>true</RandomMoons>");
            sb.AppendLine("<HeroMaxLevel>0</HeroMaxLevel>");
            sb.AppendLine("<CustomTeams>false</CustomTeams>");
            sb.AppendLine("<players>");

            string[] colours = { "PCOLOR_BLUE", "PCOLOR_RED", "PCOLOR_NEUTRAL", "PCOLOR_NEUTRAL",
                                 "PCOLOR_NEUTRAL", "PCOLOR_NEUTRAL", "PCOLOR_NEUTRAL", "PCOLOR_NEUTRAL" };
            string[] towns = { town1, town2, "TOWN_NO_TYPE", "TOWN_NO_TYPE",
                               "TOWN_NO_TYPE", "TOWN_NO_TYPE", "TOWN_NO_TYPE", "TOWN_NO_TYPE" };

            for (int i = 0; i < 8; i++)
            {
                sb.AppendLine("<Item>");
                sb.AppendLine("<MainTown/>");
                sb.AppendLine("<MainHero/>");
                sb.AppendLine("<ActivePlayer>true</ActivePlayer>");
                sb.AppendLine("<Team>0</Team>");
                sb.AppendLine("<CanBeHumanPlayer>true</CanBeHumanPlayer>");
                sb.AppendLine("<CanBeComputerPlayer>true</CanBeComputerPlayer>");
                sb.AppendLine("<Behaviour>PB_RANDOM</Behaviour>");
                sb.AppendLine("<CaptureAbility>0</CaptureAbility>");
                sb.AppendLine("<StartHero/>");
                sb.AppendLine("<HeroInTown>false</HeroInTown>");
                sb.AppendLine("<ReserveHeroes/>");
                sb.AppendLine("<AddHeroTrigger><Action><FunctionName/></Action></AddHeroTrigger>");
                sb.AppendLine("<RemoveHeroTrigger><Action><FunctionName/></Action></RemoveHeroTrigger>");
                sb.AppendLine("<VictoryMessageRef href=\"\"/>");
                sb.AppendLine("<DefeatMessageRef href=\"\"/>");
                sb.AppendLine($"<Race>{towns[i]}</Race>");
                sb.AppendLine($"<Colour>{colours[i]}</Colour>");
                sb.AppendLine("<CanBeDisabled>true</CanBeDisabled>");
                sb.AppendLine("<Attractors/>");
                sb.AppendLine("<DefaultBonus>PLAYER_BONUS_RANDOM</DefaultBonus>");
                sb.AppendLine("<CanChangeBonus>true</CanChangeBonus>");
                sb.AppendLine("<TavernFilter><BannedHeroesRaces/><BannedHeroes/><AllowedHeroes/></TavernFilter>");
                sb.AppendLine("<DenyFogOfWarForAllies/>");
                sb.AppendLine("</Item>");
            }
            sb.AppendLine("</players>");

            sb.AppendLine("<CustomMapGoal>false</CustomMapGoal>");
            sb.AppendLine("<CustomGoal href=\"\"/>");
            sb.AppendLine(BuildScenarioInformation());
            sb.AppendLine("<artifactIDs/>");
            sb.AppendLine("<isUntransferable/>");
            sb.AppendLine("<AvailableHeroes/>");
            sb.AppendLine("<MapRumours/>");
            sb.AppendLine("<Music/>");
            sb.AppendLine("<MoonCalendarModifications><BlockMonstersWeeks>false</BlockMonstersWeeks></MoonCalendarModifications>");
            sb.AppendLine("<thumbnailImages/>");
            sb.AppendLine("<PWLPicture/>");
            sb.AppendLine("<BanTransparency>false</BanTransparency>");
            sb.AppendLine("<MoonCalendar/>");
            sb.AppendLine("<StartScene/>");
            sb.AppendLine("<sRMGProps>");
            sb.AppendLine("<RMGmap>false</RMGmap>");
            sb.AppendLine("<RMGversion>0</RMGversion>");
            sb.AppendLine("<RMGstartseed>0</RMGstartseed>");
            sb.AppendLine("<InitialParams>");
            sb.AppendLine("<MapSize>MAP_SIZE_SMALL</MapSize>");
            sb.AppendLine("<Players>0</Players>");
            sb.AppendLine("<Template/>");
            sb.AppendLine("<WaterAmount>WATER_NONE</WaterAmount>");
            sb.AppendLine("<MonsterLevel>MONSTER_LEVEL_WEAK</MonsterLevel>");
            sb.AppendLine("<HasUnderground>false</HasUnderground>");
            sb.AppendLine("<PlayersInfo>");
            sb.AppendLine("<Item><Race>TOWN_SPECIAL</Race><StartHero/></Item>");
            sb.AppendLine("<Item><Race>TOWN_SPECIAL</Race><StartHero/></Item>");
            sb.AppendLine("</PlayersInfo>");
            sb.AppendLine("<MapName/>");
            sb.AppendLine("<RandomTowns>false</RandomTowns>");
            sb.AppendLine("<Minimap>false</Minimap>");
            sb.AppendLine("</InitialParams>");
            sb.AppendLine("</sRMGProps>");
            sb.AppendLine("<dialogs/>");
            sb.AppendLine("<disabledArtifactSets/>");
            sb.AppendLine("<RacesRandomGroups/>");
            sb.AppendLine("<ImportantArtifacts><PreservingArtifacts/></ImportantArtifacts>");
            sb.AppendLine("<LoadingScreenSound/>");
            sb.AppendLine("<AdditionallyRollableHeroes/>");
            sb.AppendLine("</AdvMapDesc>");

            return sb.ToString();
        }

        private static string BuildScenarioInformation()
        {
            string awardBlock = @"<Award>
<Type>AWARD_NONE</Type>
<Experience>0</Experience>
<Resources><Wood>0</Wood><Ore>0</Ore><Mercury>0</Mercury><Crystal>0</Crystal><Sulfur>0</Sulfur><Gem>0</Gem><Gold>0</Gold></Resources>
<Attribute>HERO_ATTRIB_DEFENCE</Attribute>
<AttributeAmount>0</AttributeAmount>
<ArtifactID>ARTIFACT_NONE</ArtifactID>
<SpellID>SPELL_NONE</SpellID>
<ArmySlot><Creature>CREATURE_UNKNOWN</Creature><Count>0</Count></ArmySlot>
<SpellPoints>0</SpellPoints>
<Morale>0</Morale>
<Luck>0</Luck>
<SkillWithMastery><Mastery>MASTERY_NONE</Mastery><SkillID>HERO_SKILL_NONE</SkillID></SkillWithMastery>
</Award>";

            string targetGlance = @"<TargetGlance>
<Target><Type>ADV_TARGET_NONE</Type><Name/><Coords><FloorID>0</FloorID><cell><x>0</x><y>0</y></cell></Coords></Target>
<Radius>10</Radius>
<Duration>5000</Duration>
</TargetGlance>";

            string item = $@"<Item>
<Name/>
<CaptionFileRef href=""""/>
<ObscureCaptionFileRef href=""""/>
<DescriptionFileRef href=""""/>
<ProgressCommentsFileRef/>
<Kind>OBJECTIVE_KIND_SCENARIO_INFO</Kind>
<Parameters/>
<Timeout>-1</Timeout>
<Holdout>-1</Holdout>
<CheckDelay>-1</CheckDelay>
<Dependencies/>
<InstantVictory>false</InstantVictory>
{targetGlance}
{awardBlock}
<TakeContribution>false</TakeContribution>
<CanUncomplete>false</CanUncomplete>
<IsInitialyActive>true</IsInitialyActive>
<IsInitialyVisible>true</IsInitialyVisible>
<IsHidden>false</IsHidden>
<Ignore>false</Ignore>
<ShowCompleted>true</ShowCompleted>
<NeedComplete>true</NeedComplete>
<StateChangeTrigger><Action><FunctionName/></Action></StateChangeTrigger>
<SoundActivated/>
<SoundComplete/>
<SoundFailed/>
</Item>";

            return $"<ScenarioInformation>\n{item}\n{item}\n</ScenarioInformation>";
        }
    }
}
