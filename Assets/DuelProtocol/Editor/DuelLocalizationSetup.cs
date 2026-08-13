using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace DuelProtocol.Editor
{
    public static class DuelLocalizationSetup
    {
        private const string DirectoryPath = "Assets/DuelProtocol/Config/Localization";
        private const string SettingsPath = DirectoryPath + "/DuelLocalizationSettings.asset";

        [MenuItem("Duel Protocol/Localization/Ensure English Turkish German")]
        public static void EnsureConfigured()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
                AssetDatabase.Refresh();
            }

            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Duel Protocol Localization Settings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            LocalizationSettings.InitializeSynchronously = true;

            var english = EnsureLocale(SystemLanguage.English, "English");
            EnsureLocale(SystemLanguage.Turkish, "Türkçe");
            EnsureLocale(SystemLanguage.German, "Deutsch");
            LocalizationSettings.ProjectLocale = english;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("DUEL_LOCALIZATION_READY locales=en,tr,de provider=Unity.Localization");
        }

        private static Locale EnsureLocale(SystemLanguage language, string nativeName)
        {
            var identifier = Presentation.DuelLocalization.GetLocaleIdentifier(language);
            var locale = LocalizationEditorSettings.GetLocale(identifier);
            if (locale == null)
            {
                var path = $"{DirectoryPath}/Locale-{identifier.Code}.asset";
                locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
                if (locale == null)
                {
                    locale = Locale.CreateLocale(identifier);
                    AssetDatabase.CreateAsset(locale, path);
                }
                LocalizationEditorSettings.AddLocale(locale);
            }
            locale.LocaleName = nativeName;
            EditorUtility.SetDirty(locale);
            return locale;
        }
    }
}
