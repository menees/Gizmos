namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Shapes;
	using Menees.Windows.Presentation;

	#endregion

	internal partial class OptionsDialog
	{
		#region Private Data Members

		private OptionsPage page;

		#endregion

		#region Constructors

		public OptionsDialog()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Methods

		public void ShowDialog(Window owner, OptionsPage page)
		{
			this.Owner = owner;
			this.dock.Children.Remove(this.dummy);
			this.dock.Children.Add(page);
			this.page = page;
			this.ShowDialog();
		}

		#endregion

		#region Private Event Handlers

		private void About_Click(object sender, RoutedEventArgs e)
		{
			WindowsUtility.ShowAboutBox(this, Assembly.GetExecutingAssembly());
		}

		private void Dialog_Loaded(object sender, RoutedEventArgs e)
		{
			if (this.page != null)
			{
				this.page.InitialDisplay();
			}
		}

		private void OK_Click(object sender, RoutedEventArgs e)
		{
			if (this.page != null)
			{
				// Note: The OK button can't use the ExtendedDialog.DialogResult attached property
				// for buttons because we need to be able to change the result to null if validation
				// fails.  If an attached ExtendedDialog.DialogResult is specified, then it gets re-applied
				// after this and would return true anyway.
				if (!this.page.Ok())
				{
					this.DialogResult = null;
				}
				else
				{
					this.DialogResult = true;
				}
			}
		}

		#endregion
	}
}
