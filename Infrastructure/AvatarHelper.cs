namespace mps.Infrastructure
{
    public static class AvatarHelper
    {
        public static string GetShortname(string userName, int length = 2)
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

    }
}