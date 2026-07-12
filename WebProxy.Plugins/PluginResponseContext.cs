using BPUtil;
using BPUtil.SimpleHttp;
using BPUtil.SimpleHttp.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace WebProxy.Plugins
{
	/// <summary>
	/// <para>Provides a plugin's response headers hook with access to the proxied response, before the response headers are sent to the client.</para>
	/// <para>The hook may modify <see cref="SimpleHttpResponse.StatusString"/> and <see cref="SimpleHttpResponse.Headers"/> via the <see cref="Response"/> object, and may register a response body filter to view or manipulate the response body.</para>
	/// </summary>
	public class PluginResponseContext
	{
		/// <summary>
		/// The HttpProcessor handling the client's connection.
		/// </summary>
		public HttpProcessor Processor { get; }
		/// <summary>
		/// The request context which this response belongs to.
		/// </summary>
		public PluginRequestContext RequestContext { get; }
		/// <summary>
		/// <para>The response object, currently populated with the status and headers received from the remote server.  The response headers have not been sent to the client yet and may be modified.</para>
		/// <para>If a response body filter changes the length of the response body, remember to correct the "Content-Length" header (setting <c>Response.ContentLength = null</c> is safe; chunked transfer encoding or connection-close framing will be used automatically).</para>
		/// </summary>
		public SimpleHttpResponse Response => Processor.Response;

		internal Func<Stream, Task<Stream>> ResponseBodyFilter;
		/// <summary>
		/// Id of the plugin instance which this response context was created for.  Used by the WebProxy host for error attribution.
		/// </summary>
		internal string PluginInstanceId;

		internal PluginResponseContext(HttpProcessor processor, PluginRequestContext requestContext, string pluginInstanceId)
		{
			Processor = processor;
			RequestContext = requestContext;
			PluginInstanceId = pluginInstanceId;
		}

		/// <summary>
		/// <para>Registers a filter which can view or replace the stream that the response body will be read from, enabling streaming response body manipulation.</para>
		/// <para>The filter is awaited before the response headers are written to the client, so the filter (and the response headers hook that registered it) may still modify response headers.  The stream given to the filter yields the response body from the remote server with transfer encoding already decoded, but possibly still compressed according to the "Content-Encoding" response header.  Return the original stream to leave the body unmodified, or return a replacement stream (e.g. a wrapping stream which transforms data as it is read, or a MemoryStream containing an entirely new body).</para>
		/// <para>If the replacement will have a different length than the original, correct the "Content-Length" response header before the filter returns (<c>Response.ContentLength = null</c> is safe).</para>
		/// <para>If multiple plugins register filters for the same response, each filter receives the output stream of the previous filter.</para>
		/// <para>For simple buffer-the-whole-body transformations, consider <see cref="SetBufferedBodyTransform(Func{byte[], Task{byte[]}}, int)"/> instead, which handles Content-Encoding and header fixups automatically.</para>
		/// </summary>
		/// <param name="filter">Asynchronous callback which is given the response body stream and returns the stream which the response body should be read from instead.</param>
		/// <exception cref="ArgumentNullException">If the filter is null.</exception>
		/// <exception cref="InvalidOperationException">If a filter was already registered by this hook.</exception>
		public void SetResponseBodyFilter(Func<Stream, Task<Stream>> filter)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (ResponseBodyFilter != null)
				throw new InvalidOperationException("A response body filter was already registered by this plugin for this response.");
			ResponseBodyFilter = filter;
		}

		/// <summary>
		/// <para>Registers a response body filter which buffers the entire response body, decodes the "Content-Encoding" (gzip, deflate, br) if present, passes the decoded body to the given transform function, and responds with the transform's result.</para>
		/// <para>Headers are corrected automatically: "Content-Encoding" is removed (the new body is sent uncompressed) and "Content-Length" is set to the length of the new body.  The transform may modify other response headers (such as "Content-Type") before it returns.</para>
		/// <para>This buffers the response body in memory and should not be used for responses of unbounded size; if the body exceeds <paramref name="maxBufferBytes"/>, the request is aborted.</para>
		/// </summary>
		/// <param name="transform">Asynchronous function which is given the decoded response body and returns the new response body.  Returning null leaves the original (already buffered) body in place.</param>
		/// <param name="maxBufferBytes">Maximum response body size, in bytes, that will be buffered.  Default: 50 MiB.</param>
		/// <exception cref="ArgumentNullException">If the transform is null.</exception>
		/// <exception cref="InvalidOperationException">If a filter was already registered by this hook.</exception>
		public void SetBufferedBodyTransform(Func<byte[], Task<byte[]>> transform, int maxBufferBytes = 50 * 1024 * 1024)
		{
			if (transform == null)
				throw new ArgumentNullException(nameof(transform));
			SetResponseBodyFilter(async sourceStream =>
			{
				ByteUtil.ReadToEndResult readResult = await ByteUtil.ReadToEndWithMaxLengthAsync(sourceStream, maxBufferBytes, 60000, RequestContext.CancellationToken).ConfigureAwait(false);
				if (!readResult.EndOfStream)
					throw new ApplicationException("A plugin's buffered response body transform aborted the response because the response body exceeded the maximum buffer size of " + maxBufferBytes + " bytes.");

				byte[] body = readResult.Data;

				string contentEncoding = Response.Headers.Get("Content-Encoding");
				if (!string.IsNullOrWhiteSpace(contentEncoding) && !contentEncoding.IEquals("identity"))
				{
					CompressionMethod compressionMethod = new CompressionMethod(contentEncoding);
					body = await compressionMethod.DecompressAsync(body, RequestContext.CancellationToken).ConfigureAwait(false);
				}

				byte[] newBody = await transform(body).ConfigureAwait(false);
				if (newBody == null)
					newBody = body;

				Response.Headers.Remove("Content-Encoding");
				Response.ContentLength = newBody.Length;
				return new MemoryStream(newBody);
			});
		}
	}
}
