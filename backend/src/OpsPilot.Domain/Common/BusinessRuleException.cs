namespace OpsPilot.Domain.Common;

/// <summary>A request was well-formed but violates a business rule (invalid state transition, insufficient stock, credit limit...).</summary>
public class BusinessRuleException(string message) : Exception(message);
