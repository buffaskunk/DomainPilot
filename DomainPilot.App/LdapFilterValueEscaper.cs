using System.Text;

namespace DomainPilot.App;

/// <summary>
/// Escapes an untrusted value for future LDAP filters according to RFC 4515 special-character rules.
/// </summary>
public static class LdapFilterValueEscaper
{
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\5c",
                '*' => "\\2a",
                '(' => "\\28",
                ')' => "\\29",
                '\0' => "\\00",
                _ => character
            });
        }

        return builder.ToString();
    }
}
