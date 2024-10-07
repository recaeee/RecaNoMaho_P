using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.FilmInternalUtilities {


internal class OneTimeLogger {

    internal OneTimeLogger(System.Func<bool> logCondition, string logString) {

        Assert.IsNotNull(logCondition);
        Assert.IsNotNull(logString);
        
        m_logConditionFunc = logCondition;
        m_logString        = logString;
    }    

//----------------------------------------------------------------------------------------------------------------------
    
    //True: log was output
    //False: log was not output 
    internal bool Update(string logPrefix="", string logPostFix="") {
        bool prevLogged = m_shouldLog;
        
        m_shouldLog = m_logConditionFunc();
        if (!m_shouldLog)
            return false;

        //already logged
        if (prevLogged) {
            return true;
        }
        
        Debug.LogWarning($"{logPrefix} {m_logString} {logPostFix}");
        return true;
    }

//----------------------------------------------------------------------------------------------------------------------
    
    private bool m_shouldLog = false;
    
    private readonly System.Func<bool> m_logConditionFunc = null;
    private readonly string            m_logString  = null;



}

} //end namespace
