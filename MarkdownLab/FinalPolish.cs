using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace MarkdownLab;
public partial class MainWindow
{
    private string lastHtml="";
    private bool focusMode;
    private void FocusMode_Click(object sender,RoutedEventArgs e)
    {
        focusMode=!focusMode;
        MainToolbar.Visibility=focusMode?Visibility.Collapsed:Visibility.Visible;
        if(focusMode && outlineVisible) ToggleOutline_Click(sender,e);
        else if(!focusMode && !outlineVisible) ToggleOutline_Click(sender,e);
        FormatBar.Visibility=!focusMode && started && mode!=ViewMode.Read?Visibility.Visible:Visibility.Collapsed;
    }
    private void Spell_Click(object sender,RoutedEventArgs e)
    {
        settings.SpellEnabled=SpellMenu.IsChecked;PersistAppearance();QueueSpelling();
    }
    private void ExportHtml_Click(object sender,RoutedEventArgs e)
    {
        if(!started)return;RefreshPreview();var picker=new SaveFileDialog{Filter="HTML|*.html",FileName=Path.GetFileNameWithoutExtension(filePath??"documento")+".html"};
        if(picker.ShowDialog()!=true)return;
        try{var html=Regex.Replace(lastHtml,@"https://markdownlab-assets\.invalid/([A-Fa-f0-9]+\.[a-zA-Z]+)",m=>{var p=Path.Combine(WorkspaceFolder,"images",m.Groups[1].Value);if(!File.Exists(p))return m.Value;var ext=Path.GetExtension(p).ToLowerInvariant();var mime=ext==".jpg"||ext==".jpeg"?"jpeg":ext.TrimStart('.');return "data:image/"+mime+";base64,"+Convert.ToBase64String(File.ReadAllBytes(p));});File.WriteAllText(picker.FileName,html);StatusText.Text=L.T("HTML exportado");}catch(Exception ex){MessageBox.Show(ex.Message);}
    }
    private async void ExportPdf_Click(object sender,RoutedEventArgs e)
    {
        if(!started||!ready)return;var picker=new SaveFileDialog{Filter="PDF|*.pdf",FileName=Path.GetFileNameWithoutExtension(filePath??"documento")+".pdf"};
        if(picker.ShowDialog()!=true)return;
        try{var print=Preview.CoreWebView2.Environment.CreatePrintSettings();print.ShouldPrintBackgrounds=true;StatusText.Text=await Preview.CoreWebView2.PrintToPdfAsync(picker.FileName,print)?L.T("PDF exportado"):L.T("No se pudo exportar PDF");}catch(Exception ex){MessageBox.Show(ex.Message);}
    }
    private static string HighlightCode(string html)
    {
        return Regex.Replace(html,@"(<pre><code class=""language-(?:csharp|cs|js|javascript|ts|typescript|python|py|java|cpp|c|json)"">)([\s\S]*?)(</code></pre>)",m=>{
            var raw=WebUtility.HtmlDecode(m.Groups[2].Value);
            var tokens=new Regex(@"//[^\r\n]*|#[^\r\n]*|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|\b(?:class|public|private|static|void|int|string|bool|var|new|return|if|else|for|foreach|while|using|namespace|def|import|from|function|const|let|true|false|null|None|True|False|async|await|try|catch|throw)\b");
            int pos=0;var b=new System.Text.StringBuilder();foreach(Match t in tokens.Matches(raw)){b.Append(WebUtility.HtmlEncode(raw[pos..t.Index]));var kind=t.Value.StartsWith("//")||t.Value.StartsWith("#")?"comment":t.Value.StartsWith("\"")||t.Value.StartsWith("'")?"str":"kw";b.Append("<span class=\""+kind+"\">"+WebUtility.HtmlEncode(t.Value)+"</span>");pos=t.Index+t.Length;}b.Append(WebUtility.HtmlEncode(raw[pos..]));return m.Groups[1].Value+b.ToString()+m.Groups[3].Value;
        },RegexOptions.IgnoreCase);
    }
    private void PersistAppearance(){settings.Light=light;settings.Zoom=zoom;try{File.WriteAllText(Path.Combine(WorkspaceFolder,"settings.json"),System.Text.Json.JsonSerializer.Serialize(settings));}catch{}}
}
