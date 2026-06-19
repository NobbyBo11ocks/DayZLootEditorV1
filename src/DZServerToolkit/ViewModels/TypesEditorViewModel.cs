using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Linq;
using Avalonia.Media;
using DZServerToolkit.Models;
using DZServerToolkit.Services;

namespace DZServerToolkit.ViewModels;

public sealed class TypesEditorViewModel : ObservableObject
{
    private const string AllCategories = "All categories";
    private const string LootEditorFeature = "Loot";
    private const string CustomCeFeature = "Custom CE Files";
    private const string InfoFeature = "Info";
    private const string EmptyPreview = "Generate a save diff preview to inspect XML changes before writing the file.";

    private readonly IFileDialogService _fileDialogService;
    private readonly ITypesXmlService _typesXmlService;
    private readonly IValidationService _validationService;
    private readonly IBackupService _backupService;
    private readonly ICustomCeService _customCeService;
    private readonly ILootProfileService _lootProfileService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly ITextDiffService _textDiffService;

    private readonly Stack<EditorSnapshot> _undoStack = new();
    private readonly Stack<EditorSnapshot> _redoStack = new();

    private string _activeFeature = LootEditorFeature;
    private string _searchText = string.Empty;
    private string _selectedCategory = AllCategories;
    private bool _showOnlyIssues;
    private DayzTypeEntry? _selectedEntry;
    private string _filePath = string.Empty;
    private string _missionFolder = string.Empty;
    private string _statusMessage = "Step 1: open your DayZ mission folder.";
    private bool _isBusy;
    private bool _autoBackup = true;
    private bool _hasCompletedFirstMissionLoad;
    private decimal _bulkScalePercent = 100m;
    private int _errorCount;
    private int _infoCount;
    private bool _hasWorkingFile;
    private string _validationSummary = "Open a mission folder or types.xml to begin.";
    private CustomCeFileEntry? _selectedCustomCeFile;
    private string _newCeFolderName = "modtypes";
    private string _newCeFileName = "types_custom.xml";
    private string _newCeFileType = "types";
    private string _customCeStatus = "Open a mission folder to manage custom CE files.";
    private string _customCePreviewSummary = "Choose a template or enter a folder/file/type to preview what will be written.";
    private int _customCeErrorCount;
    private int _customCeReadyCount;
    private int _customCeInfoCount;
    private bool _deleteCustomCeFileOnUnregister;
    private string _customCeSearchText = string.Empty;
    private string _selectedCustomCeFilter = "All";
    private LootProfileTemplate? _selectedProfileTemplate;
    private string _selectedRecentTypesFile = string.Empty;
    private string _selectedRecentMissionFolder = string.Empty;
    private string _savePreviewSummary = EmptyPreview;
    private string _customCeRepairDiffSummary = "Select a custom CE file to preview root-fix changes before running repair.";
    private string _customCeConflictSummary = "Refresh custom CE validation to detect duplicate registrations and path/type conflicts.";
    private XDocument? _loadedDocument;
    private CancellationTokenSource? _validationDebounceCts;
    private CancellationTokenSource? _filterDebounceCts;
    private CancellationTokenSource? _customCeDiffCts;
    private long _customCeDiffRequestVersion;
    private bool _suppressHistory;
    private bool _suspendEntryReactiveWork;
    private bool _historyDirty;
    private EditorSnapshot? _currentSnapshot;

