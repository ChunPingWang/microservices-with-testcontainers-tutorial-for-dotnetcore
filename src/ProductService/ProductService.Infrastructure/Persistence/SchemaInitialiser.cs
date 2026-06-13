using Microsoft.EntityFrameworkCore;

namespace ProductService.Infrastructure.Persistence;

public static class SchemaInitialiser
{
    public static async Task EnsureCreatedAsync(ProductDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
    }
}
