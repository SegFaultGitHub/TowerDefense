using System;

namespace Code.Utils {
    [Serializable]
    public struct WeightDistribution<T> {
        public float Weight;
        public T Obj;
    }
}
