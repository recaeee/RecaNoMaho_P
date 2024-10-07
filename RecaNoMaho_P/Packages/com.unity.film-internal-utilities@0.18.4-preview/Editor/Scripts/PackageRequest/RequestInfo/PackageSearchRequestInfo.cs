using System;                               //Action
using UnityEditor.PackageManager.Requests;  //Request<T>
using UnityEditor.PackageManager;           //PackageInfo

namespace Unity.FilmInternalUtilities.Editor {
    
internal class PackageSearchRequestInfo {
    internal readonly string PackageName;
    internal readonly bool OfflineMode;
    internal readonly Action<Request<PackageInfo[]>> OnSuccessAction;
    internal readonly Action<Request<PackageInfo[]>> OnFailAction;

    internal PackageSearchRequestInfo(string packageName, bool offlineMode,
        Action<Request<PackageInfo[]>> onSuccess, Action<Request<PackageInfo[]>> onFail)
    {
        PackageName = packageName;
        OfflineMode = offlineMode;
        OnSuccessAction = onSuccess;
        OnFailAction = onFail;
    }
}

} //namespace Unity.AnimeToolbox
