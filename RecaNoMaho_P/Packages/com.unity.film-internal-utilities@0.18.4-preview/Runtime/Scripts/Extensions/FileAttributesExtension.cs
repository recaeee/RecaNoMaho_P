using System.IO;

namespace Unity.FilmInternalUtilities {
    internal static class FileAttributesExtension {

        public static void RemoveAttribute(this FileAttributes attributes, FileAttributes attributesToRemove) {
            attributes &=~attributesToRemove;
        }

    }

}

