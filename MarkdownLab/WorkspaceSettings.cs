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
        public string? UiLanguage {get;set;}
        public bool AutoSave {get;set;}
        public bool CheckUpdatesOnStartup {get;set;} = true;
        public bool SpellEnabled {get;set;} = false;
        public string SpellLanguage {get;set;} = "es-ES";
        public bool Wrap {get;set;} = true;
        public bool Light {get;set;}
        public double Zoom {get;set;}=1;
        public bool Recovery {get;set;}=true;
        public bool RestoreTabs {get;set;}=true;
        public int Interval {get;set;}=30;
    }
    private Preferences settings=new();
    private readonly System.Windows.Threading.DispatcherTimer workspaceTimer=new();
    private void LoadSettings()
    {
        try {settings=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(WorkspaceFolder,"settings.json")))??new();}catch{}
        L.SetLanguage(settings.UiLanguage); L.Apply(System.Windows.Application.Current.Resources);
        workspaceTimer.Interval=TimeSpan.FromSeconds(Math.Clamp(settings.Interval,15,300));
        Loaded += (_,_)=>{light=settings.Light;zoom=Math.Clamp(settings.Zoom,.6,2.5);Editor.FontSize=15*zoom;Editor.WordWrap=settings.Wrap;SpellMenu.IsChecked=settings.SpellEnabled;ApplyTheme();QueueSpelling();};
    }
    private void Settings_Click(object sender,RoutedEventArgs e)
    {
        var panel=new StackPanel{Margin=new Thickness(20),Width=490};
        var tabs=new TabControl{Background=(System.Windows.Media.Brush)Resources["Panel"],Foreground=(System.Windows.Media.Brush)Resources["Ink"],BorderBrush=(System.Windows.Media.Brush)Resources["Edge"]};
        panel.Children.Add(tabs);
        StackPanel Page(string title){var p=new StackPanel{Margin=new Thickness(12)};tabs.Items.Add(new TabItem{Header=title,Content=p});return p;}
        CheckBox Check(StackPanel p,string label,bool value){var c=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,7,0,7),Foreground=(System.Windows.Media.Brush)Resources["Ink"]};p.Children.Add(c);return c;}
        var general=Page(L.T("General"));general.Children.Add(new TextBlock{Text=L.T("Idioma de la interfaz")});var uiLanguage=new ComboBox{ItemsSource=new[]{"Español","English"},SelectedIndex=L.IsSpanish?0:1};general.Children.Add(uiLanguage);var updates=Check(general,L.T("Buscar actualizaciones al iniciar"),settings.CheckUpdatesOnStartup);var restore=Check(general,L.T("Restaurar pestañas al iniciar"),settings.RestoreTabs);
        var appearance=Page(L.T("Apariencia"));var bright=Check(appearance,L.T("Tema claro"),light);
        var edit=Page("Editor");var wrap=Check(edit,L.T("Ajustar líneas al ancho"),Editor.WordWrap);var spell=Check(edit,L.T("Corrector ortográfico integrado"),settings.SpellEnabled);
        edit.Children.Add(new TextBlock{Text=L.T("Idioma del corrector")});var language=new ComboBox{ItemsSource=new[]{"es-ES","en-US","en-GB","ca-ES"},SelectedItem=settings.SpellLanguage};edit.Children.Add(language);
        var save=Page(L.T("Guardado"));var auto=Check(save,L.T("Guardar automáticamente archivos originales"),settings.AutoSave);var recovery=Check(save,L.T("Crear copias de recuperación"),settings.Recovery);
        save.Children.Add(new TextBlock{Text=L.T("Intervalo (segundos)")});var interval=new ComboBox{ItemsSource=new[]{15,30,60,300},SelectedItem=settings.Interval};save.Children.Add(interval);
        var ok=new Button{Content=L.T("Guardar configuración"),IsDefault=true,Margin=new Thickness(0,12,0,0)};panel.Children.Add(ok);
        var dialog=Dialog(L.T("Configuración"),panel);ok.Click+=(_,_)=>dialog.DialogResult=true;
        if(dialog.ShowDialog()!=true)return;
        settings.UiLanguage=uiLanguage.SelectedIndex==0?"es":"en"; L.SetLanguage(settings.UiLanguage);L.Apply(System.Windows.Application.Current.Resources);foreach(var d in documents)d.Notify();Title=current==null?"MarkdownLab":"MarkdownLab — "+current.Label;RefreshPreview();
        settings.CheckUpdatesOnStartup=updates.IsChecked==true;settings.AutoSave=auto.IsChecked==true;settings.Recovery=recovery.IsChecked==true;settings.RestoreTabs=restore.IsChecked==true;settings.Interval=interval.SelectedItem is int n?n:30;
        settings.SpellEnabled=spell.IsChecked==true;settings.SpellLanguage=language.SelectedItem as string??"es-ES";settings.Wrap=wrap.IsChecked==true;Editor.WordWrap=settings.Wrap;light=bright.IsChecked==true;SpellMenu.IsChecked=settings.SpellEnabled;
        workspaceTimer.Interval=TimeSpan.FromSeconds(settings.Interval);ApplyTheme();PersistAppearance();QueueSpelling();
        try{if(!settings.Recovery)File.Delete(Path.Combine(WorkspaceFolder,"recovery.json"));}catch(Exception ex){StatusText.Text=ex.Message;}
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
            catch(Exception ex){StatusText.Text=L.T("Autoguardado: ")+ex.Message;}
        }
        WriteRecovery();
    }
    private int AskSave(string name,bool multiple)
    {
        int answer=0;var panel=new StackPanel{Margin=new Thickness(20),Width=380};
        panel.Children.Add(new TextBlock{Text=L.T("¿Quieres guardar los cambios?"),FontSize=19,FontWeight=FontWeights.SemiBold});
        panel.Children.Add(new TextBlock{Text=name,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,16)});
        var buttons=new WrapPanel();panel.Children.Add(buttons);var dialog=Dialog("MarkdownLab",panel);
        foreach(var pair in multiple?new[]{(L.T("Guardar"),1),(L.T("Guardar todos"),3),(L.T("No guardar"),2),(L.T("Cancelar"),0)}:new[]{(L.T("Guardar"),1),(L.T("No guardar"),2),(L.T("Cancelar"),0)})
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
        var discarded=new HashSet<TabDoc>();
        foreach(var d in pending)
        {
            if(!d.Dirty)continue;Tabs.SelectedItem=d;int answer=AskSave(d.Label,pending.Length>1);
            if(answer==0)return false;
            if(answer==3){foreach(var other in pending.Where(t=>t.Dirty&&!discarded.Contains(t))){Tabs.SelectedItem=other;if(!SaveFile())return false;}break;}
            if(answer==2)discarded.Add(d);
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
