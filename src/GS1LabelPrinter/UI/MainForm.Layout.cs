namespace Gs1LabelPrinter.UI;

public sealed partial class MainForm
{
    private void BuildUi()
    {
        SuspendLayout();

        Text = "Печать GS1 DataMatrix";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1020, 700);
        ClientSize = new Size(1260, 820);
        KeyPreview = true;
        AllowDrop = true;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // без иконки тоже работает
        }

        _toolTip.AutoPopDelay = 15000;
        _toolTip.InitialDelay = 450;
        _toolTip.ReshowDelay = 100;
        _toolTip.ShowAlways = true;

        _statusLabel = new ToolStripStatusLabel("Готово")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _statusStrip = new StatusStrip
        {
            SizingGrip = false,
            ShowItemToolTips = true
        };
        _statusStrip.Items.Add(_statusLabel);

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6
        };

        _split.Panel1.Controls.Add(BuildPreviewArea());
        _split.Panel2.Controls.Add(BuildSettingsArea());

        Controls.Add(_split);
        Controls.Add(_statusStrip);

        Shown += MainForm_Shown;
        FormClosing += MainForm_FormClosing;
        KeyDown += MainForm_KeyDown;
        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;

        ResumeLayout(true);
    }

    private Control BuildPreviewArea()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 6, 8, 0),
            BackColor = Color.FromArgb(244, 246, 249)
        };

        toolbar.Controls.Add(new Label
        {
            Text = "Код",
            AutoSize = true,
            Margin = new Padding(4, 7, 4, 0)
        });

        var prevButton = new Button
        {
            Text = "◀",
            Width = 34,
            Height = 27,
            Margin = new Padding(0, 3, 4, 0),
            UseVisualStyleBackColor = true
        };
        prevButton.Click += PreviousCode_Click;
        toolbar.Controls.Add(prevButton);

        _codeIndexNumeric = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1,
            Value = 1,
            Width = 76,
            Height = 27,
            TextAlign = HorizontalAlignment.Center,
            Margin = new Padding(0, 4, 4, 0),
            ThousandsSeparator = false,
            Enabled = false
        };
        _codeIndexNumeric.ValueChanged += CodeIndexNumeric_ValueChanged;
        toolbar.Controls.Add(_codeIndexNumeric);

        _codeCountLabel = new Label
        {
            Text = "из 0",
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0),
            ForeColor = Color.FromArgb(90, 100, 112)
        };
        toolbar.Controls.Add(_codeCountLabel);

        var nextButton = new Button
        {
            Text = "▶",
            Width = 34,
            Height = 27,
            Margin = new Padding(0, 3, 14, 0),
            UseVisualStyleBackColor = true
        };
        nextButton.Click += NextCode_Click;
        toolbar.Controls.Add(nextButton);

        toolbar.Controls.Add(new Label
        {
            Text = "Масштаб:",
            AutoSize = true,
            Margin = new Padding(0, 7, 4, 0),
            ForeColor = Color.FromArgb(90, 100, 112)
        });

        var zoomOut = new Button
        {
            Text = "−",
            Width = 30,
            Height = 27,
            Margin = new Padding(0, 3, 3, 0),
            UseVisualStyleBackColor = true
        };
        zoomOut.Click += (_, _) => ChangeZoom(-1);
        toolbar.Controls.Add(zoomOut);

        _zoomLabel = new Label
        {
            Text = "вписать",
            AutoSize = false,
            Width = 56,
            Height = 27,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 4, 3, 0),
            ForeColor = Color.FromArgb(60, 70, 84)
        };
        toolbar.Controls.Add(_zoomLabel);

        var zoomIn = new Button
        {
            Text = "+",
            Width = 30,
            Height = 27,
            Margin = new Padding(0, 3, 8, 0),
            UseVisualStyleBackColor = true
        };
        zoomIn.Click += (_, _) => ChangeZoom(+1);
        toolbar.Controls.Add(zoomIn);

        var zoomFit = new Button
        {
            Text = "Вписать",
            AutoSize = true,
            Height = 27,
            Margin = new Padding(0, 3, 4, 0),
            UseVisualStyleBackColor = true
        };
        zoomFit.Click += (_, _) => SetZoom(0);
        toolbar.Controls.Add(zoomFit);

        var zoomOne = new Button
        {
            Text = "1:1",
            Width = 44,
            Height = 27,
            Margin = new Padding(0, 3, 12, 0),
            UseVisualStyleBackColor = true
        };
        zoomOne.Click += (_, _) => SetZoom(100);
        toolbar.Controls.Add(zoomOne);

        toolbar.Controls.Add(new Label
        {
            Text = "DM можно двигать мышью",
            AutoSize = true,
            Margin = new Padding(0, 7, 0, 0),
            ForeColor = Color.FromArgb(100, 110, 124)
        });

        _preview = new LabelPreviewControl
        {
            Dock = DockStyle.Fill,
            Settings = S
        };
        _preview.DmMoved += Preview_DmMoved;
        _preview.DmMoveCompleted += Preview_DmMoveCompleted;
        _toolTip.SetToolTip(_preview,
            "Перетащите DataMatrix мышью. Стрелки двигают на 1 точку, Shift+стрелки — на 10.");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_preview, 0, 1);
        return layout;
    }

    private Control BuildSettingsArea()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(250, 251, 252)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            Padding = new Padding(10, 10, 10, 16),
            BackColor = Color.FromArgb(250, 251, 252)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var groups = new[]
        {
            BuildFileGroup(),
            BuildPrinterGroup(),
            BuildPrintGroup(),
            BuildJobGroup(),
            BuildLabelSizeGroup(),
            BuildDataMatrixGroup(),
            BuildTextGroup()
        };

        foreach (var group in groups)
        {
            var row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(group, 0, row);
        }

        panel.Controls.Add(table);
        return panel;
    }

    private GroupBox BuildFileGroup()
    {
        var group = MakeGroup("Файл с кодами");
        var table = MakeTable(6, 2, 58, 42);

        _openFileButton = MakeButton("Открыть файл…", OpenFile_Click, 165);
        table.Controls.Add(_openFileButton, 0, 0);
        table.SetColumnSpan(_openFileButton, 2);

        var hint = MutedLabel("TXT: один GS1-код в строке. Файл можно перетащить в окно.");
        table.Controls.Add(hint, 0, 1);
        table.SetColumnSpan(hint, 2);

        _filePathLabel = new Label
        {
            Text = "Файл не выбран",
            AutoEllipsis = true,
            AutoSize = false,
            Height = 36,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(95, 105, 118),
            Margin = new Padding(0, 2, 0, 6)
        };
        _toolTip.SetToolTip(_filePathLabel, "Полный путь к выбранному файлу");
        table.Controls.Add(_filePathLabel, 0, 2);
        table.SetColumnSpan(_filePathLabel, 2);

        var caption = FieldLabel("Короткая надпись на этикетке:");
        table.Controls.Add(caption, 0, 3);
        table.SetColumnSpan(caption, 2);

        _productTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 5),
            MaxLength = 200
        };
        _productTextBox.TextChanged += ProductTextBox_TextChanged;
        _toolTip.SetToolTip(_productTextBox,
            "Эта строка печатается под DataMatrix. Можно изменить — значение запомнится для этого GTIN.");
        table.Controls.Add(_productTextBox, 0, 4);
        table.SetColumnSpan(_productTextBox, 2);

        _fileInfoLabel = MutedLabel("");
        _fileInfoLabel.AutoSize = false;
        _fileInfoLabel.AutoEllipsis = true;
        _fileInfoLabel.Height = 22;
        _fileInfoLabel.Dock = DockStyle.Fill;
        _toolTip.SetToolTip(_fileInfoLabel, "");
        table.Controls.Add(_fileInfoLabel, 0, 5);
        table.SetColumnSpan(_fileInfoLabel, 2);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildPrinterGroup()
    {
        var group = MakeGroup("Принтер");
        var table = MakeTable(2, 2, 76, 24);

        _printerCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 8, 4)
        };
        _printerCombo.SelectedIndexChanged += PrinterCombo_SelectedIndexChanged;
        _toolTip.SetToolTip(_printerCombo,
            "Выберите принтер Zebra. Данные отправляются как RAW-задание ZPL.");
        table.Controls.Add(_printerCombo, 0, 0);

        _refreshPrintersButton = MakeButton("Обновить", RefreshPrinters_Click);
        table.Controls.Add(_refreshPrintersButton, 1, 0);

        _printerInfoLabel = MutedLabel("Данные уходят напрямую в драйвер ZPL (RAW).");
        _printerInfoLabel.AutoSize = false;
        _printerInfoLabel.Height = 32;
        _printerInfoLabel.Dock = DockStyle.Fill;
        table.Controls.Add(_printerInfoLabel, 0, 1);
        table.SetColumnSpan(_printerInfoLabel, 2);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildJobGroup()
    {
        var group = MakeGroup("Файл задания (TXT)");
        var table = MakeTable(6, 2, 60, 40);

        _saveJobCheck = new CheckBox
        {
            Text = "Сохранять каждый отправленный код",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 2)
        };
        _saveJobCheck.CheckedChanged += SaveJobCheck_CheckedChanged;
        table.Controls.Add(_saveJobCheck, 0, 0);
        table.SetColumnSpan(_saveJobCheck, 2);

        _jobStateLabel = new Label
        {
            Text = "Файл задания появится после загрузки файла.",
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(95, 105, 118),
            Margin = new Padding(0, 2, 0, 0)
        };
        table.Controls.Add(_jobStateLabel, 0, 1);
        table.SetColumnSpan(_jobStateLabel, 2);

        _jobPathLabel = new Label
        {
            Text = "",
            AutoEllipsis = true,
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(35, 95, 160),
            Margin = new Padding(0, 0, 0, 0)
        };
        table.Controls.Add(_jobPathLabel, 0, 2);
        table.SetColumnSpan(_jobPathLabel, 2);

        _jobFolderLabel = new Label
        {
            Text = "",
            AutoEllipsis = true,
            AutoSize = false,
            Height = 22,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(120, 130, 142),
            Margin = new Padding(0, 0, 0, 2)
        };
        table.Controls.Add(_jobFolderLabel, 0, 3);
        table.SetColumnSpan(_jobFolderLabel, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        _chooseJobFolderButton = MakeButton("Выбрать папку…", ChooseJobFolder_Click, 150);
        _openJobFolderButton = MakeButton("Открыть папку", OpenJobFolder_Click, 125);
        buttons.Controls.Add(_chooseJobFolderButton);
        buttons.Controls.Add(_openJobFolderButton);
        table.Controls.Add(buttons, 0, 4);
        table.SetColumnSpan(buttons, 2);

        var hint = MutedLabel(
            "Если файл задания уже существует, новые отправленные коды добавляются в него. Иначе создаётся новый TXT.");
        hint.AutoSize = false;
        hint.Height = 34;
        hint.Dock = DockStyle.Fill;
        table.Controls.Add(hint, 0, 5);
        table.SetColumnSpan(hint, 2);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildLabelSizeGroup()
    {
        var group = MakeGroup("Этикетка");
        var table = MakeTable(3, 4, 30, 20, 30, 20);

        table.Controls.Add(FieldLabel("Ширина, точек"), 0, 0);
        _widthNumeric = MakeNumeric(200, 4000, S.LabelWidthDots);
        table.Controls.Add(_widthNumeric, 1, 0);

        table.Controls.Add(FieldLabel("Высота, точек"), 2, 0);
        _heightNumeric = MakeNumeric(100, 4000, S.LabelHeightDots);
        table.Controls.Add(_heightNumeric, 3, 0);

        table.Controls.Add(FieldLabel("DPI"), 0, 1);
        _dpiCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 4, 10, 4)
        };
        _dpiCombo.Items.AddRange(new object[] { "203 dpi", "300 dpi" });
        _dpiCombo.SelectedIndexChanged += DpiCombo_SelectedIndexChanged;
        table.Controls.Add(_dpiCombo, 1, 1);

        _labelSizeInfoLabel = MutedLabel("");
        _labelSizeInfoLabel.Dock = DockStyle.Fill;
        _labelSizeInfoLabel.AutoSize = false;
        _labelSizeInfoLabel.Height = 24;
        table.Controls.Add(_labelSizeInfoLabel, 2, 1);
        table.SetColumnSpan(_labelSizeInfoLabel, 2);

        var hint = MutedLabel("1 мм ≈ 8 точек при 203 dpi.");
        hint.AutoSize = false;
        hint.Height = 30;
        hint.Dock = DockStyle.Fill;
        table.Controls.Add(hint, 0, 2);
        table.SetColumnSpan(hint, 4);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildDataMatrixGroup()
    {
        var group = MakeGroup("DataMatrix");
        var table = MakeTable(6, 4, 30, 20, 30, 20);

        table.Controls.Add(FieldLabel("X, точек"), 0, 0);
        _dmXNumeric = MakeNumeric(0, 4000, S.DmX);
        table.Controls.Add(_dmXNumeric, 1, 0);

        table.Controls.Add(FieldLabel("Y, точек"), 2, 0);
        _dmYNumeric = MakeNumeric(0, 4000, S.DmY);
        table.Controls.Add(_dmYNumeric, 3, 0);

        table.Controls.Add(FieldLabel("Модуль, точек"), 0, 1);
        _moduleNumeric = MakeNumeric(1, 20, S.ModuleSizeDots);
        table.Controls.Add(_moduleNumeric, 1, 1);

        table.Controls.Add(FieldLabel("Символ"), 2, 1);
        _symbolInfoLabel = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(0, 4, 0, 4)
        };
        table.Controls.Add(_symbolInfoLabel, 3, 1);

        _dmSizeInfoLabel = MutedLabel("—");
        _dmSizeInfoLabel.AutoSize = false;
        _dmSizeInfoLabel.Height = 24;
        _dmSizeInfoLabel.Dock = DockStyle.Fill;
        table.Controls.Add(_dmSizeInfoLabel, 0, 2);
        table.SetColumnSpan(_dmSizeInfoLabel, 4);

        var arrows = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = true,
            Margin = new Padding(0, 2, 0, 2)
        };
        arrows.Controls.Add(MakeSmallButton("◀", (_, _) => NudgeDm(-1, 0)));
        arrows.Controls.Add(MakeSmallButton("▲", (_, _) => NudgeDm(0, -1)));
        arrows.Controls.Add(MakeSmallButton("▼", (_, _) => NudgeDm(0, 1)));
        arrows.Controls.Add(MakeSmallButton("▶", (_, _) => NudgeDm(1, 0)));
        var resetButton = new Button
        {
            Text = "Сброс",
            Width = 78,
            Height = 28,
            Margin = new Padding(8, 3, 0, 0),
            UseVisualStyleBackColor = true
        };
        resetButton.Click += (_, _) => ResetDmPosition();
        arrows.Controls.Add(resetButton);
        table.Controls.Add(arrows, 0, 3);
        table.SetColumnSpan(arrows, 4);

        _fixSizeCheck = new CheckBox
        {
            Text = "Фиксировать размер символа в ZPL (стабильная геометрия)",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 2)
        };
        _fixSizeCheck.CheckedChanged += FixSizeCheck_CheckedChanged;
        table.Controls.Add(_fixSizeCheck, 0, 4);
        table.SetColumnSpan(_fixSizeCheck, 4);

        var hint = MutedLabel(
            "DataMatrix перетаскивается мышью в предпросмотре. Стрелки — на 1 точку, Shift+стрелки — на 10.");
        hint.AutoSize = false;
        hint.Height = 34;
        hint.Dock = DockStyle.Fill;
        table.Controls.Add(hint, 0, 5);
        table.SetColumnSpan(hint, 4);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildTextGroup()
    {
        var group = MakeGroup("Текст под DataMatrix");
        var table = MakeTable(5, 4, 30, 20, 30, 20);

        table.Controls.Add(FieldLabel("Шрифт продукта"), 0, 0);
        _productFontNumeric = MakeNumeric(7, 60, (decimal)S.ProductFontPx);
        table.Controls.Add(_productFontNumeric, 1, 0);

        table.Controls.Add(FieldLabel("Шрифт кода"), 2, 0);
        _codeFontNumeric = MakeNumeric(7, 60, (decimal)S.CodeFontPx);
        table.Controls.Add(_codeFontNumeric, 3, 0);

        table.Controls.Add(FieldLabel("Интервал строк"), 0, 1);
        _gapNumeric = MakeNumeric(0, 20, (decimal)S.LineGapPx);
        table.Controls.Add(_gapNumeric, 1, 1);

        table.Controls.Add(FieldLabel("Отступ от DM"), 2, 1);
        _textGapNumeric = MakeNumeric(0, 50, S.TextGapDots);
        table.Controls.Add(_textGapNumeric, 3, 1);

        table.Controls.Add(FieldLabel("Переносов кода"), 0, 2);
        _codeBreaksNumeric = MakeNumeric(0, 10, S.CodeBreaks);
        _toolTip.SetToolTip(_codeBreaksNumeric,
            "Сколько раз перенести строку кода: 0 — без переноса, 1 — пополам, 2 — на три части.");
        table.Controls.Add(_codeBreaksNumeric, 1, 2);

        var breaksHint = MutedLabel("0 — нет · 1 — пополам");
        breaksHint.AutoSize = false;
        breaksHint.Height = 24;
        breaksHint.Dock = DockStyle.Fill;
        table.Controls.Add(breaksHint, 2, 2);
        table.SetColumnSpan(breaksHint, 2);

        _showCodeCheck = new CheckBox
        {
            Text = "Печатать содержимое кода (в скобках GS1)",
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 3)
        };
        _showCodeCheck.CheckedChanged += ShowCodeCheck_CheckedChanged;
        table.Controls.Add(_showCodeCheck, 0, 3);
        table.SetColumnSpan(_showCodeCheck, 4);

        var hint = MutedLabel(
            "Текст печатается картинкой (^GF), поэтому кириллица не зависит от шрифтов принтера.");
        hint.AutoSize = false;
        hint.Height = 32;
        hint.Dock = DockStyle.Fill;
        table.Controls.Add(hint, 0, 4);
        table.SetColumnSpan(hint, 4);

        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildPrintGroup()
    {
        var group = MakeGroup("Печать");
        var table = MakeTable(5, 2, 64, 36);

        table.Controls.Add(FieldLabel("Копий каждого кода"), 0, 0);
        _copiesNumeric = MakeNumeric(1, 100, S.Copies);
        table.Controls.Add(_copiesNumeric, 1, 0);

        _printAllButton = new Button
        {
            Text = "Печать всех",
            Dock = DockStyle.Fill,
            Height = 42,
            Margin = new Padding(0, 6, 0, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(32, 122, 201),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Enabled = false,
            UseVisualStyleBackColor = false
        };
        _printAllButton.FlatAppearance.BorderSize = 0;
        _printAllButton.Click += PrintAll_Click;
        table.Controls.Add(_printAllButton, 0, 1);
        table.SetColumnSpan(_printAllButton, 2);

        _printCurrentButton = MakeButton("Печать текущего кода", PrintCurrent_Click);
        _printCurrentButton.Height = 34;
        _printCurrentButton.Enabled = false;
        table.Controls.Add(_printCurrentButton, 0, 2);
        table.SetColumnSpan(_printCurrentButton, 2);

        _saveZplButton = MakeButton("Сохранить ZPL…", SaveZpl_Click);
        _saveZplButton.Height = 32;
        _saveZplButton.Enabled = false;
        table.Controls.Add(_saveZplButton, 0, 3);
        table.SetColumnSpan(_saveZplButton, 2);

        _printHintLabel = MutedLabel(
            "При печати «всех» будет подтверждение. Данные отправляются сразу на принтер, без диалога драйвера.");
        _printHintLabel.AutoSize = false;
        _printHintLabel.Height = 38;
        _printHintLabel.Dock = DockStyle.Fill;
        table.Controls.Add(_printHintLabel, 0, 4);
        table.SetColumnSpan(_printHintLabel, 2);

        group.Controls.Add(table);
        return group;
    }

    private static GroupBox MakeGroup(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 6, 10, 10),
            Margin = new Padding(0, 0, 0, 10)
        };
    }

    private static TableLayoutPanel MakeTable(int rows, int columns, params float[] widths)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = rows,
            ColumnCount = columns,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        for (var i = 0; i < columns; i++)
        {
            var width = i < widths.Length ? widths[i] : 100f / columns;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        }

        for (var i = 0; i < rows; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        return table;
    }

    private static Label FieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 8, 4)
        };
    }

    private static Label MutedLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 110, 124),
            Margin = new Padding(0, 4, 0, 4)
        };
    }

    private static NumericUpDown MakeNumeric(
        decimal min, decimal max, decimal value, int decimals = 0)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            DecimalPlaces = decimals,
            Increment = decimals == 0 ? 1 : 0.5m,
            ThousandsSeparator = false,
            Dock = DockStyle.Fill,
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(0, 4, 10, 4)
        };
    }

    private static Button MakeButton(string text, EventHandler? onClick = null, int width = 0)
    {
        var button = new Button
        {
            Text = text,
            Height = 30,
            Dock = width > 0 ? DockStyle.Left : DockStyle.Fill,
            Width = width > 0 ? width : 0,
            Margin = new Padding(0, 4, 0, 4),
            UseVisualStyleBackColor = true
        };

        if (width > 0)
            button.Margin = new Padding(0, 4, 8, 4);

        if (onClick is not null)
            button.Click += onClick;

        return button;
    }

    private static Button MakeSmallButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            Width = 34,
            Height = 28,
            Margin = new Padding(0, 3, 4, 0),
            UseVisualStyleBackColor = true
        };
        button.Click += onClick;
        return button;
    }
}
