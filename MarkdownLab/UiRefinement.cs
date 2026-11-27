using System.Windows;
using System.Windows.Controls;
namespace MarkdownLab;
public partial class MainWindow
{
    private void ChromeMin_Click(object sender,RoutedEventArgs e)=>SystemCommands.MinimizeWindow(this);
    private void ChromeMax_Click(object sender,RoutedEventArgs e){if(WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(this);else SystemCommands.MaximizeWindow(this);}
    private void ShowMarkdownGuide()
    {
        var panel=new StackPanel{Margin=new Thickness(20),Width=480};
        panel.Children.Add(new TextBlock{Text=L.T("Guía rápida de Markdown"),FontSize=23,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        foreach(var pair in new[]{(L.T("# Título"),L.T("Encabezado de nivel 1")),("**texto**",L.T("Negrita")),("*texto*",L.T("Cursiva")),("~~texto~~",L.T("Tachado")),("- elemento",L.T("Lista")),("1. elemento",L.T("Lista numerada")),("- [ ] tarea",L.T("Casilla de tarea")),("[texto](URL)",L.T("Enlace")),("![imagen](URL)",L.T("Imagen")),("`código`",L.T("Código en línea"))})
        {
            var row=new DockPanel{Margin=new Thickness(0,4,0,4)};
            var syntax=new TextBlock{Text=pair.Item1,FontFamily=new System.Windows.Media.FontFamily("Consolas"),Width=230,Foreground=(System.Windows.Media.Brush)Resources["Muted"]};
            DockPanel.SetDock(syntax,Dock.Left);row.Children.Add(syntax);row.Children.Add(new TextBlock{Text=pair.Item2});panel.Children.Add(row);
        }
        var close=new Button{Content=L.T("Cerrar"),IsCancel=true,IsDefault=true,Margin=new Thickness(0,12,0,0)};panel.Children.Add(close);
        var dialog=Dialog(L.T("Guía Markdown"),panel);dialog.ShowDialog();
    }
}
