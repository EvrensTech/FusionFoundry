using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuelProtocol.Content
{
    [Serializable]
    public sealed class DuelArenaDefinition
    {
        public string Id = "energy-core-arena";
        public string DisplayNameKey = "arena.energy_core";
        public Vector2 Bounds = new Vector2(16f, 16f);
        public List<Vector2> CoreSpawnPoints = new List<Vector2> { Vector2.zero };
    }

    [Serializable]
    public sealed class DuelLocalizationEntry
    {
        public string Key = string.Empty;
        [TextArea] public string Turkish = string.Empty;
        [TextArea] public string English = string.Empty;
        [TextArea] public string German = string.Empty;
    }

    [Serializable]
    public sealed class DuelCharacterSkinDefinition
    {
        public string Id = "default-runner";
        public string DisplayNameKey = "skin.default_runner";
        public Color PrimaryColor = new Color(0.1f, 0.75f, 1f);
    }

    [Serializable]
    public sealed class DuelAbilityDefinition
    {
        public string Id = "pulse";
        public string DisplayNameKey = "ability.pulse";
        public float CooldownSeconds = 3f;
        public float Range = 2.25f;
    }

    [Serializable]
    public sealed class DuelMatchRuleDefinition
    {
        public string Id = "standard";
        public string DisplayNameKey = "rules.standard";
        public int ScoreLimit = 3;
        public float RegulationSeconds = 180f;
        public float OvertimeSeconds = 60f;
    }

    [CreateAssetMenu(menuName = "Duel Protocol/Content Catalog", fileName = "DuelContentCatalog")]
    public sealed class DuelContentCatalog : ScriptableObject
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private List<DuelArenaDefinition> arenas = new List<DuelArenaDefinition>();
        [SerializeField] private List<DuelCharacterSkinDefinition> skins =
            new List<DuelCharacterSkinDefinition>();
        [SerializeField] private List<DuelAbilityDefinition> abilities =
            new List<DuelAbilityDefinition>();
        [SerializeField] private List<DuelMatchRuleDefinition> matchRules =
            new List<DuelMatchRuleDefinition>();
        [SerializeField] private List<DuelLocalizationEntry> localization = new List<DuelLocalizationEntry>();
        [NonSerialized] private readonly HashSet<string> _missingLocalizationKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public int SchemaVersion => schemaVersion;
        public IReadOnlyList<DuelArenaDefinition> Arenas => arenas;
        public IReadOnlyList<DuelCharacterSkinDefinition> Skins => skins;
        public IReadOnlyList<DuelAbilityDefinition> Abilities => abilities;
        public IReadOnlyList<DuelMatchRuleDefinition> MatchRules => matchRules;
        public IReadOnlyCollection<string> MissingLocalizationKeys => _missingLocalizationKeys;

        public string Localize(string key, SystemLanguage language)
        {
            var entry = localization.Find(item => item.Key == key);
            if (entry == null)
            {
                if (_missingLocalizationKeys.Add(key ?? string.Empty))
                {
                    Debug.LogWarning($"DUEL_LOCALIZATION_MISSING key={key}");
                }
                return $"[{key}]";
            }
            if (language == SystemLanguage.Turkish && !string.IsNullOrWhiteSpace(entry.Turkish)) return entry.Turkish;
            if (language == SystemLanguage.German && !string.IsNullOrWhiteSpace(entry.German)) return entry.German;
            return entry.English;
        }

        public bool TryGetArena(string id, out DuelArenaDefinition arena)
        {
            arena = arenas.Find(item => item.Id == id);
            return arena != null;
        }

        public bool TryGetSkin(string id, out DuelCharacterSkinDefinition skin)
        {
            skin = skins.Find(item => item.Id == id);
            return skin != null;
        }

        public bool TryGetAbility(string id, out DuelAbilityDefinition ability)
        {
            ability = abilities.Find(item => item.Id == id);
            return ability != null;
        }

        public bool TryGetMatchRules(string id, out DuelMatchRuleDefinition rules)
        {
            rules = matchRules.Find(item => item.Id == id);
            return rules != null;
        }

        public void InitializeDefaults()
        {
            arenas = new List<DuelArenaDefinition>
            {
                new DuelArenaDefinition(),
                new DuelArenaDefinition
                {
                    Id = "energy-core-arena-wide",
                    DisplayNameKey = "arena.energy_core_wide",
                    Bounds = new Vector2(20f, 14f),
                    CoreSpawnPoints = new List<Vector2>
                    {
                        Vector2.zero,
                        new Vector2(-2f, 0f),
                        new Vector2(2f, 0f)
                    }
                }
            };
            skins = new List<DuelCharacterSkinDefinition>
            {
                new DuelCharacterSkinDefinition
                {
                    Id = "default-runner", DisplayNameKey = "skin.default_runner",
                    PrimaryColor = new Color32(28, 220, 255, 255)
                },
                new DuelCharacterSkinDefinition
                {
                    Id = "solar-warden", DisplayNameKey = "skin.solar_warden",
                    PrimaryColor = new Color32(255, 190, 54, 255)
                },
                new DuelCharacterSkinDefinition
                {
                    Id = "void-runner", DisplayNameKey = "skin.void_runner",
                    PrimaryColor = new Color32(182, 94, 255, 255)
                },
                new DuelCharacterSkinDefinition
                {
                    Id = "crimson-reaver", DisplayNameKey = "skin.crimson_reaver",
                    PrimaryColor = new Color32(255, 63, 86, 255)
                },
                new DuelCharacterSkinDefinition
                {
                    Id = "arctic-signal", DisplayNameKey = "skin.arctic_signal",
                    PrimaryColor = new Color32(86, 238, 255, 255)
                },
                new DuelCharacterSkinDefinition
                {
                    Id = "carbon-elite", DisplayNameKey = "skin.carbon_elite",
                    PrimaryColor = new Color32(74, 231, 155, 255)
                }
            };
            abilities = new List<DuelAbilityDefinition> { new DuelAbilityDefinition() };
            matchRules = new List<DuelMatchRuleDefinition> { new DuelMatchRuleDefinition() };
            localization = new List<DuelLocalizationEntry>
            {
                new DuelLocalizationEntry
                {
                    Key = "arena.energy_core",
                    Turkish = "Enerji Çekirdeği Arenası",
                    English = "Energy Core Arena",
                    German = "Energiekern-Arena"
                },
                new DuelLocalizationEntry
                {
                    Key = "arena.energy_core_wide",
                    Turkish = "Geniş Enerji Arenası",
                    English = "Wide Energy Arena",
                    German = "Weite Energie-Arena"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.default_runner", Turkish = "Protokol Sıfır", English = "Protocol Zero", German = "Protokoll Null"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.solar_warden", Turkish = "Güneş Muhafızı", English = "Solar Warden", German = "Solarwächter"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.void_runner", Turkish = "Boşluk Koşucusu", English = "Void Runner", German = "Leerenläufer"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.crimson_reaver", Turkish = "Kızıl Akıncı", English = "Crimson Reaver", German = "Karmesinräuber"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.arctic_signal", Turkish = "Arktik Sinyal", English = "Arctic Signal", German = "Arktisches Signal"
                },
                new DuelLocalizationEntry
                {
                    Key = "skin.carbon_elite", Turkish = "Karbon Elit", English = "Carbon Elite", German = "Carbon-Elite"
                },
                new DuelLocalizationEntry
                {
                    Key = "ability.pulse",
                    Turkish = "Darbe Dalgası",
                    English = "Pulse",
                    German = "Impuls"
                },
                new DuelLocalizationEntry
                {
                    Key = "rules.standard",
                    Turkish = "Standart Düello",
                    English = "Standard Duel",
                    German = "Standard-Duell"
                },
                new DuelLocalizationEntry
                {
                    Key = "match.rematch",
                    Turkish = "Yeniden Maç",
                    English = "Rematch",
                    German = "Revanche"
                }
            };
        }
    }
}
