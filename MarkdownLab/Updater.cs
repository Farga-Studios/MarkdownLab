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
            var publicKey=await reader.ReadToEndAsync();
            var client=new UpdateClient(updateHttp,new Uri("https://raw.githubusercontent.com/Farga-Studios/MarkdownLab/main/channels/stable/update.json"),publicKey,"MarkdownLab","stable");
            var version=assembly.GetName().Version??new Version(0,0,0);
            var running=new Version(version.Major,version.Minor,Math.Max(0,version.Build));
            var folder=Path.Combine(WorkspaceFolder,"updates");
            if(InstallerHandoff.IsInstalled(AppContext.BaseDirectory,"Sealsworn.MarkdownLab"))
            {
                using var session=UpdateSessionLock.Acquire(folder);
                var recovery=new UpdateRecovery(new UpdateJournal(folder,"MarkdownLab"));
                var assessment=recovery.Assess(running);
                if(assessment.Status is not (RecoveryStatus.NoTransaction or RecoveryStatus.Healthy or RecoveryStatus.Aborted or RecoveryStatus.RolledBack))
                {
                    StatusText.Text=L.T("Una actualización requiere recuperación.");
                    if(manual) await HandleRecoveryAsync(assessment,recovery,new VerifiedPackageStore(Path.Combine(folder,"packages"),publicKey,"MarkdownLab","stable"),running,folder);
                    return;
                }
            }
            UpdateManifest? manifest;
            try { manifest=await client.CheckAsync(running); }
            catch(HttpRequestException ex) when(ex.StatusCode==System.Net.HttpStatusCode.NotFound)
            {
                var message=L.T("El canal de actualizaciones no está disponible. No se ha podido verificar si hay una nueva versión.");
                StatusText.Text=message;
                if(manual)UpdateNotice(message);
                return;
            }
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
                using var session=UpdateSessionLock.Acquire(folder);
                var journal=new UpdateJournal(folder,"MarkdownLab");
                var recovery=new UpdateRecovery(journal);
                if(recovery.Assess(running).Status is not (RecoveryStatus.NoTransaction or RecoveryStatus.Healthy or RecoveryStatus.Aborted or RecoveryStatus.RolledBack))
                    throw new InvalidOperationException("Unresolved update transaction.");
                var store=new VerifiedPackageStore(Path.Combine(folder,"packages"),publicKey,"MarkdownLab","stable");
                await client.RetainDownloadedAsync(manifest,path,store);
                var transaction=journal.Begin(running.ToString(3),manifest.Version);
                journal.Advance(transaction.Id,UpdatePhase.HandoffStarted);
                await InstallerHandoff.LaunchAsync(new PreparedUpdate(manifest,path),folder,applicationUpdateSwitch:"/MARKDOWNLABUPDATE",transactionId:transaction.Id);
                Application.Current.Shutdown();
            }
            catch {workspaceTimer.Start();throw;}
        }
        catch(Exception ex){if(manual)UpdateNotice(L.T("No se pudo actualizar: ")+ex.Message);else StatusText.Text=L.T("No se pudo comprobar la actualización");}
        finally{checkingUpdate=false;}
    }
    private async Task HandleRecoveryAsync(RecoveryAssessment assessment,UpdateRecovery recovery,VerifiedPackageStore store,Version running,string folder)
    {
        if(assessment.Transaction is not { } t){UpdateNotice(L.T("Registro de actualización no válido. Consulta la documentación de recuperación."));return;}
        if(assessment.Status==RecoveryStatus.Interrupted && t.Phase==UpdatePhase.Prepared)
        {
            if(MessageBox.Show(L.T("¿Cancelar la actualización preparada?"),"MarkdownLab",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
                recovery.CancelPrepared(t.Id,running);
            return;
        }
        if(!new InstallerExitEvidence(folder).HasCompletion(t.Id))
        {
            UpdateNotice(L.T("No hay justificante de instalación completada. Se requiere recuperación manual."));
            return;
        }
        if(assessment.Status is RecoveryStatus.HealthCheckRequired or RecoveryStatus.RollbackHealthCheckRequired)
        {
            var choice=MessageBox.Show(L.T("¿Ha terminado el instalador y has comprobado que puedes abrir, editar y guardar un documento de prueba? Sí: confirmar. No: solicitar recuperación. Cancelar: más tarde."),"MarkdownLab",MessageBoxButton.YesNoCancel);
            if(choice==MessageBoxResult.Yes)
            {
                if(assessment.Status==RecoveryStatus.HealthCheckRequired) recovery.ConfirmHealthy(t.Id,running);
                else recovery.ConfirmRollbackHealthy(t.Id,running);
                return;
            }
            if(choice!=MessageBoxResult.No || assessment.Status==RecoveryStatus.RollbackHealthCheckRequired)return;
        }
        if(assessment.Status is RecoveryStatus.HealthCheckRequired or RecoveryStatus.RecoveryRequired or RecoveryStatus.RollbackInterrupted)
        {
            if(MessageBox.Show(L.T("¿Restaurar la versión anterior? Confirma que el instalador ha terminado, que tienes copias de seguridad y que tus datos son compatibles con la versión anterior. Se requiere un instalador firmado conservado."),"MarkdownLab",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
            CaptureTab();
            if(documents.Any(d=>d.Dirty)&&!SaveAll())return;
            if(!ConfirmExit())return;
            try
            {
                recovery.RequireRecovery(t.Id);
                var update=await new RollbackManager(recovery,store).PrepareAsync(t.Id,running,true,true,folder);
                recovery.MarkRollbackStarted(t.Id,running);
                await InstallerHandoff.LaunchAsync(update,folder,applicationUpdateSwitch:"/MARKDOWNLABUPDATE",transactionId:t.Id);
                Application.Current.Shutdown();
            }
            catch {workspaceTimer.Start();throw;}
            return;
        }
        UpdateNotice(L.T("Actualización interrumpida. No borres el registro. Consulta la documentación de recuperación manual."));
    }

    private void UpdateNotice(string message)
    {
        var panel=new StackPanel{Margin=new Thickness(20),Width=380};
        panel.Children.Add(new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var close=new Button{Content=L.T("Aceptar"),IsCancel=true,IsDefault=true};panel.Children.Add(close);
        Dialog(L.T("Actualizaciones"),panel).ShowDialog();
    }
}
