using System;
using System.ComponentModel;

namespace RT.Ortc.Api.Extensibility
{
    /// <summary>
    /// Represents the metadata of a <see cref="IOrtcFactory"/> plugin.
    /// </summary>
    public interface IOrtcFactoryAttributes
    {
        /// <summary>
        /// Gets the type of the factory.
        /// </summary>
        /// <value>
        /// The type of the factory.
        /// </value>
        string FactoryType { get; }
    }

    /// <summary>
    /// MEF export attribute extension with the factory type metadata.
    /// </summary>
    /// <example>
    /// <code>
    /// [ExportOrtcFactory(FactoryType = "RealTimeS")]
    /// public class RealTimeSFactory : IOrtcFactory
    /// {
    ///     // Factory plugin implementation
    /// }
    /// </code>
    /// </example>
    /// <exclude/>
    //[MetadataAttribute]
    //[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ExportOrtcFactoryAttribute : System.Composition.ExportAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportOrtcFactoryAttribute"/> class.
        /// </summary>
        public ExportOrtcFactoryAttribute() : base(typeof (IOrtcFactory))
        {
        }

        /// <summary>
        /// Gets or sets the type of the factory.
        /// </summary>
        /// <value>
        /// The type of the factory.
        /// </value>
        public string FactoryType { get; set; }
    }
}