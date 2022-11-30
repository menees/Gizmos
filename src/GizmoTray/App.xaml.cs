namespace Menees.Gizmos;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Menees.Windows.Presentation;

#endregion

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	#region Private Data Members

	private TrayManager? trayManager;

	#endregion

	#region Constructors

	public App()
	{
		// Change the mode to require explicit shutdown. Otherwise, closing the About box will exit the app.
		// https://stackoverflow.com/a/5084852/1882616
		this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

		WindowsUtility.InitializeApplication("GizmoTray", null);
	}

	#endregion

	#region Protected Methods

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		this.trayManager = new TrayManager();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		this.trayManager?.Dispose();
		base.OnExit(e);
	}

	#endregion
}
