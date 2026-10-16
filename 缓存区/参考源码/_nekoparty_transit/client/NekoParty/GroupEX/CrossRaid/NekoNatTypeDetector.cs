using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fika.Core.Main.Utils;

namespace NekoParty.GroupEX.CrossRaid;

internal static class NekoNatTypeDetector
{
	private const int AttemptsPerPhase = 3;

	private const int ReceiveTimeoutMs = 800;

	private const float RetryCooldownSeconds = 600f;

	private static int _running;

	private static DateTime _lastAttemptUtc = DateTime.MinValue;

	internal static volatile NekoNatType CurrentType = NekoNatType.Unknown;

	internal static volatile string ExternalIp = "";

	internal static string Label(NekoNatType type)
	{
		return type switch
		{
			NekoNatType.Open => "NAT开放", 
			NekoNatType.Moderate => "NAT中等", 
			NekoNatType.Symmetric => "NAT对称", 
			NekoNatType.Blocked => "UDP受限", 
			NekoNatType.Detecting => "检测中", 
			_ => "", 
		};
	}

	internal static void EnsureStarted()
	{
		if ((int)(CurrentType - 2) <= 2 || (DateTime.UtcNow - _lastAttemptUtc).TotalSeconds < 600.0 || !NekoCrossTransitRuntime.TryGetNatProbeEndpoints(out var host, out var port1, out var port2) || Interlocked.CompareExchange(ref _running, 1, 0) != 0)
		{
			return;
		}
		_lastAttemptUtc = DateTime.UtcNow;
		CurrentType = NekoNatType.Detecting;
		Task.Run(delegate
		{
			try
			{
				NekoNatType type = (CurrentType = Detect(host, port1, port2));
				FikaGlobals.LogInfo("[NekoNAT] NAT 类型检测完成: " + Label(type) + "（外网 " + ExternalIp + "）", "EnsureStarted");
			}
			catch (Exception ex)
			{
				CurrentType = NekoNatType.Unknown;
				FikaGlobals.LogWarning("[NekoNAT] NAT 类型检测异常: " + ex.Message, "EnsureStarted");
			}
			finally
			{
				Interlocked.Exchange(ref _running, 0);
			}
		});
	}

	private static NekoNatType Detect(string host, int port1, int port2)
	{
		IPAddress address;
		try
		{
			if (!IPAddress.TryParse(host, out address))
			{
				address = Array.Find(Dns.GetHostAddresses(host), (IPAddress a) => a.AddressFamily == AddressFamily.InterNetwork);
			}
		}
		catch
		{
			address = null;
		}
		if (address == null)
		{
			return NekoNatType.Unknown;
		}
		using UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
		udpClient.Client.ReceiveTimeout = 800;
		IPEndPoint target = new IPEndPoint(address, port1);
		IPEndPoint target2 = new IPEndPoint(address, port2);
		IPEndPoint iPEndPoint = Probe(udpClient, target, crossPort: false);
		IPEndPoint iPEndPoint2 = Probe(udpClient, target2, crossPort: false);
		if (iPEndPoint == null && iPEndPoint2 == null)
		{
			return NekoNatType.Blocked;
		}
		if (iPEndPoint == null || iPEndPoint2 == null)
		{
			ExternalIp = (iPEndPoint ?? iPEndPoint2).Address.ToString();
			return NekoNatType.Moderate;
		}
		ExternalIp = iPEndPoint.Address.ToString();
		if (!iPEndPoint.Address.Equals(iPEndPoint2.Address) || iPEndPoint.Port != iPEndPoint2.Port)
		{
			return NekoNatType.Symmetric;
		}
		return (Probe(udpClient, target, crossPort: true) != null) ? NekoNatType.Open : NekoNatType.Moderate;
	}

	private static IPEndPoint Probe(UdpClient socket, IPEndPoint target, bool crossPort)
	{
		for (int i = 0; i < 3; i++)
		{
			string text = Guid.NewGuid().ToString("N").Substring(0, 12);
			byte[] bytes = Encoding.ASCII.GetBytes($"neko.nat1 {text} {(crossPort ? 1 : 0)}");
			try
			{
				socket.Send(bytes, bytes.Length, target);
			}
			catch
			{
				continue;
			}
			DateTime dateTime = DateTime.UtcNow.AddMilliseconds(800.0);
			while (DateTime.UtcNow < dateTime)
			{
				IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
				byte[] data;
				try
				{
					data = socket.Receive(ref remoteEP);
				}
				catch (SocketException)
				{
					break;
				}
				string[] array = SafeAscii(data).Split(' ');
				if (array.Length >= 3 && string.Equals(array[0], "neko.natr", StringComparison.Ordinal) && string.Equals(array[1], text, StringComparison.Ordinal))
				{
					IPEndPoint iPEndPoint = ParseEndpoint(array[2]);
					if (iPEndPoint != null)
					{
						return iPEndPoint;
					}
				}
			}
		}
		return null;
	}

	private static string SafeAscii(byte[] data)
	{
		try
		{
			object obj;
			if (data != null)
			{
				int num = data.Length;
				if (num > 0 && num <= 256)
				{
					obj = Encoding.ASCII.GetString(data);
					goto IL_0027;
				}
			}
			obj = "";
			goto IL_0027;
			IL_0027:
			return (string)obj;
		}
		catch
		{
			return "";
		}
	}

	private static IPEndPoint ParseEndpoint(string text)
	{
		int num = text.LastIndexOf(':');
		if (num <= 0)
		{
			return null;
		}
		if (IPAddress.TryParse(text.Substring(0, num), out var address) && int.TryParse(text.Substring(num + 1), out var result) && result > 0 && result <= 65535)
		{
			return new IPEndPoint(address, result);
		}
		return null;
	}
}
