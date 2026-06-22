using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace MaaWpfGui.Helper;

public class ConfigurationHelper
{
    private static readonly ILogger _logger = Log.ForContext<ConfigurationHelper>();

    private static ConfigurationHelper _instance;
    public static ConfigurationHelper Instance => _instance ??= new ConfigurationHelper();

    private JObject _config;
    private readonly string _configPath;

    public event Action<string, object> ConfigValueChanged;

    private ConfigurationHelper()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MaaPvz");
        Directory.CreateDirectory(baseDir);
        _configPath = Path.Combine(baseDir, "gui.json");

        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _config = JObject.Parse(json);
            }
            else
            {
                _config = new JObject();
            }
        }
        catch
        {
            _config = new JObject();
        }
    }

    public string GetValue(string key, string defaultValue = "")
    {
        try
        {
            var token = _config.SelectToken(key);
            return token?.ToString() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public bool SetValue(string key, object value)
    {
        try
        {
            var parts = key.Split('.');
            JToken current = _config;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current[parts[i]] == null)
                {
                    ((JObject)current)[parts[i]] = new JObject();
                }
                current = current[parts[i]];
            }

            var newValue = JToken.FromObject(value);
            if (current is JObject obj)
            {
                if (JToken.DeepEquals(obj[parts[^1]], newValue))
                    return true;

                obj[parts[^1]] = newValue;
            }

            Save();
            ConfigValueChanged?.Invoke(key, value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to set config value: {Key}", key);
            return false;
        }
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var value = GetValue(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        var value = GetValue(key);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public double GetDouble(string key, double defaultValue = 0)
    {
        var value = GetValue(key);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(_configPath, _config.ToString(Formatting.Indented));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save config");
        }
    }
}
