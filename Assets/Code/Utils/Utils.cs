using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Code.Utils {
    public static class Utils {

        public static bool Rate(float rate) {
            return Random.Range(0, 1f) < rate;
        }
        #region Simple sample
        public static T Sample<T>(List<T> list) {
            if (list.Count != 0) return list[Random.Range(0, list.Count)];
            Debug.LogError("Trying to sample an empty list");
            return default;
        }

        public static T Sample<T>(ICollection<T> list) {
            if (list.Count == 0) {
                Debug.LogError("Trying to sample an empty list");
                return default;
            }
            List<T> clone = new();
            clone.AddRange(list);
            return clone[Random.Range(0, list.Count)];
        }

        public static List<T> Shuffle<T>(ICollection<T> list) {
            return Sample(list, list.Count);
        }

        public static List<T> Sample<T>(IEnumerable<T> list, int n) {
            List<T> clone = new();
            clone.AddRange(list);

            List<T> result = new();
            while (result.Count < n && clone.Count > 0) {
                T element = Sample(clone);
                clone.Remove(element);
                result.Add(element);
            }
            return result;
        }
        #endregion

        #region Weighted sample
        private static WeightDistribution<T> GetRandomItemInDistribution<T>(List<WeightDistribution<T>> list) {
            float total = list.Sum(weightDistribution => weightDistribution.Weight);
            float choice = Random.Range(0, total);
            float index = 0;
            foreach (WeightDistribution<T> weightDistribution in list) {
                index += weightDistribution.Weight;
                if (choice <= index)
                    return weightDistribution;
            }

            return list[^1];
        }

        public static T Sample<T>(List<WeightDistribution<T>> list) {
            return Sample(list, 1)[0];
        }

        public static List<T> Sample<T>(List<WeightDistribution<T>> list, int n) {
            if (list.Count == 0) {
                Debug.LogError("Trying to sample an empty list");
                return default;
            }
            List<WeightDistribution<T>> clone = new();
            clone.AddRange(list);

            List<T> result = new();
            while (result.Count < n && clone.Count > 0) {
                WeightDistribution<T> element = GetRandomItemInDistribution(clone);
                clone.Remove(element);
                result.Add(element.Obj);
            }
            return result;
        }

        // private static WeightDistribution<T> ParentSample<T>(List<WeightDistribution<T>> list) => ParentSample(list, 1)[0];
        //
        // private static List<WeightDistribution<T>> ParentSample<T>(List<WeightDistribution<T>> list, int n) {
        //     if (list.Count == 0) {
        //         Debug.LogError("Trying to sample an empty list");
        //         return default;
        //     }
        //     List<WeightDistribution<T>> clone = new();
        //     clone.AddRange(list);
        //
        //     List<WeightDistribution<T>> result = new();
        //     while (result.Count < n && clone.Count > 0) {
        //         WeightDistribution<T> element = GetRandomItemInDistribution(clone);
        //         clone.Remove(element);
        //         result.Add(element);
        //     }
        //     return result;
        // }
        #endregion
    }
}
