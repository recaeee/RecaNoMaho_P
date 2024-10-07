using System;                               //Action

namespace Unity.FilmInternalUtilities.Editor {
internal class PackageRemoveRequestInfo{
    internal readonly string PackageName;
    internal readonly Action OnSuccessAction;
    internal readonly Action OnFailAction;

    internal PackageRemoveRequestInfo(string packageName,
        Action onSuccess, Action onFail)
    {
        PackageName = packageName;
        OnSuccessAction = onSuccess;
        OnFailAction = onFail;
    }
}

} //namespace Unity.AnimeToolbox
