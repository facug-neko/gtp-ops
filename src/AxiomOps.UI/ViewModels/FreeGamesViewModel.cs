using System.Collections.ObjectModel;
using System.Net.Http;
using AxiomOps.Services;
using AxiomOps.Services.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AxiomOps.UI.ViewModels;

/// <summary>Balance the offer's free games pay into. No lookup endpoint exists for this —
/// values confirmed by the user against the Axiom Admin web portal's own Free Games form.</summary>
public sealed record BalanceTypeOption(int Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// FreeGames module: awards a free-games offer to one user, mirroring the Axiom Admin
/// web portal's own "Free Games — Create" form (https://.../FreeGamesCreate) as closely
/// as this API surface allows. POST /FreeGames/FreeGamesOffer; GET /FreeGames/FreeGamesOptions
/// supplies the searchable game list (with each game's valid chip-size/coin choices) and
/// the duration-unit choices.
///
/// Deliberately NOT replicated (confirmed with the user):
///  - "Free Game Templates" selector — no endpoint found for it; the web form's only
///    functional default ("Custom") needs no real UI here.
///  - "Nearest Cost Per Bet" auto-calculation — the web form derives it from a
///    "PaylineCountWithCost" value that isn't exposed by any endpoint this app has;
///    the field is left unset (null) rather than guessed at.
/// </summary>
public partial class FreeGamesViewModel : ObservableObject
{
    private readonly IFreeGamesService _freeGames;
    private readonly IUserAccountsService _userAccounts;
    private readonly AxiomEnvironmentContext _context;
    private readonly IMessenger _messenger;

    private List<UserAccount> _allUsers = [];
    private List<FreeGameSupportedGame> _allGames = [];

    public ObservableCollection<UserAccount> Users { get; } = [];
    public ObservableCollection<FreeGameSupportedGame> Games { get; } = [];
    public ObservableCollection<DurationSetting> DurationTypes { get; } = [];
    public ObservableCollection<string> ChipSizeOptions { get; } = [];
    public ObservableCollection<string> CoinOptions { get; } = [];
    public ObservableCollection<string> Log { get; } = [];

    /// <summary>Confirmed by the user against the Axiom Admin web portal's Free Games form.</summary>
    public IReadOnlyList<BalanceTypeOption> BalanceTypes { get; } =
    [
        new BalanceTypeOption(0, "Cash"),
        new BalanceTypeOption(1, "Bonus"),
    ];

    // ----- Game (single, searchable — matches the web form's "Type to search for games…") -----

    [ObservableProperty]
    private string? _gameFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private FreeGameSupportedGame? _selectedGame;

    // ----- Player (same filter+combo UX as Launch/PlayRepository) -----

    [ObservableProperty]
    private string? _userFilter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private UserAccount? _selectedUser;

    // ----- Offer details -----

    [ObservableProperty]
    private string? _offerName;

    [ObservableProperty]
    private string? _displayLine1;

    [ObservableProperty]
    private string? _displayLine2;

    [ObservableProperty]
    private int _defaultNumberOfGames = 1;

    [ObservableProperty]
    private BalanceTypeOption _selectedBalanceType = new(0, "Cash");

    [ObservableProperty]
    private bool _hasCustomAvailableFrom;

    [ObservableProperty]
    private DateTime? _availableFromDate;

    [ObservableProperty]
    private string _availableFromTime = "00:00";

    /// <summary>Required, like the web form — defaults to a week out.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private DateTime? _expirationDate = DateTime.UtcNow.Date.AddDays(7);

    [ObservableProperty]
    private string _expirationTime = "00:00";

    [ObservableProperty]
    private int _durationLength = 7;

    [ObservableProperty]
    private DurationSetting? _selectedDurationType;

    // ----- Bet model (per selected game) -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string? _selectedChipSize;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private string? _selectedCoinOption;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public string EnvironmentName => _context.EnvironmentName ?? "—";

    public bool HasLog => Log.Count > 0;

    public FreeGamesViewModel(
        IFreeGamesService freeGames,
        IUserAccountsService userAccounts,
        AxiomEnvironmentContext context,
        IMessenger messenger)
    {
        _freeGames = freeGames;
        _userAccounts = userAccounts;
        _context = context;
        _messenger = messenger;
    }

    partial void OnUserFilterChanged(string? value) => ApplyUserFilter();

    partial void OnGameFilterChanged(string? value) => ApplyGameFilter();

    partial void OnSelectedGameChanged(FreeGameSupportedGame? value)
    {
        var limits = value?.ValidBetLimits;

        ChipSizeOptions.Clear();
        foreach (var chip in limits?.ChipSizeList ?? [])
        {
            ChipSizeOptions.Add(chip);
        }
        SelectedChipSize = ChipSizeOptions.FirstOrDefault();

        CoinOptions.Clear();
        foreach (var coin in limits?.CoinList ?? [])
        {
            CoinOptions.Add(coin);
        }
        SelectedCoinOption = CoinOptions.FirstOrDefault();

        OfferName = value is null ? null : $"{DisplayNameOf(value)} - {Guid.NewGuid()}";
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Cargando usuarios y juegos con soporte de Free Games…";

        try
        {
            var usersTask = _userAccounts.GetUserAccountsAsync();
            var optionsTask = _freeGames.GetFreeGamesOptionsAsync();
            await Task.WhenAll(usersTask, optionsTask);

            _allUsers = usersTask.Result.DataObject ?? [];
            ApplyUserFilter();

            var options = optionsTask.Result.DataObject;

            _allGames = [.. (options?.FreeGameSupportedGames ?? []).OrderBy(DisplayNameOf, StringComparer.OrdinalIgnoreCase)];
            ApplyGameFilter();

            DurationTypes.Clear();
            foreach (var duration in options?.DurationSettings ?? [])
            {
                DurationTypes.Add(duration);
            }
            SelectedDurationType = DurationTypes.FirstOrDefault();

            StatusMessage = $"{_allUsers.Count} usuarios, {_allGames.Count} juego(s) con soporte de Free Games.";
        }
        catch (Exception ex) when (ex is AxiomApiException or HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            ErrorMessage = $"No se pudo cargar el ambiente: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSubmit() =>
        !IsBusy
        && SelectedGame is not null
        && SelectedUser is not null
        && ExpirationDate is not null
        && !string.IsNullOrEmpty(SelectedChipSize)
        && !string.IsNullOrEmpty(SelectedCoinOption);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        var game = SelectedGame!;
        var user = SelectedUser!;
        var gameLabel = DisplayNameOf(game);

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = $"Creando oferta de Free Games para {user.LoginName}…";
        Append($"Creando oferta de {gameLabel} para {user.LoginName}…");

        try
        {
            var request = BuildRequest(game, user);
            var result = await _freeGames.CreateFreeGamesOfferAsync(request);

            if (result.Success)
            {
                StatusMessage = $"✔ Oferta creada para {user.LoginName}.";
                Append($"✔ Oferta creada para {user.LoginName} — {gameLabel}.");
            }
            else
            {
                StatusMessage = null;
                var detail = Shorten(result.CustomMessage);
                ErrorMessage = $"El server rechazó la oferta: {detail}";
                Append($"✗ Rechazada: {detail}");
            }
        }
        catch (AxiomApiException ex)
        {
            StatusMessage = null;
            var detail = AxiomErrorText.Describe(ex);
            ErrorMessage = $"Error creando la oferta: {detail}";
            Append($"✗ Error: {detail}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            StatusMessage = null;
            var detail = Shorten(ex.Message);
            ErrorMessage = $"Error creando la oferta: {detail}";
            Append($"✗ Error: {detail}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private CreateFreeGamesOfferRequest BuildRequest(FreeGameSupportedGame game, UserAccount user)
    {
        var games = new List<FreeGameOfferGame>
        {
            new()
            {
                ModuleId = game.ModuleId,
                ClientId = game.ClientId,
                GameName = DisplayNameOf(game),
                NumberOfCoins = long.TryParse(SelectedCoinOption, out var coins) ? coins : null,
                // NearestCostPerBet intentionally left null — the web form derives it from a
                // "PaylineCountWithCost" value this API doesn't expose anywhere we can read.
            },
        };

        var duration = SelectedDurationType is null
            ? null
            : new OfferDuration { Length = DurationLength, TimeUnitId = SelectedDurationType.DurationTypeId };

        return new CreateFreeGamesOfferRequest
        {
            UserId = user.UserId,
            FreeGameOfferByNearestCostRequest = new FreeGameOfferByNearestCostRequest
            {
                DefaultNumberOfGames = DefaultNumberOfGames,
                Games = games,
                IdempotencyId = Guid.NewGuid(),
                Reuse = false,
                BalanceTypeId = SelectedBalanceType.Id,
                DefaultDisplayLine1 = string.IsNullOrWhiteSpace(DisplayLine1) ? null : DisplayLine1.Trim(),
                DefaultDisplayLine2 = string.IsNullOrWhiteSpace(DisplayLine2) ? null : DisplayLine2.Trim(),
                DurationAvailableAfterAwarded = duration,
                OfferAvailableFromUtcDate = HasCustomAvailableFrom ? CombineUtc(AvailableFromDate, AvailableFromTime) : null,
                OfferExpirationUtcDate = CombineUtc(ExpirationDate, ExpirationTime),
                OfferName = string.IsNullOrWhiteSpace(OfferName) ? null : OfferName.Trim(),
            },
        };
    }

    [RelayCommand]
    private void Back() => _messenger.Send(new BackToDashboardMessage());

    private void ApplyUserFilter()
    {
        var filter = UserFilter?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allUsers.Take(200)
            : _allUsers.Where(u => u.LoginName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false).Take(200);

        Users.Clear();
        foreach (var user in filtered)
        {
            Users.Add(user);
        }
    }

    private void ApplyGameFilter()
    {
        var filter = GameFilter?.Trim();

        var filtered = string.IsNullOrEmpty(filter)
            ? _allGames
            : _allGames.Where(g => DisplayNameOf(g).Contains(filter, StringComparison.OrdinalIgnoreCase));

        Games.Clear();
        foreach (var game in filtered)
        {
            Games.Add(game);
        }
    }

    private void Append(string line)
    {
        Log.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        OnPropertyChanged(nameof(HasLog));
    }

    /// <summary>
    /// Combines a date (year/month/day only) with a manually-entered "HH:mm" as an
    /// explicit UTC instant — the web form's own note is "all times are displayed in
    /// UTC and consumed in UTC", so this deliberately does NOT convert from local time.
    /// </summary>
    private static DateTimeOffset? CombineUtc(DateTime? date, string? time)
    {
        if (date is null)
        {
            return null;
        }

        var offset = TimeSpan.TryParse(time, out var parsed) ? parsed : TimeSpan.Zero;
        return new DateTimeOffset(date.Value.Date + offset, TimeSpan.Zero);
    }

    private static string DisplayNameOf(FreeGameSupportedGame game) =>
        game.DisplayName ?? game.FriendlyName ?? game.InternalName ?? $"{game.ModuleId}/{game.ClientId}";

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
