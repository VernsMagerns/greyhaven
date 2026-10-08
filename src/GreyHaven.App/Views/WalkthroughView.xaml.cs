using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.App.Views;

public partial class WalkthroughView : UserControl
{
    /// <summary>Looks for USB sticks while "Make your USB stick" is showing.</summary>
    private readonly DispatcherTimer _usbPoll = new() { Interval = TimeSpan.FromSeconds(2) };
    private JourneyViewModel? _vm;

    public WalkthroughView()
    {
        InitializeComponent();
        _usbPoll.Tick += async (_, _) => { if (_vm?.Usb is { } usb) await usb.RefreshSticksAsync(); };
        DataContextChanged += (_, e) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnChanged;
            _vm = e.NewValue as JourneyViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnChanged;
            UpdatePolling();
        };
        Unloaded += (_, _) => _usbPoll.Stop();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JourneyViewModel.Usb)) UpdatePolling();
        if (e.PropertyName != nameof(JourneyViewModel.Index)) return;
        // Each new step starts at the top with keyboard focus on its title.
        Dispatcher.BeginInvoke(() =>
        {
            (Window.GetWindow(this)?.FindName("Scroller") as ScrollViewer)?.ScrollToTop();
            StepTitle.Focus();
            Keyboard.Focus(StepTitle);
        }, DispatcherPriority.Loaded);
    }

    private async void UpdatePolling()
    {
        if (_vm?.Usb is { } usb)
        {
            await usb.RefreshSticksAsync();
            _usbPoll.Start();
        }
        else _usbPoll.Stop();
    }
}
