using AxiomOps.Services.Http;
using AxiomOps.Services.Models;

namespace AxiomOps.Services;

/// <summary>Postman folder: CasinoSettings.</summary>
public interface ICasinoSettingsService
{
    /// <summary>GET /CasinoSettings/CasinoUserTypes</summary>
    Task<AxiomResponse<List<CasinoUserType>>> GetCasinoUserTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/Countries</summary>
    Task<AxiomResponse<List<Country>>> GetCountriesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/Currencies</summary>
    Task<AxiomResponse<List<Currency>>> GetCurrenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/DurationSettings</summary>
    Task<AxiomResponse<List<DurationSetting>>> GetDurationSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/InstalledCasinos</summary>
    Task<AxiomResponse<List<InstalledCasino>>> GetInstalledCasinosAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/LanguageCodes</summary>
    Task<AxiomResponse<List<string>>> GetLanguageCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/Languages</summary>
    Task<AxiomResponse<List<Language>>> GetLanguagesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/OperatorBaseCurrencyMap</summary>
    Task<AxiomResponse<OperatorBaseCurrencyMap>> GetOperatorBaseCurrencyMapAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/RegulatedMarketConfigSettings</summary>
    Task<AxiomResponse<List<RegulatedMarketConfigSetting>>> GetRegulatedMarketConfigSettingsAsync(int? regulatedSettingId = null, int? regulatedConfigId = null, CancellationToken cancellationToken = default);

    /// <summary>GET /CasinoSettings/RegulatedMarkets</summary>
    Task<AxiomResponse<List<RegulatedMarket>>> GetRegulatedMarketsAsync(CancellationToken cancellationToken = default);
}

public sealed class CasinoSettingsService(HttpClient http) : AxiomServiceBase(http), ICasinoSettingsService
{
    public Task<AxiomResponse<List<CasinoUserType>>> GetCasinoUserTypesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<CasinoUserType>>("CasinoSettings/CasinoUserTypes", cancellationToken);

    public Task<AxiomResponse<List<Country>>> GetCountriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<Country>>("CasinoSettings/Countries", cancellationToken);

    public Task<AxiomResponse<List<Currency>>> GetCurrenciesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<Currency>>("CasinoSettings/Currencies", cancellationToken);

    public Task<AxiomResponse<List<DurationSetting>>> GetDurationSettingsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<DurationSetting>>("CasinoSettings/DurationSettings", cancellationToken);

    public Task<AxiomResponse<List<InstalledCasino>>> GetInstalledCasinosAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<InstalledCasino>>("CasinoSettings/InstalledCasinos", cancellationToken);

    public Task<AxiomResponse<List<string>>> GetLanguageCodesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<string>>("CasinoSettings/LanguageCodes", cancellationToken);

    public Task<AxiomResponse<List<Language>>> GetLanguagesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<Language>>("CasinoSettings/Languages", cancellationToken);

    public Task<AxiomResponse<OperatorBaseCurrencyMap>> GetOperatorBaseCurrencyMapAsync(int productId, CancellationToken cancellationToken = default) =>
        GetAsync<OperatorBaseCurrencyMap>(WithQuery("CasinoSettings/OperatorBaseCurrencyMap", ("productId", productId)), cancellationToken);

    public Task<AxiomResponse<List<RegulatedMarketConfigSetting>>> GetRegulatedMarketConfigSettingsAsync(int? regulatedSettingId = null, int? regulatedConfigId = null, CancellationToken cancellationToken = default) =>
        GetAsync<List<RegulatedMarketConfigSetting>>(
            WithQuery("CasinoSettings/RegulatedMarketConfigSettings", ("regulatedSettingId", regulatedSettingId), ("regulatedConfigId", regulatedConfigId)),
            cancellationToken);

    public Task<AxiomResponse<List<RegulatedMarket>>> GetRegulatedMarketsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<RegulatedMarket>>("CasinoSettings/RegulatedMarkets", cancellationToken);
}
