namespace TaskManagement.Web.Helpers;

/// <summary>Business rule failure. Mapped to 400 by the API and to a form error in the UI.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>The caller is authenticated but not allowed to touch this record. Mapped to 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Record does not exist. Mapped to 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
