using CMSFeeApp.WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CMSFeeApp.WinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void ScheduleType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
        {
            ViewModel.SelectedScheduleType = item.Tag?.ToString() switch
            {
                "PfsNational" => ScheduleType.PfsNational,
                "ClinicalLab" => ScheduleType.ClinicalLab,
                "AspDrug" => ScheduleType.AspDrug,
                "Opps" => ScheduleType.Opps,
                "Asc" => ScheduleType.Asc,
                _ => ScheduleType.Dmepos
            };
        }
    }
}
