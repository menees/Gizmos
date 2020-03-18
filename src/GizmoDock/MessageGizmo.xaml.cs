namespace Menees.Gizmos
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Navigation;
	using System.Windows.Shapes;

	#endregion

	[GizmoInfo(GizmoName = nameof(Message), IsSingleInstance = true, IsTemporary = true)]
	[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Created via reflection.")]
	internal partial class MessageGizmo
	{
		#region Public Fields

		public static readonly DependencyProperty IsErrorMessageProperty =
			DependencyProperty.Register(nameof(IsErrorMessage), typeof(bool), typeof(MessageGizmo), new PropertyMetadata(false));

		public static readonly DependencyProperty MessageProperty =
			DependencyProperty.Register(nameof(Message), typeof(string), typeof(MessageGizmo), new PropertyMetadata(null));

		#endregion

		#region Constructors

		public MessageGizmo()
		{
			this.InitializeComponent();
		}

		#endregion

		#region Public Properties

		public bool IsErrorMessage
		{
			get { return (bool)this.GetValue(IsErrorMessageProperty); }
			set { this.SetValue(IsErrorMessageProperty, value); }
		}

		public string Message
		{
			get { return (string)this.GetValue(MessageProperty); }
			set { this.SetValue(MessageProperty, value); }
		}

		#endregion

		#region Private Event Handlers

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			this.Close();
		}

		#endregion
	}
}
