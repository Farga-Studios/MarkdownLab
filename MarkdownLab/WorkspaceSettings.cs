using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace MarkdownLab;
public partial class MainWindow
{
    private sealed class Preferences
    {
        public bool AutoSave {get;set;}
        public bool Recovery {get;set;}=true;
        public bool RestoreTabs {get;set;}=true;
        public int Interval {get;set;}=30;
    }
    private Preferences settings=new();
    private readonly System.Windows.Threading.DispatcherTimer workspaceTimer=new();
    private void LoadSettings()
    {
        try {settings=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(WorkspaceFolder,"settings.json")))??new();}catch{}
        workspaceTimer.Interval=TimeSpan.FromSeconds(Math.Clamp(settings.Interval,15,300));
    }
    private void Settings_Click(object sender,RoutedEventArgs e)
    {
        var panel=new StackPanel{Margin=new Thickness(20),Width=380};
        var auto=new CheckBox{Content="Guardar automáticamente archivos originales",IsChecked=settings.AutoSave};
        var recovery=new CheckBox{Content="Crear copias de recuperación",IsChecked=settings.Recovery};
        var restore=new CheckBox{Content="Restaurar pestañas al iniciar",IsChecked=settings.RestoreTabs};
        var interval=new ComboBox{ItemsSource=new[]{15,30,60,300},SelectedItem=settings.Interval};
        foreach(var check in new[]{auto,recovery,restore}){check.Foreground=(System.Windows.Media.Brush)Resources["Ink"];check.Margin=new Thickness(0,6,0,6);panel.Children.Add(check);}
        panel.Children.Add(new TextBlock{Text="Intervalo (segundos)"});panel.Children.Add(interval);
        var ok=new Button{Content="Guardar configuración",IsDefault=true,Margin=new Thickness(0,12,0,0)};panel.Children.Add(ok);
        var dialog=Dialog("Configuración",panel);ok.Click+=(_,_)=>dialog.DialogResult=true;
        if(dialog.ShowDialog()!=true)return;
        settings.AutoSave=auto.IsChecked==true;settings.Recovery=recovery.IsChecked==true;settings.RestoreTabs=restore.IsChecked==true;settings.Interval=interval.SelectedItem is int n?n:30;
        workspaceTimer.Interval=TimeSpan.FromSeconds(settings.Interval);
        try{File.WriteAllText(Path.Combine(WorkspaceFolder,"settings.json"),JsonSerializer.Serialize(settings));if(!settings.Recovery)File.Delete(Path.Combine(WorkspaceFolder,"recovery.json"));}catch(Exception ex){StatusText.Text=ex.Message;}
        WriteRecovery();
    }
    private static void WriteDocumentSafely(string path,string content)
    {
        var full=Path.GetFullPath(path);var temp=Path.Combine(Path.GetDirectoryName(full)!,"."+Guid.NewGuid().ToString("N")+".tmp");
        try{File.WriteAllText(temp,content,new UTF8Encoding(false));if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    private void WorkspaceTick()
    {
        CaptureTab();
        if(settings.AutoSave)foreach(var d in documents.Where(t=>t.Dirty && t.Path!=null).ToArray())
        {
            try{WriteDocumentSafely(d.Path!,d.Text);d.Dirty=false;d.Notify();if(ReferenceEquals(current,d)){dirty=false;Title="MarkdownLab — "+d.Label;RefreshPreview();}}
            catch(Exception ex){StatusText.Text="Autoguardado: "+ex.Message;}
        }
        WriteRecovery();
    }
    private int AskSave(string name,bool multiple)
    {
        int answer=0;var panel=new StackPanel{Margin=new Thickness(20),Width=380};
        panel.Children.Add(new TextBlock{Text="¿Quieres guardar los cambios?",FontSize=19,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text=name,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,16)});
        var buttons=new WrapPanel();panel.Children.Add(buttons);var dialog=Dialog("MarkdownLab",panel);
        foreach(var pair in multiple?new[]{("Guardar",1),("Guardar todos",3),("No guardar",2),("Cancelar",0)}:new[]{("Guardar",1),("No guardar",2),("Cancelar",0)})
        {var b=new Button{Content=pair.Item1,Margin=new Thickness(2),IsDefault=pair.Item2==1,IsCancel=pair.Item2==0};buttons.Children.Add(b);b.Click+=(_,_)=>{answer=pair.Item2;dialog.Close();};}
        dialog.ShowDialog();return answer;
    }
    private void CloseSpecific_Click(object sender,RoutedEventArgs e){if(sender is Button b && b.Tag is TabDoc d){Tabs.SelectedItem=d;CloseTab_Click(sender,e);}}
    private bool SaveAll()
    {
        CaptureTab();var previous=current;
        foreach(var d in documents.Where(t=>t.Dirty).ToArray()){Tabs.SelectedItem=d;if(!SaveFile())return false;}
        if(previous!=null)Tabs.SelectedItem=previous;return true;
    }
    private void SaveAll_Click(object sender,RoutedEventArgs e)=>SaveAll();
    private bool ConfirmExit()
    {
        CaptureTab();var pending=documents.Where(t=>t.Dirty).ToArray();
        foreach(var d in pending)
        {
            if(!d.Dirty)continue;Tabs.SelectedItem=d;int answer=AskSave(d.Label,pending.Length>1);
            if(answer==0)return false;
            if(answer==3){if(!SaveAll())return false;break;}
            if(answer==1 && !SaveFile())return false;
        }
        try{
            File.WriteAllText(Path.Combine(WorkspaceFolder,"session.json"),JsonSerializer.Serialize(documents.Where(t=>t.Path!=null).Select(t=>t.Path).ToArray()));
            File.Delete(Path.Combine(WorkspaceFolder,"recovery.json"));
        }catch(Exception ex){StatusText.Text=ex.Message;}
        workspaceTimer.Stop();return true;
    }
    private void RestoreSession()
    {
        try{foreach(var p in JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(WorkspaceFolder,"session.json")))??Array.Empty<string>())if(File.Exists(p))LoadFile(p);}catch{}
    }
}
