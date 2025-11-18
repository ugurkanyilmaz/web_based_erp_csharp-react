using System.Collections.Generic;
using System.Threading.Tasks;

namespace KetenErp.Core.Service
{
    public interface IServiceOperationRepository
    {
        Task<ServiceOperation> AddAsync(ServiceOperation op);
        Task<ServiceOperation?> GetByIdAsync(int id);
        Task<IEnumerable<ServiceOperation>> GetAllForRecordAsync(int serviceRecordId);
        Task<ServiceOperation> UpdateAsync(ServiceOperation op);
        Task DeleteAsync(int id);
    }
}
