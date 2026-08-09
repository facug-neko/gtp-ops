namespace AxiomOps.Services.Models;

public class CasinoUserType
{
    public int UserTypeId { get; set; }
    public string? Description { get; set; }
}

public class Country
{
    public string? LongCode { get; set; }
    public string? Name { get; set; }
}

public class Currency
{
    public int CurrencyId { get; set; }
    public string? IsoCode { get; set; }
    public string? IsoName { get; set; }
    public int? IsoNumericCode { get; set; }
    public string? DisplayFormat { get; set; }
}

public class DurationSetting
{
    public int DurationTypeId { get; set; }
    public string? Description { get; set; }
    public int? MaxValue { get; set; }
}

public class InstalledCasino
{
    public int ServerId { get; set; }
    public string? CasinoName { get; set; }
}

public class Language
{
    public string? LanguageId { get; set; }
    public string? DialectShortName { get; set; }
    public string? SubDialectShortName { get; set; }
}

public class OperatorBaseCurrencyMap
{
    public string? BaseCurrencyIsoCode { get; set; }
    public string? BaseCurrencyDisplayFormat { get; set; }
    public string? BaseCurrencyIsoName { get; set; }
    public List<CurrencyLeveller>? CurrencyLevellers { get; set; }
}

public class CurrencyLeveller
{
    public string? CurrencyIsoCode { get; set; }
    public double? Leveller { get; set; }
    public string? CurrencyDisplayFormat { get; set; }
    public string? CurrencyIsoName { get; set; }
}

public class RegulatedMarketConfigSetting
{
    public int MarketTypeId { get; set; }
    public int RegulatedConfigId { get; set; }
    public int RegulatedSettingId { get; set; }
    public int? IntValue { get; set; }
    public string? StringValue { get; set; }
}

public class RegulatedMarket
{
    public int MarketTypeId { get; set; }
    public string? MarketType { get; set; }
    public string? Description { get; set; }
    public string? FriendlyName { get; set; }
}
