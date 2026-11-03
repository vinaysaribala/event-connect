using EventConnectService.ViewModels;

namespace EventConnectService.Interfaces;

public interface ICategoryService
{
    /// <summary>Active categories in display order.</summary>
    Task<IReadOnlyList<CategoryViewModel>> GetActiveAsync(CancellationToken cancellationToken = default);
}
