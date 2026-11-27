using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Document;
namespace MarkdownLab;
public partial class MainWindow
{
    private readonly DispatcherTimer spellingTimer=new(){Interval=TimeSpan.FromMilliseconds(700)};
    private readonly TextBox spellingProbe=new();
    private readonly List<(int Start,int Length,string[] Suggestions)> spellingErrors=new();
    private readonly HashSet<string> ignoredWords=new(StringComparer.OrdinalIgnoreCase);
    private void InitSpelling()
    {
        spellingTimer.Tick+=(_,_)=>{spellingTimer.Stop();CheckSpelling();};
        Editor.TextChanged+=(_,_)=>QueueSpelling();
        Editor.TextArea.TextView.BackgroundRenderers.Add(new SpellingRenderer(this));
        if(Editor.ContextMenu is ContextMenu menu)menu.Opened+=(_,_)=>PopulateSpellingMenu(menu);
    }
    private void QueueSpelling()
    {
        spellingErrors.Clear();Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        spellingTimer.Stop();if(settings.SpellEnabled)spellingTimer.Start();
    }
    private void CheckSpelling()
    {
        if(!settings.SpellEnabled)return;
        try{
            spellingProbe.Language=System.Windows.Markup.XmlLanguage.GetLanguage(settings.SpellLanguage);
            SpellCheck.SetIsEnabled(spellingProbe,true);
            var source=Editor.Text;
            // Preserve offsets while excluding code, link destinations and URLs.
            var masked=Regex.Replace(source,@"(?ms)^ {0,3}(?:\x60{3,}|~{3,}).*?^ {0,3}(?:\x60{3,}|~{3,})[^\r\n]*|(?-ms:\x60[^\x60\r\n]*\x60|\]\([^\r\n)]*\)|https?://\S+)",m=>new string(' ',m.Length));
            if(masked.Length>150000){StatusText.Text=L.T("Corrector: documento demasiado largo");return;}
            spellingProbe.Text=masked;
            int offset=0;
            while(offset<masked.Length && spellingErrors.Count<1000){
                int index=spellingProbe.GetNextSpellingErrorCharacterIndex(offset,System.Windows.Documents.LogicalDirection.Forward);if(index<0)break;
                int length=spellingProbe.GetSpellingErrorLength(index);if(length<=0){offset=index+1;continue;}
                var error=spellingProbe.GetSpellingError(index);
                if(error!=null&&!ignoredWords.Contains(source.Substring(index,length)))spellingErrors.Add((index,length,error.Suggestions.Take(6).ToArray()));
                offset=index+length;
            }
        }catch(Exception){StatusText.Text=L.T("Corrector no disponible para el idioma seleccionado");}
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }
    private void PopulateSpellingMenu(ContextMenu menu)
    {
        foreach(var item in menu.Items.OfType<MenuItem>().Where(i=>Equals(i.Tag,"spell-suggestion")).ToArray())menu.Items.Remove(item);
        if(!settings.SpellEnabled)return;
        int offset=Editor.CaretOffset;
        var position=Editor.GetPositionFromPoint(Mouse.GetPosition(Editor));
        if(position.HasValue)offset=Editor.Document.GetOffset(position.Value.Location);
        var error=spellingErrors.FirstOrDefault(e=>offset>=e.Start && offset<e.Start+e.Length);
        if(error.Length==0)return;
        int at=0;var word=Editor.Document.GetText(error.Start,error.Length);
        foreach(var suggestion in error.Suggestions){var item=new MenuItem{Header=suggestion,Tag="spell-suggestion"};item.Click+=(_,_)=>{if(error.Start+error.Length<=Editor.Document.TextLength&&Editor.Document.GetText(error.Start,error.Length)==word)Editor.Document.Replace(error.Start,error.Length,suggestion);};menu.Items.Insert(at++,item);}
        var ignore=new MenuItem{Header=L.T("Ignorar: ")+word,Tag="spell-suggestion"};ignore.Click+=(_,_)=>{ignoredWords.Add(word);QueueSpelling();};menu.Items.Insert(at,ignore);
    }
    private sealed class SpellingRenderer(MainWindow owner):IBackgroundRenderer
    {
        public KnownLayer Layer=>KnownLayer.Background;
        public void Draw(TextView view,DrawingContext dc)
        {
            if(!view.VisualLinesValid||!owner.settings.SpellEnabled)return;
            var pen=new Pen(new SolidColorBrush(Color.FromRgb(232,90,99)),1.2);
            foreach(var error in owner.spellingErrors)
            {
                if(error.Start+error.Length>owner.Editor.Document.TextLength)continue;
                foreach(var rect in BackgroundGeometryBuilder.GetRectsForSegment(view,new TextSegment{StartOffset=error.Start,Length=error.Length}))
                {
                    for(double x=rect.Left;x+2<=rect.Right;x+=4){dc.DrawLine(pen,new Point(x,rect.Bottom-1),new Point(x+2,rect.Bottom-3));dc.DrawLine(pen,new Point(x+2,rect.Bottom-3),new Point(Math.Min(x+4,rect.Right),rect.Bottom-1));}
                }
            }
        }
    }
    private void WelcomeRecent_Click(object sender,MouseButtonEventArgs e){if(WelcomeRecent.SelectedItem is string path)LoadFile(path);}
}
