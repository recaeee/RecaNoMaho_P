using UnityEngine;

namespace Unity.FilmInternalUtilities {
internal static class ExposedPropertyTableExtensions {
    internal static Object Resolve(this IExposedPropertyTable table, PropertyName exposedReferenceName) {
        Object referenceValue = table.GetReferenceValue(exposedReferenceName, out bool idValid);
        return idValid ? referenceValue : null;
    }
}
} //end namespace