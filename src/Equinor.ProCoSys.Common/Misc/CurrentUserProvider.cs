﻿using System;

namespace Equinor.ProCoSys.Common.Misc
{
    /// <summary>
    /// Set and get current user in application. Normally set in Middleware for each request
    /// </summary>
    public class CurrentUserProvider : ICurrentUserProvider, ICurrentUserSetter
    {
        private Guid? _currentUserOid;

        public Guid GetCurrentUserOid()
        {
            if (_currentUserOid.HasValue)
            {
                return _currentUserOid.Value;
            }

            throw new Exception("Unable to determine current user");
        }

        public bool HasCurrentUser => _currentUserOid.HasValue;

        public void SetCurrentUserOid(Guid oid) => _currentUserOid = oid;
    }
}
