using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace DuelProtocol.Presentation
{
    /// <summary>
    /// Single locale boundary for Duel Protocol. Unity Localization owns locale identity and
    /// selection; generated runtime UI asks this service for the active localized copy.
    /// </summary>
    public static class DuelLocalization
    {
        public static readonly SystemLanguage[] SupportedLanguages =
        {
            SystemLanguage.English,
            SystemLanguage.Turkish,
            SystemLanguage.German
        };

        private static readonly IReadOnlyDictionary<string, string> GermanByEnglish =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["BACK"] = "ZURÜCK",
                ["ENERGY CORE ARENA"] = "ENERGIEKERN-ARENA",
                ["COMMAND DECK"] = "KOMMANDOZENTRALE",
                ["PLAY"] = "SPIELEN",
                ["COLLECTION / SKINS"] = "SAMMLUNG / SKINS",
                ["STORE"] = "SHOP",
                ["PROFILE"] = "PROFIL",
                ["SETTINGS"] = "EINSTELLUNGEN",
                ["QUIT"] = "BEENDEN",
                ["SELECT MODE"] = "MODUS WÄHLEN",
                ["ROBOT COLLECTION"] = "ROBOTER-SAMMLUNG",
                ["COSMETIC SHOWCASE"] = "KOSMETIK-SCHAUFENSTER",
                ["PILOT RECORD"] = "PILOTENAKTE",
                ["SYSTEM SETTINGS"] = "SYSTEMEINSTELLUNGEN",
                ["1 // GAME MODE"] = "1 // SPIELMODUS",
                ["SOLO\nAI DUEL"] = "SOLO\nKI-DUELL",
                ["ONLINE\nRANKED / PRIVATE"] = "ONLINE\nRANGLISTE / PRIVAT",
                ["2 // ARENA"] = "2 // ARENA",
                ["NEON CITADEL  •  BALANCED  •  18×18"] = "NEON CITADEL  •  AUSGEWOGEN  •  18×18",
                ["ORBITAL FOUNDRY  •  WIDE  •  20×14"] = "ORBITAL FOUNDRY  •  WEIT  •  20×14",
                ["STANDARD DUEL  //  3 SCORE  //  180 SEC  //  EQUAL LOADOUT"] = "STANDARD-DUELL  //  3 PUNKTE  //  180 SEK  //  GLEICHE AUSRÜSTUNG",
                ["DEPLOY TO ARENA"] = "ARENA BETRETEN",
                ["EQUIPPED ROBOT APPEARS IN MATCH"] = "AUSGERÜSTETER ROBOTER ERSCHEINT IM MATCH",
                ["LOCAL SHOWCASE // NO REAL-MONEY PURCHASES"] = "LOKALES SCHAUFENSTER // KEINE ECHTGELD-KÄUFE",
                ["All cosmetics are unlocked in this development build. Prices represent the economy and UI flow only."] = "In diesem Entwicklungs-Build sind alle Kosmetika freigeschaltet. Preise bilden nur Wirtschaft und UI-Ablauf ab.",
                ["DEFAULT"] = "STANDARD",
                ["EQUIPPED"] = "AUSGERÜSTET",
                ["PILOT // LOCAL DEVELOPMENT PROFILE"] = "PILOT // LOKALES ENTWICKLUNGSPROFIL",
                ["CALLSIGN"] = "RUFZEICHEN",
                ["RATING"] = "WERTUNG",
                ["SEASON"] = "SAISON",
                ["PRE-SEASON"] = "VORSAISON",
                ["EQUIPPED SKIN"] = "AUSGERÜSTETER SKIN",
                ["Offline-safe mode active. Progress syncs here when UGS is available."] = "Offline-Sicherheitsmodus aktiv. Der Fortschritt wird hier synchronisiert, sobald UGS verfügbar ist.",
                ["ACCESSIBILITY"] = "BARRIEREFREIHEIT",
                ["REDUCE MOTION"] = "BEWEGUNG REDUZIEREN",
                ["HIGH CONTRAST"] = "HOHER KONTRAST",
                ["UI SCALE"] = "UI-SKALIERUNG",
                ["AUDIO & LANGUAGE"] = "AUDIO & SPRACHE",
                ["MASTER VOLUME"] = "GESAMTLAUTSTÄRKE",
                ["MUSIC VOLUME"] = "MUSIKLAUTSTÄRKE",
                ["SFX VOLUME"] = "EFFEKTLAUTSTÄRKE",
                ["UI VOLUME"] = "UI-LAUTSTÄRKE",
                ["LANGUAGE"] = "SPRACHE",
                ["RESTORE DEFAULTS"] = "STANDARDWERTE WIEDERHERSTELLEN",
                ["SYSTEM PAUSED"] = "SYSTEM PAUSIERT",
                ["RESUME"] = "FORTSETZEN",
                ["REDUCED MOTION"] = "REDUZIERTE BEWEGUNG",
                ["RETURN TO MENU"] = "ZUM MENÜ",
                ["RETURN TO MAIN MENU"] = "ZUM HAUPTMENÜ",
                ["REMATCH"] = "REVANCHE",
                ["MAIN MENU"] = "HAUPTMENÜ",
                ["PROTOCOL COMPLETE"] = "PROTOKOLL ABGESCHLOSSEN",
                ["SIGNAL LOST"] = "SIGNAL VERLOREN",
                ["INITIALIZING"] = "INITIALISIERUNG",
                ["DUEL ACTIVE"] = "DUELL AKTIV",
                ["OVERTIME"] = "VERLÄNGERUNG",
                ["RESULT"] = "ERGEBNIS",
                ["IDENTITY"] = "IDENTITÄT",
                ["SEARCHING FOR OPPONENT"] = "GEGNER WIRD GESUCHT",
                ["SECURE CONNECTION"] = "SICHERE VERBINDUNG",
                ["ROOM"] = "RAUM",
                ["ARENA NETWORK GATEWAY"] = "ARENA-NETZWERKZUGANG",
                ["Match quickly or use a six-character private room code."] = "Finde schnell ein Match oder nutze einen sechsstelligen privaten Raumcode.",
                ["FIND UNRANKED OPPONENT"] = "GEGNER OHNE RANGLISTE FINDEN",
                ["FIND RANKED OPPONENT"] = "RANGLISTEN-GEGNER FINDEN",
                ["CREATE PRIVATE ROOM"] = "PRIVATEN RAUM ERSTELLEN",
                ["ROOM CODE"] = "RAUMCODE",
                ["JOIN WITH CODE"] = "MIT CODE BEITRETEN",
                ["CANCEL SEARCH"] = "SUCHE ABBRECHEN",
                ["READY"] = "BEREIT",
                ["LEAVE"] = "VERLASSEN",
                ["DUEL COMPLETE"] = "DUELL ABGESCHLOSSEN",
                ["READY CHECK"] = "BEREITSCHAFTSPRÜFUNG",
                ["WAITING FOR CONNECTION"] = "WARTEN AUF VERBINDUNG",
                ["CORE AVAILABLE // CAPTURE IT"] = "KERN VERFÜGBAR // EROBERN",
                ["PULSE READY  •  SPACE / R2"] = "IMPULS BEREIT  •  LEERTASTE / R2",
                ["MOVE: WASD, joystick or left stick"] = "BEWEGEN: WASD, Joystick oder linker Stick",
                ["PULSE IN MOVE DIRECTION: Space, gamepad button or ATTACK"] = "IMPULS IN BEWEGUNGSRICHTUNG: Leertaste, Gamepad oder ANGRIFF",
                ["CAPTURE: take the core and hold it for 15 seconds"] = "EROBERN: Nimm den Kern und halte ihn 15 Sekunden"
            };

        public static SystemLanguage CurrentLanguage => DuelPlayerPreferences.Current.Language;

        public static LocaleIdentifier GetLocaleIdentifier(SystemLanguage language) =>
            new LocaleIdentifier(language switch
            {
                SystemLanguage.Turkish => "tr",
                SystemLanguage.German => "de",
                _ => "en"
            });

        public static int GetLanguageIndex(SystemLanguage language) => language switch
        {
            SystemLanguage.Turkish => 1,
            SystemLanguage.German => 2,
            _ => 0
        };

        public static SystemLanguage GetLanguageAt(int index) =>
            index >= 0 && index < SupportedLanguages.Length
                ? SupportedLanguages[index]
                : SystemLanguage.English;

        public static string GetNativeName(SystemLanguage language) => language switch
        {
            SystemLanguage.Turkish => "Türkçe",
            SystemLanguage.German => "Deutsch",
            _ => "English"
        };

        public static string Text(string turkish, string english, string german = null)
        {
            return CurrentLanguage switch
            {
                SystemLanguage.Turkish => turkish,
                SystemLanguage.German => german ??
                    (GermanByEnglish.TryGetValue(english, out var translated) ? translated : english),
                _ => english
            };
        }

        public static void SyncUnityLocale(SystemLanguage language)
        {
            var settings = LocalizationSettings.GetInstanceDontCreateDefault();
            if (settings == null) return;

            var locale = settings.GetAvailableLocales()?.GetLocale(GetLocaleIdentifier(language));
            if (locale != null && settings.GetSelectedLocale() != locale)
            {
                settings.SetSelectedLocale(locale);
            }
        }
    }
}
