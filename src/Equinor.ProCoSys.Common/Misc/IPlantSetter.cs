﻿namespace Equinor.ProCoSys.Common.Misc
{
    public interface IPlantSetter
    {
        void SetPlant(string plant);
        void SetCrossPlantQuery();
        void ClearCrossPlantQuery();
    }
}
