namespace RealtimeMessaging.DotNetCore.Extensibility
{
    /// <summary>
    /// Represents a factory of a type of ortc clients.
    /// </summary>
    /// <example>
    /// <code>
    /// var api = new Api.Ortc();
    /// 
    /// IOrtcFactory factory = api.LoadOrtcFactory("RealTimeSJ");
    /// </code>
    /// </example>
    public interface IOrtcFactory
    {
        /// <summary>
        /// Creates a new instance of a <see cref="OrtcClient"/>.
        /// </summary>
        /// <returns>New instance of <see cref="OrtcClient"/>.</returns>
        /// <example>
        /// <code>
        /// var api = new Api.Ortc();
        /// 
        /// IOrtcFactory factory = api.LoadOrtcFactory("RealTimeSJ");
        /// 
        /// OrtcClient ortcClient = factory.CreateClient();
        /// </code>
        /// </example>
        OrtcClient CreateClient();
    }
}