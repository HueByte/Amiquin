namespace Amiquin.Core.Attributes;

/// <summary>
/// Indicates that this slash command presents a modal and should not be deferred.
/// Modal commands must respond immediately with the modal, not defer the interaction.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class IsModalAttribute : Attribute { }