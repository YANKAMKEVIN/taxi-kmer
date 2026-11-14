namespace UnityEngine.TestTools
{
    public class UnityTestAttribute : System.Attribute { }
    public class UnityTearDownAttribute : System.Attribute { }
    public class UnitySetUpAttribute : System.Attribute { }
    public static class LogAssert { public static bool ignoreFailingMessages; public static void NoUnexpectedReceived() { } }
}
