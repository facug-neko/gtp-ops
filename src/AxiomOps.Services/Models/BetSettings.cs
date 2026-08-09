using System.Text.Json.Serialization;

namespace AxiomOps.Services.Models;

public class MultiplierTemplateSetting
{
    public string? TemplateName { get; set; }
    public int SettingId { get; set; }
    public string? SettingName { get; set; }
    public int? SettingIntValue { get; set; }
    public string? SettingStringValue { get; set; }
}

/// <summary>Polymorphic value holder: only the member matching <see cref="BetSetting.DataType"/> is populated.</summary>
public class BetSettingValue
{
    public int? SettingId { get; set; }
    public bool? Boolean { get; set; }
    public DateTimeOffset? Date { get; set; }
    public DateTimeOffset? DateTime { get; set; }
    public long? Integer { get; set; }
    public decimal? Money { get; set; }
    public double? Percentage { get; set; }
    public string? String { get; set; }
    public string? Time { get; set; }
}

public class BetSetting
{
    public int DataType { get; set; }
    public string? SettingDescription { get; set; }
    public int SettingId { get; set; }
    public string? SettingName { get; set; }
    public BetSettingValue? SettingValue { get; set; }
}

public class UserGameBetSettings
{
    public int ClientId { get; set; }
    public int ModuleId { get; set; }
    public int UserId { get; set; }
    public List<BetSetting>? Default { get; set; }
    public List<BetSetting>? User { get; set; }
}

public class SetUserGameBetSettingsRequest : UserGameBetSettings
{
    /// <summary>Server-side validation of the submitted values. The portal always sends true.</summary>
    public bool Validate { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValidationMode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Multiplier { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NumberPayLines { get; set; }
}
