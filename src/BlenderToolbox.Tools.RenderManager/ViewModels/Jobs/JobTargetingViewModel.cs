using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobTargetingViewModel : ObservableObject
{
    public const string BlenderDefaultSelection = "Blender Default";

    private CancellationTokenSource? _inspectionCts;
    private SelectionSnapshot _selectionBeforeInspection = SelectionSnapshot.Empty;

    public bool HasInspection => Inspection is not null;

    public string InspectionSummary
    {
        get
        {
            if (InspectionState == InspectionState.Inspecting)
            {
                return "Inspecting blend...";
            }

            if (InspectionState == InspectionState.Failed)
            {
                return "Inspection failed. Click Update.";
            }

            if (Inspection is null)
            {
                return "Blend defaults are not inspected yet.";
            }

            return $"Inspected {Inspection.InspectedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        }
    }

    public bool HasSceneOverride
    {
        get => SceneOverrideEnabled;
        set
        {
            if (value == HasSceneOverride)
            {
                return;
            }

            SceneOverrideEnabled = value;
            SceneName = value ? ResolvedSceneName : string.Empty;
            OnPropertyChanged(nameof(HasSceneOverride));
            OnPropertyChanged(nameof(IsSceneSelectorEnabled));
            OnPropertyChanged(nameof(SceneSelection));
        }
    }

    public bool HasCameraOverride
    {
        get => CameraOverrideEnabled;
        set
        {
            if (value == HasCameraOverride)
            {
                return;
            }

            CameraOverrideEnabled = value;
            CameraName = value ? ResolvedCameraName : string.Empty;
            OnPropertyChanged(nameof(HasCameraOverride));
            OnPropertyChanged(nameof(IsCameraSelectorEnabled));
            OnPropertyChanged(nameof(CameraSelection));
        }
    }

    public bool HasViewLayerOverride
    {
        get => ViewLayerOverrideEnabled;
        set
        {
            if (value == HasViewLayerOverride)
            {
                return;
            }

            ViewLayerOverrideEnabled = value;
            ViewLayerName = value ? ResolvedViewLayerName : string.Empty;
            OnPropertyChanged(nameof(HasViewLayerOverride));
            OnPropertyChanged(nameof(IsViewLayerSelectorEnabled));
            OnPropertyChanged(nameof(ViewLayerSelection));
        }
    }

    public bool IsInspectionReady => InspectionState == InspectionState.Ready;

    public bool IsSceneSelectorEnabled => HasSceneOverride && IsInspectionReady;

    public bool IsCameraSelectorEnabled => HasCameraOverride && IsInspectionReady;

    public bool IsViewLayerSelectorEnabled => HasViewLayerOverride && IsInspectionReady;

    public string ResolvedSceneName => ResolveOverride(SceneName, Inspection?.SceneName);

    public string ResolvedCameraName => ResolveOverride(CameraName, Inspection?.CameraName);

    public string ResolvedViewLayerName => ResolveOverride(ViewLayerName, Inspection?.ViewLayerName);

    public IReadOnlyList<string> AvailableCameraNames
    {
        get
        {
            if (Inspection?.SceneCameras is not { Count: > 0 } sceneCameras)
            {
                return Inspection?.AvailableCameras ?? [];
            }

            var sceneKey = ResolvedSceneName;
            if (!string.IsNullOrWhiteSpace(sceneKey) && sceneCameras.TryGetValue(sceneKey, out var cameras) && cameras.Count > 0)
            {
                return cameras;
            }

            return Inspection?.AvailableCameras ?? [];
        }
    }

    public IReadOnlyList<string> AvailableSceneNames => Inspection?.AvailableScenes ?? [];

    public IReadOnlyList<string> AvailableSceneOptions => BuildDefaultOptions(AvailableSceneNames);

    public IReadOnlyList<string> AvailableViewLayerNames
    {
        get
        {
            if (Inspection?.SceneViewLayers is not { Count: > 0 } sceneViewLayers)
            {
                return Inspection?.AvailableViewLayers ?? [];
            }

            var sceneKey = ResolvedSceneName;
            if (!string.IsNullOrWhiteSpace(sceneKey) && sceneViewLayers.TryGetValue(sceneKey, out var viewLayers) && viewLayers.Count > 0)
            {
                return viewLayers;
            }

            return Inspection?.AvailableViewLayers ?? [];
        }
    }

    public IReadOnlyList<string> AvailableCameraOptions => BuildDefaultOptions(AvailableCameraNames);

    public IReadOnlyList<string> AvailableViewLayerOptions => BuildDefaultOptions(AvailableViewLayerNames);

    public string SceneSelection
    {
        get => HasSceneOverride && !string.IsNullOrWhiteSpace(SceneName)
            ? SceneName.Trim()
            : BlenderDefaultSelection;
        set => ApplyComboSelection(
            value,
            selectedValue =>
            {
                SceneOverrideEnabled = !string.IsNullOrWhiteSpace(selectedValue);
                SceneName = selectedValue;
            });
    }

    public string CameraSelection
    {
        get => HasCameraOverride && !string.IsNullOrWhiteSpace(CameraName)
            ? CameraName.Trim()
            : BlenderDefaultSelection;
        set => ApplyComboSelection(
            value,
            selectedValue =>
            {
                CameraOverrideEnabled = !string.IsNullOrWhiteSpace(selectedValue);
                CameraName = selectedValue;
            });
    }

    public string ViewLayerSelection
    {
        get => HasViewLayerOverride && !string.IsNullOrWhiteSpace(ViewLayerName)
            ? ViewLayerName.Trim()
            : BlenderDefaultSelection;
        set => ApplyComboSelection(
            value,
            selectedValue =>
            {
                ViewLayerOverrideEnabled = !string.IsNullOrWhiteSpace(selectedValue);
                ViewLayerName = selectedValue;
            });
    }

    public string SceneHint => BuildTargetHint(SceneName, AvailableSceneNames, Inspection?.SceneName, "scene");

    public string CameraHint => BuildTargetHint(CameraName, AvailableCameraNames, Inspection?.CameraName, "camera");

    public string ViewLayerHint => BuildTargetHint(ViewLayerName, AvailableViewLayerNames, Inspection?.ViewLayerName, "view layer");

    public string SceneSelectionPreservationHint => BuildSelectionPreservationHint(
        _selectionBeforeInspection.SceneName,
        SceneName,
        AvailableSceneNames,
        "scene");

    public string CameraSelectionPreservationHint => BuildSelectionPreservationHint(
        _selectionBeforeInspection.CameraName,
        CameraName,
        AvailableCameraNames,
        "camera");

    public string ViewLayerSelectionPreservationHint => BuildSelectionPreservationHint(
        _selectionBeforeInspection.ViewLayerName,
        ViewLayerName,
        AvailableViewLayerNames,
        "view layer");

    public CancellationToken BeginInspection()
    {
        _inspectionCts?.Cancel();
        _inspectionCts?.Dispose();
        _inspectionCts = new CancellationTokenSource();
        _selectionBeforeInspection = new SelectionSnapshot(SceneName, CameraName, ViewLayerName);
        InspectionState = InspectionState.Inspecting;
        NotifySelectionPreservationChanged();
        return _inspectionCts.Token;
    }

    public void ApplyInspection(BlendInspectionSnapshot inspection, bool preserveSelections = true)
    {
        Inspection = inspection;
        InspectionState = InspectionState.Ready;

        if (preserveSelections)
        {
            PreserveSelectionIfAvailable(_selectionBeforeInspection);
        }

        NotifySelectionPreservationChanged();
    }

    [ObservableProperty]
    private bool cameraOverrideEnabled;

    [ObservableProperty]
    private string cameraName = string.Empty;

    [ObservableProperty]
    private BlendInspectionSnapshot? inspection;

    [ObservableProperty]
    private InspectionState inspectionState = InspectionState.NotInspected;

    [ObservableProperty]
    private string sceneName = string.Empty;

    [ObservableProperty]
    private bool sceneOverrideEnabled;

    [ObservableProperty]
    private string viewLayerName = string.Empty;

    [ObservableProperty]
    private bool viewLayerOverrideEnabled;

    partial void OnCameraNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasCameraOverride));
        OnPropertyChanged(nameof(ResolvedCameraName));
        OnPropertyChanged(nameof(CameraSelection));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(CameraSelectionPreservationHint));
    }

    partial void OnCameraOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasCameraOverride));
        OnPropertyChanged(nameof(IsCameraSelectorEnabled));
        OnPropertyChanged(nameof(CameraSelection));
    }

    partial void OnInspectionChanged(BlendInspectionSnapshot? value)
    {
        NotifyInspectionChanged();
    }

    partial void OnInspectionStateChanged(InspectionState value)
    {
        NotifyInspectionChanged();
    }

    partial void OnSceneNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasSceneOverride));
        OnPropertyChanged(nameof(ResolvedSceneName));
        OnPropertyChanged(nameof(AvailableCameraNames));
        OnPropertyChanged(nameof(AvailableCameraOptions));
        OnPropertyChanged(nameof(AvailableViewLayerNames));
        OnPropertyChanged(nameof(AvailableViewLayerOptions));
        OnPropertyChanged(nameof(SceneSelection));
        OnPropertyChanged(nameof(CameraSelection));
        OnPropertyChanged(nameof(ViewLayerSelection));
        OnPropertyChanged(nameof(SceneHint));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(ViewLayerHint));
        OnPropertyChanged(nameof(SceneSelectionPreservationHint));
    }

    partial void OnSceneOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSceneOverride));
        OnPropertyChanged(nameof(IsSceneSelectorEnabled));
        OnPropertyChanged(nameof(SceneSelection));
    }

    partial void OnViewLayerNameChanged(string value)
    {
        OnPropertyChanged(nameof(HasViewLayerOverride));
        OnPropertyChanged(nameof(ResolvedViewLayerName));
        OnPropertyChanged(nameof(ViewLayerSelection));
        OnPropertyChanged(nameof(ViewLayerHint));
        OnPropertyChanged(nameof(ViewLayerSelectionPreservationHint));
    }

    partial void OnViewLayerOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasViewLayerOverride));
        OnPropertyChanged(nameof(IsViewLayerSelectorEnabled));
        OnPropertyChanged(nameof(ViewLayerSelection));
    }

    private void PreserveSelectionIfAvailable(SelectionSnapshot selection)
    {
        if (SceneOverrideEnabled && IsAvailable(selection.SceneName, AvailableSceneNames))
        {
            SceneName = selection.SceneName.Trim();
        }

        if (CameraOverrideEnabled && IsAvailable(selection.CameraName, AvailableCameraNames))
        {
            CameraName = selection.CameraName.Trim();
        }

        if (ViewLayerOverrideEnabled && IsAvailable(selection.ViewLayerName, AvailableViewLayerNames))
        {
            ViewLayerName = selection.ViewLayerName.Trim();
        }
    }

    private void NotifyInspectionChanged()
    {
        OnPropertyChanged(nameof(AvailableCameraNames));
        OnPropertyChanged(nameof(AvailableCameraOptions));
        OnPropertyChanged(nameof(AvailableSceneNames));
        OnPropertyChanged(nameof(AvailableSceneOptions));
        OnPropertyChanged(nameof(AvailableViewLayerNames));
        OnPropertyChanged(nameof(AvailableViewLayerOptions));
        OnPropertyChanged(nameof(CameraHint));
        OnPropertyChanged(nameof(CameraSelection));
        OnPropertyChanged(nameof(HasCameraOverride));
        OnPropertyChanged(nameof(HasInspection));
        OnPropertyChanged(nameof(HasSceneOverride));
        OnPropertyChanged(nameof(HasViewLayerOverride));
        OnPropertyChanged(nameof(InspectionSummary));
        OnPropertyChanged(nameof(IsInspectionReady));
        OnPropertyChanged(nameof(IsSceneSelectorEnabled));
        OnPropertyChanged(nameof(IsCameraSelectorEnabled));
        OnPropertyChanged(nameof(IsViewLayerSelectorEnabled));
        OnPropertyChanged(nameof(ResolvedCameraName));
        OnPropertyChanged(nameof(ResolvedSceneName));
        OnPropertyChanged(nameof(ResolvedViewLayerName));
        OnPropertyChanged(nameof(SceneHint));
        OnPropertyChanged(nameof(SceneSelection));
        OnPropertyChanged(nameof(ViewLayerHint));
        OnPropertyChanged(nameof(ViewLayerSelection));
        NotifySelectionPreservationChanged();
    }

    private void NotifySelectionPreservationChanged()
    {
        OnPropertyChanged(nameof(SceneSelectionPreservationHint));
        OnPropertyChanged(nameof(CameraSelectionPreservationHint));
        OnPropertyChanged(nameof(ViewLayerSelectionPreservationHint));
    }

    private string BuildTargetHint(
        string selectedValue,
        IReadOnlyList<string> availableValues,
        string? inspectedValue,
        string label)
    {
        if (InspectionState == InspectionState.NotInspected)
        {
            return "Waiting for inspection...";
        }

        if (InspectionState == InspectionState.Inspecting)
        {
            return "Inspecting blend...";
        }

        if (InspectionState == InspectionState.Failed)
        {
            return "Inspection failed. Click Update.";
        }

        if (!string.IsNullOrWhiteSpace(selectedValue) &&
            availableValues.Count > 0 &&
            !availableValues.Any(value => string.Equals(value, selectedValue.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return $"'{selectedValue.Trim()}' is not in scene '{ResolvedSceneName}'. Select another.";
        }

        return string.IsNullOrWhiteSpace(inspectedValue)
            ? $"{BlenderDefaultSelection} = use {label} from blend."
            : $"{BlenderDefaultSelection} = from blend: {inspectedValue.Trim()}";
    }

    private static IReadOnlyList<string> BuildDefaultOptions(IReadOnlyList<string> availableValues)
    {
        if (availableValues.Count == 0)
        {
            return [BlenderDefaultSelection];
        }

        return [BlenderDefaultSelection, .. availableValues];
    }

    private static void ApplyComboSelection(string? value, Action<string> applySelection)
    {
        var selectedValue = string.Equals(value?.Trim(), BlenderDefaultSelection, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value?.Trim() ?? string.Empty;

        applySelection(selectedValue);
    }

    private static string BuildSelectionPreservationHint(
        string previousValue,
        string selectedValue,
        IReadOnlyList<string> availableValues,
        string label)
    {
        if (string.IsNullOrWhiteSpace(previousValue))
        {
            return string.Empty;
        }

        if (string.Equals(previousValue.Trim(), selectedValue.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return $"Previous {label} selection preserved.";
        }

        return IsAvailable(previousValue, availableValues)
            ? $"Previous {label} selection is still available."
            : $"Previous {label} selection is no longer available.";
    }

    private static bool IsAvailable(string value, IReadOnlyList<string> availableValues)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               availableValues.Any(candidate => string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveOverride(string overrideValue, string? inheritedValue)
    {
        return string.IsNullOrWhiteSpace(overrideValue)
            ? inheritedValue?.Trim() ?? string.Empty
            : overrideValue.Trim();
    }

    private readonly record struct SelectionSnapshot(string SceneName, string CameraName, string ViewLayerName)
    {
        public static SelectionSnapshot Empty { get; } = new(string.Empty, string.Empty, string.Empty);
    }
}
