using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vocentra_Form
{
    public partial class Form1 : Form
    {
        private Process? vocentraProcess;
        private Button startButton = null!;
        private Label statusLabel = null!;

        public Form1()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Vocentra Launcher";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(400, 200);

            statusLabel = new Label
            {
                Text = "Server not running",
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            startButton = new Button
            {
                Text = "Start Vocentra",
                Width = 150,
                Height = 45,
                Top = 100,
                Left = (ClientSize.Width - 150) / 2
            };

            startButton.Click += StartButton_Click;

            Controls.Add(statusLabel);
            Controls.Add(startButton);
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            startButton.Enabled = false;
            statusLabel.Text = "Starting server...";

            string exePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Vocentra.exe"
            );

            if (!File.Exists(exePath))
            {
                MessageBox.Show(
                    "Vocentra.exe not found.\nPlace it next to this launcher.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );

                startButton.Enabled = true;
                statusLabel.Text = "Server not running";
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

            await Task.Delay(3000);

            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5000",
                UseShellExecute = true
            });

            statusLabel.Text = "Server running";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (vocentraProcess != null && !vocentraProcess.HasExited)
            {
                vocentraProcess.Kill(true);
            }
            base.OnFormClosing(e);
        }
    }
}
