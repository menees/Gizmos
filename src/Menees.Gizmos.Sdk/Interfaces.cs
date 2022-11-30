namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Windows;

	#endregion

	#region public IDock

	/// <summary>
	/// Defines the interface for communicating with the GizmoDock.
	/// </summary>
	public interface IDock
	{
		/// <summary>
		/// Gets the dock's main window.
		/// </summary>
		Window MainWindow { get; }
	}

	#endregion

	#region public IGizmoServer

	public interface IGizmoServer
	{
		void Close();

		(double Left, double Top, double Width, double Height) GetScreenRectangle();

		void MoveTo(double left, double top);

		void BringToFront();
	}

	#endregion
}
