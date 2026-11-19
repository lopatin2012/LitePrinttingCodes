using System.Diagnostics;
using System.Text;
using Gs1LabelPrinter.Core;

namespace Gs1LabelPrinter.UI;

public sealed partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly LabelSettings S;
    private readonly System.Windows.Forms.Timer _rebuildTimer;
    private readonly ToolTip _toolTip = new();

    private SplitContainer _split = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private LabelPreviewControl _preview = null!;

    private ComboBox _printerCombo = null!;
    private Button _refreshPrintersButton = null!;
    private Label _printerInfoLabel = null!;

    private CheckBox _saveJobCheck = null!;
    private Label _jobStateLabel = null!;
    private Label _jobPathLabel = null!;
    private Label _jobFolderLabel = null!;
    private Button _chooseJobFolderButton = null!;
    private Button _openJobFolderButton = null!;

    private Button _openFileButton = null!;
    private Label _filePathLabel = null!;
    private TextBox _productTextBox = null!;
    private Label _fileInfoLabel = null!;

    private NumericUpDown _widthNumeric = null!;
    private NumericUpDown _heightNumeric = null!;
    private ComboBox _dpiCombo = null!;
    private Label _labelSizeInfoLabel = null!;

    private NumericUpDown _dmXNumeric = null!;
    private NumericUpDown _dmYNumeric = null!;
    private NumericUpDown _moduleNumeric = null!;
    private Label _symbolInfoLabel = null!;
    private Label _dmSizeInfoLabel = null!;
    private CheckBox _fixSizeCheck = null!;

    private NumericUpDown _productFontNumeric = null!;
    private NumericUpDown _codeFontNumeric = null!;
    private NumericUpDown _gapNumeric = null!;
    private NumericUpDown _textGapNumeric = null!;
    private NumericUpDown _codeBreaksNumeric = null!;
    private CheckBox _showCodeCheck = null!;

    private NumericUpDown _copiesNumeric = null!;
    private Button _printAllButton = null!;
    private Button _printCurrentButton = null!;
    private Button _saveZplButton = null!;
    private Label _printHintLabel = null!;

    private NumericUpDown _codeIndexNumeric = null!;
    private Label _codeCountLabel = null!;
    private Label _zoomLabel = null!;

    private LabelFile? _file;
    private LabelRenderResult? _previewData;
    private int _codeIndex;
    private bool _updatingUi;
    private bool _printing;
    private bool _settingsDirty;

    public MainForm(AppSettings settings)
    {
        _settings = settings ?? new AppSettings();
        S = _settings.Label ?? new LabelSettings();
        _settings.Label = S;

        BuildUi();
        _rebuildTimer = new System.Windows.Forms.Timer { Interval = 130 };
        _rebuildTimer.Tick += RebuildTimer_Tick;

        WireSettingEvents();
        LoadSettingsIntoControls();
        PopulatePrinters();
        UpdateUiState();
        UpdateZoomLabel();
        UpdateSymbolInfo();
        UpdateLabelSizeInfo();
        UpdateJobInfo();
    }

    private string JobFolder =>
        string.IsNullOrWhiteSpace(_settings.JobFolder)
            ? AppData.DefaultJobFolder
            : _settings.JobFolder;

    private string? SelectedPrinter
    {
        get
        {
            if (_printerCombo.SelectedItem is not string name)
                return null;
            return name.StartsWith('(') ? null : name;
        }
    }

    private void WireSettingEvents()
    {
        _widthNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.LabelWidthDots = (int)_widthNumeric.Value;
            UpdateLabelSizeInfo();
            SchedulePreview();
            MarkDirty();
        };

        _heightNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.LabelHeightDots = (int)_heightNumeric.Value;
            UpdateLabelSizeInfo();
            _preview.Invalidate();
            MarkDirty();
            UpdateStatusAfterLayout();
        };

        _dmXNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.DmX = (int)_dmXNumeric.Value;
            SchedulePreview();
            UpdateSymbolInfo();
            MarkDirty();
            UpdateStatusAfterLayout();
        };

        _dmYNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.DmY = (int)_dmYNumeric.Value;
            _preview.Invalidate();
            UpdateSymbolInfo();
            MarkDirty();
            UpdateStatusAfterLayout();
        };

        _moduleNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.ModuleSizeDots = (int)_moduleNumeric.Value;
            _preview.Invalidate();
            UpdateSymbolInfo();
            MarkDirty();
            UpdateStatusAfterLayout();
        };

        _productFontNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.ProductFontPx = (float)_productFontNumeric.Value;
            SchedulePreview();
            MarkDirty();
        };

        _codeFontNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.CodeFontPx = (float)_codeFontNumeric.Value;
            SchedulePreview();
            MarkDirty();
        };

        _gapNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.LineGapPx = (float)_gapNumeric.Value;
            SchedulePreview();
            MarkDirty();
        };

        _textGapNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.TextGapDots = (int)_textGapNumeric.Value;
            _preview.Invalidate();
            MarkDirty();
            UpdateStatusAfterLayout();
        };

        _codeBreaksNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.CodeBreaks = (int)_codeBreaksNumeric.Value;
            SchedulePreview();
            MarkDirty();
        };

        _copiesNumeric.ValueChanged += (_, _) =>
        {
            if (_updatingUi) return;
            S.Copies = (int)_copiesNumeric.Value;
            UpdatePrintButtonText();
            MarkDirty();
        };
    }

    private void LoadSettingsIntoControls()
    {
        _updatingUi = true;
        try
        {
            _widthNumeric.Value = Math.Clamp(S.LabelWidthDots,
                (int)_widthNumeric.Minimum, (int)_widthNumeric.Maximum);
            _heightNumeric.Value = Math.Clamp(S.LabelHeightDots,
                (int)_heightNumeric.Minimum, (int)_heightNumeric.Maximum);
            _dpiCombo.SelectedIndex = S.Dpi == 300 ? 1 : 0;

            _dmXNumeric.Value = Math.Clamp(S.DmX, (int)_dmXNumeric.Minimum, (int)_dmXNumeric.Maximum);
            _dmYNumeric.Value = Math.Clamp(S.DmY, (int)_dmYNumeric.Minimum, (int)_dmYNumeric.Maximum);
            _moduleNumeric.Value = Math.Clamp(S.ModuleSizeDots,
                (int)_moduleNumeric.Minimum, (int)_moduleNumeric.Maximum);

            _fixSizeCheck.Checked = S.FixSymbolSize;
            _showCodeCheck.Checked = S.ShowCodeText;

            _productFontNumeric.Value = Math.Clamp((decimal)Math.Round(S.ProductFontPx),
                _productFontNumeric.Minimum, _productFontNumeric.Maximum);
            _codeFontNumeric.Value = Math.Clamp((decimal)Math.Round(S.CodeFontPx),
                _codeFontNumeric.Minimum, _codeFontNumeric.Maximum);
            _gapNumeric.Value = Math.Clamp((decimal)Math.Round(S.LineGapPx),
                _gapNumeric.Minimum, _gapNumeric.Maximum);
            _textGapNumeric.Value = Math.Clamp(S.TextGapDots,
                (int)_textGapNumeric.Minimum, (int)_textGapNumeric.Maximum);
            _codeBreaksNumeric.Value = Math.Clamp(S.CodeBreaks,
                (int)_codeBreaksNumeric.Minimum, (int)_codeBreaksNumeric.Maximum);

            _copiesNumeric.Value = Math.Clamp(S.Copies,
                (int)_copiesNumeric.Minimum, (int)_copiesNumeric.Maximum);

            _saveJobCheck.Checked = _settings.SaveJobFiles;

            _preview.ZoomPercent = _settings.PreviewZoomPercent;
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void PopulatePrinters()
    {
        var previous = _printerCombo.SelectedItem as string;
        var printers = RawPrinter.GetInstalledPrinters();

        _updatingUi = true;
        try
        {
            _printerCombo.Items.Clear();

            var items = new List<string>(printers);
            var index = -1;

            if (printers.Count == 0)
            {
                items.Add("(принтеры не найдены)");
                index = 0;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(previous) && !previous.StartsWith('('))
                    index = printers.FindIndex(p => p.Equals(previous, StringComparison.OrdinalIgnoreCase));

                // Выбор пользователя имеет приоритет (в т.ч. нестандартное имя принтера).
                if (index < 0 && !string.IsNullOrWhiteSpace(_settings.PrinterName))
                    index = printers.FindIndex(p => p.Equals(_settings.PrinterName, StringComparison.OrdinalIgnoreCase));

                if (index < 0)
                    index = printers.FindIndex(p => p.Contains("ZD421", StringComparison.OrdinalIgnoreCase));

                // Если ZD421 не найден, выбираем только явно «этикеточный» ZPL-драйвер,
                // но не обычный офисный принтер по умолчанию.
                if (index < 0)
                {
                    index = printers.FindIndex(p =>
                        p.Contains("ZDesigner", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("Zebra", StringComparison.OrdinalIgnoreCase) ||
                        p.Contains("ZPL", StringComparison.OrdinalIgnoreCase));
                }

                if (index < 0)
                {
                    items.Insert(0, "(выберите принтер)");
                    index = 0;
                }
            }

            foreach (var item in items)
                _printerCombo.Items.Add(item);

            _printerCombo.SelectedIndex = index;
        }
        finally
        {
            _updatingUi = false;
        }

        UpdatePrinterInfo();
        UpdateUiState();
    }

    private void UpdatePrinterInfo()
    {
        var printer = SelectedPrinter;
        if (printer is null)
        {
            _printerInfoLabel.Text =
                "Принтер не выбран. Установите драйвер ZDesigner ZD421 (ZPL) и нажмите «Обновить».";
            _printerInfoLabel.ForeColor = Color.FromArgb(180, 50, 40);
            return;
        }

        _printerInfoLabel.Text = "RAW-задание ZPL → " + printer;
        _printerInfoLabel.ForeColor = Color.FromArgb(80, 120, 85);
    }

    private void SaveJobCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingUi) return;

        _settings.SaveJobFiles = _saveJobCheck.Checked;
        _settingsDirty = true;
        UpdateJobInfo();
    }

    private void ChooseJobFolder_Click(object? sender, EventArgs e)
    {
        var initialFolder = Directory.Exists(JobFolder)
            ? JobFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка для TXT-файлов заданий",
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
            dialog.SelectedPath = initialFolder;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings.JobFolder = dialog.SelectedPath;
        _settingsDirty = true;
        UpdateJobInfo();
    }

    private void OpenJobFolder_Click(object? sender, EventArgs e)
    {
        try
        {
            Directory.CreateDirectory(JobFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = JobFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Не удалось открыть папку заданий." + Environment.NewLine + Environment.NewLine +
                JobFolder + Environment.NewLine + Environment.NewLine + ex.Message,
                "Папка заданий",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void UpdateJobInfo()
    {
        _chooseJobFolderButton.Enabled = _settings.SaveJobFiles;
        _openJobFolderButton.Enabled = true;

        _jobFolderLabel.Text = "Папка: " + JobFolder;
        _toolTip.SetToolTip(_jobFolderLabel, JobFolder);

        if (!_settings.SaveJobFiles)
        {
            _jobStateLabel.Text = "Сохранение файла задания отключено.";
            _jobStateLabel.ForeColor = Color.FromArgb(150, 100, 40);
            _jobPathLabel.Text = "";
            _toolTip.SetToolTip(_jobPathLabel, null);
            return;
        }

        if (_file is null)
        {
            _jobStateLabel.Text = "Файл задания появится после загрузки файла с кодами.";
            _jobStateLabel.ForeColor = Color.FromArgb(95, 105, 118);
            _jobPathLabel.Text = "";
            _toolTip.SetToolTip(_jobPathLabel, null);
            return;
        }

        var path = JobFileWriter.GetJobFilePath(JobFolder, _file);
        var exists = File.Exists(path);

        _jobStateLabel.Text = exists
            ? "Существующий файл — новые коды будут добавлены в него."
            : "Файл будет создан при первой печати.";
        _jobStateLabel.ForeColor = exists
            ? Color.FromArgb(35, 95, 160)
            : Color.FromArgb(95, 105, 118);

        _jobPathLabel.Text = "Файл: " + Path.GetFileName(path);
        _jobPathLabel.ForeColor = Color.FromArgb(35, 95, 160);
        _toolTip.SetToolTip(_jobPathLabel, path);
    }

    private void MainForm_Shown(object? sender, EventArgs e)
    {
        _split.Panel1MinSize = 520;
        _split.Panel2MinSize = 420;

        var panel2 = Math.Max(420, (int)(ClientSize.Width * 0.34));
        var distance = Math.Max(_split.Panel1MinSize, _split.Width - panel2);
        if (distance < _split.Width - _split.Panel2MinSize)
            _split.SplitterDistance = distance;

        UpdateZoomLabel();

        if (_settings.AutoLoadLastFile &&
            !string.IsNullOrWhiteSpace(_settings.LastFilePath) &&
            File.Exists(_settings.LastFilePath))
        {
            LoadFile(_settings.LastFilePath, silent: true);
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _rebuildTimer.Stop();
        if (_settingsDirty)
            SettingsStore.Save(_settings);

        _preview.Data = null;
        _previewData?.Dispose();
        _previewData = null;

        _rebuildTimer.Dispose();
        _toolTip.Dispose();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.O)
        {
            OpenFile_Click(sender, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.P)
        {
            PrintCurrent_Click(sender, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.F5)
        {
            RebuildPreview();
            e.Handled = true;
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        LoadFile(files[0]);
    }

    private void OpenFile_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файл с GS1-кодами",
            Filter = "Текстовые файлы (*.txt;*.csv)|*.txt;*.csv|Все файлы (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        var initial = _file is not null
            ? Path.GetDirectoryName(_file.FilePath)
            : !string.IsNullOrWhiteSpace(_settings.LastFilePath)
                ? Path.GetDirectoryName(_settings.LastFilePath)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
            dialog.InitialDirectory = initial;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadFile(dialog.FileName);
    }

    public void LoadFile(string path, bool silent = false)
    {
        try
        {
            var parsed = CodeFileParser.Load(path);

            _rebuildTimer.Stop();
            _preview.Data = null;
            _previewData?.Dispose();
            _previewData = null;

            _file = parsed;
            _codeIndex = 0;

            var key = parsed.SettingsKey;
            if (!_settings.ProductLabels.TryGetValue(key, out var label))
                label = CodeFileParser.MakeShortProductName(parsed.FullProductName);
            _settings.ProductLabels[key] = label;

            _updatingUi = true;
            try
            {
                _productTextBox.Text = label;
                _filePathLabel.Text = parsed.FilePath;
                _fileInfoLabel.Text = BuildFileInfo(parsed);
                _toolTip.SetToolTip(_fileInfoLabel, _fileInfoLabel.Text);
                _fileInfoLabel.ForeColor = parsed.DeclaredQuantity == parsed.Codes.Count
                    ? Color.FromArgb(100, 110, 124)
                    : Color.FromArgb(185, 95, 20);
                _codeIndexNumeric.Maximum = Math.Max(1, parsed.Codes.Count);
                _codeIndexNumeric.Value = 1;
                _codeCountLabel.Text = "из " + parsed.Codes.Count;
            }
            finally
            {
                _updatingUi = false;
            }

            _toolTip.SetToolTip(_filePathLabel, parsed.FilePath);
            _settings.LastFilePath = parsed.FilePath;
            _settingsDirty = true;
            Text = "Печать GS1 DataMatrix — " + parsed.FileName;

            UpdatePrintButtonText();
            UpdateUiState();
            UpdateJobInfo();
            RebuildPreview();

            _statusLabel.Text = $"Загружено кодов: {parsed.Codes.Count}";
            _statusLabel.ForeColor = Color.FromArgb(60, 110, 70);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Не удалось открыть файл: " + ex.Message;
            _statusLabel.ForeColor = Color.FromArgb(190, 40, 35);

            if (!silent)
            {
                MessageBox.Show(
                    this,
                    "Не удалось прочитать файл." + Environment.NewLine + Environment.NewLine +
                    ex.Message + Environment.NewLine + Environment.NewLine +
                    "Ожидается обычный текстовый файл: один GS1-код в строке.",
                    "Ошибка файла",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    private static string BuildFileInfo(LabelFile file)
    {
        var info = $"Кодов: {file.Codes.Count}";
        if (!string.IsNullOrWhiteSpace(file.Gtin))
            info += $"   ·   GTIN: {file.Gtin}";
        if (file.DeclaredQuantity != file.Codes.Count)
            info += $"   ·   в имени файла: {file.DeclaredQuantity} (не совпадает)";
        return info;
    }

    private void ProductTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_updatingUi || _file is null)
            return;

        _settings.ProductLabels[_file.SettingsKey] = _productTextBox.Text;
        _settingsDirty = true;
        SchedulePreview();
    }

    private void CodeIndexNumeric_ValueChanged(object? sender, EventArgs e)
    {
        if (_updatingUi || _file is null)
            return;

        var index = (int)_codeIndexNumeric.Value - 1;
        if (index < 0 || index >= _file.Codes.Count)
            return;

        _codeIndex = index;
        SchedulePreview();
    }

    private void PreviousCode_Click(object? sender, EventArgs e)
    {
        if (_file is null) return;
        var value = Math.Max(1, (int)_codeIndexNumeric.Value - 1);
        _codeIndexNumeric.Value = value;
    }

    private void NextCode_Click(object? sender, EventArgs e)
    {
        if (_file is null) return;
        var value = Math.Min(_file.Codes.Count, (int)_codeIndexNumeric.Value + 1);
        _codeIndexNumeric.Value = value;
    }

    private void SchedulePreview()
    {
        if (_file is null)
        {
            _preview.Invalidate();
            return;
        }

        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    private void RebuildTimer_Tick(object? sender, EventArgs e)
    {
        _rebuildTimer.Stop();
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        if (_file is null || _file.Codes.Count == 0)
        {
            _preview.Data = null;
            _previewData?.Dispose();
            _previewData = null;
            UpdateSymbolInfo();
            return;
        }

        if (_codeIndex < 0 || _codeIndex >= _file.Codes.Count)
            _codeIndex = Math.Clamp(_codeIndex, 0, _file.Codes.Count - 1);

        try
        {
            var product = _productTextBox.Text.Trim();
            var result = LabelRenderer.Build(_file, _codeIndex, product, S);

            var old = _previewData;
            _previewData = result;
            _preview.Data = result;
            old?.Dispose();

            UpdateSymbolInfo();
            _preview.Invalidate();
            UpdateStatusAfterLayout();
        }
        catch (Exception ex)
        {
            _preview.Data = null;
            _previewData?.Dispose();
            _previewData = null;
            UpdateSymbolInfo();
            _preview.Invalidate();

            _statusLabel.Text = "Ошибка: " + ex.Message;
            _statusLabel.ForeColor = Color.FromArgb(190, 40, 35);
        }
    }

    private void UpdateStatusAfterLayout()
    {
        if (_previewData is null)
            return;

        try
        {
            var layout = _previewData.GetLayout(S);
            if (layout.HasWarnings)
            {
                _statusLabel.Text = layout.Warnings[0];
                _statusLabel.ForeColor = Color.FromArgb(190, 40, 35);
            }
            else
            {
                _statusLabel.Text =
                    $"Готово · DataMatrix {_previewData.SymbolSize}×{_previewData.SymbolSize} · текст {layout.TextHeight} точ.";
                _statusLabel.ForeColor = Color.FromArgb(60, 110, 70);
            }
        }
        catch
        {
            // не критично для UI
        }
    }

    private void UpdateSymbolInfo()
    {
        if (_previewData is null)
        {
            _symbolInfoLabel.Text = "—";
            _dmSizeInfoLabel.Text = "Символ появится после загрузки файла.";
            return;
        }

        var symbol = _previewData.SymbolSize;
        var pixels = symbol * S.ModuleSizeDots;
        var mm = S.DotsToMm(pixels);
        _symbolInfoLabel.Text = $"{symbol}×{symbol}";
        _dmSizeInfoLabel.Text =
            $"{symbol}×{symbol} модулей · {pixels}×{pixels} точек · {mm:0.#}×{mm:0.#} мм";
    }

    private void UpdateLabelSizeInfo()
    {
        _labelSizeInfoLabel.Text =
            $"{S.LabelWidthDots}×{S.LabelHeightDots} точек · " +
            $"{S.DotsToMm(S.LabelWidthDots):0.#}×{S.DotsToMm(S.LabelHeightDots):0.#} мм";
    }

    private void UpdateZoomLabel()
    {
        _preview.ZoomPercent = _settings.PreviewZoomPercent;
        _zoomLabel.Text = _settings.PreviewZoomPercent == 0
            ? "вписать"
            : _settings.PreviewZoomPercent + "%";
        _preview.Invalidate();
    }

    private void ChangeZoom(int direction)
    {
        var current = _settings.PreviewZoomPercent;
        if (current == 0)
            current = _preview.GetFitScalePercent();

        var next = direction > 0
            ? (int)Math.Round(current * 1.25)
            : (int)Math.Round(current * 0.8);

        SetZoom(next);
    }

    private void SetZoom(int percent)
    {
        _settings.PreviewZoomPercent = percent <= 0 ? 0 : Math.Clamp(percent, 10, 400);
        _settingsDirty = true;
        UpdateZoomLabel();
    }

    private void Preview_DmMoved(object? sender, EventArgs e)
    {
        _updatingUi = true;
        try
        {
            _dmXNumeric.Value = Math.Clamp(S.DmX, (int)_dmXNumeric.Minimum, (int)_dmXNumeric.Maximum);
            _dmYNumeric.Value = Math.Clamp(S.DmY, (int)_dmYNumeric.Minimum, (int)_dmYNumeric.Maximum);
        }
        finally
        {
            _updatingUi = false;
        }

        _settingsDirty = true;
        UpdateSymbolInfo();
        UpdateStatusAfterLayout();
        SchedulePreview();
    }

    private void Preview_DmMoveCompleted(object? sender, EventArgs e)
    {
        _settingsDirty = true;
        UpdateStatusAfterLayout();
    }

    private void NudgeDm(int dx, int dy)
    {
        if (_file is null)
            return;

        var step = ModifierKeys.HasFlag(Keys.Shift) ? 10 : 1;
        var dmSize = (_previewData?.SymbolSize ?? 1) * S.ModuleSizeDots;
        var maxX = Math.Max(0, S.LabelWidthDots - dmSize);
        var maxY = Math.Max(0, S.LabelHeightDots - dmSize);

        S.DmX = Math.Clamp(S.DmX + dx * step, 0, maxX);
        S.DmY = Math.Clamp(S.DmY + dy * step, 0, maxY);

        _updatingUi = true;
        try
        {
            _dmXNumeric.Value = Math.Clamp(S.DmX, (int)_dmXNumeric.Minimum, (int)_dmXNumeric.Maximum);
            _dmYNumeric.Value = Math.Clamp(S.DmY, (int)_dmYNumeric.Minimum, (int)_dmYNumeric.Maximum);
        }
        finally
        {
            _updatingUi = false;
        }

        _settingsDirty = true;
        _preview.Invalidate();
        UpdateSymbolInfo();
        UpdateStatusAfterLayout();
        SchedulePreview();
    }

    private void ResetDmPosition()
    {
        S.DmX = 19;
        S.DmY = 40;

        _updatingUi = true;
        try
        {
            _dmXNumeric.Value = Math.Clamp(S.DmX, (int)_dmXNumeric.Minimum, (int)_dmXNumeric.Maximum);
            _dmYNumeric.Value = Math.Clamp(S.DmY, (int)_dmYNumeric.Minimum, (int)_dmYNumeric.Maximum);
        }
        finally
        {
            _updatingUi = false;
        }

        _settingsDirty = true;
        _preview.Invalidate();
        UpdateSymbolInfo();
        UpdateStatusAfterLayout();
        SchedulePreview();
    }

    private void FixSizeCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingUi) return;
        S.FixSymbolSize = _fixSizeCheck.Checked;
        _settingsDirty = true;
        UpdateStatusAfterLayout();
    }

    private void ShowCodeCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_updatingUi) return;
        S.ShowCodeText = _showCodeCheck.Checked;
        _settingsDirty = true;
        SchedulePreview();
    }

    private void DpiCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updatingUi) return;
        S.Dpi = _dpiCombo.SelectedIndex == 1 ? 300 : 203;
        _settingsDirty = true;
        UpdateLabelSizeInfo();
        UpdateSymbolInfo();
        _preview.Invalidate();
    }

    private void PrinterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updatingUi) return;

        if (SelectedPrinter is { } printer)
            _settings.PrinterName = printer;

        UpdatePrinterInfo();
        UpdateUiState();
        _settingsDirty = true;
    }

    private void RefreshPrinters_Click(object? sender, EventArgs e)
    {
        PopulatePrinters();
    }

    private void UpdatePrintButtonText()
    {
        _printAllButton.Text = _file is null
            ? "Печать всех"
            : $"Печать всех ({_file.Codes.Count * Math.Max(1, S.Copies)})";
    }

    private void UpdateUiState()
    {
        var hasFile = _file is { Codes.Count: > 0 };
        var hasPrinter = SelectedPrinter is not null;

        _codeIndexNumeric.Enabled = hasFile;
        _printAllButton.Enabled = hasFile && hasPrinter && !_printing;
        _printCurrentButton.Enabled = hasFile && hasPrinter && !_printing;
        _saveZplButton.Enabled = hasFile && !_printing;
        _openFileButton.Enabled = !_printing;

        UpdatePrintButtonText();

        if (!hasPrinter)
        {
            _printHintLabel.Text =
                "Принтер не выбран. Установите ZDesigner ZD421 (ZPL), затем нажмите «Обновить».";
            _printHintLabel.ForeColor = Color.FromArgb(180, 50, 40);
        }
        else if (!hasFile)
        {
            _printHintLabel.Text =
                "Откройте файл: один GS1-код в строке. Можно перетащить файл в окно.";
            _printHintLabel.ForeColor = Color.FromArgb(100, 110, 124);
        }
        else
        {
            _printHintLabel.Text =
                "Перед печатью всех будет подтверждение. Данные отправляются сразу на принтер.";
            _printHintLabel.ForeColor = Color.FromArgb(100, 110, 124);
        }
    }

    private void MarkDirty() => _settingsDirty = true;

    private void PrintAll_Click(object? sender, EventArgs e)
    {
        if (_file is null)
        {
            MessageBox.Show(this, "Сначала откройте файл с кодами.", "Печать",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var printer = SelectedPrinter;
        if (printer is null)
        {
            ShowNoPrinter();
            return;
        }

        var copies = Math.Max(1, S.Copies);
        var total = _file.Codes.Count * copies;

        var answer = MessageBox.Show(
            this,
            $"Отправить {total} этикеток на принтер?" + Environment.NewLine + Environment.NewLine +
            $"Принтер: {printer}" + Environment.NewLine +
            $"Файл: {_file.FileName}" + Environment.NewLine +
            $"Кодов: {_file.Codes.Count}, копий каждого: {copies}" + Environment.NewLine + Environment.NewLine +
            GetJobInfoForMessage(),
            "Печать всех",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        var indices = Enumerable.Range(0, _file.Codes.Count).ToArray();
        RunPrint(indices, printer, "все коды");
    }

    private string GetJobInfoForMessage()
    {
        if (!_settings.SaveJobFiles)
            return "Файл задания: сохранение отключено.";

        var path = JobFileWriter.GetJobFilePath(JobFolder, _file!);
        var exists = File.Exists(path);
        return "Файл задания:" + Environment.NewLine + path + Environment.NewLine +
               (exists ? "(существующий — коды будут добавлены)" : "(новый — будет создан)");
    }

    private void PrintCurrent_Click(object? sender, EventArgs e)
    {
        if (_file is null)
        {
            MessageBox.Show(this, "Сначала откройте файл с кодами.", "Печать",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var printer = SelectedPrinter;
        if (printer is null)
        {
            ShowNoPrinter();
            return;
        }

        RunPrint(new[] { _codeIndex }, printer, "текущий код");
    }

    private void ShowNoPrinter()
    {
        MessageBox.Show(
            this,
            "Принтер не выбран." + Environment.NewLine + Environment.NewLine +
            "Установите драйвер ZDesigner ZD421 (ZPL), затем нажмите «Обновить» в разделе «Принтер».",
            "Принтер",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void RunPrint(IReadOnlyList<int> indices, string printer, string mode)
    {
        if (_file is null || indices.Count == 0)
            return;

        var copies = Math.Max(1, S.Copies);
        var total = indices.Count * copies;

        if (total <= 1)
        {
            PrintSynchronously(indices, printer, mode);
            return;
        }

        var outcome = new PrintOutcome();
        var result = new PrintResult();
        outcome.Result = result;

        using var cancellation = new CancellationTokenSource();
        using var progressForm = new PrintProgressForm(total);
        progressForm.CancelRequested += (_, _) => cancellation.Cancel();

        _printing = true;
        UpdateUiState();

        progressForm.Shown += (_, _) =>
        {
            var progress = new Progress<int>(value => progressForm.SetProgress(value, total));

            Task.Run(() =>
            {
                try
                {
                    DoPrint(indices, printer, progress, cancellation.Token, mode, result);
                    progressForm.Finish(true, null);
                }
                catch (OperationCanceledException)
                {
                    outcome.Cancelled = true;
                    progressForm.Finish(false, "Печать отменена.");
                }
                catch (Exception ex)
                {
                    outcome.Error = ex;
                    progressForm.Finish(false, ex.Message);
                }
            });
        };

        try
        {
            progressForm.ShowDialog(this);
        }
        finally
        {
            _printing = false;
            UpdateUiState();
            UpdateJobInfo();
        }

        var sent = result.Sent;

        if (outcome.Error is not null)
        {
            _statusLabel.Text = "Ошибка печати.";
            _statusLabel.ForeColor = Color.FromArgb(190, 40, 35);

            var jobLine = string.IsNullOrWhiteSpace(result.JobPath)
                ? ""
                : Environment.NewLine + "Файл задания: " + result.JobPath;

            MessageBox.Show(
                this,
                "Не удалось отправить данные на принтер." + Environment.NewLine + Environment.NewLine +
                outcome.Error.Message + jobLine + Environment.NewLine + Environment.NewLine +
                "Проверьте, что принтер включён, выбран ZPL-драйвер и доступен порт." + Environment.NewLine +
                $"Уже отправлено этикеток: {sent} из {total}.",
                "Ошибка печати",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        else if (outcome.Cancelled)
        {
            _statusLabel.Text = $"Печать отменена. Отправлено {sent} из {total}." +
                                JobStatusSuffix(result.JobPath);
            _statusLabel.ForeColor = Color.FromArgb(185, 95, 20);
        }
        else
        {
            _statusLabel.Text = PrintStatus(sent, printer, result.JobPath);
            _statusLabel.ForeColor = Color.FromArgb(60, 110, 70);
        }
    }

    private void PrintSynchronously(IReadOnlyList<int> indices, string printer, string mode)
    {
        var result = new PrintResult();

        try
        {
            _printing = true;
            UpdateUiState();
            UseWaitCursor = true;

            DoPrint(indices, printer, null, CancellationToken.None, mode, result);

            _statusLabel.Text = PrintStatus(result.Sent, printer, result.JobPath);
            _statusLabel.ForeColor = Color.FromArgb(60, 110, 70);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка печати.";
            _statusLabel.ForeColor = Color.FromArgb(190, 40, 35);

            var jobLine = string.IsNullOrWhiteSpace(result.JobPath)
                ? ""
                : Environment.NewLine + "Файл задания: " + result.JobPath;

            MessageBox.Show(
                this,
                "Не удалось отправить данные на принтер." + Environment.NewLine + Environment.NewLine +
                ex.Message + jobLine + Environment.NewLine + Environment.NewLine +
                "Проверьте, что принтер включён, выбран ZPL-драйвер и доступен порт.",
                "Ошибка печати",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _printing = false;
            UpdateUiState();
            UpdateJobInfo();
        }
    }

    private static string PrintStatus(int sent, string printer, string? jobPath)
    {
        return $"Отправлено этикеток: {sent}. Принтер: {printer}" + JobStatusSuffix(jobPath);
    }

    private static string JobStatusSuffix(string? jobPath)
    {
        return string.IsNullOrWhiteSpace(jobPath)
            ? ""
            : " · Задание: " + Path.GetFileName(jobPath);
    }

    private void DoPrint(
        IReadOnlyList<int> indices,
        string printer,
        IProgress<int>? progress,
        CancellationToken token,
        string mode,
        PrintResult result)
    {
        if (_file is null)
            return;

        var product = _productTextBox.Text.Trim();
        var copies = Math.Clamp(S.Copies, 1, 100);
        var total = indices.Count * copies;

        JobFileWriter? job = null;
        var prepared = 0;
        var inChunk = 0;
        var chunkCodes = new List<string>(20);
        using var buffer = new MemoryStream(256 * 1024);

        try
        {
            if (_settings.SaveJobFiles)
            {
                job = JobFileWriter.OpenOrCreate(
                    JobFolder,
                    _file,
                    product,
                    printer,
                    copies,
                    mode,
                    total);
                result.JobPath = job.FilePath;
            }

            foreach (var index in indices)
            {
                if (index < 0 || index >= _file.Codes.Count)
                    throw new ArgumentOutOfRangeException(nameof(indices), "Некорректный номер кода.");

                for (var copy = 0; copy < copies; copy++)
                {
                    token.ThrowIfCancellationRequested();

                    using var label = LabelRenderer.Build(_file, index, product, S);
                    var zpl = Encoding.UTF8.GetBytes(label.BuildZpl(S));
                    buffer.Write(zpl, 0, zpl.Length);
                    chunkCodes.Add(label.RawCode);

                    prepared++;
                    inChunk++;

                    if (inChunk >= 20 || prepared == total)
                    {
                        token.ThrowIfCancellationRequested();
                        RawPrinter.SendRaw(printer, buffer.ToArray(), "GS1 DataMatrix");

                        result.Sent += chunkCodes.Count;
                        job?.AppendSent(chunkCodes);

                        buffer.SetLength(0);
                        chunkCodes.Clear();
                        inChunk = 0;
                    }

                    progress?.Report(prepared);
                }
            }

            job?.Finish($"Успешно отправлено: {result.Sent} из {total}");
        }
        catch (OperationCanceledException)
        {
            job?.Finish($"Печать отменена. Отправлено: {result.Sent} из {total}");
            throw;
        }
        catch (Exception ex)
        {
            job?.Finish($"Ошибка печати: {ex.Message}");
            throw;
        }
        finally
        {
            job?.Dispose();
        }
    }

    private void SaveZpl_Click(object? sender, EventArgs e)
    {
        if (_file is null)
        {
            MessageBox.Show(this, "Сначала откройте файл с кодами.", "ZPL",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var result = LabelRenderer.Build(
                _file, _codeIndex, _productTextBox.Text.Trim(), S);
            var zpl = result.BuildZpl(S);

            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить ZPL",
                Filter = "ZPL (*.zpl)|*.zpl|Все файлы (*.*)|*.*",
                DefaultExt = "zpl",
                AddExtension = true,
                FileName = $"label_{_codeIndex + 1:D3}.zpl"
            };

            var dir = Path.GetDirectoryName(_file.FilePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                dialog.InitialDirectory = dir;

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllText(dialog.FileName, zpl, new UTF8Encoding(false));
            _statusLabel.Text = "ZPL сохранён: " + dialog.FileName;
            _statusLabel.ForeColor = Color.FromArgb(60, 110, 70);
            _toolTip.SetToolTip(_statusStrip, dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось сохранить ZPL." + Environment.NewLine + Environment.NewLine + ex.Message,
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class PrintOutcome
    {
        public PrintResult? Result { get; set; }
        public bool Cancelled { get; set; }
        public Exception? Error { get; set; }
    }

    private sealed class PrintResult
    {
        public int Sent { get; set; }
        public string? JobPath { get; set; }
    }
}
