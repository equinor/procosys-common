using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Equinor.ProCoSys.Auth.Person;

namespace Equinor.ProCoSys.Auth.Caches
{
    public interface IPersonCache
    {
        Task<bool> ExistsAsync(Guid userOid);
        Task<ProCoSysPerson> GetAsync(Guid userOid);
        Task<List<ProCoSysPerson>> GetAllPersonsAsync(string plant, CancellationToken cancellationToken);
    }
}
