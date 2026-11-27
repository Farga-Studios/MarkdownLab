using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Highlighting;
namespace MarkdownLab;
public partial class MainWindow
{
    private Window CreateThemedDialog(string title, StackPanel panel)
    {
        var dialog=new Window{Title=title,Owner=this,SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,ShowInTaskbar=false,FontFamily=FontFamily,FontSize=FontSize,Icon=Icon};
        foreach(var key in Resources.Keys) dialog.Resources[key]=Resources[key];
        dialog.SetResourceReference(BackgroundProperty,"Panel");
        dialog.SetResourceReference(ForegroundProperty,"Ink");
        WindowChrome.SetWindowChrome(dialog,new WindowChrome{CaptionHeight=38,ResizeBorderThickness=new Thickness(0),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        var root=new DockPanel();
        var header=new DockPanel{Height=38,LastChildFill=true};
        header.SetResourceReference(Panel.BackgroundProperty,"Surface");
        var close=new Button{Content="×",Width=44,Margin=new Thickness(0),Padding=new Thickness(0),Style=(Style)Resources["CloseChromeButton"],ToolTip=L.T("Cerrar")};
        WindowChrome.SetIsHitTestVisibleInChrome(close,true);
        close.Click+=(_,_)=>dialog.Close();
        DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
        header.Children.Add(new TextBlock{Text=title,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,8,0),FontWeight=FontWeights.SemiBold});
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);root.Children.Add(panel);
        var frame=new Border{BorderThickness=new Thickness(1),Child=root};frame.SetResourceReference(Border.BorderBrushProperty,"Edge");
        dialog.Content=frame;
        dialog.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){e.Handled=true;dialog.Close();}};
        return dialog;
    }
    private void ApplyMarkdownColors()
    {
        if(Editor is null)return;
        var definition=HighlightingManager.Instance.GetDefinitionByExtension(".md");
        if(definition is null)return;
        foreach(var color in definition.NamedHighlightingColors)
        {
            var name=color.Name??"";
            var hex=name.Contains("Link",System.StringComparison.OrdinalIgnoreCase)?(light?"#155D87":"#81C9E8"):name.Contains("Code",System.StringComparison.OrdinalIgnoreCase)?(light?"#795421":"#E4C38D"):(light?"#234E91":"#9DBBFA");
            color.Foreground=new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        Editor.SyntaxHighlighting=null;
        Editor.SyntaxHighlighting=definition;
    }
}
