using System;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CatalogoDrawerItem : ViewModelBase
{
    private string code = "";
    private string name = "";
    private string detail = "";
    private string tag = "";
    private string badgeColor = "";
    private decimal numberValue;
    private int usedCount;
    private bool isSelected;
    private bool isEditingTag;
    private string editTagText = "";

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public bool IsEditingTag
    {
        get => isEditingTag;
        set => SetProperty(ref isEditingTag, value);
    }

    public string EditTagText
    {
        get => editTagText;
        set => SetProperty(ref editTagText, value);
    }

    public bool CanEditTag => Category is "Artefactos" or "Alim" or "Sistemas";
    public bool HasTag => !string.IsNullOrWhiteSpace(Tag);
    public bool ShowTagBadge => HasTag || CanEditTag;
    public string DisplayTag => string.IsNullOrWhiteSpace(Tag) ? "+ Tag" : Tag;

    private string displayName = "";
    public string DisplayName
    {
        get => string.IsNullOrWhiteSpace(displayName) ? Code : displayName;
        set => SetProperty(ref displayName, value);
    }

    private string subtitle = "";
    public string Subtitle
    {
        get => string.IsNullOrWhiteSpace(subtitle) ? Detail : subtitle;
        set => SetProperty(ref subtitle, value);
    }

    private string mermaText = "";
    public string MermaText
    {
        get => mermaText;
        set
        {
            if (SetProperty(ref mermaText, value))
                OnPropertyChanged(nameof(HasMerma));
        }
    }
    public bool HasMerma => !string.IsNullOrWhiteSpace(MermaText);

    public string Category { get; set; } = "";

    public string Code
    {
        get => code;
        set => SetProperty(ref code, value);
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public string Detail
    {
        get => detail;
        set => SetProperty(ref detail, value);
    }

    public string Tag
    {
        get => tag;
        set
        {
            if (SetProperty(ref tag, value))
            {
                OnPropertyChanged(nameof(HasTag));
                OnPropertyChanged(nameof(ShowTagBadge));
                OnPropertyChanged(nameof(DisplayTag));
            }
        }
    }

    public string BadgeColor
    {
        get => badgeColor;
        set => SetProperty(ref badgeColor, value);
    }

    public decimal NumberValue
    {
        get => numberValue;
        set => SetProperty(ref numberValue, value);
    }

    public int UsedCount
    {
        get => usedCount;
        set => SetProperty(ref usedCount, value);
    }

    private decimal alturaBajada;
    public decimal AlturaBajada
    {
        get => alturaBajada;
        set
        {
            if (SetProperty(ref alturaBajada, value))
            {
                OnPropertyChanged(nameof(AlturaBajadaText));
                OnPropertyChanged(nameof(HasAlturaBajada));
            }
        }
    }
    public string AlturaBajadaText => $"{AlturaBajada:0.##} m";
    public bool HasAlturaBajada => Category is "Artefactos";

    public object? RawItem { get; set; }
}
