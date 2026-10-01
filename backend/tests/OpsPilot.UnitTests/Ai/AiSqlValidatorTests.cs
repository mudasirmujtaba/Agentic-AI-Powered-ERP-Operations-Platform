using OpsPilot.Domain.Identity;
using OpsPilot.Infrastructure.Ai;

namespace OpsPilot.UnitTests.Ai;

public class AiSqlValidatorTests
{
    private static readonly string[] Sales = [Roles.SalesUser];
    private static readonly string[] Finance = [Roles.FinanceUser];

    [Theory]
    [InlineData("SELECT TOP 10 code, name FROM ai.products ORDER BY name")]
    [InlineData("SELECT o.order_number, l.product_code FROM ai.sales_orders o JOIN ai.sales_order_lines l ON l.order_number = o.order_number WHERE o.is_late = 1")]
    [InlineData("WITH late AS (SELECT * FROM ai.sales_orders WHERE is_late = 1) SELECT COUNT(*) AS late_orders FROM late")]
    [InlineData("SELECT customer_name, SUM(total_amount) AS revenue FROM ai.sales_orders GROUP BY customer_name")]
    public void AcceptsReadOnlySelectsOverPermittedViews(string sql)
    {
        Assert.Null(AiSqlValidator.Validate(sql, Sales));
    }

    [Theory]
    [InlineData("DELETE FROM ai.products", "Only SELECT")]
    [InlineData("UPDATE dbo.Products SET UnitPrice = 0", "Only SELECT")]
    [InlineData("SELECT * FROM dbo.AspNetUsers", "ai schema")]
    [InlineData("SELECT * FROM AspNetUsers", "ai schema")]
    [InlineData("SELECT * FROM ai.products; DROP TABLE dbo.Products", "Exactly one")]
    [InlineData("SELECT * INTO ai.copy FROM ai.products", "INTO")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x;', 'SELECT 1') AS t", "OPENROWSET")]
    [InlineData("SELECT * FROM OtherDb.ai.products", "Cross-database")]
    [InlineData("SELECT * FROM ai.secrets", "Unknown view")]
    [InlineData("EXEC sp_who", "Only SELECT")]
    [InlineData("SELEC * FROM ai.products", "syntax")]
    public void RejectsAnythingElse(string sql, string expectedReason)
    {
        var error = AiSqlValidator.Validate(sql, Sales);

        Assert.NotNull(error);
        Assert.Contains(expectedReason, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinanceViewsRequireAFinanceRole()
    {
        const string sql = "SELECT invoice_number, balance FROM ai.invoices WHERE is_overdue = 1";

        Assert.Contains("does not have access", AiSqlValidator.Validate(sql, Sales));
        Assert.Null(AiSqlValidator.Validate(sql, Finance));
    }
}
