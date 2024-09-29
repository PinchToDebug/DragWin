using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Reflection;
using Microsoft.Toolkit.Uwp.Notifications;
namespace DragWin
{
    public class Updater
    {
        private static string _url = "";
        private static string _downloadUrl = "";
        private static int updateCount = 0;
        public static async Task CheckUpdateAsync(string url)
        {
            _url = url;
            string currentVersion = Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion.ToString();
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("DragWin", currentVersion));
                    var response = await httpClient.GetStringAsync(_url);

                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        var root = doc.RootElement;
                        string latestVersion = root.GetProperty("tag_name").GetString();
                        string description = root.GetProperty("body").GetString();
                        string published_at = root.GetProperty("published_at").GetString();
                        string name = root.GetProperty("name").GetString();
                        string downloadUrl = root.GetProperty("assets")[0].GetProperty("browser_download_url").GetString();
                        _downloadUrl = downloadUrl;
                        string emoji = (name.ToLower().Contains("fix"), name.ToLower().Contains("feature")) switch
                        {
                            (true, true) => "🚀",
                            (true, false) => "🪛", // (screwdriver)
                            (false, true) => "✨",
                            _ => "🚀"
                        };

                        if (!latestVersion.Contains(currentVersion))
                        {
                            var toastBuilder = new ToastContentBuilder()
                                 .AddText($"{emoji} New release! {name}", AdaptiveTextStyle.Header)
                                 .AddText(description, AdaptiveTextStyle.Body)
                                 .AddButton(new ToastButton()
                                     .SetContent("Install")
                                     .AddArgument("action", "install_update")
                                     .SetBackgroundActivation())
                                 .AddButton(new ToastButton()
                                     .SetContent("Close")
                                     .AddArgument("action", "close")
                                     .SetBackgroundActivation());
                            toastBuilder.Show();
                        }
                        else if (updateCount != 0)
                        {
                            var toastBuilder = new ToastContentBuilder()
                               .AddText($"You are up to date!", AdaptiveTextStyle.Header)
                               .AddText($"There is no available update.", AdaptiveTextStyle.Body);
                            toastBuilder.Show();
                        }
                    }
                }
                updateCount++;
            }
            catch (Exception e)
            {
                if (updateCount != 0)
                {
                    var toastBuilder = new ToastContentBuilder()
                               .AddText($"Failed to update.", AdaptiveTextStyle.Header)
                               .AddText(e.Message, AdaptiveTextStyle.Body);
                    toastBuilder.Show();
                }
                Debug.WriteLine($"Update error: {e.Message}");
            }
        }
        public static async Task InstallUpdate()
        {
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                Debug.WriteLine($"{Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion}");
                var latestFileBytes = await httpClient.GetByteArrayAsync(_downloadUrl);
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Assembly.GetExecutingAssembly().GetName().Name}.exe");
                File.WriteAllBytes(tempFilePath, latestFileBytes);
                RestartApplication(tempFilePath);
            }
        }

        private static void ExecuteCommand(string command)
        {
            Debug.WriteLine("Starting CMD...");
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {command}",
                UseShellExecute = false,
                CreateNoWindow = true,
                Verb = "runas"
            });
            Debug.WriteLine("CMD done!");

        }

        private static void RestartApplication(string tempPath)
        {
            string currentExecutablePath = Process.GetCurrentProcess().MainModule.FileName;
            string command = $"timeout /t 2 && move /y \"{tempPath}\" \"{currentExecutablePath}\" & \"{currentExecutablePath}\" ";

            ExecuteCommand(command);
            Environment.Exit(0);
        }
    }
}