using Markdig;
using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MarkdownLab;

public partial class MainWindow : Window
{
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private string? filePath;
    private bool dirty;
    private bool light;
    private bool updating;
    private double zoom = 1;
    private ViewMode mode = ViewMode.Split;
    private readonly List<(string Heading, string Slug)> headings = new();
    private enum ViewMode { Read, Edit, Split }

    public MainWindow()
    {
        InitializeComponent();
        refreshTimer.Tick += (_, _) => { refreshTimer.Stop(); RefreshPreview(); };
        Loaded += (_, _) => { ApplyMode(); RefreshPreview(); };
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenFile()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveFile()));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
        if (Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(p => File.Exists(p) && p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) is { } first) LoadFile(first);
    }

    private bool ConfirmDiscard()
    {
        if (!dirty) return true;
        return MessageBox.Show("Hay cambios sin guardar. ¿Continuar y descartarlos?", "MarkdownLab", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void OpenFile()
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog { Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) LoadFile(dialog.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            updating = true;
            Editor.Text = File.ReadAllText(path);
            filePath = path;
            dirty = false;
            Title = $"MarkdownLab — {Path.GetFileName(path)}";
            StatusText.Text = path;
            RefreshPreview();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo abrir", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { updating = false; }
    }

    private bool SaveFile()
    {
        var path = filePath;
        if (path is null)
        {
            var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", DefaultExt = ".md" };
            if (dialog.ShowDialog() != true) return false;
            path = dialog.FileName;
        }
        try
        {
            File.WriteAllText(path, Editor.Text, new UTF8Encoding(false));
            filePath = path; dirty = false;
            Title = $"MarkdownLab — {Path.GetFileName(path)}";
            StatusText.Text = path;
            return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo guardar", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }

    private void RefreshPreview()
    {
        var markdown = Editor.Text;
        var html = Markdown.ToHtml(markdown, pipeline);
        headings.Clear();
        // Explicit anchors make the outline work even with duplicate heading titles.
        var number = 0;
        html = Regex.Replace(html, @"<h([1-6])([^>]*)>(.*?)</h\1>", match =>
        {
            var id = $"heading-{++number}";
            var title = WebUtility.HtmlDecode(Regex.Replace(match.Groups[3].Value, "<[^>]+>", ""));
            headings.Add((title, id));
            return $"<h{match.Groups[1].Value} id=\"{id}\">{match.Groups[3].Value}</h{match.Groups[1].Value}>";
        }, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        OutlineList.ItemsSource = headings.Select(h => h.Heading).ToList();
        // NavigateToString is safe here: no active content or remote files are loaded.
        var baseHref = filePath is null ? "" : $"<base href=\"{new Uri(Path.GetDirectoryName(Path.GetFullPath(filePath))! + Path.DirectorySeparatorChar).AbsoluteUri}\" />";
        var css = light
            ? "body{background:#fff;color:#252936}pre,code{background:#f0f2f5}blockquote{border-color:#bbc4d1}"
            : "body{background:#1d202a;color:#e3e8f5}pre,code{background:#292e3b}blockquote{border-color:#58627b}a{color:#82bbff}";
        var document = $@"<!doctype html><html><head><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""/><meta charset=""utf-8""/>{baseHref}<style>body{{font-family:Segoe UI,Arial,sans-serif;font-size:{Math.Round(16 * zoom)}px;line-height:1.65;margin:28px;overflow-wrap:break-word}}img{{max-width:100%}}pre{{padding:12px;overflow:auto}}code{{padding:2px 4px}}table{{border-collapse:collapse;max-width:100%}}td,th{{border:1px solid #8791a3;padding:7px}}blockquote{{border-left:4px solid;padding-left:15px;margin-left:0}}hr{{opacity:.25}}{css}</style></head><body>{html}</body></html>";
        Preview.NavigateToString(document);
    }

    private void ApplyMode()
    {
        Editor.Visibility = mode == ViewMode.Read ? Visibility.Collapsed : Visibility.Visible;
        Preview.Visibility = mode == ViewMode.Edit ? Visibility.Collapsed : Visibility.Visible;
        EditorColumn.Width = mode == ViewMode.Read ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PreviewColumn.Width = mode == ViewMode.Edit ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (updating) return;
        dirty = true;
        Title = $"MarkdownLab — {(filePath is null ? "Sin título" : Path.GetFileName(filePath))} *";
        refreshTimer.Stop(); refreshTimer.Start();
    }

    private void Outline_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = OutlineList.SelectedIndex;
        if (index < 0 || index >= headings.Count) return;
        if (mode == ViewMode.Edit)
        {
            var match = Regex.Matches(Editor.Text, @"^#{1,6}\s+.+$", RegexOptions.Multiline).Cast<Match>().ElementAtOrDefault(index);
            if (match != null) { Editor.Focus(); Editor.Select(match.Index, match.Length); }
        }
        else
        {
            try { Preview.InvokeScript("eval", new object[] { $"document.getElementById('{headings[index].Slug}').scrollIntoView();" }); }
            catch { /* Preview may be navigating. */ }
        }
    }

    private void Find()
    {
        var term = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(term)) return;
        if (mode == ViewMode.Read) { mode = ViewMode.Split; ApplyMode(); }
        var start = Editor.SelectionStart + Editor.SelectionLength;
        var index = Editor.Text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0) index = Editor.Text.IndexOf(term, 0, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) { Editor.Focus(); Editor.Select(index, term.Length); }
        else StatusText.Text = "Texto no encontrado";
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        light = !light;
        Editor.Background = new SolidColorBrush(light ? Color.FromRgb(250, 250, 252) : Color.FromRgb(29, 32, 42));
        Editor.Foreground = new SolidColorBrush(light ? Color.FromRgb(33, 37, 45) : Color.FromRgb(236, 238, 245));
        RefreshPreview();
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { zoom = Math.Min(2.5, zoom + .1); Editor.FontSize = 15 * zoom; RefreshPreview(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { zoom = Math.Max(.6, zoom - .1); Editor.FontSize = 15 * zoom; RefreshPreview(); }
    private void Open_Click(object sender, RoutedEventArgs e) => OpenFile();
    private void Save_Click(object sender, RoutedEventArgs e) => SaveFile();
    private void Read_Click(object sender, RoutedEventArgs e) { mode = ViewMode.Read; ApplyMode(); }
    private void Edit_Click(object sender, RoutedEventArgs e) { mode = ViewMode.Edit; ApplyMode(); }
    private void Split_Click(object sender, RoutedEventArgs e) { mode = ViewMode.Split; ApplyMode(); }
    private void Find_Click(object sender, RoutedEventArgs e) => Find();
    private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Find(); }
    private void Window_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.FirstOrDefault(p => File.Exists(p) && (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))) is { } path && ConfirmDiscard()) LoadFile(path);
    }
    private void Preview_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
    {
        // Prevent the embedded preview from navigating to remote or local links.
        if (e.Uri is not null) { e.Cancel = true; if (e.Uri.Scheme is "https" or "http") try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { } }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; base.OnClosing(e); }
}