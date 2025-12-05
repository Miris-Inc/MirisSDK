// Copyright © 2024 Miris. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Miris.Runtime
{
    public class StringUtils
    {
        public static string ExpandVars(string template, Dictionary<string, string> replacements)
        {
            return Regex.Replace(template, @"\{(\w+)\}", match =>
            {
                string key = match.Groups[1].Value;
                return replacements.ContainsKey(key) ? replacements[key] : match.Value;
            });
        }

        public static string GetPath(string url)
        {
            int lastSlashIndex = url.LastIndexOf('/');
            if (lastSlashIndex >= 0)
            {
                return url.Substring(0, lastSlashIndex + 1);
            }
            return "";
        }
    }
}
