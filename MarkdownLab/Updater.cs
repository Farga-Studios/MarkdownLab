using System.IO;
using System.Net.Http;
using Suite.Updater;

using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
namespace MarkdownLab;
public partial class MainWindow
{
    private static readonly HttpClient updateHttp = new(){Timeout=TimeSpan.FromMinutes(3)};
    private bool checkingUpdate;
    private const string ReleasePage="https://github.com/Farga-Studios/MarkdownLab/releases/latest";
    private async void CheckUpdates_Click(object sender,RoutedEventArgs e)=>await CheckUpdatesAsync(true);
    private async Task CheckUpdatesAsync(bool manual)
    {
        if(checkingUpdate)return;
        checkingUpdate=true;
        try
        {
            var assembly=Assembly.GetExecutingAssembly();
            using var keyStream=assembly.GetManifestResourceStream("MarkdownLab.Updates.PublicKey.pem");
            if(keyStream is null){if(manual)UpdateNotice(L.T("La firma de actualizaciones aún no está configurada."));return;}
            using var reader=new StreamReader(keyStream);
            var client=new UpdateClient(updateHttp,new Uri("https://raw.githubusercontent.com/Farga-Studios/MarkdownLab/main/channels/stable/update.json"),await reader.ReadToEndAsync(),"MarkdownLab","stable");
            var manifest=await client.CheckAsync(assembly.GetName().Version??new Version(0,0,0));
            if(manifest is null){if(manual)UpdateNotice(L.T("Ya tienes la última versión."));return;}
            var tag="v"+manifest.Version;
            var installed=InstallerHandoff.IsInstalled(AppContext.BaseDirectory,"Sealsworn.MarkdownLab");
            // Single-file apps keep the installed marker next to the executable.
            var panel=new StackPanel{Margin=new Thickness(20),Width=460};
            panel.Children.Add(new TextBlock{Text="MarkdownLab "+tag+L.T(" disponible"),FontSize=21,FontWeight=FontWeights.SemiBold});
            var notes=manifest.Notes;
            panel.Children.Add(new ScrollViewer{MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=notes,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)}});
            var buttons=new WrapPanel();panel.Children.Add(buttons);
            var download=new Button{Content=installed?L.T("Descargar e instalar"):L.T("Abrir descarga portátil"),IsDefault=true};
            var later=new Button{Content=L.T("Ahora no"),IsCancel=true};
            buttons.Children.Add(download);buttons.Children.Add(later);
            var dialog=Dialog(L.T("Actualización disponible"),panel);
            download.Click+=(_,_)=>dialog.DialogResult=true;
            if(dialog.ShowDialog()!=true)return;
            if(!installed){Process.Start(new ProcessStartInfo(ReleasePage){UseShellExecute=true});return;}
            var folder=Path.Combine(WorkspaceFolder,"updates");
            StatusText.Text=L.T("Descargando actualización…");
            var path=await client.DownloadAsync(manifest,folder,null);
            CaptureTab();
            if(documents.Any(d=>d.Dirty))
            {
                if(MessageBox.Show(L.T("Guarda todos los documentos antes de instalar la actualización. ¿Guardar ahora?"),"MarkdownLab",MessageBoxButton.YesNo)!=MessageBoxResult.Yes||!SaveAll()){StatusText.Text=L.T("Actualización cancelada");return;}
            }
            if(!ConfirmExit())return;
            try
            {
                // Installer waits for the current process to close via its standard application handling.
                await InstallerHandoff.LaunchAsync(new PreparedUpdate(manifest,path),folder,applicationUpdateSwitch:"/MARKDOWNLABUPDATE");
                Application.Current.Shutdown();
            }
            catch {workspaceTimer.Start();throw;}
        }
        catch(Exception ex){if(manual)UpdateNotice(L.T("No se pudo actualizar: ")+ex.Message);else StatusText.Text=L.T("No se pudo comprobar la actualización");}
        finally{checkingUpdate=false;}
    }
    private void UpdateNotice(string message)
    {
        var panel=new StackPanel{Margin=new Thickness(20),Width=380};
        panel.Children.Add(new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var close=new Button{Content=L.T("Aceptar"),IsCancel=true,IsDefault=true};panel.Children.Add(close);
        Dialog(L.T("Actualizaciones"),panel).ShowDialog();
    }
}
