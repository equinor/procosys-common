using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Equinor.ProCoSys.Common.Email;


public class EmailValidator
{
    public static bool IsValid(string email)
    {
        // Mostly copied from https://learn.microsoft.com/en-us/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // Normalize the domain
            email = Regex.Replace(email, @"(@)(.+)$", DomainMapper);

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

        catch (ArgumentException)
        {
            return false;
        }

        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase);
    }

    public static void ValidateEmail(string email)
    {
        if (!IsValid(email))
        {
            throw new Exception($"Not able to send email because of invalid email-address: {email}");
        }
    }

    public static void ValidateEmails(List<string> emails)
    {
        foreach (var email in emails)
        {
            ValidateEmail(email);
        }
    }
}
