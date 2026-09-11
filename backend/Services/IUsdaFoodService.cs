using System.Collections.Generic;
using System.Threading.Tasks;
using Omnom.Api.Data.Entities;

namespace Omnom.Api.Services;

public interface IUsdaFoodService
{
    Task<FoodReference?> FindBestMatchAsync(string query, bool allowRemote = true, CancellationToken cancellationToken = default);
    Task<List<FoodReference>> SearchFoodsAsync(string query, int limit = 10);
}
