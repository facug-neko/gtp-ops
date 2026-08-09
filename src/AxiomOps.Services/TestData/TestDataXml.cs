using System.Xml.Linq;

namespace AxiomOps.Services.TestData;

/// <summary>Identity of a testdata file, taken from its &lt;Key&gt; element.</summary>
public sealed record TestDataKey(string ModuleId, string? ClientId, string LoginName)
{
    /// <summary>
    /// Uniqueness key: a testdata must be unique by moduleID + loginName, or by
    /// moduleID + clientId + loginName when a clientId is present.
    /// </summary>
    public string UniquenessKey => $"{ModuleId}|{ClientId ?? string.Empty}|{LoginName}";

    public override string ToString() => ClientId is null
        ? $"module {ModuleId} · login {LoginName}"
        : $"module {ModuleId} · client {ClientId} · login {LoginName}";
}

/// <summary>
/// Helpers for the testdata XML format:
/// <code>
/// &lt;Test&gt;
///   &lt;Key moduleID="101852" clientId="50300" loginName="X6" /&gt;
///   ...game-specific payload...
/// &lt;/Test&gt;
/// </code>
/// </summary>
public static class TestDataXml
{
    /// <summary>Reads moduleID / clientId / loginName from the &lt;Key&gt; element.</summary>
    public static bool TryParseKey(string xml, out TestDataKey? key)
    {
        key = null;

        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }

        try
        {
            var element = XDocument.Parse(xml)
                .Descendants()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase));

            if (element is null)
            {
                return false;
            }

            string? Attr(string name) => element.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

            var moduleId = Attr("moduleID");
            var loginName = Attr("loginName");
            var clientId = Attr("clientId");

            if (string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(loginName))
            {
                return false;
            }

            key = new TestDataKey(
                moduleId.Trim(),
                string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim(),
                loginName.Trim());
            return true;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the &lt;Key&gt; element's loginName to <paramref name="loginName"/>,
    /// editing the raw text so everything else (formatting, comments, the
    /// game-specific payload) stays byte-for-byte identical. Adds the attribute
    /// when the element doesn't have one.
    /// </summary>
    public static bool TryRewriteLoginName(string xml, string loginName, out string result, out string? error)
    {
        result = xml;
        error = null;

        if (string.IsNullOrWhiteSpace(loginName))
        {
            error = "El nombre de usuario está vacío.";
            return false;
        }

        var keyStart = xml.IndexOf("<Key", StringComparison.OrdinalIgnoreCase);
        if (keyStart < 0)
        {
            error = "El testdata no tiene un elemento <Key>.";
            return false;
        }

        var keyEnd = xml.IndexOf('>', keyStart);
        if (keyEnd < 0)
        {
            error = "El elemento <Key> está mal formado.";
            return false;
        }

        var tag = xml[keyStart..(keyEnd + 1)];
        var attrIndex = tag.IndexOf("loginName", StringComparison.OrdinalIgnoreCase);

        if (attrIndex < 0)
        {
            // No loginName yet — insert it before the tag's closing token.
            var closingLength = tag.EndsWith("/>", StringComparison.Ordinal) ? 2 : 1;
            var head = tag[..^closingLength].TrimEnd();
            var newTag = $"{head} loginName=\"{loginName}\" {tag[^closingLength..]}";
            result = string.Concat(xml[..keyStart], newTag, xml[(keyEnd + 1)..]);
            return true;
        }

        var quoteStart = tag.IndexOf('"', attrIndex);
        var quoteEnd = quoteStart < 0 ? -1 : tag.IndexOf('"', quoteStart + 1);
        if (quoteStart < 0 || quoteEnd < 0)
        {
            error = "No se pudo interpretar el atributo loginName.";
            return false;
        }

        var updatedTag = string.Concat(tag[..(quoteStart + 1)], loginName, tag[quoteEnd..]);
        result = string.Concat(xml[..keyStart], updatedTag, xml[(keyEnd + 1)..]);
        return true;
    }
}
