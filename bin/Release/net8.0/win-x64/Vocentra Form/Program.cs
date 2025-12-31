using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VocentraLauncher
{
    internal static class Program
    {
        private static Process? vocentraProcess;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }

        private class LauncherForm : Form
        {
            public LauncherForm()
            {
                Text = "Vocentra Launcher";
                Width = 400;
                Height = 200;
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;

                var label = new Label
                {
                    Text = "Starting Vocentra…",
                    Dock = DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Font = new System.Drawing.Font("Segoe UI", 11)
                };

                Controls.Add(label);
                Load += OnLoad;
                FormClosing += OnClose;
            }

            private async void OnLoad(object? sender, EventArgs e)
            {
                string exePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Vocentra.exe"
                );

                if (!File.Exists(exePath))
                {
                    MessageBox.Show(
                        "Vocentra.exe was not found.\n\nMake sure it is in the same folder as this launcher.",
                        "Startup Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    Close();
                    return;
                }

                vocentraProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                vocentraProcess.Start();

                await Task.Delay(3000); // give server time to boot

                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:5000",
                    UseShellExecute = true
                });
            }

            private void OnClose(object? sender, FormClosingEventArgs e)
            {
                if (vocentraProcess != null && !vocentraProcess.HasExited)
                {
                    vocentraProcess.Kill(true);
                }
            }
        }
    }
}
