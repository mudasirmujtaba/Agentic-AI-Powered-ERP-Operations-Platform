namespace OpsPilot.Domain.Common;

public abstract class BaseEntity
{
    // Left unset so EF generates it on Add. A pre-assigned key makes EF treat entities discovered
    // through a navigation (e.g. new child rows) as existing, producing an UPDATE instead of an INSERT.
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }
}
