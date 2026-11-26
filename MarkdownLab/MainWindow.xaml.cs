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
    private bool syncScroll = true;
    private bool suppressList;
    private string? activeList;
    private bool outlineVisible = true;
    private readonly List<(string Heading, string Slug)> headings = new();
    private enum ViewMode { Read, Edit, Split }

    public MainWindow()
    {
        InitializeComponent();
        InitWorkspace();
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateListButtons();
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncPreview();
        Editor.PreviewKeyDown += EditorKeys;
        Editor.TextArea.TextEntering += AutoPair;
        Editor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinitionByExtension(".md");
        refreshTimer.Tick += (_, _) => { refreshTimer.Stop(); RefreshPreview(); };
        Loaded += async (_, _) => {
            ApplyMode();
            try {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownLab", "WebView2");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(userDataFolder: folder);
                await Preview.EnsureCoreWebView2Async(env);
                Directory.CreateDirectory(Path.Combine(WorkspaceFolder,"images"));
                Preview.CoreWebView2.SetVirtualHostNameToFolderMapping("markdownlab-assets.invalid", Path.Combine(WorkspaceFolder,"images"), Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Preview.CoreWebView2.Settings.IsWebMessageEnabled = false;
                Preview.CoreWebView2.NavigationStarting += (_, e) => { if (e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase)) return;
                    e.Cancel = true;
                    if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var link) && link.Scheme is "https" or "http")
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true }); } catch { } };
                Preview.CoreWebView2.NavigationCompleted += (_, e) => {
                    if (!e.IsSuccess && e.WebErrorStatus != Microsoft.Web.WebView2.Core.CoreWebView2WebErrorStatus.OperationCanceled)
                        StatusText.Text = "Error de vista previa: " + e.WebErrorStatus;
                    else if (e.IsSuccess) SyncPreview();
                };
                Preview.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; };
                ready = true; RefreshPreview();
            } catch (Exception ex) { StatusText.Text = "Vista previa no disponible: " + ex.Message; mode = ViewMode.Edit; ApplyMode(); }
        };
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (_, _) => OpenFile()));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, _) => SaveFile()));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
        PreviewKeyDown += (_, e) => {
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S) { e.Handled = true; SaveFile(true); }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F) { e.Handled = true; FocusSearch(); }
        };
        if (Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(p => File.Exists(p) && p.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) is { } first) LoadFile(first);
    }

    private bool ConfirmDiscard()
    {
        if (!dirty) return true;
        return MessageBox.Show("Hay cambios sin guardar. ¿Continuar y descartarlos?", "MarkdownLab", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog { Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|Todos los archivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) LoadFile(dialog.FileName);
    }

    private void LoadFile(string path)
    {
        try
        {
            path = Path.GetFullPath(path);
            var existing = documents.FirstOrDefault(d => string.Equals(d.Path,path,StringComparison.OrdinalIgnoreCase));
            if (existing != null) { Tabs.SelectedItem = existing; return; }
            var content = File.ReadAllText(path);
            AddDocument(path, content, false, ViewMode.Read);
            Remember(path);
            return;
#pragma warning disable CS0162
            updating = true;
            Editor.Text = content;
            filePath = path;
            started = true; mode = ViewMode.Read; ApplyMode();
            dirty = false;
            Title = $"MarkdownLab — {Path.GetFileName(path)}";
            StatusText.Text = path;
            RefreshPreview();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo abrir", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { updating = false; }
#pragma warning restore CS0162
    }

    private bool SaveFile(bool saveAs = false)
    {
        var path = filePath;
        if (path is null || saveAs)
        {
            var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", DefaultExt = ".md", FileName = path is null ? "documento.md" : Path.GetFileName(path), InitialDirectory = path is null ? "" : Path.GetDirectoryName(path) };
            if (dialog.ShowDialog() != true) return false;
            path = dialog.FileName;
        }
        try
        {
            WriteDocumentSafely(path, Editor.Text);
            filePath = path; dirty = false; CaptureTab(); Remember(path); WriteRecovery(); RefreshPreview();
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
        var html = HighlightCode(EmbedLocalImages(Markdown.ToHtml(markdown, pipeline)));
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
        var document = $@"<!doctype html><html><head><meta http-equiv=""X-UA-Compatible"" content=""IE=edge""/><meta charset=""utf-8""/>{baseHref}<style>body{{font-family:Segoe UI,Arial,sans-serif;font-size:{Math.Round(16 * zoom)}px;line-height:1.65;margin:32px auto;padding:0 32px;max-width:900px;overflow-wrap:break-word}}img{{max-width:100%}}pre{{padding:12px;overflow:auto}}code{{padding:2px 4px}}table{{border-collapse:collapse;max-width:100%}}td,th{{border:1px solid #8791a3;padding:7px}}blockquote{{border-left:4px solid;padding-left:15px;margin-left:0}}hr{{opacity:.25}} .kw{{color:#6688df;font-weight:600}} .str{{color:#329b75}} .comment{{color:#8593a8;font-style:italic}}{css}</style></head><body>{html}</body></html>";
        lastHtml = document;
        if (ready) {
            try { Preview.NavigateToString(document); }
            catch (Exception ex) { StatusText.Text = "Error de vista previa: " + ex.Message; }
        }
    }

    private void ApplyMode()
    {
        FormatBar.Visibility = started && mode != ViewMode.Read ? Visibility.Visible : Visibility.Collapsed;
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
        CaptureTab();
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
        light = !light; ApplyTheme(); PersistAppearance();
    }
    private void ApplyTheme()
    {
        foreach (var (key, dark, bright) in new[] { ("Surface","#181B22","#FFFFFF"), ("Panel","#222630","#F0F3F8"), ("Ink","#E6EAF2","#202734"), ("Muted","#A4AFC1","#536175"), ("Edge","#383F4D","#CDD5E0"), ("Accent","#42669D","#A8C8F2") })
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? bright : dark));
        ApplyMode(); RefreshPreview();
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) { zoom = Math.Min(2.5, zoom + .1); Editor.FontSize = 15 * zoom; RefreshPreview(); PersistAppearance(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { zoom = Math.Max(.6, zoom - .1); Editor.FontSize = 15 * zoom; RefreshPreview(); }
    private void New_Click(object sender, RoutedEventArgs e) {
        AddDocument(null, "", false, ViewMode.Edit); Editor.Focus();
    }
    private void ToggleOutline_Click(object sender, RoutedEventArgs e) {
        outlineVisible = !outlineVisible;
        OutlineColumn.Width = new GridLength(outlineVisible ? 230 : 0);
        OutlineSplitter.Visibility = outlineVisible ? Visibility.Visible : Visibility.Collapsed;
    }
    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveFile(true);
    private void FocusSearch() { SearchBar.Visibility = Visibility.Visible; SearchBox.Focus(); SearchBox.SelectAll(); }
    private void EditorMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        Editor.Focus();
        switch (item.Tag as string)
        {
            case "undo": Editor.Undo(); break;
            case "redo": Editor.Redo(); break;
            case "cut": Editor.Cut(); break;
            case "copy": Editor.Copy(); break;
            case "paste": Editor.Paste(); break;
            case "all": Editor.SelectAll(); break;
            case "find": if (Editor.SelectionLength > 0) SearchBox.Text = Editor.SelectedText; FocusSearch(); break;
            case "word":
                var text = Editor.Text;
                int a = Editor.CaretOffset, b = a;
                while (a > 0 && !char.IsWhiteSpace(text[a - 1])) a--;
                while (b < text.Length && !char.IsWhiteSpace(text[b])) b++;
                Editor.Select(a, b - a); break;
        }
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string tag) return;
        ApplyFormat(tag);
    }

    private void ApplyFormat(string tag)
    {
        if (!started) return;
        if (mode == ViewMode.Read) { mode = ViewMode.Edit; ApplyMode(); }
        if (tag == "image") { InsertImage(); return; }
        if (tag == "table") { InsertTable(); return; }
        if (tag is "bullet" or "number" or "task") {
            if (activeList == tag && !suppressList) { suppressList = true; UpdateListButtons(); return; }
            suppressList = false;
        }
        var selected = Editor.SelectedText;
        var offset = Editor.SelectionStart;
        var length = Editor.SelectionLength;
        var text = selected.Length == 0 ? "texto" : selected;
        string replacement;
        int selectStart = 0, selectLength = 0;
        string? prefix = tag switch {
            "normal" => "", "h1" => "# ", "h2" => "## ", "h3" => "### ", "h4" => "#### ", "h5" => "##### ", "h6" => "###### ",
            "bullet" => "- ", "number" => "1. ", "task" => "- [ ] ", "quote" => "> ", _ => null
        };
        if (prefix is not null)
        {
            var first = Editor.Document.GetLineByOffset(offset);
            var last = Editor.Document.GetLineByOffset(offset + Math.Max(0, length - 1));
            offset = first.Offset; length = last.EndOffset - offset;
            var lines = Editor.Document.GetText(offset, length).Replace("\r\n", "\n").Split('\n');
            replacement = string.Join(Environment.NewLine, lines.Select((line, i) =>
                (tag == "number" ? $"{i + 1}. " : prefix) + (tag.StartsWith("h") || tag == "normal" ? Regex.Replace(line, @"^#{1,6}\s+", "") : tag is "bullet" or "number" or "task" ? Regex.Replace(line, @"^\s*(?:\d+\. |[-+*] (?:\[[ xX]\] )?)", "") : line)));
            selectStart = prefix.Length; selectLength = replacement.Length - selectStart;
        }
        else
        {
            (string before, string after) = tag switch {
                "bold" => ("**", "**"), "italic" => ("*", "*"), "strike" => ("~~", "~~"),
                "code" => ("\x60", "\x60"), "fence" => ("\n\x60\x60\x60\n", "\n\x60\x60\x60\n"),
                "link" => ("[", "](https://example.com)"), "image" => ("![", "](imagen.png)"), _ => ("", "")
            };
            if (tag == "table") replacement = "\n| Columna 1 | Columna 2 |\n| --- | --- |\n| Texto | Texto |\n";
            else if (tag == "rule") replacement = "\n---\n";
            else { replacement = before + text + after; selectStart = before.Length; selectLength = text.Length; }
        }
        Editor.Document.Replace(offset, length, replacement);
        Editor.Focus();
        Editor.Select(offset + selectStart, selectLength);
        if (selectLength == 0) Editor.CaretOffset = offset + replacement.Length;
        UpdateListButtons();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "Desconocida";
        var panel = new StackPanel { Margin = new Thickness(24), Width = 340 };
        panel.Children.Add(new Image { Source = Icon, Width = 64, Height = 64 });
        panel.Children.Add(new TextBlock { Text = "MarkdownLab", FontSize = 26, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0,12,0,4) });
        panel.Children.Add(new TextBlock { Text = $"Versión {version}\nDesarrollado por Sealsworn\nLicencia MIT\nLector y editor Markdown para Windows", TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,16) });
        var link = new Button { Content = "Repositorio oficial · GitHub" };
        panel.Children.Add(link);
        var close = new Button { Content = "Cerrar", IsCancel = true, IsDefault = true };
        panel.Children.Add(close);
        var dialog = Dialog("Acerca de MarkdownLab", panel);
        link.Click += (_, _) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/Sealsworn/MarkdownLab") { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(dialog, ex.Message); } };
        dialog.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void CloseSearch_Click(object sender, RoutedEventArgs e) { SearchBar.Visibility = Visibility.Collapsed; Editor.Focus(); }
    private void Wrap_Click(object sender, RoutedEventArgs e) => Editor.WordWrap = !Editor.WordWrap;
    private void Sync_Click(object sender, RoutedEventArgs e) { syncScroll = !syncScroll; StatusText.Text = syncScroll ? "Desplazamiento sincronizado" : "Desplazamiento independiente"; SyncPreview(); }
    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show("# Encabezado\n**Negrita** · *Cursiva* · ~~Tachado~~\n- Lista · 1. Numerada · - [ ] Tarea\n[Texto](URL) · ![Imagen](URL)\nCtrl+B / Ctrl+I · Alt+↑/↓ mover línea", "Sintaxis Markdown");
    private void Heading_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (HeadingPicker.SelectedItem is ComboBoxItem item && item.Tag is string tag) { ApplyFormat(tag); HeadingPicker.SelectedIndex = -1; }
    }
    private static readonly Regex ListPattern = new(@"^(\s*)(?:(\d+)\. |([-+*]) (\[[ xX]\] )?|(>) ?)(.*)$");
    private void UpdateListButtons()
    {
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        var m = ListPattern.Match(Editor.Document.GetText(line.Offset, line.Length));
        var next = !m.Success ? null : m.Groups[2].Success ? "number" : m.Groups[4].Success ? "task" : m.Groups[3].Success ? "bullet" : "quote";
        if (next != activeList) suppressList = false;
        activeList = next;
        foreach (var (button, tag) in new[] { (BulletButton,"bullet"), (NumberButton,"number"), (TaskButton,"task") })
            button.Background = (Brush)Resources[!suppressList && activeList == tag ? "Accent" : "Panel"];
    }
    private void EditorKeys(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.B or Key.I)
        { e.Handled = true; ApplyFormat(e.Key == Key.B ? "bold" : "italic"); return; }
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key is Key.Up or Key.Down)
        { e.Handled = true; MoveLine(e.Key == Key.Up); return; }
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || Editor.SelectionLength != 0) return;
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        if (Editor.CaretOffset != line.EndOffset) return;
        var m = ListPattern.Match(Editor.Document.GetText(line.Offset, line.Length));
        if (!m.Success) return;
        if (suppressList) { suppressList = false; return; }
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(m.Groups[6].Value)) { Editor.Document.Replace(line.Offset, line.Length, ""); Editor.CaretOffset = line.Offset; return; }
        var marker = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var n) && n < int.MaxValue ? $"{n+1}. " : m.Groups[4].Success ? m.Groups[3].Value + " [ ] " : m.Groups[3].Success ? m.Groups[3].Value + " " : "> ";
        var insert = Environment.NewLine + m.Groups[1].Value + marker;
        int offset = Editor.CaretOffset; Editor.Document.Insert(offset, insert); Editor.CaretOffset = offset + insert.Length;
    }
    private void AutoPair(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length != 1) return;
        var close = e.Text switch { "(" => ")", "[" => "]", _ => null };
        if (close is null) return;
        int offset = Editor.SelectionStart; var selected = Editor.SelectedText;
        Editor.Document.Replace(offset, Editor.SelectionLength, e.Text + selected + close);
        Editor.Select(offset + 1, selected.Length); e.Handled = true;
    }
    private void MoveLine(bool up)
    {
        var doc = Editor.Document; var line = doc.GetLineByOffset(Editor.CaretOffset);
        var other = up ? line.PreviousLine : line.NextLine; if (other is null) return;
        var first = up ? other : line; var second = up ? line : other;
        var a = doc.GetText(first.Offset, first.Length); var b = doc.GetText(second.Offset, second.Length);
        var sep = doc.GetText(first.EndOffset, first.DelimiterLength);
        int start = first.Offset; int col = Editor.CaretOffset - line.Offset;
        doc.Replace(start, second.EndOffset - start, b + sep + a);
        Editor.CaretOffset = start + (up ? 0 : b.Length + sep.Length) + col;
    }
    private void SyncPreview()
    {
        if (!ready || !syncScroll || mode != ViewMode.Split) return;
        var range = Editor.ExtentHeight - Editor.ViewportHeight;
        var fraction = range > 0 ? Math.Clamp(Editor.VerticalOffset / range, 0, 1) : 0;
        try { _ = Preview.ExecuteScriptAsync("window.scrollTo(0,(document.documentElement.scrollHeight-innerHeight)*" + fraction.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"); } catch { }
    }
    private Window Dialog(string title, StackPanel panel) => new() { Title = title, Owner = this, Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = (Brush)Resources["Panel"], Foreground = (Brush)Resources["Ink"] };
    private void InsertImage()
    {
        var panel = new StackPanel { Margin = new Thickness(16), Width = 360 };
        var alt = new TextBox { Text = Editor.SelectedText, Margin = new Thickness(0,4,0,8) };
        var url = new TextBox { Margin = new Thickness(0,4,0,8) };
        panel.Children.Add(new TextBlock { Text = "Texto alternativo" }); panel.Children.Add(alt);
        panel.Children.Add(new TextBlock { Text = "URL o ruta de imagen" }); panel.Children.Add(url);
        var browse = new Button { Content = "Elegir del dispositivo…" }; panel.Children.Add(browse);
        var ok = new Button { Content = "Insertar", IsDefault = true }; panel.Children.Add(ok);
        var dialog = Dialog("Insertar imagen", panel);
        browse.Click += (_, _) => { var picker = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp" }; if (picker.ShowDialog(dialog) == true) url.Text = picker.FileName; };
        ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(url.Text)) dialog.DialogResult = true; };
        if (dialog.ShowDialog() != true) return;
        var path = url.Text.Trim();
        if (File.Exists(path)) { InsertText(ImageMarkdown(path,alt.Text)); return; }
        InsertText("![" + alt.Text.Replace("]", "\\]") + "](<" + path.Replace(">", "%3E") + ">)");
    }
    private void InsertText(string value) { int offset = Editor.SelectionStart; Editor.Document.Replace(offset, Editor.SelectionLength, value); Editor.CaretOffset = offset + value.Length; Editor.Focus(); }
    private void InsertTable()
    {
        var panel = new StackPanel { Margin = new Thickness(16) };
        var label = new TextBlock { Text = "Elige columnas × filas (incluye encabezado)" }; panel.Children.Add(label);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Rows = 6 }; panel.Children.Add(grid);
        var dialog = Dialog("Insertar tabla", panel);
        var cells = new List<(Button Button,int Row,int Col)>();
        for (int r=1;r<=6;r++) for(int c=1;c<=8;c++) {
            int rr=r, cc=c; var b=new Button { Width=32, Height=32, Content="·", Margin=new Thickness(2) }; cells.Add((b,r,c)); grid.Children.Add(b);
            b.MouseEnter += (_,_) => { label.Text=$"{cc} columnas × {rr} filas"; foreach(var cell in cells) cell.Button.Background=(Brush)Resources[cell.Row<=rr && cell.Col<=cc ? "Accent" : "Surface"]; };
            b.Click += (_,_) => { dialog.Close(); var header="| "+string.Join(" | ",Enumerable.Range(1,cc).Select(i=>$"Columna {i}"))+" |"; var divider="| "+string.Join(" | ",Enumerable.Repeat("---",cc))+" |"; var row="| "+string.Join(" | ",Enumerable.Repeat(" ",cc))+" |"; InsertText("\n"+header+"\n"+divider+"\n"+string.Concat(Enumerable.Repeat(row+"\n",rr-1))); };
        }
        dialog.ShowDialog();
    }
    private string EmbedLocalImages(string html)
    {
        return Regex.Replace(html, @"(<img\b[^>]*?\bsrc="")([^""]+)("")", m => {
            try {
                var src = WebUtility.HtmlDecode(m.Groups[2].Value);
                string path;
                if (Uri.TryCreate(src, UriKind.Absolute, out var uri)) { if (!uri.IsFile) return m.Value; path=uri.LocalPath; }
                else { if (filePath is null) return m.Value; path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath))!,Uri.UnescapeDataString(src)); }
                var mime=Path.GetExtension(path).ToLowerInvariant() switch { ".png"=>"image/png", ".jpg" or ".jpeg"=>"image/jpeg", ".gif"=>"image/gif", ".webp"=>"image/webp", ".bmp"=>"image/bmp", _=>null };
                if (mime is null || !File.Exists(path)) return m.Value;
                var token = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path) + File.GetLastWriteTimeUtc(path).Ticks))) + Path.GetExtension(path); var target = Path.Combine(WorkspaceFolder, "images", token); Directory.CreateDirectory(Path.GetDirectoryName(target)!); if (!File.Exists(target)) File.Copy(path,target); return m.Groups[1].Value+"https://markdownlab-assets.invalid/"+token+m.Groups[3].Value;
            } catch { return m.Value; }
        }, RegexOptions.IgnoreCase);
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
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.FirstOrDefault(p => File.Exists(p) && (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))) is { } path ) LoadFile(path);
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] images && images.Any(IsImage)) { if (!started) AddDocument(null,"",false,ViewMode.Edit); mode=ViewMode.Edit; ApplyMode(); foreach(var image in images.Where(IsImage)) InsertText("\n"+ImageMarkdown(image,Path.GetFileNameWithoutExtension(image))+"\n"); e.Handled=true; }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { e.Cancel = !ConfirmExit(); base.OnClosing(e); }
}