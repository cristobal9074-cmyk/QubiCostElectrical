using System.Windows;
namespace QubiCostElectrical.Wpf.Features.Costo;
public partial class ViaticosActionsSection : System.Windows.Controls.UserControl
{
 public ViaticosActionsSection()=>InitializeComponent();
 private void OnCancel(object s,RoutedEventArgs e){if(Window.GetWindow(this) is ViaticosProfesionalDialog d){d.DialogResult=false;d.Close();}}
 private void OnApply(object s,RoutedEventArgs e){if(Window.GetWindow(this) is ViaticosProfesionalDialog d){d.DialogResult=true;d.Close();}}
}
