namespace Bisuteria.Application.Services;

/// <summary>Error esperado con un mensaje apto para mostrar al usuario.</summary>
public sealed class BusinessRuleException(string message) : InvalidOperationException(message);
