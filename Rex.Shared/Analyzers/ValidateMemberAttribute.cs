using System;

namespace Rex.Shared.Analyzers;

/// <summary>
///     String parameter must equal a member name on the first generic type argument.
/// </summary>
/// <remarks>
///     Plain string compare against declared names. Same name on another declaring type still counts as a match.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ValidateMemberAttribute : Attribute;
