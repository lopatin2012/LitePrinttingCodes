namespace Gs1LabelPrinter.UI;

public sealed class PrintProgressForm : Form
{
    private readonly int _total;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _cancelButton;
    private bool _finished;

    public PrintProgressForm(int total)
    {
        _total = Math.Max(1, total);

        Text = "Печать этикеток";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;
        ClientSize = new Size(470, 150);
        Font = new Font("Segoe UI", 9.5f);

        _statusLabel = new Label
        {
            Text = $"Подготовка: 0 из {_total}",
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(438, 24),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = _total,
            Value = 0,
            Style = ProgressBarStyle.Continuous,
            Location = new Point(16, 48),
            Size = new Size(438, 22)
        };

        var hint = new Label
        {
            Text = "Отмена сработает после текущей пачки этикеток.",
            AutoSize = false,
            Location = new Point(16, 86),
            Size = new Size(320, 32),
            ForeColor = Color.FromArgb(100, 110, 124)
        };

        _cancelButton = new Button
        {
            Text = "Отмена",
            Location = new Point(368, 92),
            Size = new Size(86, 30),
            UseVisualStyleBackColor = true
        };
        _cancelButton.Click += (_, _) =>
        {
            _cancelButton.Enabled = false;
            _cancelButton.Text = "Отмена…";
            _statusLabel.Text = "Останавливаем печать…";
            CancelRequested?.Invoke(this, EventArgs.Empty);
        };

        Controls.Add(_statusLabel);
        Controls.Add(_progressBar);
        Controls.Add(hint);
        Controls.Add(_cancelButton);

        FormClosing += (_, e) =>
        {
            if (_finished)
                return;

            e.Cancel = true;
            _cancelButton.Enabled = false;
            _statusLabel.Text = "Останавливаем печать…";
            CancelRequested?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? CancelRequested;

    public void SetProgress(int done, int total)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => SetProgress(done, total)));
            }
            catch (InvalidOperationException)
            {
                // форма уже закрывается или закрыта
            }
            return;
        }

        if (_finished)
            return;

        total = Math.Max(1, total);
        if (_progressBar.Maximum != total)
            _progressBar.Maximum = total;

        _progressBar.Value = Math.Clamp(done, _progressBar.Minimum, _progressBar.Maximum);
        _statusLabel.Text = $"Печать: {done} из {total}";
    }

    public void Finish(bool success, string? message)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => Finish(success, message)));
            }
            catch (InvalidOperationException)
            {
                // форма уже закрывается или закрыта
            }
            return;
        }

        if (_finished)
            return;

        _finished = true;
        _cancelButton.Enabled = false;

        if (!string.IsNullOrWhiteSpace(message))
            _statusLabel.Text = message;

        if (success)
        {
            _progressBar.Value = _progressBar.Maximum;
            _statusLabel.Text = $"Готово: {_total} из {_total}";
        }

        Close();
    }
}
