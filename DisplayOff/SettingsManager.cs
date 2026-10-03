using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplayOff;

internal enum DisplayOffDelay
{
    Immediately = 0,
    Seconds3 = 3,
    Seconds5 = 5,
    Seconds10 = 10
}

internal static class DisplayOffDelayExtensions
{
    public static int ToMilliseconds(this DisplayOffDelay delay) => (int)delay * 1000;

    public static string ToLabel(this DisplayOffDelay delay) => delay switch
    {
        DisplayOffDelay.Seconds3 => "3 seconds",
        DisplayOffDelay.Seconds5 => "5 seconds",
        DisplayOffDelay.Seconds10 => "10 seconds",
        _ => "Immediately"
    };
}

internal sealed class AppSettings
{
    private const int AllowedModifiers = (int)(NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN);

    public int Modifiers { get; set; } = (int)(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT);

    public int VirtualKey { get; set; } = (int)Keys.F12;

    public bool StartWithWindows { get; set; }

    [JsonConverter(typeof(DisplayOffDelayConverter))]
    public DisplayOffDelay Delay { get; set; } = DisplayOffDelay.Immediately;

    public AppSettings Copy() => new()
    {
        Modifiers = Modifiers,
        VirtualKey = VirtualKey,
        StartWithWindows = StartWithWindows,
        Delay = Delay
    };

    public void Normalize()
    {
        Modifiers &= AllowedModifiers;
        if (Modifiers == 0 || VirtualKey is < 1 or > 255)
        {
            Modifiers = (int)(NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT);
            VirtualKey = (int)Keys.F12;
        }
    }
}

internal static class SettingsManager
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DisplayOff",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();

            byte[] json = File.ReadAllBytes(FilePath);
            AppSettings? settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            if (settings is null)
                return new AppSettings();

            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static bool TrySave(AppSettings settings, out string? error)
    {
        error = null;
        settings.Normalize();

        try
        {
            string? directory = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(directory))
                throw new IOException("The settings folder path is empty.");

            Directory.CreateDirectory(directory);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJsonContext.Default.AppSettings);
            string temporary = FilePath + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, json);
                File.Move(temporary, FilePath, overwrite: true);
            }
            catch
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);

                throw;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = "Couldn't save settings. " + ex.Message;
            return false;
        }
    }
}

internal sealed class DisplayOffDelayConverter : JsonConverter<DisplayOffDelay>
{
    public override DisplayOffDelay Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Display off delay must be a string.");

        return reader.GetString() switch
        {
            "3 seconds" => DisplayOffDelay.Seconds3,
            "5 seconds" => DisplayOffDelay.Seconds5,
            "10 seconds" => DisplayOffDelay.Seconds10,
            _ => DisplayOffDelay.Immediately
        };
    }

    public override void Write(Utf8JsonWriter writer, DisplayOffDelay value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToLabel());
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
