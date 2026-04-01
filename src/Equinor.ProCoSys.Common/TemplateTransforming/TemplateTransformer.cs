using System.Collections.Generic;
using System.Text.RegularExpressions;
using Equinor.ProCoSys.Common.Misc;

namespace Equinor.ProCoSys.Common.TemplateTransforming;

public class TemplateTransformer : ITemplateTransformer
{
    public static string UnknownPropertyValue = "-unknown-";

    private static readonly Regex PlaceholderRegex = new(@"{{\$?([0-9A-Za-z\.]+)}}", RegexOptions.Compiled);

    public string Transform(string template, object transformationContext)
    {
        var matchEvaluator = new MatchEvaluator(
            m =>
            {
                var placeholder = m.Groups[1].Value;

                var path = Parse(placeholder);
                return GetValue(transformationContext, path);
            });

        return PlaceholderRegex.Replace(template, matchEvaluator);
    }

    private string GetValue(object context, IEnumerable<string> path)
    {
        var current = context;
        foreach (var property in path)
        {
            if (current == null)
            {
                return null;
            }

            current = GetCamelCasedPropertyValue(current, property);
        }

        if (current == null)
        {
            return null;
        }

        return current.ToString();
    }

    private object GetCamelCasedPropertyValue(object target, string propertyName)
        => GetPropertyValue(target, propertyName.ToPascalCase());

    private object GetPropertyValue(object target, string propertyName)
    {
        if (target is IDictionary<string, object> dynamicObject)
        {
            return !dynamicObject.ContainsKey(propertyName) ? null : dynamicObject[propertyName];
        }

        var propertyInfo = target.GetType().GetProperty(propertyName);
        return propertyInfo == null ? UnknownPropertyValue : propertyInfo.GetValue(target, null);
    }

    private string[] Parse(string placeholder) => placeholder.Split('.');
}
