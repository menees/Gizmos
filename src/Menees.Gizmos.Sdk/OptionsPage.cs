namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using Menees.Windows.Presentation;

	#endregion

	/// <summary>
	/// A page to display in GizmoDock's OptionsDialog.
	/// </summary>
	public class OptionsPage : ExtendedUserControl
	{
		#region Public Methods

		/// <summary>
		/// Called when the Options dialog is about to display this page.
		/// </summary>
		public void InitialDisplay()
		{
			this.OnInitialDisplay();
		}

		/// <summary>
		/// Called when the Options dialog's OK button has been clicked.
		/// </summary>
		/// <returns>True if it is ok to close.  False otherwise.</returns>
		public bool Ok() => this.OnOk();

		#endregion

		#region Protected Methods

		/// <summary>
		/// Called when the Options dialog is about to display this page.
		/// </summary>
		protected virtual void OnInitialDisplay()
		{
		}

		/// <summary>
		/// Called when the Options dialog's OK button has been clicked.
		/// </summary>
		/// <returns>True if it is ok to close.  False otherwise.</returns>
		protected virtual bool OnOk() => true;

		#endregion
	}
}
