using System.Windows;
using System.Windows.Controls;
using GreyHaven.App.Infrastructure;
using GreyHaven.Presentation.ViewModels;

namespace GreyHaven.App.Views;

public partial class ResultsView : UserControl
{
    public ResultsView() => InitializeComponent();

    /// <summary>"About Ubuntu: pros and cons": a small window with the same words the report has.</summary>
    private void About_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PathOptionViewModel { About: { } about } path } button)
            InfoWindow.Show(Window.GetWindow(this), path.AboutLabel, about, button);
    }
}
