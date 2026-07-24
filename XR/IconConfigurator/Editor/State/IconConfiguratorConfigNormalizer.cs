using System.Collections.Generic;

namespace ByteDance.PICO.IconConfigurator.Editor
{
    public static class IconConfiguratorConfigNormalizer
    {
        public static void Normalize(IconConfiguratorConfigAsset config)
        {
            if (config == null)
            {
                return;
            }

            config.Manual ??= new ManualLayerState();
            config.AiSplit ??= new AiSplitState();
            config.Localizations ??= new List<LocalizationEntry>();

            EnsureDefaultLocalization(config.Localizations);
            config.AiSplit.EnsureDynamicResultLists();
        }

        public static void NormalizeLocalizations(IList<LocalizationEntry> localizations)
        {
            RemoveUnsupportedLocalizations(localizations);
            EnsureDefaultLocalization(localizations);
        }

        private static void RemoveUnsupportedLocalizations(IList<LocalizationEntry> localizations)
        {
            if (localizations == null)
            {
                return;
            }

            for (int i = localizations.Count - 1; i >= 0; i--)
            {
                LocalizationEntry entry = localizations[i];
                if (entry == null)
                {
                    continue;
                }

                if (!IconConfiguratorLocales.IsSupported(entry.LocaleCode))
                {
                    localizations.RemoveAt(i);
                }
            }
        }

        private static void EnsureDefaultLocalization(IList<LocalizationEntry> localizations)
        {
            if (localizations == null)
            {
                return;
            }

            bool hasDefaultLocale = false;
            for (int i = 0; i < localizations.Count; i++)
            {
                LocalizationEntry entry = localizations[i];
                if (entry == null)
                {
                    continue;
                }

                bool isDefaultLocale = entry.LocaleCode == IconConfiguratorLocales.DefaultLocale;
                entry.IsDefault = isDefaultLocale;
                entry.CanRemove = !isDefaultLocale;

                if (isDefaultLocale)
                {
                    hasDefaultLocale = true;
                }
            }

            if (hasDefaultLocale)
            {
                return;
            }

            localizations.Insert(0, new LocalizationEntry
            {
                LocaleCode = IconConfiguratorLocales.DefaultLocale,
                AppName = string.Empty,
                IsDefault = true,
                CanRemove = false,
            });
        }
    }
}
