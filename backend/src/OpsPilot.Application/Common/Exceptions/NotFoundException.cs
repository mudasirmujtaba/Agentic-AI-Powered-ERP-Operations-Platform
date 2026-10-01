namespace OpsPilot.Application.Common.Exceptions;

public class NotFoundException(string entityName, object key)
    : Exception($"{entityName} '{key}' was not found.");