    public TypesEditorViewModel(
        IFileDialogService fileDialogService,
        ITypesXmlService typesXmlService,
        IValidationService validationService,
        IBackupService backupService,
        ICustomCeService customCeService,
        ILootProfileService lootProfileService,
        IRecentFilesService recentFilesService,
        ITextDiffService textDiffService)
    {
        _fileDialogService = fileDialogService;
        _typesXmlService = typesXmlService;
        _validationService = validationService;
        _backupService = backupService;
        _customCeService = customCeService;
        _lootProfileService = lootProfileService;
        _recentFilesService = recentFilesService;
        _textDiffService = textDiffService;

        foreach (var type in _customCeService.SupportedTypes)
        {
            CustomCeTypeOptions.Add(type);
        }

        foreach (var template in _lootProfileService.GetTemplates())
        {
            ProfileTemplates.Add(template);
        }

        foreach (var filter in new[] { "All", "Ready", "Broken", "Missing", "Duplicate" })
        {
            CustomCeFilterOptions.Add(filter);
        }

        SelectedCustomCeFilter = CustomCeFilterOptions.FirstOrDefault() ?? "All";
        _hasCompletedFirstMissionLoad = _recentFilesService.GetHasCompletedFirstMissionLoad();
        ActiveFeature = LootEditorFeature;

        OpenTypesFileCommand = new AsyncRelayCommand(OpenTypesFileAsync, () => !IsBusy, HandleUnhandledError);
        OpenMissionFolderCommand = new AsyncRelayCommand(OpenMissionFolderAsync, () => !IsBusy, HandleUnhandledError);
        UnloadLoadedFileCommand = new AsyncRelayCommand(UnloadLoadedFileAsync, () => !IsBusy && (HasWorkingFile || HasMissionFolder), HandleUnhandledError);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && Entries.Count > 0, HandleUnhandledError);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => !IsBusy && Entries.Count > 0, HandleUnhandledError);
        ValidateCommand = new RelayCommand(() => Validate(updateStatusMessage: true), () => !IsBusy);
        AddEntryCommand = new RelayCommand(AddEntry, () => !IsBusy && HasWorkingFile);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => !IsBusy && SelectedEntry is not null);
        ScaleVisibleCommand = new RelayCommand(ScaleVisibleRows, () => !IsBusy && FilteredEntries.Count > 0);
        ClearFiltersCommand = new RelayCommand(ClearFilters, () => !IsBusy);
        ClearLootSearchCommand = new RelayCommand(ClearLootSearch, () => !IsBusy && HasSearchFilter);
        ClearLootCategoryFilterCommand = new RelayCommand(ClearLootCategoryFilter, () => !IsBusy && HasCategoryFilter);
        ClearLootIssueFilterCommand = new RelayCommand(ClearLootIssueFilter, () => !IsBusy && HasIssueFilter);
        ApplyProfileTemplateCommand = new RelayCommand(ApplyProfileTemplate, () => !IsBusy && FilteredEntries.Count > 0 && SelectedProfileTemplate is not null);
        UndoCommand = new RelayCommand(Undo, () => !IsBusy && _undoStack.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => !IsBusy && _redoStack.Count > 0);
        GenerateSavePreviewCommand = new AsyncRelayCommand(GenerateSavePreviewAsync, () => !IsBusy && Entries.Count > 0, HandleUnhandledError);
        OpenSelectedRecentTypesFileCommand = new AsyncRelayCommand(OpenSelectedRecentTypesFileAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedRecentTypesFile), HandleUnhandledError);
        OpenSelectedRecentMissionFolderCommand = new AsyncRelayCommand(OpenSelectedRecentMissionFolderAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedRecentMissionFolder), HandleUnhandledError);

        ShowLootEditorCommand = new RelayCommand(() => ActiveFeature = LootEditorFeature, () => !IsBusy);
        ShowCustomCeCommand = new RelayCommand(() => ActiveFeature = CustomCeFeature, () => !IsBusy);
        ShowInfoCommand = new RelayCommand(() => ActiveFeature = InfoFeature, () => !IsBusy);
        AddCustomCeFileCommand = new AsyncRelayCommand(AddCustomCeFileAsync, () => !IsBusy && HasMissionFolder, HandleUnhandledError);
        ClearAllCustomCeFiltersCommand = new RelayCommand(ClearAllCustomCeFilters, () => !IsBusy && HasActiveCustomCeFilterSummary);
        OpenSelectedCustomTypesCommand = new AsyncRelayCommand(OpenSelectedCustomTypesAsync, () => !IsBusy && SelectedCustomCeFile is not null, HandleUnhandledError);
        UnregisterSelectedCustomCeCommand = new AsyncRelayCommand(UnregisterSelectedCustomCeAsync, () => !IsBusy && SelectedCustomCeFile is not null, HandleUnhandledError);
        DeleteSelectedCustomCeCommand = new AsyncRelayCommand(DeleteSelectedCustomCeAsync, () => !IsBusy && SelectedCustomCeFile is not null, HandleUnhandledError);
        RepairSelectedCustomCeCommand = new AsyncRelayCommand(RepairSelectedCustomCeAsync, () => !IsBusy && SelectedCustomCeFile is not null, HandleUnhandledError);
        UseTypesPresetCommand = new RelayCommand(UseTypesPreset, () => !IsBusy);
        UseSpawnablePresetCommand = new RelayCommand(UseSpawnablePreset, () => !IsBusy);
        UseEventsPresetCommand = new RelayCommand(UseEventsPreset, () => !IsBusy);
        UseGlobalsPresetCommand = new RelayCommand(UseGlobalsPreset, () => !IsBusy);
        ResetCustomCeFormCommand = new RelayCommand(ResetCustomCeForm, () => !IsBusy);
        RefreshSelectedCustomCeDiffCommand = new AsyncRelayCommand(RefreshSelectedCustomCeDiffAsync, () => !IsBusy && SelectedCustomCeFile is not null, HandleUnhandledError);

        SelectedProfileTemplate = ProfileTemplates.FirstOrDefault();
        RefreshCustomCePreview();
        RefreshRecentCollections();
        CaptureSnapshot(clearHistory: true);
    }

    public async Task TryAutoDetectAndOpenDayzServerAsync()
    {
        if (HasWorkingFile || HasMissionFolder || IsBusy)
        {
            return;
        }

        try
        {
            var autoDetectedMissionFolder = TryFindMissionFolderFromAppDirectory(AppContext.BaseDirectory);
            if (string.IsNullOrWhiteSpace(autoDetectedMissionFolder))
            {
                return;
            }

            await OpenMissionFolderPathAsync(autoDetectedMissionFolder).ConfigureAwait(true);

            if (HasWorkingFile)
            {
                StatusMessage = $"Auto-detected DayZ mission: {Path.GetFileName(autoDetectedMissionFolder)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"DayZ auto-detect skipped: {ex.Message}";
        }
    }

    public ObservableCollection<DayzTypeEntry> Entries { get; } = new();
    public ObservableCollection<DayzTypeEntry> FilteredEntries { get; } = new();
    public ObservableCollection<ValidationIssue> ValidationIssues { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { AllCategories };
    public ObservableCollection<CustomCeFileEntry> CustomCeFiles { get; } = new();
    public ObservableCollection<CustomCeFileEntry> FilteredCustomCeFiles { get; } = new();
    public ObservableCollection<ValidationIssue> CustomCeIssues { get; } = new();
    public bool HasCustomCeIssues => CustomCeIssues.Count > 0;
    public bool IsCustomCeIssuesEmpty => !HasCustomCeIssues;
    public ObservableCollection<string> CustomCeTypeOptions { get; } = new();
    public ObservableCollection<string> CustomCeFilterOptions { get; } = new();
    public ObservableCollection<LootProfileTemplate> ProfileTemplates { get; } = new();
    public ObservableCollection<string> RecentTypesFiles { get; } = new();
    public ObservableCollection<string> RecentMissionFolders { get; } = new();
    public ObservableCollection<SaveDiffSection> SavePreviewSections { get; } = new();
    public ObservableCollection<SaveDiffSection> CustomCeRepairDiffSections { get; } = new();
    public ObservableCollection<string> CustomCeConflictItems { get; } = new();

    public AsyncRelayCommand OpenTypesFileCommand { get; }
    public AsyncRelayCommand OpenMissionFolderCommand { get; }
    public AsyncRelayCommand UnloadLoadedFileCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand AddEntryCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand ScaleVisibleCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ClearLootSearchCommand { get; }
    public RelayCommand ClearLootCategoryFilterCommand { get; }
    public RelayCommand ClearLootIssueFilterCommand { get; }
    public RelayCommand ApplyProfileTemplateCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public AsyncRelayCommand GenerateSavePreviewCommand { get; }
    public AsyncRelayCommand OpenSelectedRecentTypesFileCommand { get; }
    public AsyncRelayCommand OpenSelectedRecentMissionFolderCommand { get; }
    public RelayCommand ShowLootEditorCommand { get; }
    public RelayCommand ShowCustomCeCommand { get; }
    public RelayCommand ShowInfoCommand { get; }
    public AsyncRelayCommand AddCustomCeFileCommand { get; }
    public RelayCommand ClearAllCustomCeFiltersCommand { get; }
    public AsyncRelayCommand OpenSelectedCustomTypesCommand { get; }
    public AsyncRelayCommand UnregisterSelectedCustomCeCommand { get; }
    public AsyncRelayCommand DeleteSelectedCustomCeCommand { get; }
    public AsyncRelayCommand RepairSelectedCustomCeCommand { get; }
    public RelayCommand UseTypesPresetCommand { get; }
    public RelayCommand UseSpawnablePresetCommand { get; }
    public RelayCommand UseEventsPresetCommand { get; }
    public RelayCommand UseGlobalsPresetCommand { get; }
    public RelayCommand ResetCustomCeFormCommand { get; }
    public AsyncRelayCommand RefreshSelectedCustomCeDiffCommand { get; }

    public string ActiveFeature
    {
        get => _activeFeature;
        set
        {
            if (SetProperty(ref _activeFeature, NormalizeWorkspace(value)))
            {
                OnPropertyChanged(nameof(IsLootEditorVisible));
                OnPropertyChanged(nameof(IsCustomCeVisible));
                OnPropertyChanged(nameof(IsInfoVisible));
                _ = _recentFilesService.SetLastWorkspaceAsync(_activeFeature);
            }
        }
    }

    public bool IsLootEditorVisible => string.Equals(ActiveFeature, LootEditorFeature, StringComparison.OrdinalIgnoreCase);
    public bool IsCustomCeVisible => string.Equals(ActiveFeature, CustomCeFeature, StringComparison.OrdinalIgnoreCase);
    public bool IsInfoVisible => string.Equals(ActiveFeature, InfoFeature, StringComparison.OrdinalIgnoreCase);

    public bool HasCompletedFirstMissionLoad
    {
        get => _hasCompletedFirstMissionLoad;
        private set
        {
            if (SetProperty(ref _hasCompletedFirstMissionLoad, value))
            {
                OnPropertyChanged(nameof(ShowFirstRunWelcome));
            }
        }
    }

    public bool ShowFirstRunWelcome => !HasCompletedFirstMissionLoad;

    public bool HasSearchFilter => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasCategoryFilter => !string.Equals(SelectedCategory, AllCategories, StringComparison.OrdinalIgnoreCase);
    public bool HasIssueFilter => ShowOnlyIssues;
    public bool HasActiveLootFilterSummary => HasSearchFilter || HasCategoryFilter || HasIssueFilter;
    public string SearchFilterChipText => $"Search: {SearchText.Trim()}";
    public string CategoryFilterChipText => $"Category: {SelectedCategory}";
    public string IssueFilterChipText => "Issues only";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                QueueLootFilterRefresh();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value ?? AllCategories))
            {
                ApplyFilters();
            }
        }
    }

    public bool ShowOnlyIssues
    {
        get => _showOnlyIssues;
        set
        {
            if (SetProperty(ref _showOnlyIssues, value))
            {
                ApplyFilters();
            }
        }
    }

    public DayzTypeEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                NotifyLootSelectionStateChanged();
                UnloadLoadedFileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedEntry => SelectedEntry is not null;

    public bool IsEntrySelectionEmpty => SelectedEntry is null;

    public string FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FileDisplayName));
                OnPropertyChanged(nameof(HasWorkingFile));
                OnPropertyChanged(nameof(LoadedMode));
                OnPropertyChanged(nameof(HasLootRowCountSummary));
                OnPropertyChanged(nameof(LootRowCountSummaryText));
                OnPropertyChanged(nameof(ShowLootOnboarding));
                OnPropertyChanged(nameof(ShowLootNoResults));
                NotifyLootSelectionStateChanged();
            }
        }
    }

    public string MissionFolder
    {
        get => _missionFolder;
        private set
        {
            if (SetProperty(ref _missionFolder, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasMissionFolder));
                OnPropertyChanged(nameof(MissionDisplayName));
                OnPropertyChanged(nameof(HasCustomCeRowCountSummary));
                OnPropertyChanged(nameof(CustomCeRowCountSummaryText));
                NotifyCustomCeSelectionStateChanged();
                RefreshCustomCePreview();
                AddCustomCeFileCommand.NotifyCanExecuteChanged();
                UnloadLoadedFileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasMissionFolder => !string.IsNullOrWhiteSpace(MissionFolder) && Directory.Exists(MissionFolder);
    public string MissionDisplayName => HasMissionFolder ? Path.GetFileName(MissionFolder) : "No mission folder opened";

    private void SetWorkingFileState(bool hasWorkingFile)
    {
        if (_hasWorkingFile == hasWorkingFile)
        {
            return;
        }

        _hasWorkingFile = hasWorkingFile;
        OnPropertyChanged(nameof(HasWorkingFile));
        OnPropertyChanged(nameof(ShowLootOnboarding));
        OnPropertyChanged(nameof(ShowLootNoResults));
        OnPropertyChanged(nameof(ShowLootSelectionHint));
        OnPropertyChanged(nameof(HasLootRowCountSummary));
        OnPropertyChanged(nameof(LootRowCountSummaryText));
        OnPropertyChanged(nameof(LoadedMode));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value ?? string.Empty);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public bool AutoBackup
    {
        get => _autoBackup;
        set => SetProperty(ref _autoBackup, value);
    }

    public decimal BulkScalePercent
    {
        get => _bulkScalePercent;
        set => SetProperty(ref _bulkScalePercent, value <= 0 ? 1 : value);
    }

    public string ProfileTemplateLabel => "Balance Preset";
    public string ProfileTemplateActionLabel => "Apply Preset";
    public string ProfileTemplateHelpSummary => "Applies a ready-made balance style to the rows you can currently see.";
    public string ProfileTemplateHelpDetails => "Use this when you want faster, broader balancing than editing every item by hand. Presets look at each visible row and adjust fields like Nominal, Min, and sometimes Restock based on item category, tags, and value tiers. Hidden rows outside your current search or filters are not changed.";

    public string SavePreviewHelpLabel => "Save Preview";
    public string SavePreviewHelpSummary => "Shows what will change in the XML before you save the file.";
    public string SavePreviewHelpDetails => "Use Refresh Preview to compare the file on disk with what your current session would write right now. It helps you spot accidental edits before saving. It does not save the file by itself.";

    public int ErrorCount
    {
        get => _errorCount;
        private set
        {
            if (SetProperty(ref _errorCount, value))
            {
                OnPropertyChanged(nameof(FooterValidationText));
                OnPropertyChanged(nameof(FooterValidationBrush));
            }
        }
    }

    public int InfoCount
    {
        get => _infoCount;
        private set => SetProperty(ref _infoCount, value);
    }

    public int TotalEntries => Entries.Count;
    public int FilteredCount => FilteredEntries.Count;
    public int DirtyCount => Entries.Count(entry => entry.IsDirty);
    public bool HasUnsavedChanges => DirtyCount > 0;
    public bool HasWorkingFile => _hasWorkingFile && !string.IsNullOrWhiteSpace(FilePath);
    public bool ShowLootOnboarding => !HasWorkingFile;
    public bool ShowLootNoResults => HasWorkingFile && FilteredEntries.Count == 0;
    public bool ShowLootSelectionHint => HasWorkingFile && FilteredEntries.Count > 0 && IsEntrySelectionEmpty;
    public string FileDisplayName => string.IsNullOrWhiteSpace(FilePath) ? "No types.xml loaded" : Path.GetFileName(FilePath);
    public bool HasLootRowCountSummary => HasWorkingFile && TotalEntries > 0;
    public string LootRowCountSummaryText => FilteredCount == TotalEntries
        ? $"Showing all {TotalEntries:N0} row(s)"
        : $"Showing {FilteredCount:N0} of {TotalEntries:N0} row(s)";
    public string LoadedMode => string.IsNullOrWhiteSpace(FilePath) ? "Open a mission folder first" : "Live file mode - backup enabled by default";

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value ?? string.Empty);
    }

    public string FooterValidationText => ErrorCount == 1
        ? "Validation: 1 error"
        : $"Validation: {ErrorCount:N0} errors";

    public IBrush FooterValidationBrush => ErrorCount > 0
        ? Brushes.IndianRed
        : Brushes.LimeGreen;

    public CustomCeFileEntry? SelectedCustomCeFile
    {
        get => _selectedCustomCeFile;
        set
        {
            if (SetProperty(ref _selectedCustomCeFile, value))
            {
                OpenSelectedCustomTypesCommand.NotifyCanExecuteChanged();
                UnregisterSelectedCustomCeCommand.NotifyCanExecuteChanged();
                DeleteSelectedCustomCeCommand.NotifyCanExecuteChanged();
                RepairSelectedCustomCeCommand.NotifyCanExecuteChanged();
                RefreshSelectedCustomCeDiffCommand.NotifyCanExecuteChanged();
                NotifyCustomCeSelectionStateChanged();
                _ = RefreshSelectedCustomCeDiffAsync();
            }
        }
    }

    public bool HasSelectedCustomCeFile => SelectedCustomCeFile is not null;

    public bool IsCustomCeSelectionEmpty => !HasSelectedCustomCeFile;

    public string NewCeFolderName
    {
        get => _newCeFolderName;
        set
        {
            if (SetProperty(ref _newCeFolderName, value ?? string.Empty))
            {
                RefreshCustomCePreview();
            }
        }
    }

    public string NewCeFileName
    {
        get => _newCeFileName;
        set
        {
            if (SetProperty(ref _newCeFileName, value ?? string.Empty))
            {
                RefreshCustomCePreview();
            }
        }
    }

    public string NewCeFileType
    {
        get => _newCeFileType;
        set
        {
            if (SetProperty(ref _newCeFileType, string.IsNullOrWhiteSpace(value) ? "types" : value))
            {
                RefreshCustomCePreview();
            }
        }
    }

    public string CustomCeStatus
    {
        get => _customCeStatus;
        private set => SetProperty(ref _customCeStatus, value ?? string.Empty);
    }

    public int CustomCeErrorCount
    {
        get => _customCeErrorCount;
        private set => SetProperty(ref _customCeErrorCount, value);
    }

    public int CustomCeReadyCount
    {
        get => _customCeReadyCount;
        private set => SetProperty(ref _customCeReadyCount, value);
    }

    public int CustomCeInfoCount
    {
        get => _customCeInfoCount;
        private set => SetProperty(ref _customCeInfoCount, value);
    }

    public string CustomCePreviewSummary
    {
        get => _customCePreviewSummary;
        private set => SetProperty(ref _customCePreviewSummary, value ?? string.Empty);
    }

    public bool DeleteCustomCeFileOnUnregister
    {
        get => _deleteCustomCeFileOnUnregister;
        set => SetProperty(ref _deleteCustomCeFileOnUnregister, value);
    }

    public string CustomCeSearchText
    {
        get => _customCeSearchText;
        set
        {
            if (SetProperty(ref _customCeSearchText, value ?? string.Empty))
            {
                ApplyCustomCeFilter();
            }
        }
    }

    public string SelectedCustomCeFilter
    {
        get => _selectedCustomCeFilter;
        set
        {
            if (SetProperty(ref _selectedCustomCeFilter, string.IsNullOrWhiteSpace(value) ? "All" : value))
            {
                ApplyCustomCeFilter();
            }
        }
    }

    public int CustomCeFileCount => CustomCeFiles.Count;
    public int FilteredCustomCeFileCount => FilteredCustomCeFiles.Count;
    public bool HasCustomCeRowCountSummary => HasMissionFolder && CustomCeFileCount > 0;
    public string CustomCeRowCountSummaryText => FilteredCustomCeFileCount == CustomCeFileCount
        ? $"Showing all {CustomCeFileCount:N0} file(s)"
        : $"Showing {FilteredCustomCeFileCount:N0} of {CustomCeFileCount:N0} file(s)";
    public bool ShowCustomCeOnboarding => !HasMissionFolder && CustomCeFileCount == 0;
    public bool ShowCustomCeEmptyState => HasMissionFolder && CustomCeFileCount == 0;
    public bool ShowCustomCeNoResults => HasMissionFolder && CustomCeFileCount > 0 && FilteredCustomCeFileCount == 0;
    public bool HasCustomCeFiles => FilteredCustomCeFileCount > 0;
    public bool IsCustomCeEmpty => !HasCustomCeFiles;
    public bool ShowCustomCeSelectionHint => HasMissionFolder && HasCustomCeFiles && IsCustomCeSelectionEmpty;
    public bool HasCustomCeSearchFilter => !string.IsNullOrWhiteSpace(CustomCeSearchText);
    public bool HasCustomCeStateFilter => !string.Equals(SelectedCustomCeFilter, "All", StringComparison.OrdinalIgnoreCase);
    public bool HasActiveCustomCeFilterSummary => HasCustomCeSearchFilter || HasCustomCeStateFilter;
    public string CustomCeSearchFilterChipText => $"Search: {CustomCeSearchText.Trim()}";
    public string CustomCeStateFilterChipText => $"Health: {SelectedCustomCeFilter}";

    public string SelectedCustomCeNextAction
    {
        get
        {
            if (SelectedCustomCeFile is null)
            {
                return "Next: select a registered CE row.";
            }

            if (!string.Equals(SelectedCustomCeFile.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                return "Next: preview the repair diff first, then repair or unregister if the registration is stale.";
            }

            return "Next: open the file for review, or refresh validation if you changed registrations on disk.";
        }
    }

    public string CustomCeRepairDiffSummary
    {
        get => _customCeRepairDiffSummary;
        private set => SetProperty(ref _customCeRepairDiffSummary, value ?? string.Empty);
    }

    public bool HasCustomCeRepairDiff => CustomCeRepairDiffSections.Count > 0;

    public bool IsCustomCeRepairDiffEmpty => !HasCustomCeRepairDiff;

    public string CustomCeConflictSummary
    {
        get => _customCeConflictSummary;
        private set => SetProperty(ref _customCeConflictSummary, value ?? string.Empty);
    }

    public bool HasCustomCeConflicts => CustomCeConflictItems.Count > 0;

    public bool IsCustomCeConflictsEmpty => !HasCustomCeConflicts;

    public LootProfileTemplate? SelectedProfileTemplate
    {
        get => _selectedProfileTemplate;
        set
        {
            if (SetProperty(ref _selectedProfileTemplate, value))
            {
                ApplyProfileTemplateCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedRecentTypesFile
    {
        get => _selectedRecentTypesFile;
        set
        {
            if (SetProperty(ref _selectedRecentTypesFile, value ?? string.Empty))
            {
                OpenSelectedRecentTypesFileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedRecentMissionFolder
    {
        get => _selectedRecentMissionFolder;
        set
        {
            if (SetProperty(ref _selectedRecentMissionFolder, value ?? string.Empty))
            {
                OpenSelectedRecentMissionFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SavePreviewSummary
    {
        get => _savePreviewSummary;
        private set => SetProperty(ref _savePreviewSummary, value ?? EmptyPreview);
    }

    public bool HasSavePreviewSections => SavePreviewSections.Count > 0;
    public bool IsSavePreviewEmpty => !HasSavePreviewSections;

    private void ResetSavePreview(string summary = EmptyPreview)
    {
        SavePreviewSummary = summary;
        SavePreviewSections.Clear();
        OnPropertyChanged(nameof(HasSavePreviewSections));
        OnPropertyChanged(nameof(IsSavePreviewEmpty));
    }

    private void ApplySavePreview(SaveDiffPreview preview, string fallbackText)
    {
        SavePreviewSummary = string.IsNullOrWhiteSpace(preview.Summary) ? EmptyPreview : preview.Summary;
        ReplaceCollection(SavePreviewSections, preview.Sections);
        OnPropertyChanged(nameof(HasSavePreviewSections));
        OnPropertyChanged(nameof(IsSavePreviewEmpty));
    }

    private async Task<bool> ConfirmReplaceCurrentSessionAsync(string actionName)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        var shouldDiscard = await _fileDialogService.ConfirmDiscardChangesAsync(
            actionName,
            "You have unsaved changes in the loaded loot file. Continue and discard those changes?").ConfigureAwait(true);

        if (!shouldDiscard)
        {
            StatusMessage = $"{actionName} cancelled. The current loot file is still loaded.";
            return false;
        }

        return true;
    }

    private async Task OpenTypesFileAsync()
    {
        try
        {
            var path = await _fileDialogService.PickTypesFileAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(path) &&
                await ConfirmReplaceCurrentSessionAsync("Open File").ConfigureAwait(true))
            {
                await LoadFileAsync(path).ConfigureAwait(true);
                ActiveFeature = LootEditorFeature;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Open file failed: {ex.Message}";
        }
    }

    private async Task OpenMissionFolderAsync()
    {
        try
        {
            var folder = await _fileDialogService.PickMissionFolderAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            if (!await ConfirmReplaceCurrentSessionAsync("Open Mission Folder").ConfigureAwait(true))
            {
                return;
            }

            await OpenMissionFolderPathAsync(folder).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Open mission folder failed: {ex.Message}";
        }
    }

    private async Task OpenSelectedRecentTypesFileAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentTypesFile))
        {
            return;
        }

        if (!await ConfirmReplaceCurrentSessionAsync("Open Recent File").ConfigureAwait(true))
        {
            return;
        }

        await LoadFileAsync(SelectedRecentTypesFile).ConfigureAwait(true);
        ActiveFeature = LootEditorFeature;
    }

    private async Task OpenSelectedRecentMissionFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentMissionFolder))
        {
            return;
        }

        if (!await ConfirmReplaceCurrentSessionAsync("Open Recent Mission Folder").ConfigureAwait(true))
        {
            return;
        }

        await OpenMissionFolderPathAsync(SelectedRecentMissionFolder).ConfigureAwait(true);
    }

    private async Task OpenMissionFolderPathAsync(string folder)
    {
        var resolvedMissionFolder = ResolveMissionFolderSelection(folder);

        MissionFolder = resolvedMissionFolder;
        await _recentFilesService.AddRecentMissionFolderAsync(resolvedMissionFolder).ConfigureAwait(true);
        RefreshRecentCollections();
        await RefreshCustomCeAsync().ConfigureAwait(true);

        var typesPath = GetTypesPathForMission(resolvedMissionFolder);
        if (typesPath is null)
        {
            ClearLoadedFileState(
                resolvedMissionFolder,
                "Mission folder opened, but no types.xml was found in db or the mission root. Use Open XML to choose it manually.");
            return;
        }

        await LoadFileAsync(typesPath, resolvedMissionFolder).ConfigureAwait(true);
        if (HasMissionFolder && Entries.Count > 0)
        {
            await MarkFirstMissionLoadCompleteAsync().ConfigureAwait(true);
        }
    }

    private async Task MarkFirstMissionLoadCompleteAsync()
    {
        if (HasCompletedFirstMissionLoad)
        {
            return;
        }

        HasCompletedFirstMissionLoad = true;
        await _recentFilesService.SetHasCompletedFirstMissionLoadAsync(true).ConfigureAwait(true);
    }

    private async Task LoadFileAsync(string path, string? preferredMissionFolder = null)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading DayZ Central Economy data...";

            var result = await _typesXmlService.LoadAsync(path).ConfigureAwait(true);
            var missionFolder = ResolveMissionFolder(result.Path, preferredMissionFolder ?? MissionFolder);
            ReplaceEntries(result.Entries, result.SourceDocument, result.Path, missionFolder, $"Loaded {result.Entries.Count:N0} loot entries from {Path.GetFileName(result.Path)}.");
            SetWorkingFileState(true);
            FilePath = result.Path;
            MissionFolder = missionFolder;

            await _recentFilesService.AddRecentTypesFileAsync(result.Path).ConfigureAwait(true);
            if (HasMissionFolder)
            {
                await _recentFilesService.AddRecentMissionFolderAsync(MissionFolder).ConfigureAwait(true);
                await RefreshCustomCeAsync().ConfigureAwait(true);
            }

            RefreshRecentCollections();
            CaptureSnapshot(clearHistory: true);
        }
        catch (Exception ex)
        {
            if (HasWorkingFile)
            {
                StatusMessage = $"Load failed: {ex.Message}. The current loot file is still loaded.";
                return;
            }

            var fallbackMissionFolder = ResolveMissionFolder(path, preferredMissionFolder ?? MissionFolder);
            ClearLoadedFileState(fallbackMissionFolder, $"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    private async Task UnloadLoadedFileAsync()
    {
        if (HasUnsavedChanges)
        {
            var shouldDiscard = await _fileDialogService.ConfirmDiscardChangesAsync(
                "Unload File",
                "You have unsaved changes in the loaded loot file. Unload it and discard those changes?").ConfigureAwait(true);

            if (!shouldDiscard)
            {
                StatusMessage = "Unload cancelled. The current loot file is still loaded.";
                return;
            }
        }

        var missionFolder = HasMissionFolder ? MissionFolder : string.Empty;
        var statusMessage = HasMissionFolder
            ? "Loaded loot file unloaded. Mission session is still open."
            : "Loaded loot file unloaded.";

        ClearLoadedFileState(missionFolder, statusMessage);
        await RefreshCustomCeAsync().ConfigureAwait(true);
    }

    public async Task<bool> SaveBeforeCloseAsync()
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        await SaveAsync().ConfigureAwait(true);
        return !HasUnsavedChanges;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            await SaveAsAsync().ConfigureAwait(true);
            return;
        }

        await SaveToPathAsync(FilePath).ConfigureAwait(true);
    }

    private async Task SaveAsAsync()
    {
        try
        {
            var suggested = string.IsNullOrWhiteSpace(FilePath) ? "types.xml" : Path.GetFileName(FilePath);
            var path = await _fileDialogService.PickSaveTypesPathAsync(suggested).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(path))
            {
                await SaveToPathAsync(path).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save As failed: {ex.Message}";
        }
    }

    private async Task SaveToPathAsync(string path)
    {
        try
        {
            IsBusy = true;
            Validate();

            if (ErrorCount > 0)
            {
                StatusMessage = $"Save blocked: fix {ErrorCount:N0} problem(s) first.";
                return;
            }

            string? backupPath = null;
            string? backupFailureMessage = null;
            if (AutoBackup && File.Exists(path))
            {
                try
                {
                    backupPath = await _backupService.CreateBackupAsync(path).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    backupFailureMessage = ex.Message;
                }
            }

            await _typesXmlService.SaveAsync(path, Entries, _loadedDocument).ConfigureAwait(true);

            FilePath = path;
            SetWorkingFileState(true);

            var resolvedMissionFolder = ResolveMissionFolder(path, MissionFolder);
            MissionFolder = resolvedMissionFolder;
            _loadedDocument = await ReloadSavedDocumentAsync(path).ConfigureAwait(true);
            OnPropertyChanged(nameof(LoadedMode));
            OnPropertyChanged(nameof(DirtyCount));
            OnPropertyChanged(nameof(HasUnsavedChanges));

            await _recentFilesService.AddRecentTypesFileAsync(path).ConfigureAwait(true);
            if (HasMissionFolder)
            {
                await _recentFilesService.AddRecentMissionFolderAsync(MissionFolder).ConfigureAwait(true);
            }

            RefreshRecentCollections();
            CaptureSnapshot(clearHistory: true);
            GenerateSavePreview();

            var saveMessage = backupPath is null
                ? $"Saved {Entries.Count:N0} entries to {Path.GetFileName(path)}."
                : $"Saved {Entries.Count:N0} entries. Backup created: {Path.GetFileName(backupPath)}.";

            StatusMessage = string.IsNullOrWhiteSpace(backupFailureMessage)
                ? saveMessage
                : $"{saveMessage} Backup failed: {backupFailureMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RefreshCommandStates();
        }
    }

    private void Validate(bool updateStatusMessage = false)
    {
        ValidationIssues.Clear();

        if (!_hasWorkingFile && Entries.Count == 0)
        {
            ErrorCount = 0;
            InfoCount = 0;
            ValidationSummary = "No file loaded yet. Open a mission folder or types.xml first.";
            if (updateStatusMessage)
            {
                StatusMessage = "Open a mission folder or types.xml to begin.";
            }

            ApplyFilters();
            return;
        }

        var issues = _validationService.ValidateTypes(Entries.ToList());
        foreach (var issue in issues)
        {
            ValidationIssues.Add(issue);
        }

        ErrorCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var warningCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning);
        InfoCount = issues.Count(issue => issue.Severity == ValidationSeverity.Info);
        ApplyFilters();

        if (ErrorCount == 0 && warningCount == 0 && InfoCount == 0)
        {
            ValidationSummary = "No problems found. This file is ready to save.";
            if (updateStatusMessage)
            {
                StatusMessage = "Validation complete: no problems found.";
            }
        }
        else if (ErrorCount > 0)
        {
            ValidationSummary = $"Fix {ErrorCount:N0} problem(s) before saving.";
            if (updateStatusMessage)
            {
                StatusMessage = $"Validation found {ErrorCount:N0} problem(s). Fix them before saving.";
            }
        }
        else
        {
            ValidationSummary = $"Review {warningCount + InfoCount:N0} note(s). Saving is allowed.";
            if (updateStatusMessage)
            {
                StatusMessage = $"Validation complete: {warningCount + InfoCount:N0} note(s).";
            }
        }
    }

    private void AddEntry()
    {
        CaptureSnapshot();
        var baseName = "NewCustomItem";
        var index = 1;
        var existing = Entries.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = baseName;
        while (existing.Contains(name))
        {
            name = $"{baseName}_{index++}";
        }

        SetWorkingFileState(true);

        var entry = new DayzTypeEntry
        {
            Name = name,
            Nominal = 1,
            Min = 1,
            Lifetime = 14400,
            Restock = 0,
            QuantMin = -1,
            QuantMax = -1,
            Cost = 100,
            CountInMap = true,
            Category = "tools",
            UsagesCsv = "Village",
            ValuesCsv = "Tier1"
        };

        SubscribeEntry(entry);
        Entries.Add(entry);
        RefreshCategories();
        ApplyFilters();
        SelectedEntry = entry;
        Validate();
        GenerateSavePreview();
        StatusMessage = $"Added {entry.Name}. Edit the details, then validate before saving.";
    }

    private void DeleteSelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        CaptureSnapshot();
        var deletedName = SelectedEntry.Name;
        SelectedEntry.PropertyChanged -= EntryOnPropertyChanged;
        Entries.Remove(SelectedEntry);
        ApplyFilters();
        SelectedEntry = FilteredEntries.FirstOrDefault();
        RefreshCategories();
        Validate();
        GenerateSavePreview();
        StatusMessage = $"Deleted {deletedName}.";
    }

    private void ScaleVisibleRows()
    {
        var targets = FilteredEntries.ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "No visible rows to adjust.";
            return;
        }

        var factor = BulkScalePercent / 100m;
        ExecuteBulkEntryMutation(
            targets,
            entry =>
            {
                entry.Nominal = Math.Max(0, (int)Math.Round(entry.Nominal * factor, MidpointRounding.AwayFromZero));
                entry.Min = Math.Max(0, (int)Math.Round(entry.Min * factor, MidpointRounding.AwayFromZero));
            },
            $"Adjusted Nominal and Min for {targets.Count:N0} visible row(s) by {BulkScalePercent:N0}%.");
    }

    private void ApplyProfileTemplate()
    {
        var targets = FilteredEntries.ToList();
        if (targets.Count == 0)
        {
            StatusMessage = "No visible rows to apply a profile to.";
            return;
        }

        var profileId = SelectedProfileTemplate?.Id ?? string.Empty;
        string result = "No profile applied.";
        ExecuteBulkMutation(
            targets.Count,
            () => result = _lootProfileService.ApplyTemplate(profileId, targets),
            successMessageFactory: () => result);
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedCategory = AllCategories;
        ShowOnlyIssues = false;
        ApplyFilters();
        StatusMessage = "Filters cleared.";
    }

    private void ClearLootSearch()
    {
        if (!HasSearchFilter)
        {
            return;
        }

        SearchText = string.Empty;
        StatusMessage = "Search cleared.";
    }

    private void ClearLootCategoryFilter()
    {
        if (!HasCategoryFilter)
        {
            return;
        }

        SelectedCategory = AllCategories;
        StatusMessage = "Category filter cleared.";
    }

    private void ClearLootIssueFilter()
    {
        if (!HasIssueFilter)
        {
            return;
        }

        ShowOnlyIssues = false;
        StatusMessage = "Issues-only filter cleared.";
    }

    private void ClearCustomCeSearch()
    {
        if (!HasCustomCeSearchFilter)
        {
            return;
        }

        CustomCeSearchText = string.Empty;
        StatusMessage = "Custom CE search cleared.";
    }

    private void ClearCustomCeStateFilter()
    {
        if (!HasCustomCeStateFilter)
        {
            return;
        }

        SelectedCustomCeFilter = CustomCeFilterOptions.FirstOrDefault() ?? "All";
        StatusMessage = "Custom CE filter cleared.";
    }

    private void ClearAllCustomCeFilters()
    {
        var changed = false;

        if (HasCustomCeSearchFilter)
        {
            CustomCeSearchText = string.Empty;
            changed = true;
        }

        if (HasCustomCeStateFilter)
        {
            SelectedCustomCeFilter = CustomCeFilterOptions.FirstOrDefault() ?? "All";
            changed = true;
        }

        if (changed)
        {
            StatusMessage = "Custom CE filters cleared.";
        }
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var target = _undoStack.Pop();
        if (_currentSnapshot is not null)
        {
            _redoStack.Push(CloneSnapshot(_currentSnapshot));
        }

        RestoreSnapshot(target);
        StatusMessage = "Undo complete.";
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var target = _redoStack.Pop();
        if (_currentSnapshot is not null)
        {
            _undoStack.Push(CloneSnapshot(_currentSnapshot));
        }

        RestoreSnapshot(target);
        StatusMessage = "Redo complete.";
    }

    private void GenerateSavePreview()
    {
        _ = GenerateSavePreviewAsync();
    }

    private async Task GenerateSavePreviewAsync()
    {
        try
        {
            if (Entries.Count == 0)
            {
                ResetSavePreview();
                return;
            }

            var filePath = FilePath;
            var loadedDocument = _loadedDocument;
            var entrySnapshots = Entries.ToList();

            var preview = await Task.Run(async () =>
            {
                var updatedXml = _typesXmlService.BuildPreviewXml(entrySnapshots, loadedDocument);
                var originalText = string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)
                    ? string.Empty
                    : await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

                var friendlyPreview = _textDiffService.BuildFriendlyDiff(originalText, updatedXml);
                var rawPreview = _textDiffService.BuildLineDiff(originalText, updatedXml, "Save Preview");
                return (friendlyPreview, rawPreview);
            }).ConfigureAwait(true);

            ApplySavePreview(preview.friendlyPreview, preview.rawPreview);
        }
        catch (Exception ex)
        {
            ResetSavePreview($"Could not generate save preview: {ex.Message}");
        }
    }

    private async Task AddCustomCeFileAsync()
    {
        if (!HasMissionFolder)
        {
            CustomCeStatus = "Open a DayZ mission folder first.";
            return;
        }

        try
        {
            IsBusy = true;
            var economyCorePath = _customCeService.GetEconomyCorePath(MissionFolder);
            if (AutoBackup && File.Exists(economyCorePath))
            {
                await _backupService.CreateBackupAsync(economyCorePath).ConfigureAwait(true);
            }

            var entry = await _customCeService.AddOrRegisterFileAsync(
                MissionFolder,
                NewCeFolderName,
                NewCeFileName,
                NewCeFileType).ConfigureAwait(true);

            await LoadCustomCeFilesAsync().ConfigureAwait(true);
            SelectedCustomCeFile = CustomCeFiles.FirstOrDefault(item =>
                string.Equals(item.RelativePath, entry.RelativePath, StringComparison.OrdinalIgnoreCase));
            CustomCeStatus = $"Registered {entry.RelativePath} in cfgeconomycore.xml.";
            StatusMessage = $"Custom CE file ready: {entry.RelativePath}.";
        }
        catch (Exception ex)
        {
            CustomCeStatus = $"Custom CE setup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCustomCeAsync()
    {
        try
        {
            IsBusy = true;
            await LoadCustomCeFilesAsync().ConfigureAwait(true);
            CustomCeStatus = HasMissionFolder
                ? "Custom CE files refreshed. Only real setup problems are shown."
                : "Open a mission folder to manage custom CE files.";
        }
        catch (Exception ex)
        {
            CustomCeStatus = $"Could not refresh Custom CE files: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCustomCeFilesAsync()
    {
        CustomCeFiles.Clear();
        FilteredCustomCeFiles.Clear();
        CustomCeIssues.Clear();
        OnPropertyChanged(nameof(HasCustomCeIssues));
        OnPropertyChanged(nameof(IsCustomCeIssuesEmpty));

        if (!HasMissionFolder)
        {
            CustomCeErrorCount = 0;
            CustomCeReadyCount = 0;
            CustomCeInfoCount = 0;
            ReplaceCollection(CustomCeConflictItems, Array.Empty<string>());
            CustomCeConflictSummary = "Refresh custom CE validation to detect duplicate registrations and path/type conflicts.";
            ReplaceCollection(CustomCeRepairDiffSections, Array.Empty<SaveDiffSection>());
            CustomCeRepairDiffSummary = "Select a custom CE file to preview root-fix changes before running repair.";
            OnPropertyChanged(nameof(HasCustomCeConflicts));
            OnPropertyChanged(nameof(IsCustomCeConflictsEmpty));
            OnPropertyChanged(nameof(HasCustomCeRepairDiff));
            OnPropertyChanged(nameof(IsCustomCeRepairDiffEmpty));
            OnPropertyChanged(nameof(CustomCeFileCount));
            OnPropertyChanged(nameof(FilteredCustomCeFileCount));
            OnPropertyChanged(nameof(HasCustomCeFiles));
            OnPropertyChanged(nameof(IsCustomCeEmpty));
            OnPropertyChanged(nameof(ShowCustomCeOnboarding));
            OnPropertyChanged(nameof(ShowCustomCeEmptyState));
            OnPropertyChanged(nameof(ShowCustomCeNoResults));
            OnPropertyChanged(nameof(ShowCustomCeSelectionHint));
            return;
        }

        var result = await _customCeService.LoadAsync(MissionFolder).ConfigureAwait(true);
        foreach (var entry in result.Entries)
        {
            CustomCeFiles.Add(entry);
        }

        foreach (var issue in result.Issues)
        {
            CustomCeIssues.Add(issue);
        }

        OnPropertyChanged(nameof(HasCustomCeIssues));
        OnPropertyChanged(nameof(IsCustomCeIssuesEmpty));

        CustomCeErrorCount = result.Issues.Count(issue => issue.Severity == ValidationSeverity.Error);
        CustomCeInfoCount = result.Issues.Count(issue => issue.Severity == ValidationSeverity.Info);
        CustomCeReadyCount = result.Entries.Count(entry => string.Equals(entry.Status, "OK", StringComparison.OrdinalIgnoreCase));
        ApplyCustomCeFilter();
        RefreshCustomCeConflictSummary();
        OnPropertyChanged(nameof(CustomCeFileCount));
        OnPropertyChanged(nameof(FilteredCustomCeFileCount));
        OnPropertyChanged(nameof(HasCustomCeFiles));
        OnPropertyChanged(nameof(IsCustomCeEmpty));
        OnPropertyChanged(nameof(ShowCustomCeOnboarding));
        OnPropertyChanged(nameof(ShowCustomCeEmptyState));
        OnPropertyChanged(nameof(ShowCustomCeNoResults));
        OnPropertyChanged(nameof(ShowCustomCeSelectionHint));
    }

    private void ApplyCustomCeFilter()
    {
        var selectedPath = SelectedCustomCeFile?.FullPath;
        var searchText = CustomCeSearchText.Trim();

        IEnumerable<CustomCeFileEntry> filtered = CustomCeFiles;

        if (!string.Equals(SelectedCustomCeFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(MatchesCustomCeFilter);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(entry =>
                ContainsIgnoreCase(entry.Status, searchText)
                || ContainsIgnoreCase(entry.Type, searchText)
                || ContainsIgnoreCase(entry.Folder, searchText)
                || ContainsIgnoreCase(entry.FileName, searchText)
                || ContainsIgnoreCase(entry.RelativePath, searchText)
                || ContainsIgnoreCase(entry.FullPath, searchText)
                || ContainsIgnoreCase(entry.IssueSummary, searchText));
        }

        ReplaceCollection(FilteredCustomCeFiles, filtered.OrderBy(entry => entry.RelativePath).ToArray());

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            if (SelectedCustomCeFile is not null && !FilteredCustomCeFiles.Contains(SelectedCustomCeFile))
            {
                SelectedCustomCeFile = FilteredCustomCeFiles.FirstOrDefault();
            }
        }
        else
        {
            SelectedCustomCeFile = FilteredCustomCeFiles.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
        }

        NotifyCustomCeFilterSummaryStateChanged();
    }

    private bool MatchesCustomCeFilter(CustomCeFileEntry entry)
    {
        return SelectedCustomCeFilter switch
        {
            "Ready" => string.Equals(entry.Status, "OK", StringComparison.OrdinalIgnoreCase),
            "Broken" => !string.Equals(entry.Status, "OK", StringComparison.OrdinalIgnoreCase),
            "Missing" => entry.IssueSummary.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || entry.Status.Contains("missing", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(entry.FullPath),
            "Duplicate" => entry.IssueSummary.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || entry.Status.Contains("duplicate", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private void RefreshCustomCeConflictSummary()
    {
        var conflicts = new List<string>();

        var duplicateRegistrations = CustomCeFiles
            .GroupBy(entry => $"{entry.Folder}|{entry.FileName}|{entry.Type}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in duplicateRegistrations)
        {
            var sample = group.First();
            conflicts.Add($"Duplicate registration: {sample.RelativePath} is registered {group.Count()} times as type '{sample.Type}'.");
        }

        var pathTypeConflicts = CustomCeFiles
            .GroupBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                RelativePath = group.Key,
                Types = group.Select(entry => entry.Type).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(type => type, StringComparer.OrdinalIgnoreCase).ToArray()
            })
            .Where(group => group.Types.Length > 1)
            .OrderBy(group => group.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var conflict in pathTypeConflicts)
        {
            conflicts.Add($"Path/type conflict: {conflict.RelativePath} is registered with multiple CE types ({string.Join(", ", conflict.Types)}).");
        }

        ReplaceCollection(CustomCeConflictItems, conflicts);
        CustomCeConflictSummary = conflicts.Count == 0
            ? "No duplicate registration or path/type conflicts detected in cfgeconomycore.xml."
            : $"Detected {conflicts.Count} CE conflict{Pluralize(conflicts.Count)} that should be cleaned up.";

        OnPropertyChanged(nameof(HasCustomCeConflicts));
        OnPropertyChanged(nameof(IsCustomCeConflictsEmpty));
    }

    private async Task RefreshSelectedCustomCeDiffAsync()
    {
        var selection = SelectedCustomCeFile!;
        var requestVersion = Interlocked.Increment(ref _customCeDiffRequestVersion);

        _customCeDiffCts?.Cancel();
        _customCeDiffCts?.Dispose();
        _customCeDiffCts = new CancellationTokenSource();
        var cancellationToken = _customCeDiffCts.Token;

        ReplaceCollection(CustomCeRepairDiffSections, Array.Empty<SaveDiffSection>());
        OnPropertyChanged(nameof(HasCustomCeRepairDiff));
        OnPropertyChanged(nameof(IsCustomCeRepairDiffEmpty));

        if (selection is null)
        {
            CustomCeRepairDiffSummary = "Select a custom CE file to preview root-fix changes before running repair.";
            return;
        }

        if (!File.Exists(selection.FullPath))
        {
            CustomCeRepairDiffSummary = "This file is missing on disk, so there is no XML diff to preview yet.";
            return;
        }

        if (!TryGetExpectedCustomCeRootName(selection.Type, out var expectedRootName))
        {
            CustomCeRepairDiffSummary = $"Type '{selection.Type}' is not supported for repair diff preview.";
            return;
        }

        string originalText;
        try
        {
            originalText = await File.ReadAllTextAsync(selection.FullPath, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!IsLatestCustomCeDiffRequest(requestVersion, selection))
            {
                return;
            }

            CustomCeRepairDiffSummary = $"Could not read the selected custom CE file: {ex.Message}";
            return;
        }

        if (!IsLatestCustomCeDiffRequest(requestVersion, selection))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(originalText))
        {
            CustomCeRepairDiffSummary = "The selected file is empty, so there is no useful repair diff to preview.";
            return;
        }

        try
        {
            var preview = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var document = XDocument.Parse(originalText, LoadOptions.PreserveWhitespace);
                var actualRootName = document.Root?.Name.LocalName ?? string.Empty;
                if (string.Equals(actualRootName, expectedRootName, StringComparison.OrdinalIgnoreCase))
                {
                    return (sections: Array.Empty<SaveDiffSection>(), summary: $"Root is already valid (<{expectedRootName}>). Repair would not change this file.");
                }

                if (document.Root is null)
                {
                    return (sections: Array.Empty<SaveDiffSection>(), summary: "The selected XML file does not have a root element, so a repair diff cannot be previewed safely.");
                }

                document.Root.Name = expectedRootName;
                var updatedText = document.Declaration is null
                    ? document.ToString()
                    : document.Declaration + Environment.NewLine + document.ToString();

                var diff = _textDiffService.BuildFriendlyDiff(originalText, updatedText);
                return (sections: diff.Sections.ToArray(), summary: $"Repair preview for {selection.RelativePath}: {diff.Summary}");
            }, cancellationToken).ConfigureAwait(true);

            if (!IsLatestCustomCeDiffRequest(requestVersion, selection))
            {
                return;
            }

            ReplaceCollection(CustomCeRepairDiffSections, preview.sections);
            CustomCeRepairDiffSummary = preview.summary;
            OnPropertyChanged(nameof(HasCustomCeRepairDiff));
            OnPropertyChanged(nameof(IsCustomCeRepairDiffEmpty));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            if (!IsLatestCustomCeDiffRequest(requestVersion, selection))
            {
                return;
            }

            CustomCeRepairDiffSummary = $"Cannot preview repair diff because the XML is malformed: {ex.Message}";
        }
    }

    private bool IsLatestCustomCeDiffRequest(long requestVersion, CustomCeFileEntry selection)
    {
        return requestVersion == Interlocked.Read(ref _customCeDiffRequestVersion)
            && ReferenceEquals(SelectedCustomCeFile, selection);
    }

    private static bool TryGetExpectedCustomCeRootName(string type, out string rootName)
    {
        switch ((type ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "types":
                rootName = "types";
                return true;
            case "spawnabletypes":
                rootName = "spawnabletypes";
                return true;
            case "events":
                rootName = "events";
                return true;
            case "eventspawns":
                rootName = "eventposdef";
                return true;
            case "globals":
                rootName = "variables";
                return true;
            default:
                rootName = string.Empty;
                return false;
        }
    }

    private static bool ContainsIgnoreCase(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private async Task OpenSelectedCustomTypesAsync()
    {
        if (SelectedCustomCeFile is null)
        {
            CustomCeStatus = "Select a custom CE file first.";
            return;
        }

        if (!string.Equals(SelectedCustomCeFile.Type, "types", StringComparison.OrdinalIgnoreCase))
        {
            CustomCeStatus = "Only custom files with type='types' can open in the Loot Editor screen right now.";
            return;
        }

        if (!File.Exists(SelectedCustomCeFile.FullPath))
        {
            CustomCeStatus = "That custom file does not exist yet. Validate or recreate it first.";
            return;
        }

        if (!await ConfirmReplaceCurrentSessionAsync("Open Custom Types").ConfigureAwait(true))
        {
            return;
        }

        await LoadFileAsync(SelectedCustomCeFile.FullPath, MissionFolder).ConfigureAwait(true);
        if (HasWorkingFile && string.Equals(FilePath, SelectedCustomCeFile.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            ActiveFeature = LootEditorFeature;
        }
    }

    private void ResetCustomCeForm()
    {
        NewCeFolderName = "modtypes";
        NewCeFileName = "types_custom.xml";
        NewCeFileType = "types";
        CustomCeStatus = "Custom CE form reset.";
    }
private async Task UnregisterSelectedCustomCeAsync()
{
    await RemoveSelectedCustomCeAsync(deleteFile: false).ConfigureAwait(true);
}

private async Task DeleteSelectedCustomCeAsync()
{
    await RemoveSelectedCustomCeAsync(deleteFile: true).ConfigureAwait(true);
}

private async Task RemoveSelectedCustomCeAsync(bool deleteFile)
{
    if (SelectedCustomCeFile is null)
    {
        CustomCeStatus = "Select a custom CE file first.";
        return;
    }

    try
    {
        IsBusy = true;
        var selection = SelectedCustomCeFile!;
        var economyCorePath = _customCeService.GetEconomyCorePath(MissionFolder);
        string? economyCoreBackupPath = null;
        string? customCeBackupPath = null;

        if (AutoBackup && File.Exists(economyCorePath))
        {
            economyCoreBackupPath = await _backupService.CreateBackupAsync(economyCorePath).ConfigureAwait(true);
        }

        if (deleteFile && AutoBackup && File.Exists(selection.FullPath))
        {
            customCeBackupPath = await _backupService.CreateBackupAsync(selection.FullPath).ConfigureAwait(true);
        }

        var removed = await _customCeService.UnregisterFileAsync(
            MissionFolder,
            selection.Folder,
            selection.FileName,
            selection.Type,
            deleteFile).ConfigureAwait(true);

        if (!removed)
        {
            CustomCeStatus = "That CE registration was already missing.";
            return;
        }

        var relativePath = selection.RelativePath;
        var removedLoadedFile = deleteFile &&
            HasWorkingFile &&
            string.Equals(FilePath, selection.FullPath, StringComparison.OrdinalIgnoreCase);

        await LoadCustomCeFilesAsync().ConfigureAwait(true);
        SelectedCustomCeFile = CustomCeFiles.FirstOrDefault();

        if (removedLoadedFile)
        {
            ClearLoadedFileState(MissionFolder, $"Removed {relativePath} from cfgeconomycore.xml, deleted the XML file, and unloaded it from the editor.");
        }

        CustomCeStatus = deleteFile
            ? customCeBackupPath is null
                ? removedLoadedFile
                    ? $"Removed {relativePath} from cfgeconomycore.xml, deleted the XML file, and unloaded it from the editor."
                    : $"Removed {relativePath} from cfgeconomycore.xml and deleted the XML file."
                : removedLoadedFile
                    ? $"Removed {relativePath} from cfgeconomycore.xml, deleted the XML file, and unloaded it from the editor. Backup created: {Path.GetFileName(customCeBackupPath)}."
                    : $"Removed {relativePath} from cfgeconomycore.xml and deleted the XML file. Backup created: {Path.GetFileName(customCeBackupPath)}."
            : economyCoreBackupPath is null
                ? $"Removed {relativePath} from cfgeconomycore.xml. The XML file was kept on disk."
                : $"Removed {relativePath} from cfgeconomycore.xml. The XML file was kept on disk. Backup created: {Path.GetFileName(economyCoreBackupPath)}.";
    }
    catch (Exception ex)
    {
        CustomCeStatus = $"Could not remove custom CE file: {ex.Message}";
    }
    finally
    {
        IsBusy = false;
    }
}

private async Task RepairSelectedCustomCeAsync()
{
    if (SelectedCustomCeFile is null)
    {
        CustomCeStatus = "Select a custom CE file first.";
        return;
    }

    try
    {
        IsBusy = true;

        string? backupPath = null;
        if (AutoBackup && File.Exists(SelectedCustomCeFile.FullPath))
        {
            backupPath = await _backupService.CreateBackupAsync(SelectedCustomCeFile.FullPath).ConfigureAwait(true);
        }

        var repaired = await _customCeService.RepairFileRootAsync(
            MissionFolder,
            SelectedCustomCeFile.Folder,
            SelectedCustomCeFile.FileName,
            SelectedCustomCeFile.Type).ConfigureAwait(true);

        await LoadCustomCeFilesAsync().ConfigureAwait(true);
        SelectedCustomCeFile = CustomCeFiles.FirstOrDefault(item =>
            string.Equals(item.RelativePath, repaired.RelativePath, StringComparison.OrdinalIgnoreCase));
        CustomCeStatus = backupPath is null
            ? $"Repaired XML root for {repaired.RelativePath}."
            : $"Repaired XML root for {repaired.RelativePath}. Backup created: {Path.GetFileName(backupPath)}.";
    }
    catch (Exception ex)
    {
        CustomCeStatus = $"Could not repair custom CE file: {ex.Message}";
    }
    finally
    {
        IsBusy = false;
    }
}

private void UseTypesPreset()
{
    NewCeFolderName = "modtypes";
    NewCeFileName = "types_custom.xml";
    NewCeFileType = "types";
    CustomCeStatus = "Loaded the default types.xml starter preset.";
}

private void UseSpawnablePreset()
{
    NewCeFolderName = "modtypes";
    NewCeFileName = "spawnabletypes_custom.xml";
    NewCeFileType = "spawnabletypes";
    CustomCeStatus = "Loaded the spawnabletypes starter preset.";
}

private void UseEventsPreset()
{
    NewCeFolderName = "events";
    NewCeFileName = "events_custom.xml";
    NewCeFileType = "events";
    CustomCeStatus = "Loaded the events starter preset.";
}

private void UseGlobalsPreset()
{
    NewCeFolderName = "globals";
    NewCeFileName = "globals_custom.xml";
    NewCeFileType = "globals";
    CustomCeStatus = "Loaded the globals starter preset.";
}

private void RefreshCustomCePreview()
{
    if (!HasMissionFolder)
    {
        CustomCePreviewSummary = "Open a mission folder to preview where the custom CE file will be created.";
        return;
    }

    try
    {
        var preview = _customCeService.BuildPreview(MissionFolder, NewCeFolderName, NewCeFileName, NewCeFileType);
        CustomCePreviewSummary = preview.Summary;
    }
    catch (Exception ex)
    {
        CustomCePreviewSummary = ex.Message;
    }
}

    private void ApplyFilters()
    {
        var query = SearchText.Trim();
        IEnumerable<DayzTypeEntry> visible = Entries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            visible = visible.Where(entry => Contains(entry.Name, query)
                || Contains(entry.Category, query)
                || Contains(entry.TagsCsv, query)
                || Contains(entry.UsagesCsv, query)
                || Contains(entry.ValuesCsv, query)
                || Contains(entry.IssueSummary, query));
        }

        if (!string.Equals(SelectedCategory, AllCategories, StringComparison.OrdinalIgnoreCase))
        {
            visible = visible.Where(entry => string.Equals(entry.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (ShowOnlyIssues)
        {
            visible = visible.Where(entry => entry.HasIssues);
        }

        var materialized = visible.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
        FilteredEntries.Clear();
        foreach (var entry in materialized)
        {
            FilteredEntries.Add(entry);
        }

        OnPropertyChanged(nameof(TotalEntries));
        OnPropertyChanged(nameof(FilteredCount));
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(ShowLootNoResults));
        OnPropertyChanged(nameof(ShowLootSelectionHint));
        ScaleVisibleCommand.NotifyCanExecuteChanged();
        ApplyProfileTemplateCommand.NotifyCanExecuteChanged();
        NotifyLootFilterSummaryStateChanged();
    }

    private void RefreshCategories()
    {
        var categories = Entries
            .Select(entry => entry.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Categories.Clear();
        Categories.Add(AllCategories);
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        if (!Categories.Contains(SelectedCategory))
        {
            SelectedCategory = AllCategories;
        }
    }

    private void SubscribeEntry(DayzTypeEntry entry)
    {
        entry.PropertyChanged -= EntryOnPropertyChanged;
        entry.PropertyChanged += EntryOnPropertyChanged;
    }

    private void EntryOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressHistory || _suspendEntryReactiveWork)
        {
            return;
        }

        if (e.PropertyName is nameof(DayzTypeEntry.IsDirty))
        {
            OnPropertyChanged(nameof(DirtyCount));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        if (ReferenceEquals(sender, SelectedEntry)
            && e.PropertyName is nameof(DayzTypeEntry.Name)
                or nameof(DayzTypeEntry.Category)
                or nameof(DayzTypeEntry.UsagesCsv)
                or nameof(DayzTypeEntry.ValuesCsv)
                or nameof(DayzTypeEntry.IssueSummary)
                or nameof(DayzTypeEntry.ValidationState)
                or nameof(DayzTypeEntry.IsDirty))
        {
            NotifyLootSelectionStateChanged();
        }

        if (e.PropertyName is nameof(DayzTypeEntry.Category))
        {
            RefreshCategories();
        }

        var affectsSaveData = e.PropertyName is nameof(DayzTypeEntry.Name)
            or nameof(DayzTypeEntry.Category)
            or nameof(DayzTypeEntry.TagsCsv)
            or nameof(DayzTypeEntry.UsagesCsv)
            or nameof(DayzTypeEntry.ValuesCsv)
            or nameof(DayzTypeEntry.Nominal)
            or nameof(DayzTypeEntry.Min)
            or nameof(DayzTypeEntry.Lifetime)
            or nameof(DayzTypeEntry.Restock)
            or nameof(DayzTypeEntry.QuantMin)
            or nameof(DayzTypeEntry.QuantMax)
            or nameof(DayzTypeEntry.Cost)
            or nameof(DayzTypeEntry.CountInCargo)
            or nameof(DayzTypeEntry.CountInHoarder)
            or nameof(DayzTypeEntry.CountInMap)
            or nameof(DayzTypeEntry.CountInPlayer)
            or nameof(DayzTypeEntry.Crafted)
            or nameof(DayzTypeEntry.Deloot);

        if (affectsSaveData)
        {
            if (!_historyDirty)
            {
                CaptureSnapshot();
                _historyDirty = true;
            }

            ResetSavePreview("The preview is out of date because you changed loot values. Click Refresh Preview to compare the current editor state with the file on disk.");

            var affectsVisibleRows = e.PropertyName is nameof(DayzTypeEntry.Name)
                or nameof(DayzTypeEntry.Category)
                or nameof(DayzTypeEntry.TagsCsv)
                or nameof(DayzTypeEntry.UsagesCsv)
                or nameof(DayzTypeEntry.ValuesCsv);

            if (affectsVisibleRows)
            {
                ApplyFilters();
            }

            QueueValidation();
        }
    }

    private void ExecuteBulkMutation(
        int targetCount,
        Action mutate,
        string? successMessage = null,
        Func<string>? successMessageFactory = null)
    {
        if (targetCount <= 0)
        {
            return;
        }

        CaptureSnapshot();
        var previousBusyState = IsBusy;
        IsBusy = true;
        _suppressHistory = true;
        _suspendEntryReactiveWork = true;

        try
        {
            mutate();
        }
        finally
        {
            _suspendEntryReactiveWork = false;
            _suppressHistory = false;
            IsBusy = previousBusyState;
        }

        _historyDirty = false;
        ResetSavePreview("The preview is out of date. Click Refresh Preview to compare the current editor state with the file on disk.");
        Validate();
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        NotifyLootSelectionStateChanged();
        StatusMessage = successMessageFactory?.Invoke()
            ?? successMessage
            ?? $"Updated {targetCount:N0} visible rows.";
    }

    private void ExecuteBulkEntryMutation(
        IReadOnlyCollection<DayzTypeEntry> targets,
        Action<DayzTypeEntry> mutate,
        string? successMessage = null,
        Func<string>? successMessageFactory = null)
    {
        ExecuteBulkMutation(
            targets.Count,
            () =>
            {
                foreach (var entry in targets)
                {
                    mutate(entry);
                }
            },
            successMessage,
            successMessageFactory);
    }

    private void NotifyLootSelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedEntry));
        OnPropertyChanged(nameof(IsEntrySelectionEmpty));
        OnPropertyChanged(nameof(ShowLootSelectionHint));
    }

    private void NotifyLootFilterSummaryStateChanged()
    {
        OnPropertyChanged(nameof(HasSearchFilter));
        OnPropertyChanged(nameof(HasCategoryFilter));
        OnPropertyChanged(nameof(HasIssueFilter));
        OnPropertyChanged(nameof(HasActiveLootFilterSummary));
        OnPropertyChanged(nameof(SearchFilterChipText));
        OnPropertyChanged(nameof(CategoryFilterChipText));
        OnPropertyChanged(nameof(IssueFilterChipText));
        OnPropertyChanged(nameof(HasLootRowCountSummary));
        OnPropertyChanged(nameof(LootRowCountSummaryText));
        ClearLootSearchCommand.NotifyCanExecuteChanged();
        ClearLootCategoryFilterCommand.NotifyCanExecuteChanged();
        ClearLootIssueFilterCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCustomCeSelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedCustomCeFile));
        OnPropertyChanged(nameof(IsCustomCeSelectionEmpty));
        OnPropertyChanged(nameof(ShowCustomCeSelectionHint));
        OnPropertyChanged(nameof(SelectedCustomCeNextAction));
    }

    private void NotifyCustomCeFilterSummaryStateChanged()
    {
        OnPropertyChanged(nameof(FilteredCustomCeFileCount));
        OnPropertyChanged(nameof(HasCustomCeFiles));
        OnPropertyChanged(nameof(IsCustomCeEmpty));
        OnPropertyChanged(nameof(ShowCustomCeEmptyState));
        OnPropertyChanged(nameof(ShowCustomCeNoResults));
        OnPropertyChanged(nameof(ShowCustomCeSelectionHint));
        OnPropertyChanged(nameof(HasCustomCeSearchFilter));
        OnPropertyChanged(nameof(HasCustomCeStateFilter));
        OnPropertyChanged(nameof(HasActiveCustomCeFilterSummary));
        OnPropertyChanged(nameof(CustomCeSearchFilterChipText));
        OnPropertyChanged(nameof(CustomCeStateFilterChipText));
        OnPropertyChanged(nameof(HasCustomCeRowCountSummary));
        OnPropertyChanged(nameof(CustomCeRowCountSummaryText));
        ClearAllCustomCeFiltersCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCommandStates()
    {
        OpenTypesFileCommand.NotifyCanExecuteChanged();
        OpenMissionFolderCommand.NotifyCanExecuteChanged();
        UnloadLoadedFileCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        AddEntryCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        ScaleVisibleCommand.NotifyCanExecuteChanged();
        ClearFiltersCommand.NotifyCanExecuteChanged();
        ClearLootSearchCommand.NotifyCanExecuteChanged();
        ClearLootCategoryFilterCommand.NotifyCanExecuteChanged();
        ClearLootIssueFilterCommand.NotifyCanExecuteChanged();
        ApplyProfileTemplateCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        GenerateSavePreviewCommand.NotifyCanExecuteChanged();
        OpenSelectedRecentTypesFileCommand.NotifyCanExecuteChanged();
        OpenSelectedRecentMissionFolderCommand.NotifyCanExecuteChanged();
        ShowLootEditorCommand.NotifyCanExecuteChanged();
        ShowCustomCeCommand.NotifyCanExecuteChanged();
        ShowInfoCommand.NotifyCanExecuteChanged();
        AddCustomCeFileCommand.NotifyCanExecuteChanged();
        ClearAllCustomCeFiltersCommand.NotifyCanExecuteChanged();
        OpenSelectedCustomTypesCommand.NotifyCanExecuteChanged();
        UnregisterSelectedCustomCeCommand.NotifyCanExecuteChanged();
        DeleteSelectedCustomCeCommand.NotifyCanExecuteChanged();
        RepairSelectedCustomCeCommand.NotifyCanExecuteChanged();
        RefreshSelectedCustomCeDiffCommand.NotifyCanExecuteChanged();
        UseTypesPresetCommand.NotifyCanExecuteChanged();
        UseSpawnablePresetCommand.NotifyCanExecuteChanged();
        UseEventsPresetCommand.NotifyCanExecuteChanged();
        UseGlobalsPresetCommand.NotifyCanExecuteChanged();
        ResetCustomCeFormCommand.NotifyCanExecuteChanged();
    }

    private void QueueValidation()
    {
        _validationDebounceCts?.Cancel();
        _validationDebounceCts?.Dispose();
        _validationDebounceCts = new CancellationTokenSource();
        var token = _validationDebounceCts.Token;
        _ = DebouncedValidateAsync(token);
    }

    private async Task DebouncedValidateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested && !IsBusy)
            {
                _historyDirty = false;
                Validate();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void QueueLootFilterRefresh()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts?.Dispose();
        _filterDebounceCts = new CancellationTokenSource();
        var token = _filterDebounceCts.Token;
        _ = DebouncedApplyFiltersAsync(token);
    }

    private async Task DebouncedApplyFiltersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(150, cancellationToken).ConfigureAwait(true);
            if (!cancellationToken.IsCancellationRequested && !IsBusy)
            {
                ApplyFilters();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<XDocument?> ReloadSavedDocumentAsync(string path)
    {
        try
        {
            var reloaded = await _typesXmlService.LoadAsync(path).ConfigureAwait(true);
            return reloaded.SourceDocument;
        }
        catch
        {
            return _loadedDocument;
        }
    }

    private void CaptureSnapshot(bool clearHistory = false)
    {
        if (_suppressHistory)
        {
            return;
        }

        var snapshot = CreateSnapshot();
        if (clearHistory)
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
        else if (_currentSnapshot is not null)
        {
            _undoStack.Push(CloneSnapshot(_currentSnapshot));
            _redoStack.Clear();
        }

        _currentSnapshot = snapshot;
        RefreshCommandStates();
    }

    private EditorSnapshot CreateSnapshot()
    {
        return new EditorSnapshot
        {
            Entries = Entries.Select(entry => entry.Clone()).ToList(),
            LoadedDocument = _loadedDocument is null ? null : new XDocument(_loadedDocument),
            FilePath = FilePath,
            MissionFolder = MissionFolder,
            StatusMessage = StatusMessage
        };
    }

    private static EditorSnapshot CloneSnapshot(EditorSnapshot snapshot)
    {
        return new EditorSnapshot
        {
            Entries = snapshot.Entries.Select(entry => entry.Clone()).ToList(),
            LoadedDocument = snapshot.LoadedDocument is null ? null : new XDocument(snapshot.LoadedDocument),
            FilePath = snapshot.FilePath,
            MissionFolder = snapshot.MissionFolder,
            StatusMessage = snapshot.StatusMessage
        };
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        _suppressHistory = true;
        try
        {
            ReplaceEntries(snapshot.Entries, snapshot.LoadedDocument, snapshot.FilePath, snapshot.MissionFolder, snapshot.StatusMessage);
            _currentSnapshot = CloneSnapshot(snapshot);
            _historyDirty = false;
        }
        finally
        {
            _suppressHistory = false;
            RefreshCommandStates();
        }
    }

    private void ClearLoadedFileState(string missionFolder, string statusMessage)
    {
        foreach (var existing in Entries)
        {
            existing.PropertyChanged -= EntryOnPropertyChanged;
        }

        Entries.Clear();
        FilteredEntries.Clear();
        ValidationIssues.Clear();
        _loadedDocument = null;
        _currentSnapshot = null;
        _undoStack.Clear();
        _redoStack.Clear();
        _historyDirty = false;

        SetWorkingFileState(false);
        FilePath = string.Empty;
        MissionFolder = missionFolder;
        ErrorCount = 0;
        InfoCount = 0;
        ValidationSummary = "No file loaded yet. Open a mission folder or types.xml first.";
        RefreshCategories();
        ApplyFilters();
        SelectedEntry = null;
        ResetSavePreview();
        StatusMessage = statusMessage;
        OnPropertyChanged(nameof(LoadedMode));
        OnPropertyChanged(nameof(DirtyCount));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RefreshCommandStates();
    }

    private void ReplaceEntries(
        IReadOnlyCollection<DayzTypeEntry> entries,
        XDocument? sourceDocument,
        string filePath,
        string missionFolder,
        string statusMessage)
    {
        foreach (var existing in Entries)
        {
            existing.PropertyChanged -= EntryOnPropertyChanged;
        }

        Entries.Clear();
        FilteredEntries.Clear();
        ValidationIssues.Clear();

        foreach (var entry in entries.Select(item => item.Clone()))
        {
            SubscribeEntry(entry);
            Entries.Add(entry);
        }

        _loadedDocument = sourceDocument is null ? null : new XDocument(sourceDocument);
        SetWorkingFileState(!string.IsNullOrWhiteSpace(filePath));
        FilePath = filePath;
        MissionFolder = missionFolder;
        RefreshCategories();
        Validate();
        ApplyFilters();
        SelectedEntry = FilteredEntries.FirstOrDefault();
        ResetSavePreview();
        StatusMessage = statusMessage;
        OnPropertyChanged(nameof(LoadedMode));
    }

    private void RefreshRecentCollections()
    {
        ReplaceCollection(RecentTypesFiles, _recentFilesService.GetRecentTypesFiles().Where(File.Exists));
        ReplaceCollection(RecentMissionFolders, _recentFilesService.GetRecentMissionFolders().Where(Directory.Exists));

        if (!RecentTypesFiles.Contains(SelectedRecentTypesFile))
        {
            SelectedRecentTypesFile = RecentTypesFiles.FirstOrDefault() ?? string.Empty;
        }

        if (!RecentMissionFolders.Contains(SelectedRecentMissionFolder))
        {
            SelectedRecentMissionFolder = RecentMissionFolders.FirstOrDefault() ?? string.Empty;
        }
    }

    private static string NormalizeWorkspace(string? workspace)
    {
        if (string.Equals(workspace, CustomCeFeature, StringComparison.OrdinalIgnoreCase))
        {
            return CustomCeFeature;
        }

        return string.Equals(workspace, InfoFeature, StringComparison.OrdinalIgnoreCase)
            ? InfoFeature
            : LootEditorFeature;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static void ReplaceCollection(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(value);
        }
    }

    private void HandleUnhandledError(Exception exception)
    {
        var logPath = CrashLogService.LogException("TypesEditorViewModel", exception);
        StatusMessage = $"Unexpected error: {exception.Message} See log: {Path.GetFileName(logPath)}.";
    }

    private static string Pluralize(int count)
    {
        return count == 1 ? string.Empty : "s";
    }

    private static bool Contains(string? haystack, string needle)
    {
        return haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolveMissionFolderSelection(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return string.Empty;
        }

        var normalizedFolder = Path.GetFullPath(folder);
        if (IsMissionFolder(normalizedFolder))
        {
            return normalizedFolder;
        }

        var configuredMissionFolder = TryResolveMissionFolderFromServerRoot(normalizedFolder);
        if (!string.IsNullOrWhiteSpace(configuredMissionFolder))
        {
            return configuredMissionFolder;
        }

        var nestedMissionFolder = FindMissionFolderWithin(normalizedFolder);
        if (!string.IsNullOrWhiteSpace(nestedMissionFolder))
        {
            return nestedMissionFolder;
        }

        return normalizedFolder;
    }

    private static string? GetTypesPathForMission(string missionFolder)
    {
        var candidatePaths = new[]
        {
            Path.Combine(missionFolder, "db", "types.xml"),
            Path.Combine(missionFolder, "db", "Types.xml"),
            Path.Combine(missionFolder, "types.xml"),
            Path.Combine(missionFolder, "Types.xml")
        };

        return candidatePaths.FirstOrDefault(File.Exists);
    }

    private static string? TryFindMissionFolderFromAppDirectory(string appDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDirectory) || !Directory.Exists(appDirectory))
        {
            return null;
        }

        var probe = new DirectoryInfo(Path.GetFullPath(appDirectory));
        while (probe is not null)
        {
            var missionFolder = TryResolveMissionFolderFromServerRoot(probe.FullName);
            if (!string.IsNullOrWhiteSpace(missionFolder))
            {
                return missionFolder;
            }

            probe = probe.Parent;
        }

        return null;
    }

    private static string? TryResolveMissionFolderFromServerRoot(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        var normalizedFolder = Path.GetFullPath(folder);
        var missionRoot = Path.Combine(normalizedFolder, "mpmissions");
        if (!Directory.Exists(missionRoot))
        {
            return null;
        }

        var configuredMissionName = TryReadMissionNameFromServerConfig(normalizedFolder);
        if (!string.IsNullOrWhiteSpace(configuredMissionName))
        {
            var configuredMissionFolder = Path.Combine(missionRoot, configuredMissionName);
            if (IsMissionFolder(configuredMissionFolder))
            {
                return configuredMissionFolder;
            }
        }

        var knownDefaultMissionFolders = new[]
        {
            "dayzOffline.chernarusplus",
            "dayzOffline.enoch",
            "dayzOffline.sakhal"
        };

        foreach (var missionName in knownDefaultMissionFolders)
        {
            var candidate = Path.Combine(missionRoot, missionName);
            if (IsMissionFolder(candidate))
            {
                return candidate;
            }
        }

        return FindMissionFolderWithin(missionRoot);
    }

    private static string? TryReadMissionNameFromServerConfig(string serverRoot)
    {
        var candidateConfigPaths = new[]
        {
            Path.Combine(serverRoot, "serverDZ.cfg"),
            Path.Combine(serverRoot, "server.cfg")
        };

        foreach (var configPath in candidateConfigPaths)
        {
            if (!File.Exists(configPath))
            {
                continue;
            }

            var configText = File.ReadAllText(configPath);
            var templateMatch = System.Text.RegularExpressions.Regex.Match(
                configText,
                """template\s*=\s*"(?<mission>[^"]+)"\s*;""",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (templateMatch.Success)
            {
                var missionName = templateMatch.Groups["mission"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(missionName))
                {
                    return missionName;
                }
            }
        }

        return null;
    }

    private static string? FindMissionFolderWithin(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return null;
        }

        return Directory.EnumerateDirectories(rootFolder, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(IsMissionFolder);
    }

    private static bool IsMissionFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return false;
        }

        return File.Exists(Path.Combine(folder, "cfgeconomycore.xml")) ||
            GetTypesPathForMission(folder) is not null;
    }

    private static string ResolveMissionFolder(string typesPath, string? preferredMissionFolder = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredMissionFolder) && IsPathUnderDirectory(typesPath, preferredMissionFolder))
        {
            return preferredMissionFolder;
        }

        var directory = Path.GetDirectoryName(typesPath) ?? string.Empty;
        var probe = directory;

        while (!string.IsNullOrWhiteSpace(probe))
        {
            if (File.Exists(Path.Combine(probe, "cfgeconomycore.xml")) ||
                File.Exists(Path.Combine(probe, "db", "types.xml")) ||
                File.Exists(Path.Combine(probe, "types.xml")))
            {
                return probe;
            }

            var parent = Path.GetDirectoryName(probe);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, probe, StringComparison.Ordinal))
            {
                break;
            }

            probe = parent;
        }

        if (!string.IsNullOrWhiteSpace(preferredMissionFolder))
        {
            return preferredMissionFolder;
        }

        var directoryName = Path.GetFileName(directory);
        return string.Equals(directoryName, "db", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(directory) ?? directory
            : directory;
    }

    private static bool IsPathUnderDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var normalizedPath = Path.GetFullPath(path);
        var normalizedDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
