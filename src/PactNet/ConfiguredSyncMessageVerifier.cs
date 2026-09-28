using System;
using System.Text.Json;
using System.Threading.Tasks;
using PactNet.Drivers;
using PactNet.Exceptions;
using PactNet.Interop;
using PactNet.Models;

namespace PactNet
{
    /// <summary>
    /// Verifies a configured synchronous (request/response) message interaction
    /// </summary>
    internal class ConfiguredSyncMessageVerifier : IConfiguredSyncMessageVerifier
    {
        // the native message returned from the FFI always uses camel case property
        // names, but the inner content may use different settings supplied by the user
        private static readonly JsonSerializerOptions NativeMessageSettings = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ISyncMessageInteractionDriver driver;
        private readonly PactConfig config;
        private readonly PactSpecification version;

        /// <summary>
        /// Initialises a new instance of the <see cref="ConfiguredSyncMessageVerifier"/>
        /// </summary>
        /// <param name="driver">Pact driver</param>
        /// <param name="config">Pact configuration</param>
        /// <param name="version">Pact specification version</param>
        internal ConfiguredSyncMessageVerifier(ISyncMessageInteractionDriver driver, PactConfig config, PactSpecification version)
        {
            this.driver = driver;
            this.config = config;
            this.version = version;
        }

        /// <summary>
        /// Verify a request message is read and handled correctly, and that the response returned
        /// by the handler matches the configured response content, then write the message pact
        /// </summary>
        /// <param name="handler">The method handling the request message and producing a response</param>
        public void Verify<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        {
            try
            {
                NativeSyncMessage generated = this.GeneratedMessage();
                TRequest request = DeserializeContent<TRequest>((JsonElement)generated.Request.Contents);

                TResponse actualResponse = handler(request);

                this.VerifyResponse(generated, actualResponse);
                this.driver.WritePactFile(this.config.PactDir);
            }
            catch (PactMessageConsumerVerificationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new PactMessageConsumerVerificationException($"The message could not be verified by the consumer handler", e);
            }
        }

        /// <summary>
        /// Verify a request message is read and handled correctly, and that the response returned
        /// by the handler matches the configured response content, then write the message pact
        /// </summary>
        /// <param name="handler">The method handling the request message and producing a response</param>
        public async Task VerifyAsync<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler)
        {
            try
            {
                NativeSyncMessage generated = this.GeneratedMessage();
                TRequest request = DeserializeContent<TRequest>((JsonElement)generated.Request.Contents);

                TResponse actualResponse = await handler(request);

                this.VerifyResponse(generated, actualResponse);
                this.driver.WritePactFile(this.config.PactDir);
            }
            catch (PactMessageConsumerVerificationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new PactMessageConsumerVerificationException($"The message could not be verified by the consumer handler", e);
            }
        }

        /// <summary>
        /// Get the generated message, with any matchers removed and any configured generators applied
        /// </summary>
        /// <returns>the native message</returns>
        private NativeSyncMessage GeneratedMessage()
        {
            string generated = this.driver.GenerateContents();
            return JsonSerializer.Deserialize<NativeSyncMessage>(generated, NativeMessageSettings);
        }

        /// <summary>
        /// Check the handler's response against the configured response content
        /// </summary>
        /// <param name="generated">the generated message</param>
        /// <param name="actualResponse">the response returned by the handler under test</param>
        private void VerifyResponse<TResponse>(NativeSyncMessage generated, TResponse actualResponse)
        {
            JsonElement expectedContent = ((JsonElement)generated.Response[0].Contents).GetProperty("content");

            string actualJson = JsonSerializer.Serialize(actualResponse, this.config.DefaultJsonSettings);
            using JsonDocument actualDocument = JsonDocument.Parse(actualJson);

            if (!JsonElementsEqual(expectedContent, actualDocument.RootElement))
            {
                throw new PactMessageConsumerVerificationException(
                    $"The response returned by the consumer handler did not match the configured response content. " +
                    $"Expected {expectedContent.GetRawText()} but got {actualJson}");
            }
        }

        /// <summary>
        /// Deserialize the content of the request or response, with any matchers removed and any
        /// configured generators applied
        /// </summary>
        /// <typeparam name="T">the type to deserialize the content into</typeparam>
        /// <param name="contents">the generated message contents</param>
        /// <returns>the deserialized content</returns>
        private T DeserializeContent<T>(JsonElement contents)
        {
            // Synchronous messages only exist in the V4 Pact format, so the generated contents
            // always use the V4 body envelope (a `content` field)
            string contentString = contents.GetProperty("content").GetRawText();

            return JsonSerializer.Deserialize<T>(contentString, this.config.DefaultJsonSettings);
        }

        /// <summary>
        /// Recursively compare two JSON elements for structural equality, ignoring object property order
        /// </summary>
        private static bool JsonElementsEqual(JsonElement expected, JsonElement actual)
        {
            if (expected.ValueKind != actual.ValueKind)
            {
                return false;
            }

            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    var expectedProperties = new System.Collections.Generic.Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (JsonProperty property in expected.EnumerateObject())
                    {
                        expectedProperties[property.Name] = property.Value;
                    }

                    var actualProperties = new System.Collections.Generic.Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (JsonProperty property in actual.EnumerateObject())
                    {
                        actualProperties[property.Name] = property.Value;
                    }

                    if (expectedProperties.Count != actualProperties.Count)
                    {
                        return false;
                    }

                    foreach (var property in expectedProperties)
                    {
                        if (!actualProperties.TryGetValue(property.Key, out JsonElement actualValue) ||
                            !JsonElementsEqual(property.Value, actualValue))
                        {
                            return false;
                        }
                    }

                    return true;

                case JsonValueKind.Array:
                    var expectedItems = new System.Collections.Generic.List<JsonElement>();
                    foreach (JsonElement item in expected.EnumerateArray())
                    {
                        expectedItems.Add(item);
                    }

                    var actualItems = new System.Collections.Generic.List<JsonElement>();
                    foreach (JsonElement item in actual.EnumerateArray())
                    {
                        actualItems.Add(item);
                    }

                    if (expectedItems.Count != actualItems.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < expectedItems.Count; i++)
                    {
                        if (!JsonElementsEqual(expectedItems[i], actualItems[i]))
                        {
                            return false;
                        }
                    }

                    return true;

                case JsonValueKind.String:
                    return expected.GetString() == actual.GetString();

                case JsonValueKind.Number:
                    return expected.GetRawText() == actual.GetRawText();

                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return true;

                default:
                    return expected.GetRawText() == actual.GetRawText();
            }
        }
    }
}
