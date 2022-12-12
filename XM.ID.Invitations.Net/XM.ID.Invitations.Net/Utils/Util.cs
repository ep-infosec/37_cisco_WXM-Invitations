using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace XM.ID.Invitations.Net
{
    public class Util
    {
        internal static List<string> CleanPhrases = new List<string> { "javascript:", "=cmd|", "<script>", "</script>", "alert(" };
        internal static List<Char> CleanTextStrict = new List<char> { '<', '>', '`', '=', '{', '}', '|' };
        internal static List<Char> CleanText = new List<char> { '<', '>', '`' };

        public static void CleanObject(Object obj, List<string> exclude = null, bool strict = false, int truncatestring = 0)
        {
            try
            {
                if (obj == null)
                    return;

                foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                {
                    if (descriptor.IsReadOnly || descriptor.PropertyType.IsEnum || descriptor.PropertyType.IsAbstract)
                        continue;

                    if (descriptor.PropertyType.IsPrimitive && descriptor.PropertyType.Name != "String")
                        continue;

                    if (descriptor.PropertyType.Name == "String")
                    {
                        string value = (String)descriptor.GetValue(obj);
                        if (!string.IsNullOrEmpty(value) && (exclude == null || !exclude.Contains(descriptor.Name)))
                            descriptor.SetValue(obj, SimpleString(value, strict, truncatestring));
                    }
                    else if (descriptor.PropertyType.IsAnsiClass && descriptor.PropertyType.IsClass && descriptor.PropertyType.IsGenericType && descriptor.Attributes?.Count > 0)
                    {
                        var elems = descriptor.GetValue(obj) as System.Collections.IList;
                        if (elems?.Count > 0)
                        {
                            bool IsString = descriptor.PropertyType.GenericTypeArguments.FirstOrDefault()?.Name == "String";
                            for (int i = 0; i < elems.Count; i++)
                            {
                                if (IsString)
                                    elems[i] = SimpleString((string)elems[i], strict, truncatestring);
                                else
                                    CleanObject(elems[i], exclude, strict);
                            }

                            if (IsString)
                                descriptor.SetValue(obj, elems);
                        }
                    }
                    else if (descriptor.PropertyType.IsAnsiClass && descriptor.PropertyType.IsClass)
                    { // Complex Object
                        var nested = descriptor.GetValue(obj);
                        if (nested != null)
                            CleanObject(nested, exclude);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                //LogException(ex, obj != null ? JsonConvert.SerializeObject(obj, Formatting.Indented) : null);
            }
        }

        internal static string SimpleString(string value, bool strict = false, int truncate = 0)
        {
            if (string.IsNullOrEmpty(value) || value == "true" || value == "false")
                return value;

            if (truncate > 0 && value.Length > truncate)
                value = value.Substring(0, truncate);

            foreach (var cp in CleanPhrases)
            {
                if (value.IndexOf(cp, StringComparison.OrdinalIgnoreCase) >= 0)
                    value = ReplaceString(value, cp, "", StringComparison.OrdinalIgnoreCase);
            }

            if (strict)
                return string.Join("", value.ToArray().Where(x => !CleanTextStrict.Contains(x)));

            return string.Join("", value.ToArray().Where(x => !CleanText.Contains(x)));
        }

        internal static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison)
        {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static bool IsValidMobile(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
                return false;

            if (mobile.StartsWith('+'))
                mobile = mobile.Replace("+", "");

            return Regex.IsMatch(mobile, @"^[0-9]*$");
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                                      RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    var domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email,
                    @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                    @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-0-9a-z]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}
