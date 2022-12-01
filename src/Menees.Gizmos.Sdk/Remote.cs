namespace Menees.Gizmos;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Menees.Remoting;
using Menees.Remoting.Pipes;

#endregion

public static class Remote
{
	#region Private Data Members

	// Calls to our servers should be short-lived, so one listener will normally be all that's kept alive.
	// If more than this max comes in at once, then calls will have to queue up until they're processed
	// or until they timeout.
	private const int MaxListenersPerServer = 5;

	// Named pipe names can't include a backslash per MSDN, so we can't use Path.DirectorySeparatorChar.
	// https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createnamedpipea#parameters
	private const char ServerPathSeparator = '`';

	private static readonly Log Log = Log.GetLog(typeof(Remote));

	private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

	#endregion

	#region Public Methods

	public static IEnumerable<string> GetBaseNames<TServiceInterface>(string wildcardMask = "*")
		where TServiceInterface : class
	{
		string serverPathMask = BuildServerPath<TServiceInterface>(wildcardMask);

		// We can enumerate named pipes using the file system API, PowerShell, or SysInternals's PipeList.exe.
		// dir -path \\.\pipe\ -Filter *sql*
		// https://stackoverflow.com/a/66588424/1882616
		// https://docs.microsoft.com/en-us/sysinternals/downloads/pipelist
		const string NamedPipePrefix = @"\\.\pipe\";
		foreach (string pipeName in Directory.EnumerateFiles(NamedPipePrefix, serverPathMask, SearchOption.AllDirectories))
		{
			string serverPath = pipeName.Substring(NamedPipePrefix.Length);
			string baseName = GetBaseName<TServiceInterface>(serverPath);
			yield return baseName;
		}
	}

	public static IDisposable CreateServer<TServiceInterface>(Gizmo gizmo, TServiceInterface serverInstance)
		where TServiceInterface : class
	{
		Type currentInterface = typeof(TServiceInterface);
		string baseName = GetBaseName(gizmo);
		string serverPath = BuildServerPath(currentInterface, baseName);
		ServerSettings settings = new(serverPath)
		{
			MaxListeners = MaxListenersPerServer,
			Security = PipeServerSecurity.CurrentUserOnly,
		};

		Type concreteServerType = typeof(RmiServer<>).MakeGenericType(currentInterface);
		IServer server = (IServer)Activator.CreateInstance(concreteServerType, serverInstance, settings)!;
		server.Start();

		IDisposable result = server;
		return result;
	}

	public static IDisposable CreateClient<TServiceInterface>(string baseName, out TServiceInterface client, TimeSpan? connectTimeout = null)
		where TServiceInterface : class
	{
		string serverPath = BuildServerPath<TServiceInterface>(baseName);
		ClientSettings settings = new(serverPath)
		{
			ConnectTimeout = connectTimeout ?? DefaultConnectTimeout,
			Security = PipeClientSecurity.CurrentUserOnly,
		};

		RmiClient<TServiceInterface> rmiClient = new(settings);
		client = rmiClient.CreateProxy();
		return rmiClient;
	}

	public static Exception? TryCallService<TServiceInterface>(
		string baseName,
		Action<TServiceInterface> callService,
		TimeSpan? connectTimeout = null)
		where TServiceInterface : class
	{
		Conditions.RequireString(baseName, nameof(baseName));
		Conditions.RequireReference(callService, nameof(callService));

		using IDisposable clientDispose = CreateClient(baseName, out TServiceInterface service, connectTimeout);
		Exception? result = null;
		try
		{
			callService(service);
		}
		catch (Exception ex) when (HandleException(ex))
		{
			string serverPath = BuildServerPath<TServiceInterface>(baseName);
			Log.Warning($"Ignoring exception when calling service.", ex, new Dictionary<string, object> { ["ServerPath"] = serverPath });
			result = ex;
		}

		return result;

		// Ignore common exceptions like TimeoutException, IOException, and ObjectDisposedException.
		// These can occur in an asynchronous multi-process world when the process hosting a server
		// or the client is shutting down or has already shut down.
		static bool HandleException(Exception ex)
			=> ex is TimeoutException // The operation has timed out.
			|| ex is IOException // Pipe is broken.
			|| ex is ObjectDisposedException // Cannot access a disposed object.
			|| ex is EndOfStreamException // Unable to read 4 byte message length from stream. Only 0 bytes were available.
			|| (ex is AggregateException agg && agg.InnerExceptions.Count == 1 && HandleException(agg.InnerExceptions[0]));
	}

	public static void TryCallAllServers(Action<IGizmoServer> callServer)
	{
		string[] baseNames = GetBaseNames<IGizmoServer>().ToArray();
		Parallel.ForEach(baseNames, baseName =>
		{
			TryCallService<IGizmoServer>(baseName, server => callServer(server));
		});
	}

	public static void CloseAll() => TryCallAllServers(server => server.Close());

	#endregion

	#region Private Methods

	private static string BuildServerPath<TServiceInterface>(string baseName)
		where TServiceInterface : class
		=> BuildServerPath(typeof(TServiceInterface), baseName);

	private static string BuildServerPath(Type serviceInterface, string baseName)
	{
		Conditions.RequireReference(serviceInterface, nameof(serviceInterface));
		if (!serviceInterface.IsInterface)
		{
			throw Exceptions.NewArgumentException($"{serviceInterface.FullName} must be an interface type.");
		}

		string result = string.Join(
			ServerPathSeparator.ToString(),
			typeof(Remote).Namespace,
			GetCurrentServerGroup(),
			baseName,
			serviceInterface.Name);

		return result;
	}

	private static string GetBaseName<TServiceInterface>(string serverPath)
		where TServiceInterface : class
	{
		string[] parts = serverPath.Split(ServerPathSeparator);

		const int RequiredPartLength = 4;
		if (parts.Length != RequiredPartLength
			|| parts[0] != typeof(Remote).Namespace
			|| parts[1] != GetCurrentServerGroup()
			|| parts[RequiredPartLength - 1] != typeof(TServiceInterface).Name)
		{
			throw Exceptions.NewArgumentException($"Unable to split server path: {serverPath}");
		}

		string result = parts[2];
		return result;
	}

	private static string GetCurrentServerGroup()
	{
		// Group servers by the user's current session ID. This way if multiple users are running
		// gizmos in separate sessions, they won't conflict with each other.
		using Process current = Process.GetCurrentProcess();
		int sessionId = current.SessionId;
		string result = sessionId.ToString();
		return result;
	}

	private static string GetBaseName(Gizmo gizmo)
	{
		StringBuilder sb = new(gizmo.Info.GizmoName);
		if (gizmo.InstanceName.IsNotEmpty())
		{
			sb.Append('@').Append(gizmo.InstanceName);
		}

		string result = sb.ToString();
		return result;
	}

	#endregion
}
