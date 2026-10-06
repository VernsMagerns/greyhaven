using System.ComponentModel;
using System.Windows;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnViewModelChanged;
            if (e.NewValue is INotifyPropertyChanged vm) vm.PropertyChanged += OnViewModelChanged;
        };
    }

    // Each new screen starts at the top.
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Screen)) Scroller.ScrollToTop();
    }
}
