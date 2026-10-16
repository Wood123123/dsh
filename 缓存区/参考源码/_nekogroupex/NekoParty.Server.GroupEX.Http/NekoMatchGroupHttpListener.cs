using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers.Http;

namespace NekoParty.Server.GroupEX.Http;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoMatchGroupHttpListener(NekoNativeGroupController controller, ISptLogger<NekoMatchGroupHttpListener> logger) : IHttpListener
{
	public bool CanHandle(HttpContext context)
	{
		if (NekoGroupExServerConfig.NativeGroupPassthrough)
		{
			return false;
		}
		string method = context.Request.Method;
		if (method != "POST" && method != "PUT" && method != "GET")
		{
			return false;
		}
		return NekoNativeGroupController.HandledPaths.Contains(NormalizePath(context.Request.Path.Value));
	}

	public async Task HandleAsync(MongoId sessionId, HttpContext context, CancellationToken cancellationToken)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		string path = NormalizePath(context.Request.Path.Value);
		string body = await ReadBodyAsync(context.Request);
		string output;
		try
		{
			output = controller.Dispatch(path, body, sessionId);
		}
		catch (Exception value)
		{
			logger.Error($"[NekoMatchGroup] 处理 {path} 异常: {value}", (Exception)null);
			output = "{\"err\":0,\"errmsg\":null,\"data\":null}";
		}
		await WriteResponseAsync(context, sessionId, output);
	}

	private static string NormalizePath(string? path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return string.Empty;
		}
		int num = path.IndexOf("?retry=", StringComparison.Ordinal);
		if (num < 0)
		{
			return path;
		}
		return path.Substring(0, num);
	}

	private static async Task<string> ReadBodyAsync(HttpRequest request)
	{
		bool flag = !request.Headers.TryGetValue("requestcompressed", out var value) || value != "0";
		if ((request.Method == "PUT") | flag)
		{
			string result;
			await using (ZLibStream deflateStream = new ZLibStream(request.Body, CompressionMode.Decompress))
			{
				using StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8);
				result = await reader.ReadToEndAsync();
			}
			return result;
		}
		using StreamReader plainReader = new StreamReader(request.Body, Encoding.UTF8);
		return await plainReader.ReadToEndAsync();
	}

	private unsafe static async Task WriteResponseAsync(HttpContext context, MongoId sessionId, string output)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		HttpResponse response = context.Response;
		response.StatusCode = 200;
		response.ContentType = "application/json";
		response.Headers.Append("Set-Cookie", "PHPSESSID=" + ((object)(*(MongoId*)(&sessionId))/*cast due to constrained. prefix*/).ToString());
		byte[] bytes = Encoding.UTF8.GetBytes(output);
		if (context.Request.Headers.TryGetValue("responsecompressed", out var value) && value == "0")
		{
			await response.Body.WriteAsync(bytes);
			return;
		}
		await using ZLibStream deflateStream = new ZLibStream(response.Body, CompressionLevel.SmallestSize);
		await deflateStream.WriteAsync(bytes);
	}
}
