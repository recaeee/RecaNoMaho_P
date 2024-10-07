using System;                               //Action
using UnityEditor.PackageManager.Requests;  //Request<T>
using UnityEditor.PackageManager;           //PackageInfo

namespace Unity.FilmInternalUtilities.Editor {

internal class PackageSearchAllRequestInfo {
    internal readonly bool OfflineMode;
    internal readonly Action<Request<PackageInfo[]>> OnSuccessAction;
    internal readonly Action<Request<PackageInfo[]>> OnFailAction;

    internal PackageSearchAllRequestInfo(bool offlineMode, 
        Action<Request<PackageInfo[]>> onSuccess, Action<Request<PackageInfo[]>> onFail)
    {
        OfflineMode = offlineMode;
        OnSuccessAction = onSuccess;
        OnFailAction = onFail;
    }
}

} //namespace Unity.AnimeToolbox
