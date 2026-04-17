using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobRendersetViewModel : ObservableObject
{
    private bool _hasExplicitSelection;
    private List<string> _selectedContextNames = [];

    public JobRendersetViewModel()
    {
        Contexts.CollectionChanged += OnContextsCollectionChanged;
    }

    public ObservableCollection<RendersetContextViewModel> Contexts { get; } = [];

    public IReadOnlyList<string> SelectedContextNames => _selectedContextNames;

    public bool HasContexts => Contexts.Count > 0;

    public int TotalContextCount => Contexts.Count;

    public int SelectedContextCount => Contexts.Count(static context => context.IsSelected);

    public bool CanRenderSelectedContexts => !UseRenderset || SelectedContextCount > 0;

    public string SummaryText
    {
        get
        {
            if (!HasContexts)
            {
                return "No RenderSet contexts loaded. Click Update.";
            }

            return UseRenderset
                ? $"{SelectedContextCount}/{TotalContextCount} contexts selected"
                : $"{TotalContextCount} contexts found";
        }
    }

    public string CurrentContextSummary
    {
        get
        {
            if (!UseRenderset)
            {
                return "RenderSet is off.";
            }

            if (string.IsNullOrWhiteSpace(CurrentContextName))
            {
                return SelectedContextCount == 0 ? "No contexts selected." : "Waiting for context.";
            }

            return string.IsNullOrWhiteSpace(ContextProgressText)
                ? CurrentContextName.Trim()
                : $"{CurrentContextName.Trim()} | {ContextProgressText.Trim()}";
        }
    }

    public void ApplyInspection(RendersetInspectionSnapshot? inspection)
    {
        var explicitSelection = new HashSet<string>(
            _hasExplicitSelection ? _selectedContextNames : SelectedContextNames,
            StringComparer.Ordinal);

        Contexts.Clear();
        if (inspection is null)
        {
            NotifyContextCollectionChanged();
            return;
        }

        foreach (var context in inspection.Contexts.OrderBy(static context => context.Index))
        {
            var selected = _hasExplicitSelection
                ? explicitSelection.Contains(context.Name)
                : context.IncludeInRenderAll;
            Contexts.Add(new RendersetContextViewModel(context, selected));
        }

        SyncSelectedContextNames();
        NotifyContextCollectionChanged();
    }

    public void InitializeSelection(IEnumerable<string>? contextNames, bool hasExplicitSelection = false)
    {
        _selectedContextNames = (contextNames ?? [])
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _hasExplicitSelection = hasExplicitSelection || _selectedContextNames.Count > 0;
        OnPropertyChanged(nameof(SelectedContextNames));
    }

    public void ResetRuntimeContextProgress()
    {
        CurrentContextName = string.Empty;
        ContextProgressValue = 0;
        ContextProgressText = "Waiting for context.";
        CompletedContextCount = 0;
        TotalRuntimeContextCount = Math.Max(0, SelectedContextCount);
    }

    [ObservableProperty]
    private int completedContextCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentContextSummary))]
    private string contextProgressText = "Waiting for context.";

    [ObservableProperty]
    private double contextProgressValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentContextSummary))]
    private string currentContextName = string.Empty;

    [ObservableProperty]
    private int totalRuntimeContextCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CanRenderSelectedContexts))]
    [NotifyPropertyChangedFor(nameof(CurrentContextSummary))]
    private bool useRenderset;

    private void OnContextsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<RendersetContextViewModel>())
            {
                item.PropertyChanged -= OnContextPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<RendersetContextViewModel>())
            {
                item.PropertyChanged += OnContextPropertyChanged;
            }
        }

        SyncSelectedContextNames();
        NotifyContextCollectionChanged();
    }

    private void OnContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RendersetContextViewModel.IsSelected))
        {
            _hasExplicitSelection = true;
            SyncSelectedContextNames();
            NotifyContextCollectionChanged();
        }
    }

    private void SyncSelectedContextNames()
    {
        _selectedContextNames = ComputeSelectedContextNames();
    }

    private List<string> ComputeSelectedContextNames()
    {
        return Contexts
            .Where(static context => context.IsSelected)
            .Select(static context => context.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();
    }

    private void NotifyContextCollectionChanged()
    {
        OnPropertyChanged(nameof(HasContexts));
        OnPropertyChanged(nameof(TotalContextCount));
        OnPropertyChanged(nameof(SelectedContextCount));
        OnPropertyChanged(nameof(SelectedContextNames));
        OnPropertyChanged(nameof(CanRenderSelectedContexts));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(CurrentContextSummary));
    }
}
