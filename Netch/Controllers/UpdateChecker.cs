﻿using Netch.Models.GitHubRelease;
using Netch.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;

namespace Netch.Controllers
{
    public static class UpdateChecker
    {
        public const string Owner = @"fxzxmicah";
        public const string Repo = @"Netch-LTS";

        public const string Name = @"Netch";
        public const string Copyright = @"Copyright © 2019 - 2021";

        public const string AssemblyVersion = @"1.8.5";
        private const string Suffix = @"LTSu02";

        public static readonly string Version = $"{AssemblyVersion}{(string.IsNullOrEmpty(Suffix) ? "" : $"-{Suffix}")}";

        public static Release LatestRelease = null!;

        public static string LatestVersionNumber => LatestRelease.tag_name;

        public static string LatestVersionUrl => LatestRelease.html_url;

        public static event EventHandler? NewVersionFound;

        public static event EventHandler? NewVersionFoundFailed;

        public static event EventHandler? NewVersionNotFound;

        public static async Task Check(bool isPreRelease)
        {
            try
            {
                var updater = new GitHubRelease(Owner, Repo);
                var url = updater.AllReleaseUrl;

                var json = await WebUtil.DownloadStringAsync(WebUtil.CreateRequest(url));

                var releases = JsonSerializer.Deserialize<List<Release>>(json)!;
                LatestRelease = GetLatestRelease(releases, isPreRelease);
                Log.Information("Github 最新发布版本: {Version}", LatestRelease.tag_name);
                if (VersionUtil.CompareVersion(LatestRelease.tag_name, Version) > 0)
                {
                    Log.Information("发现新版本");
                    NewVersionFound?.Invoke(null, new EventArgs());
                }
                else
                {
                    Log.Information("目前是最新版本");
                    NewVersionNotFound?.Invoke(null, new EventArgs());
                }
            }
            catch (Exception e)
            {
                if (e is WebException)
                    Log.Warning(e, "获取新版本失败");
                else
                    Log.Error(e, "获取新版本异常");

                NewVersionFoundFailed?.Invoke(null, new EventArgs());
            }
        }

        public static void GetLatestUpdateFileNameAndHash(out string fileName, out string sha256, string? keyword = null)
        {
            fileName = string.Empty;
            sha256 = string.Empty;

            var matches = Regex.Matches(LatestRelease.body, @"^\| (?<filename>.*) \| (?<sha256>.*) \|\r?$", RegexOptions.Multiline)
                .Skip(2);
            /*
              Skip(2)
              
              | 文件名 | SHA256 |
              | :- | :- |
           */

            Match match = keyword == null ? matches.First() : matches.First(m => m.Groups["filename"].Value.Contains(keyword));

            fileName = match.Groups["filename"].Value;
            sha256 = match.Groups["sha256"].Value;
        }

        public static string GetLatestReleaseContent()
        {
            var sb = new StringBuilder();
            foreach (string l in LatestRelease.body.GetLines(false).SkipWhile(l => l.FirstOrDefault() != '#'))
            {
                if (l.Contains("校验和"))
                    break;

                sb.AppendLine(l);
            }

            return sb.ToString();
        }

        private static Release GetLatestRelease(IEnumerable<Release> releases, bool isPreRelease)
        {
            if (!isPreRelease)
                releases = releases.Where(release => !release.prerelease);

            var ordered = releases.OrderByDescending(release => release.tag_name, new VersionUtil.VersionComparer());
            return ordered.ElementAt(0);
        }
    }
}
