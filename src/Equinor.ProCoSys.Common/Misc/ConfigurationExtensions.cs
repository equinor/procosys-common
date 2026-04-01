using System;
using Microsoft.Extensions.Configuration;

namespace Equinor.ProCoSys.Common.Misc
{
    public static class ConfigurationExtensions
    {
        public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
            => configuration.GetConnectionString(name)
               ?? throw new ArgumentNullException($"Missing connectionstring in configuration {name}");

        public static string GetRequiredConfiguration(this IConfiguration configuration, string key)
            => configuration[key] ?? throw new ArgumentNullException($"Missing configuration {key}");

        public static int GetRequiredIntConfiguration(this IConfiguration configuration, string key)
        {
            var value = GetRequiredConfiguration(configuration, key);
            if (!int.TryParse(value, out var result))
            {
                throw new ArgumentNullException($"Configuration {key} can't be treated as integer");
            }
            return result;
        }
    }
}
