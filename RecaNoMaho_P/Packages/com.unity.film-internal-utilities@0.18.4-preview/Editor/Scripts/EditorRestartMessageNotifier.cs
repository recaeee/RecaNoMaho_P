
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;


namespace Unity.FilmInternalUtilities.Editor {

/// <summary>
/// A class to notify users to restart Unity, if requested 
/// </summary>
internal static class EditorRestartMessageNotifier {

    [InitializeOnLoadMethod]
    private static void EditorRestartMessageNotifier_OnEditorLoad() {
        m_notifyTime             = EditorApplication.timeSinceStartup + WAIT_THRESHOLD;
        EditorApplication.update += WaitUntilNotify;
    }
    
//----------------------------------------------------------------------------------------------------------------------    

    static void WaitUntilNotify() {
        if (EditorApplication.timeSinceStartup < m_notifyTime) {
            return;
        }

        if (m_onLoadPackageRequesters.Count <= 0) {
            EditorApplication.update -= WaitUntilNotify;            
            return;            
        }
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Please restart editor because the following packages have been updated: ");
        foreach (PackageInfo packageInfo in m_onLoadPackageRequesters) {
            sb.AppendLine($"-{packageInfo.name}@{packageInfo.version}");
        }

        if (EditorUtility.DisplayDialog("Warning", sb.ToString(), "Exit Unity now", "Later")) {
            EditorApplication.Exit(0);
        }
                    
        m_onLoadPackageRequesters.Clear();
        EditorApplication.update -= WaitUntilNotify;            
        
    }

//----------------------------------------------------------------------------------------------------------------------    

    /// <summary>
    /// Request to notify users to restart Unity after loading (compiling) scripts.
    /// </summary>
    /// <param name="packageInfo">The package that requests the restart</param>
    public static void RequestNotificationOnLoad(PackageInfo packageInfo) {
        m_onLoadPackageRequesters.Add(packageInfo);
        m_notifyTime = EditorApplication.timeSinceStartup + WAIT_THRESHOLD;
        
    }
    
//----------------------------------------------------------------------------------------------------------------------  

    private static readonly List<PackageInfo> m_onLoadPackageRequesters = new List<PackageInfo>();
    private static double m_notifyTime = 0;
    private const double WAIT_THRESHOLD = 3.0f;
}

} //end namespace

