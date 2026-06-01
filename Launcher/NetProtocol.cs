using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Launcher
{
    // ── Сетевой протокол комнат ────────────────────────────────────────────────
    // Передаём только идентификаторы и числа; объекты (герой/юниты/арты/заклы)
    // восстанавливаются на стороне получателя из локально распарсенных данных мода.
    // У обоих игроков одинаковые файлы → детерминированная сборка пресета.

    public enum NetMsgType : byte
    {
        PlayerReady = 1,   // финальный выбор игрока (после «Готов»)
    }

    public class NetSlot
    {
        public string Id { get; set; } = "";
        public int Count { get; set; }
    }

    public class NetSkill
    {
        public string Id { get; set; } = "";
        public int Mastery { get; set; }
    }

    // Полный финальный выбор одного игрока (то, что нужно для сборки пресета).
    public class NetPlayerData
    {
        public string HeroId { get; set; } = "";        // HeroInfo.InternalName
        public string Faction { get; set; } = "";
        public List<NetSlot> Army { get; set; } = new();
        public Dictionary<string, string> Artifacts { get; set; } = new(); // slot → artifactId
        public int HeroLevel { get; set; } = 1;
        public int Offence { get; set; }
        public int Defence { get; set; }
        public int Spellpower { get; set; }
        public int Knowledge { get; set; }
        public List<NetSkill> Skills { get; set; } = new();
        public List<string> Perks { get; set; } = new();
        public int RacialMastery { get; set; }
        public List<string> Spells { get; set; } = new();  // SpellInfo.Id
        public List<string> Runes { get; set; } = new();
        public int GoldSpent { get; set; }
    }

    public static class NetProtocol
    {
        private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

        // Сворачивает PlayerPreset в компактный DTO с идентификаторами.
        public static NetPlayerData ToDto(PlayerPreset p, string faction)
        {
            var dto = new NetPlayerData
            {
                HeroId = p.Hero?.InternalName ?? "",
                Faction = faction,
                HeroLevel = p.HeroLevel,
                Offence = p.TotalOffence,
                Defence = p.TotalDefence,
                Spellpower = p.TotalSpellpower,
                Knowledge = p.TotalKnowledge,
                RacialMastery = p.RacialMastery,
                Perks = new List<string>(p.Perks ?? new List<string>()),
                GoldSpent = p.GoldSpent,
            };

            foreach (var s in p.ArmySlots ?? Array.Empty<ArmySlot>())
            {
                if (s?.Creature == null || s.Count <= 0) continue;
                dto.Army.Add(new NetSlot { Id = s.Creature.Id, Count = s.Count });
            }

            foreach (var kv in p.EquippedArtifacts ?? new Dictionary<string, ArtifactInfo?>())
            {
                if (kv.Value != null) dto.Artifacts[kv.Key] = kv.Value.Id;
            }

            foreach (var (skillId, mastery) in p.Skills ?? new List<(string, int)>())
                dto.Skills.Add(new NetSkill { Id = skillId, Mastery = mastery });

            foreach (var sp in p.Spells ?? new List<SpellInfo>())
                dto.Spells.Add(sp.Id);
            foreach (var rn in p.Runes ?? new List<SpellInfo>())
                dto.Runes.Add(rn.Id);

            return dto;
        }

        // Кодирует сообщение: [тип:1 байт][json UTF-8].
        public static byte[] Encode(NetMsgType type, NetPlayerData data)
        {
            string json = JsonSerializer.Serialize(data, Opts);
            byte[] body = Encoding.UTF8.GetBytes(json);
            var buf = new byte[body.Length + 1];
            buf[0] = (byte)type;
            Array.Copy(body, 0, buf, 1, body.Length);
            return buf;
        }

        public static bool TryDecode(byte[] buf, out NetMsgType type, out NetPlayerData? data)
        {
            type = default;
            data = null;
            if (buf == null || buf.Length < 1) return false;
            type = (NetMsgType)buf[0];
            try
            {
                string json = Encoding.UTF8.GetString(buf, 1, buf.Length - 1);
                data = JsonSerializer.Deserialize<NetPlayerData>(json, Opts);
                return data != null;
            }
            catch { return false; }
        }
    }
}
