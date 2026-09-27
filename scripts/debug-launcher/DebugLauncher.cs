// DeskBox debug-edition launcher.
//
// Purpose: the delivered debug build must always run at medium integrity
// (Explorer's level) and with the isolated dev data root. Drag-and-drop from
// Explorer into a widget is blocked by UIPI when DeskBox runs elevated, and
// the data root must never fall back to the retail %LocalAppData%\DeskBox.
// Double-clicking this launcher satisfies both: it sets the environment
// variable in-process and starts the sibling payload folder's DeskBox.exe
// through ShellExecute, inheriting the launcher's own (normally medium)
// integrity level.
//
// Built with the in-box .NET Framework 4 csc.exe (no SDK required):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
//     /target:winexe /codepage:65001 /win32icon:<repo>\src\DeskBox\Assets\deskbox.ico
//     /r:System.Windows.Forms.dll /out:Launcher.exe DebugLauncher.cs
// The binary is then renamed to "DeskBox 调试版.exe" next to the payload folder.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class DebugLauncher
{
    private const string DevDataRoot = @"D:\Github\DeskBox-TS";

    // "DeskBox 调试版" kept as escapes so the source stays pure ASCII.
    private const string PayloadFolderName = "DeskBox \u8C03\u8BD5\u7248";

    [STAThread]
    private static int Main()
    {
        try
        {
            string payloadFolder = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, PayloadFolderName);
            string exePath = Path.Combine(payloadFolder, "DeskBox.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show(
                    "未找到调试版程序：" + exePath,
                    "DeskBox 调试版",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }

            Environment.SetEnvironmentVariable("DESKBOX_DEV_DATA_ROOT", DevDataRoot);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = payloadFolder,
                UseShellExecute = true
            };
            using (Process.Start(startInfo))
            {
            }

            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "启动 DeskBox 调试版失败：" + ex.Message,
                "DeskBox 调试版",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }
}
