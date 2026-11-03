using EventConnectCore.Data;
using EventConnectService.Interfaces;
using EventConnectService.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventConnectService.Implementations;

public class CategoryService(EventConnectDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryViewModel>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryViewModel(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);
    }
}
