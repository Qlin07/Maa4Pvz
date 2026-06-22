using System;
using System.Collections.Generic;
using System.Windows;

namespace MaaWpfGui.Helper;

public static class LocalizationHelper
{
    private static ResourceDictionary _resourceDictionary;

    public static void LoadLanguage(string language)
    {
        var uri = language switch
        {
            "zh-cn" or "" => new Uri("pack://application:,,,/Res/Localizations/zh-cn.xaml", UriKind.Absolute),
            _ => new Uri("pack://application:,,,/Res/Localizations/zh-cn.xaml", UriKind.Absolute),
        };

        try
        {
            var dict = new ResourceDictionary { Source = uri };

            if (_resourceDictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_resourceDictionary);
            }

            _resourceDictionary = dict;
            Application.Current.Resources.MergedDictionaries.Add(_resourceDictionary);
        }
        catch
        {
            // 如果加载失败，使用默认
        }
    }

    public static string GetString(string key)
    {
        if (_resourceDictionary != null && _resourceDictionary.Contains(key))
        {
            return _resourceDictionary[key]?.ToString() ?? key;
        }

        return key;
    }
}
