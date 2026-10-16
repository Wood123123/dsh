using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Common.Utils;

namespace StashTools;

internal static class MarketTransport
{
	private const string Path = "/client/ragfair/itemMarketPrice";

	private static readonly SemaphoreSlim StartGate = new SemaphoreSlim(1, 1);

	private static long _lastStart;

	internal static async Task<MarketQuote> Request(string template, CancellationToken cancellation)
	{
		await StartGate.WaitAsync(cancellation).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			double num = 0.25 - (double)(Stopwatch.GetTimestamp() - _lastStart) / (double)Stopwatch.Frequency;
			if (num > 0.0)
			{
				await Task.Delay(TimeSpan.FromSeconds(num), cancellation).ConfigureAwait(continueOnCapturedContext: false);
			}
			_lastStart = Stopwatch.GetTimestamp();
		}
		finally
		{
			StartGate.Release();
		}
		Client httpClient = RequestHandler.HttpClient;
		using HttpRequestMessage request = httpClient.CreateNewHttpRequest(HttpMethod.Post, "/client/ragfair/itemMarketPrice");
		byte[] content = Zlib.Compress(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject((object)new
		{
			templateId = template
		})), (ZlibCompression)9);
		request.Content = new ByteArrayContent(content);
		HttpResponseMessage response = await httpClient.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			response.EnsureSuccessStatusCode();
			if (response.Content.Headers.ContentLength > 1048576)
			{
				throw new InvalidDataException("市场报价响应过大");
			}
			using (cancellation.Register(delegate
			{
				response.Dispose();
			}))
			{
				cancellation.ThrowIfCancellationRequested();
				using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
				using MemoryStream buffer = new MemoryStream();
				byte[] chunk = new byte[4096];
				while (true)
				{
					int num2 = await stream.ReadAsync(chunk, 0, chunk.Length, cancellation).ConfigureAwait(continueOnCapturedContext: false);
					if (num2 == 0)
					{
						break;
					}
					if (buffer.Length + num2 > 1048576)
					{
						throw new InvalidDataException("市场报价响应过大");
					}
					buffer.Write(chunk, 0, num2);
				}
				cancellation.ThrowIfCancellationRequested();
				byte[] array = buffer.ToArray();
				if (array.Length == 0)
				{
					throw new InvalidDataException("市场报价响应为空");
				}
				if (Zlib.IsCompressed(array))
				{
					array = Zlib.Decompress(array);
				}
				cancellation.ThrowIfCancellationRequested();
				return MarketResponse.Parse(array);
			}
		}
		finally
		{
			if (response != null)
			{
				((IDisposable)response).Dispose();
			}
		}
	}
}
