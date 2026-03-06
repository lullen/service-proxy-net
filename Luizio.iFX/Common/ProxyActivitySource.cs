using System.Diagnostics;

namespace Luizio.iFX;

public static class ProxyActivitySource
{
	public const string SourceName = "Proxy";
	public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}
