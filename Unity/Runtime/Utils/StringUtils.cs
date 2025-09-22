// Copyright © 2024 Miris. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Aqua.Runtime
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
    }
}
