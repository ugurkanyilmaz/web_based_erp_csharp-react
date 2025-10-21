using KetenErp.Core.Entities;
using KetenErp.Core.Repositories;
using KetenErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KetenErp.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly KetenErpDbContext _db;

        public ProductRepository(KetenErpDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            return await _db.Products.ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _db.Products.FindAsync(id);
        }
    }
}
