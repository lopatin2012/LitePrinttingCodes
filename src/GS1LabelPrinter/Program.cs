using System.Text;
using Gs1LabelPrinter.Core;
using Gs1LabelPrinter.UI;

namespace Gs1LabelPrinter;

internal static class Program
{
    private static System.Windows.Forms.Timer? _smokeTimer;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
        {
            var dir = args.Length > 1 ? args[1] : Environment.CurrentDirectory;
            var exitCode = SelfTest.Run(dir);
            Environment.ExitCode = exitCode;
            return;
        }

        if (args.Length >= 3 && args[0].Equals("--print-test", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var diagnosticSettings = SettingsStore.Load();
                L.Current = L.Parse(diagnosticSettings.Language);
                var file = CodeFileParser.Load(args[2]);
                var product = CodeFileParser.MakeShortProductName(file.FullProductName);
                using var label = LabelRenderer.Build(file, 0, product, diagnosticSettings.Label);
                GdiLabelPrinter.Print(args[1], new[] { label }, diagnosticSettings.Label);
                File.WriteAllText("print-test.log", "OK: " + args[1] + Environment.NewLine);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText("print-test.log", ex.ToString());
                Environment.ExitCode = 1;
            }

            return;
        }

        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) =>
        {
            ShowFatal(L.T("Необработанная ошибка"), e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ShowFatal(L.T("Необработанная ошибка"), ex);
        };

        var settings = SettingsStore.Load();
        L.Current = L.Parse(settings.Language);

        using var form = new MainForm(settings);

        var smoke = args.Length > 0 && args[0].Equals("--smoke", StringComparison.OrdinalIgnoreCase);
        var screenshot = args.Length > 1 && args[0].Equals("--screenshot", StringComparison.OrdinalIgnoreCase)
            ? args[1]
            : null;

        if (smoke || screenshot is not null)
        {
            _smokeTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _smokeTimer.Tick += (_, _) =>
            {
                _smokeTimer?.Stop();

                if (screenshot is not null)
                {
                    try
                    {
                        using var bitmap = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                        bitmap.Save(screenshot, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    catch
                    {
                        // скриншот — вспомогательный режим
                    }
                }

                form.Close();
            };
            form.Shown += (_, _) => _smokeTimer?.Start();
        }

        Application.Run(form);
    }

    private static void ShowFatal(string title, Exception ex)
    {
        try
        {
            var log = Path.Combine(AppData.Directory, "error.log");
            Directory.CreateDirectory(AppData.Directory);
            File.AppendAllText(log,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}",
                Encoding.UTF8);
            MessageBox.Show(
                L.F("Произошла ошибка. Подробности сохранены в файле:{0}{1}{0}{0}{2}",
                    Environment.NewLine, log, ex.Message),
                title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
