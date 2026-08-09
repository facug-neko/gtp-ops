using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using AxiomOps.Services.TestData;
using AxiomOps.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;

namespace AxiomOps.UI.ViewModels;

/// <summary>
/// Account creation: a quick mode (name only, sensible defaults), a full form
/// mirroring the portal's Create User Account, and a combined flow that creates
/// a user together with its testdata, rewriting the testdata's loginName to match.
/// </summary>
public partial class CreateUserViewModel : ObservableObject
{
    // Defaults requested for the quick mode.
    private const int DefaultMarketTypeId = 0;      // DEF — No Regulated Market
    private const int DefaultServerId = 5001;       // Island Paradise
    private const string DefaultCountry = "Argentina";
    private const string DefaultCurrency = "USD";
    private const int DefaultUserTypeId = 0;        // Real Player
    private const string DefaultPassword = "test1234$";

    private readonly IUserAccountsService _userAccounts;
    private readonly ICasinoSettingsService _casinoSettings;
    private readonly IUploadService _upload;
    private readonly IMessenger _messenger;

    private HashSet<string> _existingLogins = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<CasinoUserType> UserTypes { get; } = [];
    public ObservableCollection<RegulatedMarket> Markets { get; } = [];
    public ObservableCollection<InstalledCasino> Products { get; } = [];
    public ObservableCollection<Country> Countries { get; } = [];
    public ObservableCollection<Currency> Currencies { get; } = [];

    // ----- quick mode -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickCreateCommand))]
    [NotifyPropertyChangedFor(nameof(QuickNameStatus))]
    private string _quickUsername = string.Empty;

    public string QuickNameStatus => DescribeAvailability(QuickUsername);

    // ----- full form -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyPropertyChangedFor(nameof(UsernameStatus))]
    private string _username = string.Empty;

    /// <summary>
    /// In bulk mode the name is a prefix (the server appends the sequence), so an
    /// existing base name isn't necessarily a conflict — we warn but don't block.
    /// </summary>
    public string UsernameStatus => BulkCreate
        ? (string.IsNullOrWhiteSpace(Username) ? string.Empty : "Se usará como prefijo de las cuentas")
        : DescribeAvailability(Username);

    [ObservableProperty]
    private string _password = DefaultPassword;

    [ObservableProperty]
    private CasinoUserType? _selectedUserType;

    [ObservableProperty]
    private RegulatedMarket? _selectedMarket;

    [ObservableProperty]
    private InstalledCasino? _selectedProduct;

    [ObservableProperty]
    private Country? _selectedCountry;

