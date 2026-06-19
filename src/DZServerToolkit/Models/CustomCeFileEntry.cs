using DZServerToolkit.ViewModels;

namespace DZServerToolkit.Models;

public sealed class CustomCeFileEntry : ObservableObject
{
    private string _folder = string.Empty;
    private string _fileName = string.Empty;
    private string _type = string.Empty;
    private string _relativePath = string.Empty;
    private string _fullPath = string.Empty;
    private string _status = "OK";
    private string _issueSummary = string.Empty;
    private int _itemCount;

    public string Folder
    {
        get => _folder;
        set => SetProperty(ref _folder, value ?? string.Empty);
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value ?? string.Empty);
    }

    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value ?? string.Empty);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => SetProperty(ref _relativePath, value ?? string.Empty);
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetProperty(ref _fullPath, value ?? string.Empty);
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value ?? "OK"))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeBorderBrush));
                OnPropertyChanged(nameof(StatusBadgeForeground));
            }
        }
    }

    public string IssueSummary
    {
        get => _issueSummary;
        set
        {
            if (SetProperty(ref _issueSummary, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeBorderBrush));
                OnPropertyChanged(nameof(StatusBadgeForeground));
            }
        }
    }

    public int ItemCount
    {
        get => _itemCount;
        set => SetProperty(ref _itemCount, value);
    }

    public string StatusBadgeText
    {
        get
        {
            var summary = IssueSummary ?? string.Empty;
            if (summary.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return "Duplicate";
            }

            if (summary.Contains("wrong xml root", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("broken xml", StringComparison.OrdinalIgnoreCase))
            {
                return "Broken";
            }

            if (summary.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || summary.Contains("missing", StringComparison.OrdinalIgnoreCase))
            {
                return "Missing";
            }

            if (summary.Contains("valid but empty", StringComparison.OrdinalIgnoreCase))
            {
                return "Empty";
            }

            return string.IsNullOrWhiteSpace(Status) || string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase)
                ? "Ready"
                : Status.Trim();
        }
    }

    public string StatusBadgeBackground => StatusBadgeText switch
    {
        "Ready" => "#203421",
        "Broken" => "#442521",
        "Missing" => "#4A3518",
        "Duplicate" => "#3D2A4D",
        "Empty" => "#1F2F3B",
        _ => "#2A3021"
    };

    public string StatusBadgeBorderBrush => StatusBadgeText switch
    {
        "Ready" => "#4FA36A",
        "Broken" => "#C45C52",
        "Missing" => "#D3A34A",
        "Duplicate" => "#A879E6",
        "Empty" => "#65A7D5",
        _ => "#6A7D48"
    };

    public string StatusBadgeForeground => StatusBadgeText switch
    {
        "Ready" => "#DDF8DE",
        "Broken" => "#FFE0D8",
        "Missing" => "#FFF0C7",
        "Duplicate" => "#F1E4FF",
        "Empty" => "#DDEFFF",
        _ => "#F2EFE3"
    };
}
