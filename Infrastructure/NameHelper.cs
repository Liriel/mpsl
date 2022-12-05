using System.Globalization;

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

        public static string GetNormalizedName(string name){
            name = name.Replace("ä", "ae", true, CultureInfo.InvariantCulture);
            name = name.Replace("ü", "ue", true, CultureInfo.InvariantCulture);
            name = name.Replace("ö", "oe", true, CultureInfo.InvariantCulture);
            name = name.Replace("ß", "sz", true, CultureInfo.InvariantCulture);
            return name.ToUpper();
        }
    }
}