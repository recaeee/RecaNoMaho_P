using System;

namespace Unity.FilmInternalUtilities.Editor {

/// <summary>
/// A class that holds the version (semantic versioned) of a package in a structure
/// </summary>
internal class PackageVersion {

    //private: force to use TryParse()
    private PackageVersion() { }

//----------------------------------------------------------------------------------------------------------------------    

    internal int? GetMajor() => m_major;
    internal int? GetMinor() => m_minor;
    internal int? GetPatch() => m_patch;
    
    internal PackageLifecycle GetLifeCycle() => m_lifecycle;
    
    internal string GetMetadata() => m_additionalMetadata;
    
//----------------------------------------------------------------------------------------------------------------------
    
    public override string ToString() {        
        string ret = $"{SingleVerToString(m_major)}.{SingleVerToString(m_minor)}.{SingleVerToString(m_patch)}";

        switch (this.m_lifecycle) {
            case PackageLifecycle.RELEASED: break;
            case PackageLifecycle.PRERELEASE: ret += "-pre"; break;
            case PackageLifecycle.PREVIEW:
            case PackageLifecycle.EXPERIMENTAL: {
                ret += "-" + m_lifecycle.ToString().ToLower();
                break;                
            }
            default: break;
        }       
        
        if (!string.IsNullOrEmpty(m_additionalMetadata)) {
            ret += "." + m_additionalMetadata;
        }

        return ret;

    }
    
//----------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Parse a semantic versioned string to a PackageVersion class
    /// </summary>
    /// <param name="semanticVer">Semantic versioned input string</param>
    /// <param name="packageVersion">The detected PackageVersion. Set to null when the parsing fails</param>
    /// <returns>true if successful, false otherwise</returns>
    public static bool TryParse(string semanticVer, out PackageVersion packageVersion) {
        packageVersion = new PackageVersion();
        
        string[] tokens = semanticVer.Split('.');
        if (tokens.Length <= 2)
            return false;

        if (!TryParseSingleVer(tokens[0], out packageVersion.m_major))
            return false;
        

        if (!TryParseSingleVer(tokens[1], out packageVersion.m_minor))
            return false;
        

        //Find patch and lifecycle
        string[] patches = tokens[2].Split('-');
        if (!TryParseSingleVer(patches[0], out packageVersion.m_patch))
            return false;
               
        PackageLifecycle lifecycle = PackageLifecycle.RELEASED;
        if (patches.Length > 1) {
            string lifecycleStr = patches[1].ToLower();                    
            switch (lifecycleStr) {
                case "experimental": lifecycle = PackageLifecycle.EXPERIMENTAL; break;
                case "preview"     : lifecycle = PackageLifecycle.PREVIEW; break;
                case "pre"         : lifecycle = PackageLifecycle.PRERELEASE; break;
                default: lifecycle             = PackageLifecycle.INVALID; break;
            }
            
        } 

        packageVersion.m_lifecycle = lifecycle;

        const int METADATA_INDEX = 3;
        if (tokens.Length > METADATA_INDEX) {
            packageVersion.m_additionalMetadata = String.Join(".",tokens, METADATA_INDEX, tokens.Length-METADATA_INDEX);
        }

        return true;
    }

//----------------------------------------------------------------------------------------------------------------------    
    static bool TryParseSingleVer(string token, out int? version) {
        if (token == "x") {
            version = null;
            return true;
        }
        
        if (int.TryParse(token, out int parsedToken)) {
            version = parsedToken;
            return true;
        }

        version = 0;
        return false;
    }

    static string SingleVerToString(int? ver) {
        return null == ver ? "x" : ver.ToString();
    }

    
//----------------------------------------------------------------------------------------------------------------------
    private int? m_major = 0;
    private int? m_minor = 0;
    private int? m_patch = 0;
    
    private PackageLifecycle m_lifecycle;
    private string           m_additionalMetadata;
    
}

}

