using System;
using UnityEngine;

namespace DuelProtocol.Content
{
    [Serializable]
    public readonly struct DuelCosmeticSkin
    {
        public DuelCosmeticSkin(string id, string tr, string en, string de, Color armor, Color glow, int price, bool owned)
        {
            Id = id; TurkishName = tr; EnglishName = en; GermanName = de; Armor = armor; Glow = glow; Price = price; OwnedByDefault = owned;
        }

        public string Id { get; }
        public string TurkishName { get; }
        public string EnglishName { get; }
        public string GermanName { get; }
        public Color Armor { get; }
        public Color Glow { get; }
        public int Price { get; }
        public bool OwnedByDefault { get; }

        public string GetName(SystemLanguage language) => language switch
        {
            SystemLanguage.Turkish => TurkishName,
            SystemLanguage.German => GermanName,
            _ => EnglishName
        };
    }

    public static class DuelCosmeticCatalog
    {
        public const string EquippedSkinKey = "duel.cosmetic.equipped.v1";
        public static readonly DuelCosmeticSkin[] Skins =
        {
            new DuelCosmeticSkin("protocol-zero", "Protokol Sıfır", "Protocol Zero", "Protokoll Null", new Color32(48, 61, 75, 255), new Color32(28, 220, 255, 255), 0, true),
            new DuelCosmeticSkin("solar-warden", "Güneş Muhafızı", "Solar Warden", "Solarwächter", new Color32(87, 58, 20, 255), new Color32(255, 190, 54, 255), 1200, true),
            new DuelCosmeticSkin("void-runner", "Boşluk Koşucusu", "Void Runner", "Leerenläufer", new Color32(39, 27, 63, 255), new Color32(182, 94, 255, 255), 1600, true),
            new DuelCosmeticSkin("crimson-reaver", "Kızıl Akıncı", "Crimson Reaver", "Karmesinräuber", new Color32(78, 26, 35, 255), new Color32(255, 63, 86, 255), 1600, true),
            new DuelCosmeticSkin("arctic-signal", "Arktik Sinyal", "Arctic Signal", "Arktisches Signal", new Color32(180, 205, 218, 255), new Color32(86, 238, 255, 255), 2000, true),
            new DuelCosmeticSkin("carbon-elite", "Karbon Elit", "Carbon Elite", "Carbon-Elite", new Color32(20, 24, 29, 255), new Color32(74, 231, 155, 255), 2400, true)
        };

        public static string EquippedId
        {
            get => PlayerPrefs.GetString(EquippedSkinKey, Skins[0].Id);
            set { PlayerPrefs.SetString(EquippedSkinKey, value); PlayerPrefs.Save(); }
        }

        public static DuelCosmeticSkin Equipped
        {
            get
            {
                var id = EquippedId;
                foreach (var skin in Skins) if (skin.Id == id) return skin;
                return Skins[0];
            }
        }
    }
}
