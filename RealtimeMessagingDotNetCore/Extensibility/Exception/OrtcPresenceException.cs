//System.Serializable]
public class OrtcPresenceException : System.Exception
{
    public OrtcPresenceException() { }
    public OrtcPresenceException(string message) : base(message) { }
    public OrtcPresenceException(string message, System.Exception inner) : base(message, inner) { }
    //protected OrtcPresenceException(
     // System.Runtime.Serialization.SerializationInfo info,
     // System.Runtime.Serialization.StreamingContext context)
   // ) : base(info, context) { }
}