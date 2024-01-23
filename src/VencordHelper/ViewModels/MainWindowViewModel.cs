using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Windows;
using VencordHelper.Models;

namespace VencordHelper.ViewModels
{
    partial class MainWindowViewModel : ObservableObject
    {
        private readonly HttpClient _client;
        private readonly string _vencordUrl;

        public MainWindowViewModel()
        {
            _client = new HttpClient();
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VencordHelper", "1.0"));

            _vencordUrl = "https://api.github.com/repos/Vencord/Installer/releases/latest";
        }

        [RelayCommand]
        private async Task InstallVencordAsync()
        {
            if(!CheckForDiscord())
                return;

            GithubResponse.Root response = await _client.GetFromJsonAsync<GithubResponse.Root>(_vencordUrl) ?? throw new Exception("No response from Github");
            GithubResponse.Asset asset = response.Assets.First(x => x.Name == "VencordInstallerCli.exe");

            await VerifyLatestVersionDownloadedAsync(asset.Name, asset.BrowserDownloadUrl);

            await RunInstallerAsync(asset.Name, "-install -branch stable");

            StartDiscord();
        }

        [RelayCommand]
        private async Task UninstallVencordAsync()
        {
            if(!CheckForDiscord())
                return;

            GithubResponse.Root response = await _client.GetFromJsonAsync<GithubResponse.Root>(_vencordUrl) ?? throw new Exception("No response from Github");
            GithubResponse.Asset asset = response.Assets.First(x => x.Name == "VencordInstallerCli.exe");

            await VerifyLatestVersionDownloadedAsync(asset.Name, asset.BrowserDownloadUrl);

            await RunInstallerAsync(asset.Name, "-uninstall -branch stable");

            StartDiscord();
        }

        private async Task VerifyLatestVersionDownloadedAsync(string fileName, string downloadUrl)
        {
            string localHash = GetLocalHash(fileName);
            string onlineHash = await GetOnlineHash(downloadUrl);

            if(localHash != onlineHash)
                await DownloadFileToTemp(fileName, downloadUrl);
        }

        private async Task RunInstallerAsync(string fileName, string args)
        {
            Process? process = Process.Start(new ProcessStartInfo()
            {
                FileName = Path.Combine(Path.GetTempPath(), fileName),
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true
            });

            if(process is null)
                throw new NotSupportedException("Unsupported sequence of events");

            await Task.Delay(TimeSpan.FromSeconds(1));

            do
            {
                await Task.Delay(100);

				// Fix for https://github.com/Vencord/Installer/blob/839ff036caf9add05c0e235d37b67428386ccaa1/cli.go#L186C9-L186C14
				if(!process.HasExited)
                    process.StandardInput.WriteLine();
                else
                    break;
            } while(true);
        }

        private bool CheckForDiscord()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");

            if(!Directory.Exists(path))
            {
                MessageBoxResult result = MessageBox.Show("Discord was not found. \nClick \"Ok\" to vist Discord's website.", "Error", MessageBoxButton.OKCancel, MessageBoxImage.Error);

                if(result == MessageBoxResult.OK)
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "explorer.exe",
                        Arguments = "https://discord.com",
                        UseShellExecute = true
                    });
                }

                return false;
            }

            return true;
        }

        private void StartDiscord()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Discord");

            var appFolder = Directory.GetDirectories(path).OrderDescending().First(x => x.Contains("app"));

            Process.Start(new ProcessStartInfo()
            {
                FileName = Path.Combine(appFolder, "Discord.exe"),
                UseShellExecute = true,
                CreateNoWindow = false
            });
        }

        private async Task DownloadFileToTemp(string fileName, string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseContentRead);

            string path = Path.Combine(Path.GetTempPath(), fileName);

            // Will overwrite existing file if present
            using(FileStream fileStream = new FileStream(path, FileMode.Create))
            {
                await response.Content.CopyToAsync(fileStream);
            }
        }

        private async Task<string> GetOnlineHash(string url)
        {
            HttpResponseMessage response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            string hash = string.Empty;

            if(response.Content.Headers.ContentMD5 is not null)
                hash = Convert.ToBase64String(response.Content.Headers.ContentMD5);

            return hash;
        }

        private string GetLocalHash(string fileName)
        {
            string path = Path.Combine(Path.GetTempPath(), fileName);

            if(!File.Exists(path))
                return string.Empty;

            using(MD5 md5 = MD5.Create())
            {
                using(FileStream stream = File.OpenRead(path))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }
    }
}
