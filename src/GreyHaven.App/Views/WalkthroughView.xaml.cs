using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.App.Views;

public partial class WalkthroughView : UserControl
{
    public WalkthroughView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is WalkthroughViewModel old) old.PropertyChanged -= OnStepChanged;
            if (e.NewValue is WalkthroughViewModel vm) vm.PropertyChanged += OnStepChanged;
        };
    }

    /// <summary>Each new step starts at the top with keyboard focus on its title.</summary>
    private void OnStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WalkthroughViewModel.Index)) return;
        Dispatcher.BeginInvoke(() =>
        {
            (Window.GetWindow(this)?.FindName("Scroller") as ScrollViewer)?.ScrollToTop();
            StepTitle.Focus();
            Keyboard.Focus(StepTitle);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
