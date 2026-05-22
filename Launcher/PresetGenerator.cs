using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Launcher
{
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
            { "Орден Света",    "TOWN_HEAVEN"     },
            { "Инферно",        "TOWN_INFERNO"    },
            { "Некрополис",     "TOWN_NECROMANCY" },
            { "Лесной Союз",    "TOWN_PRESERVE"   },
            { "Академия",       "TOWN_ACADEMY"    },
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
            List<SpellInfo> allParsedSpells, string faction1, string faction2)
        {
            string fileName = "ER_presets_ru.h5u";
            string outputPath = Path.Combine(outputDir, fileName);

            Directory.CreateDirectory(outputDir);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

            string town1 = FactionToTown.TryGetValue(faction1, out var t1) ? t1 : "TOWN_HEAVEN";
            string town2 = FactionToTown.TryGetValue(faction2, out var t2) ? t2 : "TOWN_INFERNO";

            AddEntry(zip, "UI/MPDMLobby/presets.(DuelPresets).xdb", BuildPresetsXdb());
            AddEntry(zip, "Maps/DuelMode/PresetMap/map.xdb", BuildMapXdb(town1, town2));
            AddEntry(zip, "Maps/DuelMode/Heroes/AdvMapHero1.xdb", BuildHeroXdb(p1));
            AddEntry(zip, "Maps/DuelMode/Heroes/AdvMapHero2.xdb", BuildHeroXdb(p2));
            AddEntry(zip, "GameMechanics/RefTables/UndividedSpells.xdb",
                BuildUndividedSpellsXdb(allParsedSpells));

            return outputPath;
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
                sb.AppendLine($"<Item>{spell.Id}</Item>");
            foreach (var rune in p.Runes)
                sb.AppendLine($"<Item>{rune.Id}</Item>");
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

        private static string BuildUndividedSpellsXdb(List<SpellInfo> allSpells)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<UndividedSpells>");
            sb.AppendLine("<spellIDs>");
            foreach (var spell in allSpells)
                sb.AppendLine($"<Item>{spell.Id}</Item>");
            sb.AppendLine("</spellIDs>");
            sb.AppendLine("</UndividedSpells>");
            return sb.ToString();
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
