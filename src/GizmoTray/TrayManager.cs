namespace Menees.Gizmos;

#region Using Directives

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Menees.Windows.Presentation;

#endregion

internal sealed class TrayManager : IDisposable
{
	#region Private Data Members

	private readonly NotifyIcon notifyIcon;

	#endregion

	#region Constructors

	public TrayManager()
	{
		this.notifyIcon = CreateNotifyIcon();
	}

	#endregion

	#region Public Methods

	public void Dispose()
	{
		this.notifyIcon?.Dispose();
		this.notifyIcon?.ContextMenuStrip?.Dispose();
	}

	#endregion

	#region Private Methods

	private static ToolStripMenuItem CreateMenuItem(string text, EventHandler click, bool isDefault = false)
	{
		ToolStripMenuItem result = new(text);
		result.Click += click;
		if (isDefault)
		{
			result.Font = new Font(result.Font, FontStyle.Bold);
		}

		return result;
	}

	private static NotifyIcon CreateNotifyIcon()
	{
		NotifyIcon notifyIcon = new()
		{
			Icon = Properties.Resources.GizmoDock,
			Text = nameof(Gizmos),
			Visible = true,
		};
		notifyIcon.MouseDoubleClick += IconDoubleClick;

		ContextMenuStrip notifyIconMenu = new();
		notifyIcon.ContextMenuStrip = notifyIconMenu;

		notifyIconMenu.Items.AddRange(new ToolStripItem[]
		{
#pragma warning disable CC0022 // Should dispose object. Disposing of the context menu cleans these up.
			CreateMenuItem("&Show All", ShowAll, isDefault: true),
			CreateMenuItem("&Align All", AlignAll),
			new ToolStripSeparator(),
			CreateMenuItem("C&reate Shortcut...", CreateShortcut),
			CreateMenuItem("A&bout...", About),
			new ToolStripSeparator(),
			CreateMenuItem("&Close All", CloseAll),
			CreateMenuItem("C&lose All And Exit", CloseAllAndExit),
			CreateMenuItem("E&xit", Exit),
#pragma warning restore CC0022 // Should dispose object
		});

		return notifyIcon;
	}

	private static void CreateShortcut(object? sender, EventArgs e)
	{
		string gizmoDockExe = Path.Combine(ApplicationInfo.BaseDirectory, "GizmoDock.exe");
#if DEBUG
		if (!File.Exists(gizmoDockExe))
		{
			gizmoDockExe = ApplicationInfo.ExecutableFile.Replace("GizmoTray", "GizmoDock");
		}
#endif

		if (File.Exists(gizmoDockExe))
		{
			using Process p = Process.Start(gizmoDockExe);
		}
		else
		{
			MessageBox.Show("Unable to find GizmoDock.exe", ApplicationInfo.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private static void ShowAll(object? sender, EventArgs e)
		=> Remote.TryCallAllServers(server => server.BringToFront());

	private static void AlignAll(object? sender, EventArgs e)
	{
		string[] baseNames = Remote.GetBaseNames<IGizmoServer>().ToArray();
		if (baseNames.Length > 0)
		{
			List<(string BaseName, double Left, double Top, double Height)> windowInfo = new();
			Parallel.ForEach(baseNames, baseName =>
			{
				Remote.TryCallService<IGizmoServer>(baseName, server =>
				{
					lock (windowInfo)
					{
						(double left, double top, _, double height) = server.GetScreenRectangle();
						windowInfo.Add((baseName, left, top, height));
					}
				});
			});

			if (windowInfo.Count > 0)
			{
				windowInfo = windowInfo.OrderBy(rect => rect.Top).ThenBy(rect => rect.Left).ToList();
				(_, double left, double nextTop, _) = windowInfo[0];
				foreach ((string baseName, _, _, double height) in windowInfo)
				{
					Remote.TryCallService<IGizmoServer>(baseName, server => server.MoveTo(left, nextTop));

					const double Separator = 2;
					nextTop += height + Separator;
				}
			}
		}
	}

	private static void CloseAll(object? sender, EventArgs e)
		=> Remote.CloseAll();

	private static void CloseAllAndExit(object? sender, EventArgs e)
	{
		CloseAll(sender, e);
		Exit(sender, e);
	}

	private static void IconDoubleClick(object? sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ShowAll(sender, e);
		}
	}

	private static void About(object? sender, EventArgs e)
	{
		WindowsUtility.ShowAboutBox(null, Assembly.GetExecutingAssembly());
	}

	private static void Exit(object? sender, EventArgs e)
	{
		// Tell the WPF Application to exit (not the Windows.Forms Application).
		System.Windows.Application.Current.Shutdown();
	}

	#endregion
}
