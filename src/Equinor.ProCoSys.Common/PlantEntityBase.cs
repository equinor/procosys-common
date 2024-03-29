﻿namespace Equinor.ProCoSys.Common
{
    /// <summary>
    /// Base class for entities to be filtered by plant
    /// </summary>
    public abstract class PlantEntityBase : EntityBase
    {
        public const int PlantLengthMax = 255;
        public const int PlantLengthMin = 5;

        protected PlantEntityBase(string plant) => Plant = plant;

        public virtual string Plant { get; private set; }
    }
}
