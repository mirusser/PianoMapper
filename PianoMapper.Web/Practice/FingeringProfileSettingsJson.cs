using System.Text;
using System.Text.Json;

namespace PianoMapper.Web.Practice;

/// <summary>
/// The browser-storage format of <see cref="FingeringProfileSettings"/>. <see cref="Parse"/> never throws: missing,
/// unreadable or unsupported values fall back to the generic default one hand at a time, so older or damaged stored
/// settings stay usable.
/// </summary>
internal static class FingeringProfileSettingsJson
{
    private const string RightComfortableName = "rightComfortable";
    private const string RightMaximumName = "rightMaximum";
    private const string LeftComfortableName = "leftComfortable";
    private const string LeftMaximumName = "leftMaximum";

    internal static FingeringProfileSettings Parse(string? json)
    {
        FingeringProfileSettings defaults = FingeringProfileSettings.Default;
        if (string.IsNullOrWhiteSpace(json))
        {
            return defaults;
        }

        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return defaults;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return defaults;
        }

        (double rightComfortable, double rightMaximum) = ReadHand(
            root,
            RightComfortableName,
            RightMaximumName,
            (defaults.RightComfortable, defaults.RightMaximum));
        (double leftComfortable, double leftMaximum) = ReadHand(
            root,
            LeftComfortableName,
            LeftMaximumName,
            (defaults.LeftComfortable, defaults.LeftMaximum));
        return new FingeringProfileSettings(rightComfortable, rightMaximum, leftComfortable, leftMaximum);
    }

    internal static string Serialize(FingeringProfileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber(RightComfortableName, settings.RightComfortable);
            writer.WriteNumber(RightMaximumName, settings.RightMaximum);
            writer.WriteNumber(LeftComfortableName, settings.LeftComfortable);
            writer.WriteNumber(LeftMaximumName, settings.LeftMaximum);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static (double Comfortable, double Maximum) ReadHand(
        JsonElement root,
        string comfortableName,
        string maximumName,
        (double Comfortable, double Maximum) fallback)
    {
        double? comfortable = null;
        double? maximum = null;
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out double value))
            {
                continue;
            }

            if (property.Name.Equals(comfortableName, StringComparison.OrdinalIgnoreCase))
            {
                comfortable = value;
            }
            else if (property.Name.Equals(maximumName, StringComparison.OrdinalIgnoreCase))
            {
                maximum = value;
            }
        }

        return comfortable is { } storedComfortable &&
            maximum is { } storedMaximum &&
            FingeringProfileSettings.IsValidHand(storedComfortable, storedMaximum)
                ? (storedComfortable, storedMaximum)
                : fallback;
    }
}
