using System.Globalization;
using System.Text.RegularExpressions;

namespace mps.Infrastructure
{
    public static class NameHelper
    {
        public static string GetUserAvatarName(string userName, int length = 2)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return null;

            if (userName.Length <= length)
                return userName.ToUpper();

            // var (domain, user, _) = userName.Split(new[] { '\\' }, 2);
            var idx = userName.IndexOf('@');
            if (idx > 0 && idx < userName.Length - 2)
                return $"{userName.Substring(0, 1)}{userName.Substring(idx + 1, 1)}".ToUpper();

            return userName.Substring(0, 2).ToUpper();
        }

        public static string GetShortName(string name, int length = 2, int offset = 1)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            if(offset >= name.Length)
                return (name.Substring(0, 1) + offset.ToString()).ToUpper();

            if (name.Length <= length)
                return name.ToUpper();

            var words = name.Split(' ');

            if (words.Length > 1)
                return (words.First().Substring(0, 1) + words.Last().Substring(offset, 1)).ToUpper();

            return (words[0].Substring(0, 1) + words[0].Substring(offset, 1)).ToUpper();
        }

        /// <summary>
        /// Builds the comparison key stored in <c>NormalizedName</c>: whitespace trimmed and collapsed,
        /// umlauts spelled out, upper-cased. The migration that cleans up old rows
        /// (<c>ArchiveRaceNameTrimAndPushEndpoints</c>) mirrors these steps in SQL.
        /// </summary>
        public static string GetNormalizedName(string name){
            name = CollapseWhitespace(name);
            name = name.Replace("ä", "ae", true, CultureInfo.InvariantCulture);
            name = name.Replace("ü", "ue", true, CultureInfo.InvariantCulture);
            name = name.Replace("ö", "oe", true, CultureInfo.InvariantCulture);
            name = name.Replace("ß", "sz", true, CultureInfo.InvariantCulture);
            return name.ToUpper();
        }

        /// <summary>
        /// Builds the fuzzy comparison key used by <c>IItemMatchService</c>. Deliberately separate
        /// from <see cref="GetNormalizedName"/>, which search and the uniqueness lookup depend on:
        /// lower-cased, umlauts spelled out, <c>ß</c> and <c>sz</c> both folded to <c>ss</c>, and
        /// everything that is not a letter or digit (spaces, punctuation) dropped. So "Weißwein",
        /// "Weisswein", "Wiener wurst" and "Wienerwurst" collapse onto one key.
        /// </summary>
        public static string GetMatchKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var lower = name.ToLowerInvariant()
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

            var sb = new System.Text.StringBuilder(lower.Length);
            foreach (var c in lower)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }

            return sb.ToString().Replace("sz", "ss");
        }

        /// <summary>
        /// Trims the string and collapses every internal whitespace run to a single space.
        /// </summary>
        public static string CollapseWhitespace(string name)
        {
            if (name == null)
                return null;

            return WhitespaceRun.Replace(name.Trim(), " ");
        }

        private static readonly Regex WhitespaceRun = new Regex(@"\s+", RegexOptions.Compiled);
    }
}