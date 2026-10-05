using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GreyHaven.Compat.Data;

/// <summary>
/// YAML loading for the data directory. Unknown keys are errors, so a misspelt field in a
/// contributor's rule fails loudly instead of being silently ignored.
/// </summary>
public static class Yaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static T Load<T>(string path)
    {
        try
        {
            return Deserializer.Deserialize<T>(File.ReadAllText(path))
                   ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} line {ex.Start.Line}: {ex.InnerException?.Message ?? ex.Message}", ex);
        }
    }
}
