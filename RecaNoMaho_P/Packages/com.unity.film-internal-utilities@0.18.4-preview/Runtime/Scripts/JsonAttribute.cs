using System;


namespace Unity.FilmInternalUtilities {
[AttributeUsage(AttributeTargets.Class)]
internal class JsonAttribute : Attribute {
    internal JsonAttribute(string path) {
        m_path = path;
    }

    internal string GetPath() => m_path;

//----------------------------------------------------------------------------------------------------------------------

    private readonly string m_path;
}
} //end namespace