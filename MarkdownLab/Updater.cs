using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Security.Cryptography;
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
            using var request=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/Farga-Studios/MarkdownLab/releases/latest");
            request.Headers.UserAgent.ParseAdd("MarkdownLab/"+(Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)??"1.7.0"));
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response=await updateHttp.SendAsync(request);
            if(response.StatusCode==System.Net.HttpStatusCode.NotFound){if(manual)UpdateNotice(L.T("Todavía no hay versiones publicadas."));return;}
            response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root=json.RootElement;
            if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return;
            var tag=root.GetProperty("tag_name").GetString()??"";
            if(!Version.TryParse(tag.TrimStart('v','V'),out var next))return;
            var current=Assembly.GetExecutingAssembly().GetName().Version??new Version(0,0);
            if(new Version(next.Major,next.Minor,Math.Max(0,next.Build),Math.Max(0,next.Revision))<=current){if(manual)UpdateNotice(L.T("Ya tienes la última versión."));return;}
            var installed=File.Exists(Path.Combine(AppContext.BaseDirectory,"installed.marker"));
            // Single-file apps keep the installed marker next to the executable.
            var panel=new StackPanel{Margin=new Thickness(20),Width=460};
            panel.Children.Add(new TextBlock{Text="MarkdownLab "+tag+L.T(" disponible"),FontSize=21,FontWeight=FontWeights.SemiBold});
            var notes=root.TryGetProperty("body",out var body)?body.GetString()??"":"";
            panel.Children.Add(new ScrollViewer{MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=new TextBlock{Text=notes,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12)}});
            var buttons=new WrapPanel();panel.Children.Add(buttons);
            var download=new Button{Content=installed?L.T("Descargar e instalar"):L.T("Abrir descarga portátil"),IsDefault=true};
            var later=new Button{Content=L.T("Ahora no"),IsCancel=true};
            buttons.Children.Add(download);buttons.Children.Add(later);
            var dialog=Dialog(L.T("Actualización disponible"),panel);
            download.Click+=(_,_)=>dialog.DialogResult=true;
            if(dialog.ShowDialog()!=true)return;
            if(!installed){Process.Start(new ProcessStartInfo(ReleasePage){UseShellExecute=true});return;}
            var asset=root.GetProperty("assets").EnumerateArray().FirstOrDefault(a=>a.GetProperty("name").GetString()=="MarkdownLab-Setup-win-x64.exe");
            if(asset.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException(L.T("No se encontró el instalador."));
            var url=asset.GetProperty("browser_download_url").GetString()??"";
            if(!url.StartsWith("https://github.com/Farga-Studios/MarkdownLab/releases/download/",StringComparison.Ordinal))throw new InvalidOperationException(L.T("Origen de descarga no válido."));
            if(!asset.TryGetProperty("digest",out var digestProp)||digestProp.ValueKind!=JsonValueKind.String)throw new InvalidOperationException(L.T("La versión no proporciona una huella SHA-256. Descárgala manualmente."));
            var digest=digestProp.GetString()??"";
            if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||digest.Length!=71)throw new InvalidOperationException(L.T("Huella del instalador no válida."));
            var folder=Path.Combine(WorkspaceFolder,"updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var path=Path.Combine(folder,"MarkdownLab-Setup-win-x64.exe");
            StatusText.Text=L.T("Descargando actualización…");
            using(var file=File.Create(path))
            using(var stream=await updateHttp.GetStreamAsync(url))
                await stream.CopyToAsync(file);
            using(var file=File.OpenRead(path))
            {
                var hash=Convert.ToHexString(await SHA256.HashDataAsync(file));
                if(!hash.Equals(digest[7..],StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("La verificación SHA-256 ha fallado."));
            }
            CaptureTab();
            if(documents.Any(d=>d.Dirty))
            {
                if(MessageBox.Show(L.T("Guarda todos los documentos antes de instalar la actualización. ¿Guardar ahora?"),"MarkdownLab",MessageBoxButton.YesNo)!=MessageBoxResult.Yes||!SaveAll()){StatusText.Text=L.T("Actualización cancelada");return;}
            }
            if(!ConfirmExit())return;
            try
            {
                // Installer waits for the current process to close via its standard application handling.
                Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
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
