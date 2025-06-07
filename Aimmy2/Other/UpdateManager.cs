﻿using Aimmy2.Other;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Visuality;

namespace Other
{
    internal class UpdateManager
    {
        private readonly HttpClient client;

        public UpdateManager()
        {
            client = new HttpClient();
        }

        private int CompareVersions(string currentVersion, string latestVersion)
        {
            try
            {
                // Remove 'v' prefix if present
                currentVersion = currentVersion.TrimStart('v', 'V');
                latestVersion = latestVersion.TrimStart('v', 'V');

                // Parse versions
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);

                return current.CompareTo(latest);
            }
            catch (Exception ex)
            {

                // Fallback to string comparison if parsing fails
                return string.Compare(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase);
            }
        }

        public async Task CheckForUpdate(string currentVersion)
        {
            GithubManager githubManager = new();
            var (latestVersion, latestZipUrl) = await githubManager.GetLatestReleaseInfo("Babyhamsta", "Aimmy");

            if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(latestZipUrl))
            {
                new NoticeBar("Failed to get latest release information from Github.", 5000).Show();
                return;
            }

            // Compare versions
            var comparison = CompareVersions(currentVersion, latestVersion);

            if (comparison == 0)
            {
                new NoticeBar("You are up to date.", 5000).Show();
                return;
            }
            else if (comparison > 0)
            {
                new NoticeBar($"You are running a newer version ({currentVersion}) than the latest release ({latestVersion}).", 5000).Show();
                return;
            }

            // Only update if latest version is newer
            new NoticeBar("An update was found, downloading the update from Github.", 5000).Show();
            githubManager.Dispose();
            await DoUpdate(latestZipUrl);
        }

        private async Task DoUpdate(string latestZipUrl)
        {
            // Download the newest release of Aimmy to %temp%
            string envTempPath = Path.GetTempPath();
            string localZipPath = Path.Combine(envTempPath, "AimmyUpdate.zip");

            var response = await client.GetAsync(new Uri(latestZipUrl), HttpCompletionOption.ResponseHeadersRead);

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(localZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await stream.CopyToAsync(fileStream);

            // Extract update to %temp%
            string extractPath = Path.Combine(envTempPath, "AimmyUpdate");
            await Task.Run(() => // Run extraction in a separate task
            {
                ZipFile.ExtractToDirectory(localZipPath, extractPath, true);
            });

            // Create a batch script to move the files and restart Aimmy
            string? mainAppPath = Environment.ProcessPath;

            string? mainAppDir = Path.GetDirectoryName(mainAppPath) ?? throw new InvalidOperationException("Failed to get the directory name from the main module file path.");

            string batchScriptPath = Path.Combine(mainAppDir!, "update.bat");

            using (StreamWriter sw = new(batchScriptPath))
            {
                sw.WriteLine("@echo off");
                sw.WriteLine("timeout /t 3 /nobreak");
                sw.WriteLine($"xcopy /Y \"{extractPath}\\*\" \"{mainAppDir}\"");
                sw.WriteLine($"start \"\" \"{mainAppPath}\"");
                sw.WriteLine($"del /f \"{localZipPath}\"");
                sw.WriteLine($"rd /s /q \"{extractPath}\"");
                sw.WriteLine($"del /f \"{batchScriptPath}\"");
                sw.WriteLine($"( del /F /Q \"%~f0\" >nul 2>&1 & exit ) >nul");
            }

            Process.Start(batchScriptPath);
            Environment.Exit(0);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}