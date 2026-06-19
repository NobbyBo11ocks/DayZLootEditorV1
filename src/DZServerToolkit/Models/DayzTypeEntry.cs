using System.Runtime.CompilerServices;
using System.Xml.Linq;
using DZServerToolkit.ViewModels;

namespace DZServerToolkit.Models;

public sealed class DayzTypeEntry : ObservableObject
{
    private string _name = string.Empty;
    private int _nominal;
    private int _lifetime;
    private int _restock;
    private int _min;
    private int _quantMin = -1;
    private int _quantMax = -1;
    private int _cost = 100;
    private bool _countInCargo;
    private bool _countInHoarder;
    private bool _countInMap = true;
    private bool _countInPlayer;
    private bool _crafted;
    private bool _deloot;
    private string _category = string.Empty;
    private string _tagsCsv = string.Empty;
    private string _usagesCsv = string.Empty;
    private string _valuesCsv = string.Empty;
    private string _issueSummary = string.Empty;
    private string _validationState = "OK";
    private bool _isDirty;
    private bool _suppressDirty;

    public XElement? SourceElement { get; set; }

    public string Name
    {
        get => _name;
        set => SetAndMarkDirty(ref _name, value?.Trim() ?? string.Empty);
    }

    public int Nominal
    {
        get => _nominal;
        set => SetAndMarkDirty(ref _nominal, value);
    }

    public int Lifetime
    {
        get => _lifetime;
        set => SetAndMarkDirty(ref _lifetime, value);
    }

    public int Restock
    {
        get => _restock;
        set => SetAndMarkDirty(ref _restock, value);
    }

    public int Min
    {
        get => _min;
        set => SetAndMarkDirty(ref _min, value);
    }

    public int QuantMin
    {
        get => _quantMin;
        set => SetAndMarkDirty(ref _quantMin, value);
    }

    public int QuantMax
    {
        get => _quantMax;
        set => SetAndMarkDirty(ref _quantMax, value);
    }

    public int Cost
    {
        get => _cost;
        set => SetAndMarkDirty(ref _cost, value);
    }

    public bool CountInCargo
    {
        get => _countInCargo;
        set => SetAndMarkDirty(ref _countInCargo, value);
    }

    public bool CountInHoarder
    {
        get => _countInHoarder;
        set => SetAndMarkDirty(ref _countInHoarder, value);
    }

    public bool CountInMap
    {
        get => _countInMap;
        set => SetAndMarkDirty(ref _countInMap, value);
    }

    public bool CountInPlayer
    {
        get => _countInPlayer;
        set => SetAndMarkDirty(ref _countInPlayer, value);
    }

    public bool Crafted
    {
        get => _crafted;
        set => SetAndMarkDirty(ref _crafted, value);
    }

    public bool Deloot
    {
        get => _deloot;
        set => SetAndMarkDirty(ref _deloot, value);
    }

    public string Category
    {
        get => _category;
        set => SetAndMarkDirty(ref _category, value?.Trim() ?? string.Empty);
    }

    public string TagsCsv
    {
        get => _tagsCsv;
        set => SetAndMarkDirty(ref _tagsCsv, NormalizeCsv(value));
    }

    public string UsagesCsv
    {
        get => _usagesCsv;
        set => SetAndMarkDirty(ref _usagesCsv, NormalizeCsv(value));
    }

    public string ValuesCsv
    {
        get => _valuesCsv;
        set => SetAndMarkDirty(ref _valuesCsv, NormalizeCsv(value));
    }

    public string IssueSummary
    {
        get => _issueSummary;
        set
        {
            if (SetProperty(ref _issueSummary, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasIssues));
            }
        }
    }

    public string ValidationState
    {
        get => _validationState;
        set => SetProperty(ref _validationState, value ?? "OK");
    }

    public bool HasIssues => !string.IsNullOrWhiteSpace(IssueSummary);

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public void AcceptClean()
    {
        _suppressDirty = true;
        IsDirty = false;
        _suppressDirty = false;
    }

    public DayzTypeEntry Clone()
    {
        var clone = new DayzTypeEntry
        {
            Name = Name,
            Nominal = Nominal,
            Lifetime = Lifetime,
            Restock = Restock,
            Min = Min,
            QuantMin = QuantMin,
            QuantMax = QuantMax,
            Cost = Cost,
            CountInCargo = CountInCargo,
            CountInHoarder = CountInHoarder,
            CountInMap = CountInMap,
            CountInPlayer = CountInPlayer,
            Crafted = Crafted,
            Deloot = Deloot,
            Category = Category,
            TagsCsv = TagsCsv,
            UsagesCsv = UsagesCsv,
            ValuesCsv = ValuesCsv,
            IssueSummary = IssueSummary,
            ValidationState = ValidationState,
            SourceElement = SourceElement is null ? null : new XElement(SourceElement)
        };

        clone.AcceptClean();
        return clone;
    }

    private bool SetAndMarkDirty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        var changed = SetProperty(ref storage, value, propertyName);
        if (changed && !_suppressDirty)
        {
            IsDirty = true;
        }

        return changed;
    }

    private static string NormalizeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var parts = value.Split(new[] { ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join(", ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
