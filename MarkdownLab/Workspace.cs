using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit.Document;

namespace MarkdownLab;
public partial class MainWindow
{
    private sealed class TabDoc : System.ComponentModel.INotifyPropertyChanged
    {
        public string? Path {get;set;}
        public string Text {get;set;} = "";
        public bool Dirty {get;set;}
        public int Mode {get;set;}
        public int Caret {get;set;}
        public string Label => (Path is null ? "Sin título" : System.IO.Path.GetFileName(Path)) + (Dirty ? " *" : "");
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void Notify() => PropertyChanged?.Invoke(this,new(nameof(Label)));
        [System.Text.Json.Serialization.JsonIgnore] public TextDocument? Document {get;set;}
    }
    private readonly System.Collections.ObjectModel.ObservableCollection<TabDoc> documents = new();
    private TabDoc? current;
    private bool switching;
    private readonly List<string> recent = new();
    private static string WorkspaceFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MarkdownLab","Workspace");
    private void InitWorkspace()
    {
        Directory.CreateDirectory(WorkspaceFolder);
        Tabs.ItemsSource=documents;
        try { recent.AddRange(JsonSerializer.Deserialize<List<string>>(File.ReadAllText(Path.Combine(WorkspaceFolder,"recent.json"))) ?? new()); } catch {}
        UpdateRecent();
        LoadSettings();
        workspaceTimer.Tick += (_,_)=>WorkspaceTick(); workspaceTimer.Start();
        Loaded += (_,_)=> {
            try {
                if (settings.RestoreTabs) RestoreSession();
                var recovered=JsonSerializer.Deserialize<List<TabDoc>>(File.ReadAllText(Path.Combine(WorkspaceFolder,"recovery.json")));
                if(recovered?.Count>0 && MessageBox.Show("¿Recuperar documentos sin guardar?","MarkdownLab",MessageBoxButton.YesNo)==MessageBoxResult.Yes)
                    foreach(var d in recovered) { var existing=documents.FirstOrDefault(t=>d.Path!=null && string.Equals(t.Path,d.Path,StringComparison.OrdinalIgnoreCase)); if(existing!=null){Tabs.SelectedItem=existing; Editor.Text=d.Text; dirty=true; CaptureTab();} else AddDocument(d.Path,d.Text,true,(ViewMode)d.Mode); }
            } catch {}
        };
        PreviewKeyDown += (_,e)=> {
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.W){e.Handled=true;CloseTab_Click(this,e);}
            if(Keyboard.Modifiers==ModifierKeys.Control && e.Key==Key.H){e.Handled=true;Replace_Click(this,e);}
        };
        Editor.PreviewDragOver += (_,e)=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsImage)){e.Effects=DragDropEffects.Copy;e.Handled=true;} };
        Editor.PreviewDrop += (_,e)=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsImage)){foreach(var f in files.Where(IsImage)) InsertText("\n"+ImageMarkdown(f,Path.GetFileNameWithoutExtension(f))+"\n");e.Handled=true;} };
    }
    private void CaptureTab()
    {
        if(current is null || switching) return;
        current.Path=filePath;current.Text=Editor.Text;current.Dirty=dirty;current.Mode=(int)mode;current.Caret=Editor.CaretOffset;current.Notify();
    }
    private void AddDocument(string? path,string content,bool changed,ViewMode view)
    {
        CaptureTab();
        var d=new TabDoc{Path=path,Text=content,Dirty=changed,Mode=(int)view,Document=new TextDocument(content)};
        documents.Add(d);Tabs.SelectedItem=d;
    }
    private void Tab_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(switching || Tabs.SelectedItem is not TabDoc d || ReferenceEquals(current,d)) return;
        CaptureTab();switching=true;updating=true;
        try {
            current=d;filePath=d.Path;dirty=d.Dirty;mode=(ViewMode)d.Mode;started=true;
            Editor.Document=d.Document ??=new TextDocument(d.Text);
            Editor.CaretOffset=Math.Clamp(d.Caret,0,Editor.Document.TextLength);
            Title="MarkdownLab — "+d.Label;StatusText.Text=filePath??"Nuevo documento";ApplyMode();RefreshPreview();
        } finally {updating=false;switching=false;}
    }
    private void CloseTab_Click(object sender,RoutedEventArgs e)
    {
        CaptureTab();var d=current;if(d is null)return;
        if(d.Dirty){var result=AskSave(d.Label,false);if(result==0 || (result==1 && !SaveFile()))return;}
        int index=documents.IndexOf(d);switching=true;documents.Remove(d);current=null;switching=false;
        if(documents.Count>0){Tabs.SelectedIndex=-1;Tabs.SelectedIndex=Math.Min(index,documents.Count-1);}
        else {updating=true;Editor.Document=new TextDocument();updating=false;started=false;dirty=false;filePath=null;ApplyMode();RefreshPreview();Title="MarkdownLab";}
        WriteRecovery();
    }
    private void Remember(string path){recent.RemoveAll(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));recent.Insert(0,path);if(recent.Count>15)recent.RemoveRange(15,recent.Count-15);try{File.WriteAllText(Path.Combine(WorkspaceFolder,"recent.json"),JsonSerializer.Serialize(recent));}catch{}UpdateRecent();}
    private void UpdateRecent(){WelcomeRecent.ItemsSource=recent.Where(File.Exists).Take(5).ToList();RecentMenu.Items.Clear();foreach(var p in recent){var item=new MenuItem{Header=p};item.Click+=(_,_)=>LoadFile(p);RecentMenu.Items.Add(item);}}
    private void WriteRecovery(){try{if(!settings.Recovery)return;CaptureTab();var path=Path.Combine(WorkspaceFolder,"recovery.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(documents.Where(d=>d.Dirty).ToList()));File.Move(path+".tmp",path,true);}catch{}}
    private void Folder_Click(object sender,RoutedEventArgs e){var picker=new OpenFolderDialog();if(picker.ShowDialog()!=true)return;try{FolderFiles.ItemsSource=Directory.EnumerateFiles(picker.FolderName,"*",SearchOption.TopDirectoryOnly).Where(p=>p.EndsWith(".md",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".markdown",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p).ToList();FolderFiles.Visibility=Visibility.Visible;}catch(Exception ex){StatusText.Text=ex.Message;}}
    private void FolderFile_Click(object sender,MouseButtonEventArgs e){if(FolderFiles.SelectedItem is string path)LoadFile(path);}
    private void Replace_Click(object sender,RoutedEventArgs e){if(!started)return;var panel=new StackPanel{Margin=new Thickness(16),Width=320};var find=new TextBox{Text=SearchBox.Text};var replace=new TextBox();panel.Children.Add(new TextBlock{Text="Buscar"});panel.Children.Add(find);panel.Children.Add(new TextBlock{Text="Reemplazar por"});panel.Children.Add(replace);var all=new Button{Content="Reemplazar todo"};panel.Children.Add(all);var dialog=Dialog("Buscar y reemplazar",panel);all.Click+=(_,_)=>{if(find.Text.Length==0)return;var value=Editor.Text.Replace(find.Text,replace.Text,StringComparison.OrdinalIgnoreCase);Editor.Document.Replace(0,Editor.Document.TextLength,value);dialog.Close();};dialog.ShowDialog();}
    private static bool IsImage(string path)=>new[]{".png",".jpg",".jpeg",".gif",".webp",".bmp"}.Contains(Path.GetExtension(path).ToLowerInvariant()) && File.Exists(path);
    private string ImageMarkdown(string path,string alt){var full=Path.GetFullPath(path);var relative=filePath is null?new Uri(full).AbsoluteUri:Path.GetRelativePath(Path.GetDirectoryName(Path.GetFullPath(filePath))!,full).Replace('\\','/');return "!["+alt.Replace("]","\\]")+"](<"+relative.Replace(">","%3E")+">)";}
}
