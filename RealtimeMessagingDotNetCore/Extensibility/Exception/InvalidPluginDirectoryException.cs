using System;

namespace RT.Ortc.Api.Extensibility
{
    /// <summary>
    /// Represents the exception thrown when a channel is already subscribed by the client.
    /// </summary>
    public class InvalidPluginDirectoryException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPluginDirectoryException"/> class.
        /// </summary>
        public InvalidPluginDirectoryException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPluginDirectoryException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public InvalidPluginDirectoryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidPluginDirectoryException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner exception.</param>
        public InvalidPluginDirectoryException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}