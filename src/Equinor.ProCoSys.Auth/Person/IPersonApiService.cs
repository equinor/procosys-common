using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Equinor.ProCoSys.Auth.Person
{
    public interface IPersonApiService
    {
        Task<ProCoSysPerson> TryGetPersonByOidAsync(Guid azureOid);
        Task<List<ProCoSysPerson>> GetAllPersonsAsync(string plant, CancellationToken cancellationToken);
    }
}