    [ObservableProperty]
    private Currency? _selectedCurrency;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyPropertyChangedFor(nameof(UsernameStatus))]
    private bool _bulkCreate;

    [ObservableProperty]
    private int _numberOfAccounts = 1;

    // ----- user + testdata -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateWithTestDataCommand))]
    [NotifyPropertyChangedFor(nameof(TestDataNameStatus))]
    private string _testDataUsername = string.Empty;

    public string TestDataNameStatus => DescribeAvailability(TestDataUsername);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateWithTestDataCommand))]
    private string _testDataContent = string.Empty;

    [ObservableProperty]
    private string _testDataFileName = string.Empty;

    /// <summary>loginName currently present in the loaded testdata XML.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginNameWillChange))]
    private string? _testDataLoginName;

    [ObservableProperty]
    private string? _testDataModuleId;

    [ObservableProperty]
    private string? _testDataClientId;

    public bool LoginNameWillChange =>
        !string.IsNullOrWhiteSpace(TestDataLoginName)
        && !string.IsNullOrWhiteSpace(TestDataUsername)
        && !string.Equals(TestDataLoginName, TestDataUsername.Trim(), StringComparison.Ordinal);

    // ----- shared -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickCreateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateWithTestDataCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadTestDataFileCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public CreateUserViewModel(
        IUserAccountsService userAccounts,
        ICasinoSettingsService casinoSettings,
        IUploadService upload,
        IMessenger messenger)
    {
        _userAccounts = userAccounts;
        _casinoSettings = casinoSettings;
        _upload = upload;
        _messenger = messenger;
    }

    partial void OnTestDataUsernameChanged(string value) => OnPropertyChanged(nameof(LoginNameWillChange));

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando catálogos…";

        try
        {
            var typesTask = _casinoSettings.GetCasinoUserTypesAsync();
            var marketsTask = _casinoSettings.GetRegulatedMarketsAsync();
            var productsTask = _casinoSettings.GetInstalledCasinosAsync();
            var countriesTask = _casinoSettings.GetCountriesAsync();
            var currenciesTask = _casinoSettings.GetCurrenciesAsync();
            var usersTask = _userAccounts.GetUserAccountsAsync();

            await Task.WhenAll(typesTask, marketsTask, productsTask, countriesTask, currenciesTask, usersTask);

            Fill(UserTypes, typesTask.Result.DataObject);
            Fill(Markets, marketsTask.Result.DataObject);
            Fill(Products, productsTask.Result.DataObject);
            Fill(Countries, countriesTask.Result.DataObject);
            Fill(Currencies, currenciesTask.Result.DataObject);

            SelectedUserType = UserTypes.FirstOrDefault(t => t.UserTypeId == DefaultUserTypeId) ?? UserTypes.FirstOrDefault();
            SelectedMarket = Markets.FirstOrDefault(m => m.MarketTypeId == DefaultMarketTypeId) ?? Markets.FirstOrDefault();
            SelectedProduct = Products.FirstOrDefault(p => p.ServerId == DefaultServerId) ?? Products.FirstOrDefault();
            SelectedCountry = Countries.FirstOrDefault(c => c.Name == DefaultCountry) ?? Countries.FirstOrDefault();
            SelectedCurrency = Currencies.FirstOrDefault(c => c.IsoCode == DefaultCurrency) ?? Currencies.FirstOrDefault();

            RefreshExistingLogins(usersTask.Result.DataObject);
            StatusMessage = $"Listo. {_existingLogins.Count} usuarios existentes en el ambiente.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudieron cargar los catálogos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Fill<T>(ObservableCollection<T> target, List<T>? source)
    {
        target.Clear();
        foreach (var item in source ?? [])
        {
            target.Add(item);
        }
    }

    private void RefreshExistingLogins(List<UserAccount>? users)
    {
        _existingLogins = (users ?? [])
            .SelectMany(u => new[] { u.LoginName, u.Username })
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        OnPropertyChanged(nameof(QuickNameStatus));
        OnPropertyChanged(nameof(TestDataNameStatus));
        OnPropertyChanged(nameof(UsernameStatus));
        CreateCommand.NotifyCanExecuteChanged();
        QuickCreateCommand.NotifyCanExecuteChanged();
        CreateWithTestDataCommand.NotifyCanExecuteChanged();
    }

    private bool Exists(string? name) =>
        !string.IsNullOrWhiteSpace(name) && _existingLogins.Contains(name.Trim());

    private string DescribeAvailability(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return Exists(name) ? "✘ Ya existe un usuario con ese nombre" : "✔ Disponible";
    }

    // ----- quick create -----

    private bool CanQuickCreate() => !IsBusy && !string.IsNullOrWhiteSpace(QuickUsername) && !Exists(QuickUsername);

    [RelayCommand(CanExecute = nameof(CanQuickCreate))]
    private async Task QuickCreateAsync()
    {
        var created = await CreateAccountAsync(new CreateUserAccountRequest
        {
            Username = QuickUsername.Trim(),
            Password = DefaultPassword,
            MarketTypeId = DefaultMarketTypeId,
            ServerId = DefaultServerId,
            UserTypeId = DefaultUserTypeId,
            CurrencyIsoCode = DefaultCurrency,
            Country = DefaultCountry,
            NumberOfAccounts = 1,
        });

        // Clearing the field also clears the availability label, which would
        // otherwise flip to "ya existe" for the account we just created.
        if (created)
        {
            QuickUsername = string.Empty;
        }
    }

    // ----- full create -----

    private bool CanCreate() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Username) && (BulkCreate || !Exists(Username));

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        var created = await CreateAccountAsync(new CreateUserAccountRequest
        {
            Username = Username.Trim(),
            Password = string.IsNullOrWhiteSpace(Password) ? DefaultPassword : Password,
            MarketTypeId = SelectedMarket?.MarketTypeId ?? DefaultMarketTypeId,
            ServerId = SelectedProduct?.ServerId ?? DefaultServerId,
            UserTypeId = SelectedUserType?.UserTypeId ?? DefaultUserTypeId,
            CurrencyIsoCode = SelectedCurrency?.IsoCode ?? DefaultCurrency,
            Country = SelectedCountry?.Name ?? DefaultCountry,
            NumberOfAccounts = BulkCreate ? Math.Max(1, NumberOfAccounts) : 1,
        });

        if (created)
        {
            Username = string.Empty;
        }
    }

    private async Task<bool> CreateAccountAsync(CreateUserAccountRequest request)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Creando {request.Username}…";

        try
        {
            var result = await _userAccounts.CreateUserAccountAsync(request);
            if (!result.Success)
            {
                ErrorMessage = $"No se pudo crear la cuenta: {Shorten(result.CustomMessage)}";
                return false;
            }

            var howMany = request.NumberOfAccounts > 1 ? $" ({request.NumberOfAccounts} cuentas)" : string.Empty;
            StatusMessage = $"✔ Usuario \"{request.Username}\" creado correctamente{howMany}.";
            await ReloadExistingLoginsAsync();
            return true;
        }
        catch (AxiomApiException ex)
        {
            ErrorMessage = $"No se pudo crear la cuenta — {AxiomErrorText.Describe(ex)}";
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"Error creando la cuenta: {Shorten(ex.Message)}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadExistingLoginsAsync()
    {
        try
        {
            var users = await _userAccounts.GetUserAccountsAsync();
            RefreshExistingLogins(users.DataObject);
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            // Non-fatal: the account was created; only the local "already exists" cache is stale.
        }
    }

    // ----- user + testdata -----

    private bool CanLoadTestData() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoadTestData))]
    private void LoadTestDataFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Elegí el testdata",
            Filter = "Testdata (*.testdata)|*.testdata|XML (*.xml)|*.xml|Todos (*.*)|*.*",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            TestDataContent = File.ReadAllText(dialog.FileName, Encoding.UTF8).TrimStart('﻿');
            TestDataFileName = Path.GetFileName(dialog.FileName);
            InspectTestData();
        }
        catch (IOException ex)
        {
            ErrorMessage = $"No se pudo leer el archivo: {ex.Message}";
        }
    }

    partial void OnTestDataContentChanged(string value) => InspectTestData();

    /// <summary>Reads moduleID / clientId / loginName out of the loaded testdata.</summary>
    private void InspectTestData()
    {
        TestDataLoginName = null;
        TestDataModuleId = null;
        TestDataClientId = null;

        if (string.IsNullOrWhiteSpace(TestDataContent))
        {
            return;
        }

        if (!TestDataXml.TryParseKey(TestDataContent, out var key) || key is null)
        {
            ErrorMessage = "El testdata no tiene un elemento <Key> con moduleID y loginName válidos.";
            return;
        }

        TestDataLoginName = key.LoginName;
        TestDataModuleId = key.ModuleId;
        TestDataClientId = key.ClientId;
        ErrorMessage = null;

        // Suggest the conventional file name once we know the module.
        if (!string.IsNullOrWhiteSpace(TestDataUsername))
        {
            TestDataFileName = $"{key.ModuleId}_{TestDataUsername.Trim()}.testdata";
        }
    }

    private bool CanCreateWithTestData() =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(TestDataUsername)
        && !Exists(TestDataUsername)
        && !string.IsNullOrWhiteSpace(TestDataContent);

    [RelayCommand(CanExecute = nameof(CanCreateWithTestData))]
    private async Task CreateWithTestDataAsync()
    {
        var username = TestDataUsername.Trim();

        // 1. Align the testdata's loginName with the account we're about to create.
        if (!TestDataXml.TryRewriteLoginName(TestDataContent, username, out var alignedXml, out var rewriteError))
        {
            ErrorMessage = rewriteError;
            return;
        }

        // 2. Create the account.
        var created = await CreateAccountAsync(new CreateUserAccountRequest
        {
            Username = username,
            Password = DefaultPassword,
            MarketTypeId = SelectedMarket?.MarketTypeId ?? DefaultMarketTypeId,
            ServerId = SelectedProduct?.ServerId ?? DefaultServerId,
            UserTypeId = SelectedUserType?.UserTypeId ?? DefaultUserTypeId,
            CurrencyIsoCode = SelectedCurrency?.IsoCode ?? DefaultCurrency,
            Country = SelectedCountry?.Name ?? DefaultCountry,
            NumberOfAccounts = 1,
        });

        if (!created)
        {
            return;
        }

        // 3. Upload the testdata.
        var fileName = string.IsNullOrWhiteSpace(TestDataFileName)
            ? $"{TestDataModuleId}_{username}.testdata"
            : TestDataFileName.Trim();

        IsBusy = true;
        StatusMessage = $"Subiendo {fileName}…";

        try
        {
            using var stream = new MemoryStream(Base64Text.ToUtf8Bytes(alignedXml, withBom: true));
            var result = await _upload.UploadTestDataAsync(stream, fileName);

            if (result.Success)
            {
                TestDataContent = alignedXml;
                StatusMessage = $"✔ Usuario \"{username}\" creado y testdata \"{fileName}\" subido correctamente.";
                // Clear the name so its availability label doesn't flip to "ya existe".
                TestDataUsername = string.Empty;
            }
            else
            {
                ErrorMessage = $"El usuario se creó, pero el testdata falló: {Shorten(result.CustomMessage)}";
            }
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = $"El usuario se creó, pero el testdata falló: {Shorten(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new OpenUsersToolMessage());

    private static string Shorten(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "sin detalle";
        }

        var clean = message.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= 250 ? clean : clean[..250] + "…";
    }
}
