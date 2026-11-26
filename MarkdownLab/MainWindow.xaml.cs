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
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
    private readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private string? filePath;
    private bool dirty;
    private bool light;
    private bool updating;
    private double zoom = 1;
    private ViewMode mode = ViewMode.Read;
    private bool ready;
    private bool started;
    private bool outlineVisible = true;
    private readonly List<(string Heading, string Slug)> headings = new();
    private enum ViewMode { Read, Edit, Split }

    public MainWindow()
    {
        InitializeComponent();
        Editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinitionByExtension(".md");
        refreshTimer.Tick += (_, _) => { refreshTimer.Stop(); RefreshPreview(); };
        Loaded += async (_, _) => {
            ApplyMode();
            try {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownLab", "WebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: folder);
                await Preview.EnsureCoreWebView2Async(env);
                Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Preview.CoreWebView2.Settings.IsWebMessageEnabled = false;
                Preview.CoreWebView2.NavigationStarting += (_, e) => { if (e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)) return;
                    e.Cancel = true;
                    if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var link) && link.Scheme is "https" or "http")
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true }); } catch { } };
                Preview.CoreWebView2.NavigationCompleted += (_, e) => {
                    if (!e.IsSuccess && e.WebErrorStatus != Microsoft.Web.WebView2.Core.CoreWebView2WebErrorStatus.OperationCanceled)
                        StatusText.Text = "Error de vista previa: " + e.WebErrorStatus;
                };
                Preview.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; };
                ready = true; RefreshPreview();
            } catch (Exception ex) { StatusText.Text = "Vista previa no disponible: " + ex.Message; mode = ViewMode.Edit; ApplyMode(); }
        };
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
            started = true; mode = ViewMode.Read; ApplyMode();
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
            filePath = path; dirty = false; RefreshPreview();
            Title = $"MarkdownLab — {Path.GetFileName(path)}";
            StatusText.Text = path;
            return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo guardar", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }

    private void RefreshPreview()
    {
        var markdown = Editor.Text;
        DocumentName.Text = (filePath is null ? "Sin título" : Path.GetFileName(filePath)) + (dirty ? " *" : "");
        Stats.Text = $"{Regex.Matches(markdown, @"\S+").Count} palabras   ·   {markdown.Length} caracteres   ·   UTF-8   ·   {zoom:P0}";
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
        var document = $@"<!doctype html><html><head><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""/><meta charset=""utf-8""/>{baseHref}<style>body{{font-family:Segoe UI,Arial,sans-serif;font-size:{Math.Round(16 * zoom)}px;line-height:1.65;margin:32px auto;padding:0 32px;max-width:900px;overflow-wrap:break-word}}img{{max-width:100%}}pre{{padding:12px;overflow:auto}}code{{padding:2px 4px}}table{{border-collapse:collapse;max-width:100%}}td,th{{border:1px solid #8791a3;padding:7px}}blockquote{{border-left:4px solid;padding-left:15px;margin-left:0}}hr{{opacity:.25}}{css}</style></head><body>{html}</body></html>";
        if (ready) {
            try { Preview.NavigateToString(document); }
            catch (Exception ex) { StatusText.Text = "Error de vista previa: " + ex.Message; }
        }
    }

    private void ApplyMode()
    {
        Welcome.Visibility = started ? Visibility.Collapsed : Visibility.Visible;
        Editor.Visibility = !started || mode == ViewMode.Read ? Visibility.Collapsed : Visibility.Visible;
        ReadButton.Background = (Brush)Resources[mode == ViewMode.Read ? "Accent" : "Panel"];
        EditButton.Background = (Brush)Resources[mode == ViewMode.Edit ? "Accent" : "Panel"];
        SplitButton.Background = (Brush)Resources[mode == ViewMode.Split ? "Accent" : "Panel"];
        Preview.Visibility = !started || mode == ViewMode.Edit ? Visibility.Collapsed : Visibility.Visible;
        EditorColumn.Width = mode == ViewMode.Read ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PreviewColumn.Width = mode == ViewMode.Edit ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    private void Editor_TextChanged(object sender, EventArgs e)
    {
        if (updating || !IsInitialized) return;
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
            if (match != null) { Editor.Focus(); Editor.Select(match.Index, match.Length); Editor.ScrollToLine(Editor.Document.GetLineByOffset(match.Index).LineNumber); }
        }
        else
        {
            try { if (ready) _ = Preview.ExecuteScriptAsync($"document.getElementById('{headings[index].Slug}')?.scrollIntoView();"); }
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
        if (index >= 0) { Editor.Focus(); Editor.Select(index, term.Length); Editor.ScrollToLine(Editor.Document.GetLineByOffset(index).LineNumber); }
        else StatusText.Text = "Texto no encontrado";
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        light = !light;
        foreach (var (key, dark, bright) in new[] { ("Surface","#181B22","#FFFFFF"), ("Panel","#222630","#F0F3F8"), ("Ink","#E6EAF2","#202734"), ("Muted","#A4AFC1","#536175"), ("Edge","#383F4D","#CDD5E0"), ("Accent","#42669D","#A8C8F2") })
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? bright : dark));
        ApplyMode(); RefreshPreview();
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { zoom = Math.Min(2.5, zoom + .1); Editor.FontSize = 15 * zoom; RefreshPreview(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { zoom = Math.Max(.6, zoom - .1); Editor.FontSize = 15 * zoom; RefreshPreview(); }
    private void New_Click(object sender, RoutedEventArgs e) {
        if (!ConfirmDiscard()) return;
        updating = true; Editor.Text = ""; updating = false; filePath = null; dirty = false; started = true; mode = ViewMode.Edit;
        Title = "MarkdownLab — Sin título"; StatusText.Text = "Nuevo documento"; ApplyMode(); RefreshPreview(); Editor.Focus();
    }
    private void ToggleOutline_Click(object sender, RoutedEventArgs e) {
        outlineVisible = !outlineVisible;
        OutlineColumn.Width = new GridLength(outlineVisible ? 230 : 0);
        OutlineSplitter.Visibility = outlineVisible ? Visibility.Visible : Visibility.Collapsed;
    }
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
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { if (!ConfirmDiscard()) e.Cancel = true; base.OnClosing(e); }
}