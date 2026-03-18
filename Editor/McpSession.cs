using System.Net;
using System.Threading.Tasks;

namespace SboxMcpServer;

public class McpSession
{
	public string SessionId { get; set; }
	public HttpListenerResponse SseResponse { get; set; }
	public TaskCompletionSource<bool> Tcs { get; set; } = new TaskCompletionSource<bool>();
	public bool Initialized { get; set; }
}
