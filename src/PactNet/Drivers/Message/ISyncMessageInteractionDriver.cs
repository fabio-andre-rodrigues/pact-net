namespace PactNet.Drivers
{
    /// <summary>
    /// Driver for synchronous (request/response) message interactions
    /// </summary>
    internal interface ISyncMessageInteractionDriver : IProviderStateDriver, ICompletedPactDriver
    {
        /// <summary>
        /// Set a metadata value of the request message
        /// </summary>
        /// <param name="key">the key</param>
        /// <param name="value">the value as JSON, which may contain matchers</param>
        void WithRequestMetadata(string key, string value);

        /// <summary>
        /// Set a metadata value of all the existing response messages
        /// </summary>
        /// <param name="key">the key</param>
        /// <param name="value">the value as JSON, which may contain matchers</param>
        void WithResponseMetadata(string key, string value);

        /// <summary>
        /// Set the contents of the request message
        /// </summary>
        /// <param name="contentType">the content type</param>
        /// <param name="body">the body of the request message</param>
        void WithRequestContents(string contentType, string body);

        /// <summary>
        /// Set the contents of the response message
        /// </summary>
        /// <param name="contentType">the content type</param>
        /// <param name="body">the body of the response message</param>
        void WithResponseContents(string contentType, string body);

        /// <summary>
        /// Get the actual request and response contents, with any matchers removed and any
        /// configured generators applied
        /// </summary>
        /// <returns>The generated request and response contents</returns>
        string GenerateContents();
    }
}
