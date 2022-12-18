using UnityEngine;

namespace Extension {
    public static class MathExtensions {
        public static double MapValue(float x, float xLeft, float xRight, float resLeft, float resRight) {
            if (Mathf.Approximately( xLeft, xRight)) {
                return resLeft;
            }
            
            return (x - xLeft) / (xRight - xLeft) * (resRight - resLeft) + resLeft;
        }
    }
}
