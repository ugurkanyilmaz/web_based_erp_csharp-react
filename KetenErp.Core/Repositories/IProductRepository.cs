using KetenErp.Core.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KetenErp.Core.Repositories
{
    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
    }
}
