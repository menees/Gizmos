namespace Menees.Gizmos;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#endregion

internal sealed class WindowSaverLock : IDisposable
{
	#region Private Data Members

	private readonly Mutex mutex;

	#endregion

	#region Constructors

	public WindowSaverLock()
	{
		// Because all gizmos share the same .stgx file, we'll use the full path to the .exe as the mutex name
		// (since the .stgx file will typically be in the same folder as the .exe).  However the '\' character is
		// reserved for kernel object namespaces, so we have to replace it.
		// http://stackoverflow.com/questions/4313756/creating-a-mutex-throws-a-directorynotfoundexception.
		string mutexName = ApplicationInfo.ExecutableFile.Replace('\\', '_');
		this.mutex = new(false, mutexName);
	}

	#endregion

	#region Public Methods

	public IDisposable? TryLock(TimeSpan timeout)
	{
		IDisposable? result = null;

		if (this.mutex.WaitOne(timeout))
		{
			result = new Disposer(() => this.mutex.ReleaseMutex());
		}

		return result;
	}

	public void Dispose()
	{
		this.mutex.Dispose();
	}

	#endregion
}
