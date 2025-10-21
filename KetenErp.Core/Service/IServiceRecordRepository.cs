using System.Collections.Generic;
using System.Threading.Tasks;

namespace KetenErp.Core.Service
{
    public interface IServiceRecordRepository
    {
        Task<ServiceRecord> AddAsync(ServiceRecord record);
        Task<ServiceRecord?> GetByIdAsync(int id);
        Task<IEnumerable<ServiceRecord>> GetAllAsync();
        Task<ServiceRecord> UpdateAsync(ServiceRecord record);
        Task DeleteAsync(int id);
    }
}
