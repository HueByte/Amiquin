using Amiquin.Core.Attributes;
using Amiquin.Core.Options;
using Discord.Interactions;
using System.ComponentModel;
using System.Reflection;

namespace Amiquin.Core.Utilities;

public class Reflection
{
    public static object ConvertTo(PropertyInfo property, string input)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        Type targetType = property.PropertyType;

        // Special case for nullable types: extract the underlying type.
        if (Nullable.GetUnderlyingType(targetType) != null)
        {
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, input);
        }

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter != null && converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromString(input)!;
        }

        // As a fallback, attempt to use Convert.ChangeType (works for IConvertible types)
        return Convert.ChangeType(input, targetType);
    }

    public static T? ConvertTo<T>(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return default;
        }

        var type = typeof(T);
        var converter = TypeDescriptor.GetConverter(type);
        if (converter is not null && converter.CanConvertFrom(typeof(string)))
        {
            return (T)converter.ConvertFromString(input)!;
        }

        return (T)Convert.ChangeType(input, typeof(T));
    }

    public static bool IsExtendedPrimitiveType(PropertyInfo property)
    {
        return property.PropertyType.IsPrimitive || property.PropertyType == typeof(string) || property.PropertyType == typeof(decimal);
    }

    public static bool IsExtendedPrimitiveType(object obj)
    {
        return obj.GetType().IsPrimitive || obj.GetType() == typeof(string) || obj.GetType() == typeof(decimal);
    }

    public static HashSet<string> GetAllEphemeralCommands()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(asm => !string.IsNullOrEmpty(asm.FullName) && asm.FullName.StartsWith("Amiquin"))
            .ToList();

        // Ephemeral command must have both SlashCommandAttribute and EphemeralAttribute and live inside Amiquin assembly family.
        var ephemeralCommandsNames = assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttributes(typeof(EphemeralAttribute), false).Length > 0
                && method.GetCustomAttributes(typeof(SlashCommandAttribute), false).Length > 0)
            .Select(m => m.GetCustomAttribute<SlashCommandAttribute>(false)?.Name)
            .Where(name => name is not null)
            .Select(name => name!) // Ensure the value is treated as non-nullable.
            .ToList();

        return ephemeralCommandsNames.ToHashSet();
    }

    public static HashSet<string> GetAllModalCommands()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(asm => !string.IsNullOrEmpty(asm.FullName) && asm.FullName.StartsWith("Amiquin"))
            .ToList();

        // Modal command must have both SlashCommandAttribute and IsModalAttribute and live inside Amiquin assembly family.
        var modalCommandsNames = assemblies.SelectMany(a => a.GetTypes())
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttributes(typeof(IsModalAttribute), false).Length > 0
                && method.GetCustomAttributes(typeof(SlashCommandAttribute), false).Length > 0)
            .Select(m => m.GetCustomAttribute<SlashCommandAttribute>(false)?.Name)
            .Where(name => name is not null)
            .Select(name => name!) // Ensure the value is treated as non-nullable.
            .ToList();

        return modalCommandsNames.ToHashSet();
    }

    public static Type[] GetOptionTypes()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(asm => !string.IsNullOrEmpty(asm.FullName) && asm.FullName.StartsWith("Amiquin"))
            .ToList();

        var optionTypes = assemblies.SelectMany(a => a.GetTypes())
            .Where(type => type.GetCustomAttributes(typeof(IOption), false).Length > 0)
            .ToArray();

        return optionTypes;
    }
}