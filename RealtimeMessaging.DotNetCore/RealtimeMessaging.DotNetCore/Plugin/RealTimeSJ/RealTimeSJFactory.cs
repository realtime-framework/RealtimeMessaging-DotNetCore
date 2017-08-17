using RealtimeMessaging.DotNetCore.Extensibility;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.IO;


namespace RealtimeMessaging.DotNetCore.Plugin.RealTimeSJ
{
    /// <summary>
    /// Real Time SJ type factory.
    /// </summary>
    /// <remarks>
    /// Factory Type = RealTimeSJ.
    /// </remarks>
    //[ExportOrtcFactory(FactoryType = "RealTimeSJ")]
    public class RealTimeSJFactory : IOrtcFactory
    {
        #region Attributes

        /// <summary>
        /// To load the assemblies just once.
        /// </summary>
        private readonly IDictionary<string, Assembly> _loadedAssemblies;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="RealTimeKFactory"/> class.
        /// </summary>
        public RealTimeSJFactory()
        {
            _loadedAssemblies = new Dictionary<string, Assembly>();

            //LoadWebSocket4NetAssembly();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new instance of a ortc client.
        /// </summary>
        /// <returns>
        /// New instance of <see cref="OrtcClient"/>.
        /// </returns>
        public OrtcClient CreateClient()
        {
            return new RealTimeSJClient();
        }

        #endregion
    }
}