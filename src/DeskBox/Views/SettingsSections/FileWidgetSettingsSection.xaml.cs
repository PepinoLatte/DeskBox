using DeskBox.Models;
using DeskBox.Services;
using DeskBox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

public sealed partial class FileWidgetSettingsSection : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SettingsViewModel),
            typeof(FileWidgetSettingsSection),
            new PropertyMetadata(null));

    // Depth counter instead of a bool: rebuilding the picker raises nested
    // SelectionChanged/Toggled callbacks that must stay suppressed until the
    // outermost refresh finishes.
    private int _perWidgetEventDepth;
    private string? _selectedPerWidgetId;

    public FileWidgetSettingsSection()
    {
        InitializeComponent();
        Loaded += OnSectionLoaded;
        Unloaded += OnSectionUnloaded;
    }

    public SettingsViewModel? ViewModel
    {
        get => (SettingsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<SettingsSectionNavigationRequestedEventArgs>? NavigationRequested;

    private void NestedSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionTag })
        {
            NavigationRequested?.Invoke(this, new SettingsSectionNavigationRequestedEventArgs(sectionTag));
        }
    }

    private void OrganizeDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        global::DeskBox.App.Current.ShowDesktopOrganizationWindow();
    }

    private void OnSectionLoaded(object sender, RoutedEventArgs e)
    {
        var app = global::DeskBox.App.Current;
        app.SettingsService.SettingsChanged += OnSettingsChangedForPerWidget;
        app.LocalizationService.LanguageChanged += OnLanguageChangedForPerWidget;
        RefreshPerWidgetManagement();
    }

    private void OnSectionUnloaded(object sender, RoutedEventArgs e)
    {
        var app = global::DeskBox.App.Current;
        app.SettingsService.SettingsChanged -= OnSettingsChangedForPerWidget;
        app.LocalizationService.LanguageChanged -= OnLanguageChangedForPerWidget;
    }

    private void OnSettingsChangedForPerWidget()
    {
        RunOnUiDispatcher(RefreshPerWidgetManagement);
    }

    private void OnLanguageChangedForPerWidget()
    {
        RunOnUiDispatcher(RefreshPerWidgetManagement);
    }

    private static void RunOnUiDispatcher(Action action)
    {
        if (global::DeskBox.App.UiDispatcherQueue is { } dispatcherQueue &&
            !dispatcherQueue.HasThreadAccess)
        {
            dispatcherQueue.TryEnqueue(() => action());
            return;
        }

        action();
    }

    private void RefreshPerWidgetManagement()
    {
        var settings = global::DeskBox.App.Current.SettingsService.Settings;
        List<WidgetConfig> fileWidgets = settings.Widgets
            .Where(widget => widget.WidgetKind == WidgetKind.File)
            .ToList();

        _perWidgetEventDepth++;
        try
        {
            PerWidgetPicker.PlaceholderText = global::DeskBox.App.Current
                .LocalizationService.T("Settings.FileWidget.PerWidget.Placeholder");
            PerWidgetPicker.Items.Clear();
            foreach (WidgetConfig widget in fileWidgets)
            {
                PerWidgetPicker.Items.Add(new ComboBoxItem
                {
                    Content = widget.Name,
                    Tag = widget.Id,
                    IsEnabled = true
                });
            }

            int selectedIndex = fileWidgets.FindIndex(
                widget => widget.Id == _selectedPerWidgetId);
            if (selectedIndex < 0 && fileWidgets.Count > 0)
            {
                selectedIndex = 0;
            }

            PerWidgetPicker.SelectedIndex = selectedIndex;
            _selectedPerWidgetId = selectedIndex >= 0
                ? fileWidgets[selectedIndex].Id
                : null;
            PerWidgetIconLabelToggle.IsEnabled = selectedIndex >= 0;
            ApplyPerWidgetToggleState();
        }
        finally
        {
            _perWidgetEventDepth--;
        }
    }

    private WidgetConfig? GetSelectedPerWidget()
    {
        if (PerWidgetPicker.SelectedItem is not ComboBoxItem { Tag: string id })
        {
            return null;
        }

        return global::DeskBox.App.Current.SettingsService.Settings.Widgets
            .FirstOrDefault(widget => widget.Id == id);
    }

    private void ApplyPerWidgetToggleState()
    {
        WidgetConfig? config = GetSelectedPerWidget();
        bool hidden = FileWidgetIconLayout.ResolveEffectiveFileNameLineCount(
            global::DeskBox.App.Current.SettingsService.Settings.FileNameLineCount,
            config?.IconLabelHiddenOverride) == SettingsService.HiddenFileNameLineCount;
        _perWidgetEventDepth++;
        try
        {
            PerWidgetIconLabelToggle.IsOn = hidden;
        }
        finally
        {
            _perWidgetEventDepth--;
        }
    }

    private void PerWidgetPicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_perWidgetEventDepth > 0)
        {
            return;
        }

        _selectedPerWidgetId =
            (PerWidgetPicker.SelectedItem as ComboBoxItem)?.Tag as string;
        ApplyPerWidgetToggleState();
    }

    private void PerWidgetIconLabelToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_perWidgetEventDepth > 0)
        {
            return;
        }

        WidgetConfig? config = GetSelectedPerWidget();
        if (config is null)
        {
            return;
        }

        bool desiredHidden = PerWidgetIconLabelToggle.IsOn;
        bool globalHidden = FileWidgetIconLayout.ResolveEffectiveFileNameLineCount(
            global::DeskBox.App.Current.SettingsService.Settings.FileNameLineCount,
            null) == SettingsService.HiddenFileNameLineCount;

        // Store the minimal override: clear it whenever the desired state
        // already matches the global setting, so the widget keeps following
        // future global changes until the user overrides it again.
        config.IconLabelHiddenOverride = desiredHidden == globalHidden ? null : desiredHidden;
        global::DeskBox.App.Current.SettingsService.SaveDebounced();
    }
}
