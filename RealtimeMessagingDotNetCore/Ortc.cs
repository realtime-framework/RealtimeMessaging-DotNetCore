using System;
using System.Collections.Generic;
//using System.ComponentModel.Composition;
//using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using RT.Ortc.Api.Extensibility;
using System.Net.Http;
using System.Composition;

namespace RT.Ortc.Api
{
    /// <summary>
    /// The channel permission.
    /// </summary>
    public enum ChannelPermissions
    {
        /// <summary>
        /// Read permission
        /// </summary>
        Read = 'r',

        /// <summary>
        /// Read and Write permission
        /// </summary>
        Write = 'w',

        /// <summary>
        /// Presence permission
        /// </summary>
        Presence = 'p'
    }

    /// <summary>
    /// ORTC server side API that contains ORTC factories as plugins.
    /// </summary>
    public class Ortc
    {
        #region Constructors (2)


        public Ortc()
        {
            
        }

        #endregion

        #region Public Methods (3)

        /// <summary>
        /// Loads the ORTC factory with the specified ORTC type.
        /// </summary>
        /// <param name="ortcType">The type of the ORTC client created by the factory.</param>
        /// <returns>Loaded instance of ORTC factory of the specified ORTC type.</returns>
        /// <example>
        /// <code>
        ///     var api = new Api.Ortc("Plugins");
        /// 
        ///     IOrtcFactory factory = api.LoadOrtcFactory("RealTimeSJ");
        ///     
        ///     // Use the factory instance to create new clients
        /// </code>
        /// </example>
        public IOrtcFactory LoadOrtcFactory(string ortcType)
        {
            IOrtcFactory iOFactory = new RT.Ortc.Plugin.RealTimeSJ.RealTimeSJFactory();
            return iOFactory;
            //Lazy<IOrtcFactory, IOrtcFactoryAttributes> factory = OrtcProviders.Where(f => f.Metadata.FactoryType.Equals(ortcType, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            //return factory == null ? null : factory.Value;
        }



        #endregion
    }
}